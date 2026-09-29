using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LoadFileService"/> covering attach/list/detach and their ownership rules.
/// Backed by EF Core's InMemory provider, mirroring <see cref="LoadServiceTests"/>'s pattern.
/// </summary>
public class LoadFileServiceTests
{
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test.</summary>
    private static AppDbContext CreateContext() => CreateContext(Guid.NewGuid().ToString());

    /// <summary>Creates an InMemory-backed <see cref="AppDbContext"/> against a caller-supplied database name.</summary>
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Builds a real <see cref="LoadFileService"/> wired to the given DB context.</summary>
    private static LoadFileService CreateSut(AppDbContext dbContext) => new(dbContext);

    /// <summary>Seeds a minimal Shipper user row.</summary>
    private static async Task<Guid> SeedShipperUserAsync(AppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-hash",
            FullName = "Jane Shipper",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.UserId;
    }

    /// <summary>Directly inserts a <see cref="Load"/> owned by <paramref name="shipperUserId"/>.</summary>
    private static async Task<Load> SeedLoadAsync(AppDbContext dbContext, Guid shipperUserId)
    {
        var now = DateTimeOffset.UtcNow;
        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            CargoDescription = "Seeded cargo",
            WeightKg = 100m,
            VolumeM3 = 1m,
            PickupAddress = "100 Pickup Street, Colombo",
            PickupLat = 6.9271m,
            PickupLng = 79.8612m,
            DropoffAddress = "200 Dropoff Road, Kandy",
            DropoffLat = 7.2906m,
            DropoffLng = 80.6337m,
            PickupWindowStart = now.AddDays(1),
            PickupWindowEnd = now.AddDays(2),
            Status = LoadStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Loads.Add(load);
        await dbContext.SaveChangesAsync();
        return load;
    }

    /// <summary>Directly inserts an <see cref="UploadedFile"/> owned by <paramref name="uploadedByUserId"/>.</summary>
    private static async Task<UploadedFile> SeedUploadedFileAsync(AppDbContext dbContext, Guid uploadedByUserId)
    {
        var uploadedFile = new UploadedFile
        {
            FileId = Guid.NewGuid(),
            UploadedByUserId = uploadedByUserId,
            PublicId = $"fake/{Guid.NewGuid():N}",
            SecureUrl = "https://res.cloudinary.com/fake/raw/upload/fake.pdf",
            Format = "pdf",
            Bytes = 1024,
            ResourceType = "raw",
            ContentType = "application/pdf",
            OriginalFileName = "manifest.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };
        dbContext.UploadedFiles.Add(uploadedFile);
        await dbContext.SaveChangesAsync();
        return uploadedFile;
    }

    private static AttachLoadFileDto ValidAttachDto(string publicId, FileType fileType = FileType.Manifest) => new()
    {
        PublicId = publicId,
        FileType = fileType
    };

    [Fact]
    public async Task AttachAsync_OwnLoadAndOwnUpload_CreatesLoadFileAndReturnsJoinedResponse()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var uploadedFile = await SeedUploadedFileAsync(dbContext, shipperId);

        var result = await sut.AttachAsync(load.LoadId, shipperId, ValidAttachDto(uploadedFile.PublicId, FileType.Invoice));

        Assert.Equal(load.LoadId, result.LoadId);
        Assert.Equal(FileType.Invoice, result.FileType);
        Assert.Equal(uploadedFile.PublicId, result.PublicId);
        Assert.Equal(uploadedFile.SecureUrl, result.SecureUrl);
        Assert.Equal(uploadedFile.Bytes, result.Bytes);
        Assert.Single(dbContext.Files.Where(lf => lf.LoadId == load.LoadId));
    }

    [Fact]
    public async Task AttachAsync_LoadNotFound_ThrowsNotFound()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var uploadedFile = await SeedUploadedFileAsync(dbContext, shipperId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AttachAsync(Guid.NewGuid(), shipperId, ValidAttachDto(uploadedFile.PublicId)));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task AttachAsync_LoadNotOwned_ThrowsForbidden()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var ownerId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, ownerId);
        var uploadedFile = await SeedUploadedFileAsync(dbContext, otherShipperId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AttachAsync(load.LoadId, otherShipperId, ValidAttachDto(uploadedFile.PublicId)));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task AttachAsync_UploadedFilePublicIdNotFound_ThrowsNotFound()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AttachAsync(load.LoadId, shipperId, ValidAttachDto("nonexistent/publicid")));

        Assert.Equal(ErrorCode.LOAD_FILE_UPLOAD_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task AttachAsync_UploadedFileNotOwned_ThrowsForbidden()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var uploadedFile = await SeedUploadedFileAsync(dbContext, otherShipperId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AttachAsync(load.LoadId, shipperId, ValidAttachDto(uploadedFile.PublicId)));

        Assert.Equal(ErrorCode.FILE_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task AttachAsync_UploadedFileAlreadyAttachedElsewhere_ThrowsConflict()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var loadA = await SeedLoadAsync(dbContext, shipperId);
        var loadB = await SeedLoadAsync(dbContext, shipperId);
        var uploadedFile = await SeedUploadedFileAsync(dbContext, shipperId);

        await sut.AttachAsync(loadA.LoadId, shipperId, ValidAttachDto(uploadedFile.PublicId));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AttachAsync(loadB.LoadId, shipperId, ValidAttachDto(uploadedFile.PublicId)));

        Assert.Equal(ErrorCode.FILE_IN_USE, ex.Code);
    }

    [Fact]
    public async Task ListAsync_OwnLoad_ReturnsAllAttachedFiles()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var uploadA = await SeedUploadedFileAsync(dbContext, shipperId);
        var uploadB = await SeedUploadedFileAsync(dbContext, shipperId);
        await sut.AttachAsync(load.LoadId, shipperId, ValidAttachDto(uploadA.PublicId, FileType.Manifest));
        await sut.AttachAsync(load.LoadId, shipperId, ValidAttachDto(uploadB.PublicId, FileType.CargoPhoto));

        var result = await sut.ListAsync(load.LoadId, shipperId, UserRole.Shipper);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ListAsync_AdminOnAnyLoad_ReturnsAllAttachedFiles()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var upload = await SeedUploadedFileAsync(dbContext, shipperId);
        await sut.AttachAsync(load.LoadId, shipperId, ValidAttachDto(upload.PublicId));

        var result = await sut.ListAsync(load.LoadId, Guid.NewGuid(), UserRole.Admin);

        Assert.Single(result);
    }

    [Fact]
    public async Task ListAsync_NotOwnedByNonAdmin_ThrowsForbidden()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ListAsync(load.LoadId, otherShipperId, UserRole.Shipper));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task ListAsync_LoadNotFound_ThrowsNotFound()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ListAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task DetachAsync_OwnLoad_RemovesLoadFileRow()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var upload = await SeedUploadedFileAsync(dbContext, shipperId);
        var attached = await sut.AttachAsync(load.LoadId, shipperId, ValidAttachDto(upload.PublicId));

        await sut.DetachAsync(load.LoadId, attached.FileId, shipperId);

        Assert.Empty(dbContext.Files.Where(lf => lf.LoadId == load.LoadId));
    }

    [Fact]
    public async Task DetachAsync_LoadNotFound_ThrowsNotFound()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.DetachAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task DetachAsync_LoadNotOwned_ThrowsForbidden()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.DetachAsync(load.LoadId, Guid.NewGuid(), otherShipperId));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task DetachAsync_LoadFileNotFound_ThrowsNotFound_ForNonexistentId()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.DetachAsync(load.LoadId, Guid.NewGuid(), shipperId));

        Assert.Equal(ErrorCode.LOAD_FILE_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task DetachAsync_LoadFileNotFound_ThrowsNotFound_ForAttachmentBelongingToAnotherLoad()
    {
        await using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var loadA = await SeedLoadAsync(dbContext, shipperId);
        var loadB = await SeedLoadAsync(dbContext, shipperId);
        var upload = await SeedUploadedFileAsync(dbContext, shipperId);
        var attachedToLoadA = await sut.AttachAsync(loadA.LoadId, shipperId, ValidAttachDto(upload.PublicId));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.DetachAsync(loadB.LoadId, attachedToLoadA.FileId, shipperId));

        Assert.Equal(ErrorCode.LOAD_FILE_NOT_FOUND, ex.Code);
    }
}
