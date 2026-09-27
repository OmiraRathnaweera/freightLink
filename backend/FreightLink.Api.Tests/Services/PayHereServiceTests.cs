using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Payments;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreightLink.Api.Tests.Services;

public class PayHereServiceTests
{
    private const string MerchantId = "1220001";
    private const string MerchantSecret = "4d186321c1a7f0f354b4077d3ca75c6a";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PayHereService CreateSut(AppDbContext dbContext, PayHereOptions? options = null)
    {
        var opts = options ?? new PayHereOptions
        {
            MerchantId = MerchantId,
            MerchantSecret = MerchantSecret,
            Env = "sandbox",
            BaseUrl = "https://sandbox.payhere.lk/pay/checkout",
            ReturnUrl = "http://localhost:5173/invoices/{id}/payment-success",
            CancelUrl = "http://localhost:5173/invoices/{id}/payment-cancelled",
            NotifyUrl = "http://localhost:5159/api/payments/payhere/notify"
        };

        return new PayHereService(
            dbContext,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PayHereService>.Instance);
    }

    private static async Task<(User Shipper, Invoice Invoice)> SeedInvoiceGraphAsync(
        AppDbContext db,
        InvoiceStatus status = InvoiceStatus.Issued,
        decimal amount = 1500.00m)
    {
        var now = DateTimeOffset.UtcNow;
        var shipper = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = "sunil@example.com",
            FullName = "Sunil Weerakkody",
            PhoneE164 = "+94771234567",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            ShipperProfile = new ShipperProfile
            {
                UserId = Guid.NewGuid(),
                CompanyName = "Lanka Tea Ltd",
                BillingAddress = "Peliyagoda Hub, Colombo",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipper.UserId,
            ShipperUser = shipper,
            ReferenceCode = "LD-TEST",
            CargoDescription = "Tea crates",
            WeightKg = 1000,
            VolumeM3 = 5,
            PickupAddress = "Peliyagoda Hub",
            DropoffAddress = "Kandy Warehouse",
            PickupWindowStart = now.AddDays(1),
            PickupWindowEnd = now.AddDays(2),
            Status = LoadStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now
        };

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            Load = load,
            AgencyId = Guid.NewGuid(),
            WorkflowRunId = Guid.NewGuid(),
            ProposedPrice = amount,
            Status = AssignmentStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };

        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            AssignmentId = assignment.AssignmentId,
            Assignment = assignment,
            VehicleId = Guid.NewGuid(),
            DriverId = Guid.NewGuid(),
            Status = TripStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now
        };

        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            TripId = trip.TripId,
            Trip = trip,
            InvoiceNumber = "INV-2026-001",
            Amount = amount,
            Currency = "LKR",
            Status = status,
            IssuedAt = now,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(shipper);
        db.Loads.Add(load);
        db.Assignments.Add(assignment);
        db.Trips.Add(trip);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return (shipper, invoice);
    }

    [Fact]
    public void GenerateCheckoutHash_ProducesExpectedUppercaseMd5()
    {
        var sut = CreateSut(CreateContext());
        string hash = sut.GenerateCheckoutHash(
            MerchantId,
            "12345",
            "1500.00",
            "LKR",
            MerchantSecret);

        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);
        Assert.Equal(hash.ToUpperInvariant(), hash);
    }

    [Fact]
    public void GenerateWebhookSignature_ProducesExpectedSignature()
    {
        var sut = CreateSut(CreateContext());
        string sig = sut.GenerateWebhookSignature(
            MerchantId,
            "order-001",
            "1500.00",
            "LKR",
            "2",
            MerchantSecret);

        Assert.NotNull(sig);
        Assert.Equal(32, sig.Length);
        Assert.Equal(sig.ToUpperInvariant(), sig);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_SucceedsForUnpaidInvoice()
    {
        using var db = CreateContext();
        var (shipper, invoice) = await SeedInvoiceGraphAsync(db, InvoiceStatus.Issued, 25000.50m);
        var sut = CreateSut(db);

        var result = await sut.CreateCheckoutSessionAsync(
            invoice.InvoiceId,
            shipper.UserId,
            UserRole.Shipper,
            CancellationToken.None);

        Assert.Equal("25000.50", result.Amount);
        Assert.Equal("LKR", result.Currency);
        Assert.Equal(invoice.InvoiceId.ToString(), result.OrderId);
        Assert.Equal(MerchantId, result.MerchantId);
        Assert.NotEmpty(result.Hash);
        Assert.Equal("Sunil", result.FirstName);
        Assert.Equal("Weerakkody", result.LastName);
        Assert.Equal("sunil@example.com", result.Email);
        Assert.Contains(invoice.InvoiceId.ToString(), result.ReturnUrl);

        // Check invoice status advanced to PaymentPending
        var reloaded = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.Equal(InvoiceStatus.PaymentPending, reloaded!.Status);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ThrowsIfAlreadyPaid()
    {
        using var db = CreateContext();
        var (shipper, invoice) = await SeedInvoiceGraphAsync(db, InvoiceStatus.Paid);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateCheckoutSessionAsync(
                invoice.InvoiceId,
                shipper.UserId,
                UserRole.Shipper,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_ALREADY_PAID, ex.Code);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ThrowsIfForbiddenUser()
    {
        using var db = CreateContext();
        var (_, invoice) = await SeedInvoiceGraphAsync(db, InvoiceStatus.Issued);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateCheckoutSessionAsync(
                invoice.InvoiceId,
                Guid.NewGuid(), // different user
                UserRole.Shipper,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task HandleWebhookCallbackAsync_ThrowsOnInvalidSignature()
    {
        using var db = CreateContext();
        var (_, invoice) = await SeedInvoiceGraphAsync(db, InvoiceStatus.Issued);
        var sut = CreateSut(db);

        var callback = new PayHereWebhookDto
        {
            MerchantId = MerchantId,
            OrderId = invoice.InvoiceId.ToString(),
            PaymentId = "PAY-9999",
            PayhereAmount = "1500.00",
            PayhereCurrency = "LKR",
            StatusCode = "2",
            Md5Sig = "INVALID_HASH_VALUE_HERE"
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.HandleWebhookCallbackAsync(callback, null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(ErrorCode.PAYMENT_SIGNATURE_INVALID, ex.Code);

        // Verify rejected event was logged
        var loggedEvent = await db.PaymentWebhookEvents.FirstOrDefaultAsync();
        Assert.NotNull(loggedEvent);
        Assert.False(loggedEvent.SignatureValid);
        Assert.Equal(WebhookProcessingStatus.SignatureRejected, loggedEvent.ProcessingStatus);
    }

    [Fact]
    public async Task HandleWebhookCallbackAsync_ProcessesApprovedPaymentSuccessfully()
    {
        using var db = CreateContext();
        var (_, invoice) = await SeedInvoiceGraphAsync(db, InvoiceStatus.PaymentPending, 1500.00m);
        var sut = CreateSut(db);

        string validSig = sut.GenerateWebhookSignature(
            MerchantId,
            invoice.InvoiceId.ToString(),
            "1500.00",
            "LKR",
            "2",
            MerchantSecret);

        var callback = new PayHereWebhookDto
        {
            MerchantId = MerchantId,
            OrderId = invoice.InvoiceId.ToString(),
            PaymentId = "PAY-SUCCESS-001",
            PayhereAmount = "1500.00",
            PayhereCurrency = "LKR",
            StatusCode = "2",
            Md5Sig = validSig
        };

        bool result = await sut.HandleWebhookCallbackAsync(callback, "mock_raw_body", CancellationToken.None);
        Assert.True(result);

        // Invoice must be transitioned to Paid
        var reloadedInvoice = await db.Invoices.Include(i => i.Payments).FirstAsync(i => i.InvoiceId == invoice.InvoiceId);
        Assert.Equal(InvoiceStatus.Paid, reloadedInvoice.Status);

        // Payment record created
        Assert.Single(reloadedInvoice.Payments);
        var payment = reloadedInvoice.Payments.First();
        Assert.Equal("PAY-SUCCESS-001", payment.GatewayRef);
        Assert.Equal(1500.00m, payment.Amount);
        Assert.Equal(PaymentStatus.Success, payment.Status);

        // Webhook event marked Applied
        var webhookEvent = await db.PaymentWebhookEvents.FirstAsync();
        Assert.True(webhookEvent.SignatureValid);
        Assert.Equal(WebhookProcessingStatus.Applied, webhookEvent.ProcessingStatus);
    }

    [Fact]
    public async Task HandleWebhookCallbackAsync_HandlesDuplicateIdempotently()
    {
        using var db = CreateContext();
        var (_, invoice) = await SeedInvoiceGraphAsync(db, InvoiceStatus.PaymentPending, 1500.00m);
        var sut = CreateSut(db);

        string validSig = sut.GenerateWebhookSignature(
            MerchantId,
            invoice.InvoiceId.ToString(),
            "1500.00",
            "LKR",
            "2",
            MerchantSecret);

        var callback = new PayHereWebhookDto
        {
            MerchantId = MerchantId,
            OrderId = invoice.InvoiceId.ToString(),
            PaymentId = "PAY-DUPLICATE-001",
            PayhereAmount = "1500.00",
            PayhereCurrency = "LKR",
            StatusCode = "2",
            Md5Sig = validSig
        };

        // First delivery
        await sut.HandleWebhookCallbackAsync(callback, null, CancellationToken.None);

        // Second delivery (PayHere retry)
        bool secondResult = await sut.HandleWebhookCallbackAsync(callback, null, CancellationToken.None);
        Assert.True(secondResult);

        // Still single payment created
        var reloaded = await db.Invoices.Include(i => i.Payments).FirstAsync(i => i.InvoiceId == invoice.InvoiceId);
        Assert.Single(reloaded.Payments);

        // Second webhook event marked as Duplicate
        var duplicateEvent = await db.PaymentWebhookEvents.OrderByDescending(e => e.ReceivedAt).FirstAsync();
        Assert.Equal(WebhookProcessingStatus.Duplicate, duplicateEvent.ProcessingStatus);
    }
}
