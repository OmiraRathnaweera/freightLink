using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.LoadProposals;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LoadProposalService"/> — the manual, non-AI-matching bidding path
/// (an Agency proposes a price directly on a Posted load; the Shipper accepts one).
/// </summary>
public class LoadProposalServiceTests
{
    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LoadProposalService CreateSut(AppDbContext db) => new(db);

    private static async Task<(Guid ShipperId, Load Load, Guid AgencyId, Guid AgencyStaffId)> SeedPostedLoadWithAgencyAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var shipper = new User { UserId = Guid.NewGuid(), Role = UserRole.Shipper, Email = $"s-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Shipper", IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Users.Add(shipper);

        var load = new Load
        {
            LoadId = Guid.NewGuid(), ShipperUserId = shipper.UserId, ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Cargo", WeightKg = 300m, VolumeM3 = 2m,
            PickupAddress = "A", PickupLat = 6.9m, PickupLng = 79.8m,
            DropoffAddress = "B", DropoffLat = 7.0m, DropoffLng = 80.0m,
            PickupWindowStart = now, PickupWindowEnd = now.AddHours(2),
            Status = LoadStatus.Posted, CreatedAt = now, UpdatedAt = now
        };
        db.Loads.Add(load);

        var agency = new Agency { AgencyId = Guid.NewGuid(), Name = "Bidding Agency", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15], YardAddress = "Y", YardLat = 6.9m, YardLng = 79.9m, Status = AgencyStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Agencies.Add(agency);

        var staffUser = new User { UserId = Guid.NewGuid(), Role = UserRole.AgencyStaff, Email = $"st-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Staff", IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Users.Add(staffUser);
        db.AgencyStaff.Add(new AgencyStaff { UserId = staffUser.UserId, AgencyId = agency.AgencyId, CreatedAt = now, UpdatedAt = now });

        await db.SaveChangesAsync();
        return (shipper.UserId, load, agency.AgencyId, staffUser.UserId);
    }

    [Fact]
    public async Task CreateAsync_ByAgencyStaff_OnPostedLoad_CreatesPendingProposal()
    {
        var db = CreateContext();
        var (_, load, _, staffId) = await SeedPostedLoadWithAgencyAsync(db);
        var sut = CreateSut(db);

        var result = await sut.CreateAsync(load.LoadId, staffId, UserRole.AgencyStaff, new CreateLoadProposalDto { ProposedPrice = 18000m, Message = "We can do it" });

        Assert.Equal("Pending", result.Status);
        Assert.Equal(18000m, result.ProposedPrice);
    }

    [Fact]
    public async Task CreateAsync_BySameAgencyTwice_ThrowsConflict_ForDuplicatePendingProposal()
    {
        var db = CreateContext();
        var (_, load, _, staffId) = await SeedPostedLoadWithAgencyAsync(db);
        var sut = CreateSut(db);

        await sut.CreateAsync(load.LoadId, staffId, UserRole.AgencyStaff, new CreateLoadProposalDto { ProposedPrice = 18000m });

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.CreateAsync(load.LoadId, staffId, UserRole.AgencyStaff, new CreateLoadProposalDto { ProposedPrice = 19000m }));

        Assert.Equal(ErrorCode.LOAD_PROPOSAL_ALREADY_EXISTS, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_ByShipper_ThrowsForbidden()
    {
        var db = CreateContext();
        var (shipperId, load, _, _) = await SeedPostedLoadWithAgencyAsync(db);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.CreateAsync(load.LoadId, shipperId, UserRole.Shipper, new CreateLoadProposalDto { ProposedPrice = 18000m }));

        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_OnNonPostedLoad_ThrowsLoadNotBiddable()
    {
        var db = CreateContext();
        var (_, load, _, staffId) = await SeedPostedLoadWithAgencyAsync(db);
        load.Status = LoadStatus.Matched;
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.CreateAsync(load.LoadId, staffId, UserRole.AgencyStaff, new CreateLoadProposalDto { ProposedPrice = 18000m }));

        Assert.Equal(ErrorCode.LOAD_NOT_BIDDABLE, ex.Code);
    }

    [Fact]
    public async Task AcceptAsync_ByOwningShipper_CreatesAcceptedAssignment_AndMarksLoadMatched_AndRejectsOtherPendingProposals()
    {
        var db = CreateContext();
        var (shipperId, load, agencyId, staffId) = await SeedPostedLoadWithAgencyAsync(db);
        var sut = CreateSut(db);

        var accepted = await sut.CreateAsync(load.LoadId, staffId, UserRole.AgencyStaff, new CreateLoadProposalDto { ProposedPrice = 18000m });

        // A second agency's competing proposal on the same load, which should be auto-rejected.
        var now = DateTimeOffset.UtcNow;
        var otherAgency = new Agency { AgencyId = Guid.NewGuid(), Name = "Other Agency", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15], YardAddress = "Y2", YardLat = 6.8m, YardLng = 79.7m, Status = AgencyStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Agencies.Add(otherAgency);
        var otherProposal = new LoadProposal
        {
            LoadProposalId = Guid.NewGuid(), LoadId = load.LoadId, AgencyId = otherAgency.AgencyId,
            ProposedByUserId = Guid.NewGuid(), ProposedPrice = 17000m, Status = LoadProposalStatus.Pending,
            CreatedAt = now, UpdatedAt = now
        };
        db.LoadProposals.Add(otherProposal);
        await db.SaveChangesAsync();

        var result = await sut.AcceptAsync(load.LoadId, accepted.LoadProposalId, shipperId, UserRole.Shipper);

        Assert.Equal("Accepted", result.Status);

        var persistedLoad = await db.Loads.FirstAsync(l => l.LoadId == load.LoadId);
        Assert.Equal(LoadStatus.Matched, persistedLoad.Status);

        var assignment = await db.Assignments.FirstOrDefaultAsync(a => a.LoadId == load.LoadId && a.AgencyId == agencyId);
        Assert.NotNull(assignment);
        Assert.Equal(AssignmentStatus.Accepted, assignment!.Status);
        Assert.Equal(18000m, assignment.ProposedPrice);

        var refreshedOther = await db.LoadProposals.FirstAsync(p => p.LoadProposalId == otherProposal.LoadProposalId);
        Assert.Equal(LoadProposalStatus.Rejected, refreshedOther.Status);
    }

    [Fact]
    public async Task AcceptAsync_ByNonOwningShipper_ThrowsForbidden_WithLoadNotOwned()
    {
        var db = CreateContext();
        var (_, load, _, staffId) = await SeedPostedLoadWithAgencyAsync(db);
        var sut = CreateSut(db);
        var proposal = await sut.CreateAsync(load.LoadId, staffId, UserRole.AgencyStaff, new CreateLoadProposalDto { ProposedPrice = 18000m });

        var otherShipperId = Guid.NewGuid();
        db.Users.Add(new User { UserId = otherShipperId, Role = UserRole.Shipper, Email = $"other-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Other Shipper", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.AcceptAsync(load.LoadId, proposal.LoadProposalId, otherShipperId, UserRole.Shipper));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task WithdrawAsync_ByProposingAgency_MarksProposalWithdrawn()
    {
        var db = CreateContext();
        var (_, load, _, staffId) = await SeedPostedLoadWithAgencyAsync(db);
        var sut = CreateSut(db);
        var proposal = await sut.CreateAsync(load.LoadId, staffId, UserRole.AgencyStaff, new CreateLoadProposalDto { ProposedPrice = 18000m });

        var result = await sut.WithdrawAsync(load.LoadId, proposal.LoadProposalId, staffId, UserRole.AgencyStaff);

        Assert.Equal("Withdrawn", result.Status);
    }
}
