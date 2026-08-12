using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileUploadService"/> covering validation and delete semantics. Backed
/// by a hand-rolled fake <see cref="IFileStorageService"/> — no real Cloudinary calls, and this
/// project has no mocking framework (same pattern as every other service test).
/// </summary>
public class FileUploadServiceTests
{
    /// <summary>Fake storage that never fails, unless a file's name is in <see cref="FailingFileNames"/>.</summary>
    private class FakeFileStorageService : IFileStorageService
    {
        public HashSet<string> FailingFileNames { get; } = new();
        public List<string> UploadedFileNames { get; } = new();

        public Task<FileUploadResultDto> UploadFileAsync(IFormFile file, string? folder, CancellationToken cancellationToken = default)
        {
            UploadedFileNames.Add(file.FileName);
            if (FailingFileNames.Contains(file.FileName))
            {
                throw new ApiException(System.Net.HttpStatusCode.InternalServerError, ErrorCode.FILE_UPLOAD_FAILED, $"Simulated failure for {file.FileName}.");
            }

            return Task.FromResult(new FileUploadResultDto
            {
                PublicId = $"fake/{file.FileName}",
                SecureUrl = $"https://res.cloudinary.com/fake/raw/upload/{file.FileName}",
                // Cloudinary's real raw-resource-type uploads don't always detect a format (unlike
                // images) — null here mirrors that instead of masking it like a hardcoded "bin" would.
                Format = null,
                Bytes = file.Length,
                ResourceType = "raw",
                ContentType = file.ContentType,
                OriginalFileName = file.FileName
            });
        }

        public Task<FileDeleteResultDto> DeleteFileAsync(string publicId, CancellationToken cancellationToken = default)
        {
            if (publicId == "missing")
            {
                return Task.FromResult(new FileDeleteResultDto { PublicId = publicId, Deleted = false, Detail = "not found" });
            }

            if (publicId == "broken")
            {
                throw new ApiException(System.Net.HttpStatusCode.InternalServerError, ErrorCode.FILE_DELETE_FAILED, "Simulated delete failure.");
            }

            return Task.FromResult(new FileDeleteResultDto { PublicId = publicId, Deleted = true, Detail = "ok" });
        }
    }

    private static readonly Guid TestUserId = Guid.NewGuid();

    /// <summary>
    /// Builds a <see cref="FileUploadService"/> backed by a fresh InMemory <see cref="AppDbContext"/>
    /// (seeded with a single user, <see cref="TestUserId"/>, to satisfy the required
    /// <c>UploadedFiles.UploadedByUserId</c> foreign key) so persistence behavior can be asserted
    /// directly against the returned context.
    /// </summary>
    private static (FileUploadService Sut, AppDbContext DbContext) CreateSut(FakeFileStorageService storage, string? folder = "freightlink")
    {
        var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        dbContext.Users.Add(new User
        {
            UserId = TestUserId,
            Role = UserRole.Shipper,
            Email = "uploader@example.com",
            PasswordHash = "not-used-by-this-test",
            FullName = "Test Uploader",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();

        var sut = new FileUploadService(storage, dbContext, Options.Create(new CloudinaryOptions { Folder = folder }));
        return (sut, dbContext);
    }

    private static IFormFile MakeFile(string fileName, long length = 1024)
    {
        var stream = new MemoryStream(new byte[length]);
        return new FormFile(stream, 0, length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    // --- Single upload validation ---

    /// <summary>A null file is rejected before reaching storage.</summary>
    [Fact]
    public async Task UploadSingleAsync_Throws_ForNullFile()
    {
        var storage = new FakeFileStorageService();
        var (sut, _) = CreateSut(storage);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UploadSingleAsync(null!, TestUserId));

        Assert.Equal(ErrorCode.FILE_REQUIRED, exception.Code);
        Assert.Empty(storage.UploadedFileNames);
    }

    /// <summary>A disallowed extension (e.g. .exe) is rejected before reaching storage.</summary>
    [Fact]
    public async Task UploadSingleAsync_Throws_ForBlockedExtension()
    {
        var storage = new FakeFileStorageService();
        var (sut, _) = CreateSut(storage);
        var file = MakeFile("virus.exe");

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UploadSingleAsync(file, TestUserId));

        Assert.Equal(ErrorCode.BLOCKED_FILE_TYPE, exception.Code);
        Assert.Empty(storage.UploadedFileNames);
    }

    /// <summary>A non-image, non-PDF document type (e.g. .docx) is rejected — this feature is scoped to images and PDF only.</summary>
    [Fact]
    public async Task UploadSingleAsync_Throws_ForDisallowedDocumentType()
    {
        var storage = new FakeFileStorageService();
        var (sut, _) = CreateSut(storage);
        var file = MakeFile("contract.docx");

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UploadSingleAsync(file, TestUserId));

        Assert.Equal(ErrorCode.BLOCKED_FILE_TYPE, exception.Code);
        Assert.Empty(storage.UploadedFileNames);
    }

    /// <summary>A file over the 10 MB limit is rejected before reaching storage.</summary>
    [Fact]
    public async Task UploadSingleAsync_Throws_ForOversizedFile()
    {
        var storage = new FakeFileStorageService();
        var (sut, _) = CreateSut(storage);
        var file = MakeFile("huge.pdf", length: 11 * 1024 * 1024);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UploadSingleAsync(file, TestUserId));

        Assert.Equal(ErrorCode.FILE_TOO_LARGE, exception.Code);
        Assert.Empty(storage.UploadedFileNames);
    }

    /// <summary>A valid PDF uploads successfully — this feature accepts images and PDF, not just images.</summary>
    [Fact]
    public async Task UploadSingleAsync_Succeeds_ForValidPdfFile()
    {
        var storage = new FakeFileStorageService();
        var (sut, _) = CreateSut(storage);
        var file = MakeFile("manifest.pdf");

        var result = await sut.UploadSingleAsync(file, TestUserId);

        Assert.Equal("fake/manifest.pdf", result.PublicId);
        Assert.Equal(file.Length, result.Bytes);
    }

    /// <summary>
    /// Regression test for the bug this fix addresses: a successful Cloudinary upload must also be
    /// recorded in the <c>UploadedFiles</c> table, not just returned to the caller.
    /// </summary>
    [Fact]
    public async Task UploadSingleAsync_PersistsMetadataToDatabase()
    {
        var storage = new FakeFileStorageService();
        var (sut, dbContext) = CreateSut(storage);
        var file = MakeFile("manifest.pdf");

        var result = await sut.UploadSingleAsync(file, TestUserId);

        var persisted = Assert.Single(dbContext.UploadedFiles);
        Assert.Equal(result.PublicId, persisted.PublicId);
        Assert.Equal(result.SecureUrl, persisted.SecureUrl);
        Assert.Equal(result.Bytes, persisted.Bytes);
        Assert.Equal(TestUserId, persisted.UploadedByUserId);
    }

    // --- Delete ---

    /// <summary>Deleting an existing file returns Deleted = true.</summary>
    [Fact]
    public async Task DeleteSingleAsync_ReturnsDeletedTrue_ForExistingFile()
    {
        var (sut, _) = CreateSut(new FakeFileStorageService());

        var result = await sut.DeleteSingleAsync("some-file");

        Assert.True(result.Deleted);
    }

    /// <summary>Deleting a missing/unknown publicId is not an error — Deleted = false, no exception.</summary>
    [Fact]
    public async Task DeleteSingleAsync_ReturnsDeletedFalse_ForMissingFile()
    {
        var (sut, _) = CreateSut(new FakeFileStorageService());

        var result = await sut.DeleteSingleAsync("missing");

        Assert.False(result.Deleted);
        Assert.Equal("not found", result.Detail);
    }

    /// <summary>A blank publicId is rejected.</summary>
    [Fact]
    public async Task DeleteSingleAsync_Throws_ForBlankPublicId()
    {
        var (sut, _) = CreateSut(new FakeFileStorageService());

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.DeleteSingleAsync("   "));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, exception.Code);
    }

    /// <summary>Deleting a file also removes its persisted <c>UploadedFiles</c> metadata row.</summary>
    [Fact]
    public async Task DeleteSingleAsync_RemovesPersistedMetadata()
    {
        var storage = new FakeFileStorageService();
        var (sut, dbContext) = CreateSut(storage);
        var uploadResult = await sut.UploadSingleAsync(MakeFile("to-delete.png"), TestUserId);
        Assert.Single(dbContext.UploadedFiles);

        await sut.DeleteSingleAsync(uploadResult.PublicId);

        Assert.Empty(dbContext.UploadedFiles);
    }
}
