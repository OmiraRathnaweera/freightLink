using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AgencyService"/>'s registration and admin-verification status lifecycle
/// (Pending → Verified → Active, and Suspend from any state) plus ownership enforcement — the parts
/// of this large service that were previously only exercised indirectly via <c>AgenciesControllerTests</c>.
/// </summary>
public class AgencyServiceTests
{
    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AgencyService CreateSut(AppDbContext db) => new(db, new PasswordHasher());

    private static AgencyCreateDto ValidCreateDto() => new()
    {
        Name = "Test Agency",
        BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15],
        YardAddress = "1 Yard Road",
        YardLat = 6.9m,
        YardLng = 79.9m
    };

    private static async Task<Agency> SeedAgencyAsync(AppDbContext db, AgencyStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(), Name = "Seed Agency", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15],
            YardAddress = "Y", YardLat = 6.9m, YardLng = 79.9m, Status = status, CreatedAt = now, UpdatedAt = now
        };
        db.Agencies.Add(agency);
        await db.SaveChangesAsync();
        return agency;
    }

    [Fact]
    public async Task CreateAsync_WithNewBusinessRegNo_CreatesAgency_InPendingStatus()
    {
        var db = CreateContext();
        var sut = CreateSut(db);
        var adminId = Guid.NewGuid();

        var result = await sut.CreateAsync(adminId, UserRole.Admin, ValidCreateDto());

        Assert.Equal(AgencyStatus.Pending, result.Status);
        var history = await db.AgencyStatusHistories.FirstOrDefaultAsync(h => h.AgencyId == result.AgencyId);
        Assert.NotNull(history);
        Assert.Null(history!.FromStatus);
        Assert.Equal(AgencyStatus.Pending, history.ToStatus);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateBusinessRegNo_ThrowsConflict()
    {
        var db = CreateContext();
        var sut = CreateSut(db);
        var dto = ValidCreateDto();
        await sut.CreateAsync(Guid.NewGuid(), UserRole.Admin, dto);

        var duplicateDto = ValidCreateDto();
        duplicateDto.BusinessRegNo = dto.BusinessRegNo;

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(Guid.NewGuid(), UserRole.Admin, duplicateDto));

        Assert.Equal(ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, ex.Code);
    }

    [Fact]
    public async Task VerifyAsync_OnPendingAgency_TransitionsToVerified()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Pending);
        var sut = CreateSut(db);

        await sut.VerifyAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin);

        var persisted = await db.Agencies.FindAsync(agency.AgencyId);
        Assert.Equal(AgencyStatus.Verified, persisted!.Status);
    }

    [Fact]
    public async Task VerifyAsync_OnAlreadyVerifiedAgency_ThrowsInvalidTransition()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Verified);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.VerifyAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, ex.Code);
    }

    [Fact]
    public async Task ActivateAsync_OnVerifiedAgency_TransitionsToActive()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Verified);
        var sut = CreateSut(db);

        await sut.ActivateAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin);

        var persisted = await db.Agencies.FindAsync(agency.AgencyId);
        Assert.Equal(AgencyStatus.Active, persisted!.Status);
    }

    [Fact]
    public async Task ActivateAsync_OnPendingAgency_ThrowsInvalidTransition_BecauseNotYetVerified()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Pending);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ActivateAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, ex.Code);
    }

    [Fact]
    public async Task SuspendAsync_OnActiveAgency_TransitionsToSuspended()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var sut = CreateSut(db);

        await sut.SuspendAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin);

        var persisted = await db.Agencies.FindAsync(agency.AgencyId);
        Assert.Equal(AgencyStatus.Suspended, persisted!.Status);
    }

    [Fact]
    public async Task SuspendAsync_OnAlreadySuspendedAgency_ThrowsInvalidTransition()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Suspended);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.SuspendAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, ex.Code);
    }

    [Fact]
    public async Task ActivateAsync_OnSuspendedAgency_ReactivatesIt()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Suspended);
        var sut = CreateSut(db);

        await sut.ActivateAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin);

        var persisted = await db.Agencies.FindAsync(agency.AgencyId);
        Assert.Equal(AgencyStatus.Active, persisted!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_SuspendedToActive_PersistsStatusAndWritesAuditRow()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Suspended);
        var sut = CreateSut(db);
        var adminId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var result = await sut.UpdateStatusAsync(agency.AgencyId, adminId, UserRole.Admin,
            new UpdateAgencyStatusDto { Status = AgencyStatus.Active, Reason = "  Suspended in error  " });

        Assert.Equal(AgencyStatus.Active, result.Status);
        var audit = Assert.Single(await db.AgencyStatusHistories.Where(h => h.AgencyId == agency.AgencyId).ToListAsync());
        Assert.Equal(AgencyStatus.Suspended, audit.FromStatus);
        Assert.Equal(AgencyStatus.Active, audit.ToStatus);
        Assert.Equal(adminId, audit.ChangedByUserId);
        Assert.Equal("Suspended in error", audit.Reason);
        Assert.True(audit.ChangedAt >= before);
    }

    [Fact]
    public async Task UpdateStatusAsync_ActiveToSuspendedAndBack_RecordsBothTransitions()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var sut = CreateSut(db);
        var adminId = Guid.NewGuid();

        await sut.UpdateStatusAsync(agency.AgencyId, adminId, UserRole.Admin, new UpdateAgencyStatusDto { Status = AgencyStatus.Suspended, Reason = "Audit" });
        await sut.UpdateStatusAsync(agency.AgencyId, adminId, UserRole.Admin, new UpdateAgencyStatusDto { Status = AgencyStatus.Active, Reason = "Cleared" });

        var persisted = await db.Agencies.FindAsync(agency.AgencyId);
        Assert.Equal(AgencyStatus.Active, persisted!.Status);
        Assert.Equal(2, await db.AgencyStatusHistories.CountAsync(h => h.AgencyId == agency.AgencyId));
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsAndLeavesStatusUnchanged()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Pending);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateStatusAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin,
            new UpdateAgencyStatusDto { Status = AgencyStatus.Active, Reason = "Skip verification" }));

        Assert.Equal(ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, ex.Code);
        var persisted = await db.Agencies.FindAsync(agency.AgencyId);
        Assert.Equal(AgencyStatus.Pending, persisted!.Status);
        Assert.Empty(await db.AgencyStatusHistories.Where(h => h.AgencyId == agency.AgencyId).ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateStatusAsync_BlankReason_ThrowsValidationError(string reason)
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Suspended);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateStatusAsync(agency.AgencyId, Guid.NewGuid(), UserRole.Admin,
            new UpdateAgencyStatusDto { Status = AgencyStatus.Active, Reason = reason }));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, ex.Code);
    }

    [Fact]
    public async Task UpdateStatusAsync_ByOwningAgencyStaff_ThrowsForbidden_AndLeavesStatusUnchanged()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Suspended);
        var staffUserId = Guid.NewGuid();
        db.AgencyStaff.Add(new AgencyStaff { UserId = staffUserId, AgencyId = agency.AgencyId });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateStatusAsync(agency.AgencyId, staffUserId, UserRole.AgencyStaff,
            new UpdateAgencyStatusDto { Status = AgencyStatus.Active, Reason = "Reactivate myself" }));

        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
        var persisted = await db.Agencies.FindAsync(agency.AgencyId);
        Assert.Equal(AgencyStatus.Suspended, persisted!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownAgency_ThrowsNotFound()
    {
        var db = CreateContext();
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateStatusAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin,
            new UpdateAgencyStatusDto { Status = AgencyStatus.Active, Reason = "x" }));

        Assert.Equal(ErrorCode.AGENCY_NOT_FOUND, ex.Code);
    }

    private static async Task<ComplianceDoc> SeedDocAsync(AppDbContext db, Guid agencyId, ComplianceDocStatus status, ComplianceDocType type = ComplianceDocType.BusinessRegistration)
    {
        var doc = new ComplianceDoc
        {
            ComplianceDocId = Guid.NewGuid(), AgencyId = agencyId, DocType = type, DocNumber = "DOC-1", StorageKey = "orig-key",
            IssuedOn = new DateOnly(2026, 1, 1), ExpiresOn = new DateOnly(2030, 1, 1), Status = status
        };
        db.ComplianceDocs.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    private static ComplianceDocUpdateDto ReplacementDto() => new()
    {
        PublicId = "new-key", DocNumber = "DOC-2", IssuedOn = new DateOnly(2026, 6, 1), ExpiresOn = new DateOnly(2031, 6, 1)
    };

    private static async Task<Guid> SeedStaffAsync(AppDbContext db, Guid agencyId)
    {
        var userId = Guid.NewGuid();
        db.AgencyStaff.Add(new AgencyStaff { UserId = userId, AgencyId = agencyId });
        await db.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task UpdateComplianceDocAsync_OnVerifiedDoc_ThrowsConflict_AndLeavesDocUntouched()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var staffId = await SeedStaffAsync(db, agency.AgencyId);
        var doc = await SeedDocAsync(db, agency.AgencyId, ComplianceDocStatus.Verified);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateComplianceDocAsync(agency.AgencyId, doc.ComplianceDocId, staffId, UserRole.AgencyStaff, ReplacementDto()));

        Assert.Equal(System.Net.HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal(ErrorCode.INVALID_COMPLIANCE_DOC_STATUS_TRANSITION, ex.Code);
        var persisted = await db.ComplianceDocs.FindAsync(doc.ComplianceDocId);
        Assert.Equal(ComplianceDocStatus.Verified, persisted!.Status);
        Assert.Equal("orig-key", persisted.StorageKey);
        Assert.Equal("DOC-1", persisted.DocNumber);
    }

    [Theory]
    [InlineData(ComplianceDocStatus.Pending)]
    [InlineData(ComplianceDocStatus.Rejected)]
    public async Task UpdateComplianceDocAsync_OnNonVerifiedDoc_ReplacesItAndResetsToPending(ComplianceDocStatus status)
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var staffId = await SeedStaffAsync(db, agency.AgencyId);
        var doc = await SeedDocAsync(db, agency.AgencyId, status);
        var sut = CreateSut(db);

        var result = await sut.UpdateComplianceDocAsync(agency.AgencyId, doc.ComplianceDocId, staffId, UserRole.AgencyStaff, ReplacementDto());

        Assert.Equal(ComplianceDocStatus.Pending, result.Status);
        Assert.Equal("new-key", result.StorageKey);
    }

    [Fact]
    public async Task VerifyComplianceDocAsync_ThenAgencyUpdate_IsBlocked()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var staffId = await SeedStaffAsync(db, agency.AgencyId);
        var doc = await SeedDocAsync(db, agency.AgencyId, ComplianceDocStatus.Pending);
        var sut = CreateSut(db);

        await sut.VerifyComplianceDocAsync(agency.AgencyId, doc.ComplianceDocId, Guid.NewGuid(), UserRole.Admin);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateComplianceDocAsync(agency.AgencyId, doc.ComplianceDocId, staffId, UserRole.AgencyStaff, ReplacementDto()));
        Assert.Equal(ErrorCode.INVALID_COMPLIANCE_DOC_STATUS_TRANSITION, ex.Code);
    }

    [Theory]
    [InlineData(ComplianceDocStatus.Pending)]
    [InlineData(ComplianceDocStatus.Verified)]
    public async Task AddComplianceDocAsync_WhenLiveDocOfSameTypeExists_ThrowsConflict(ComplianceDocStatus existing)
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var staffId = await SeedStaffAsync(db, agency.AgencyId);
        await SeedDocAsync(db, agency.AgencyId, existing);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.AddComplianceDocAsync(agency.AgencyId, staffId, UserRole.AgencyStaff,
            new ComplianceDocCreateDto { PublicId = "k", DocType = ComplianceDocType.BusinessRegistration, DocNumber = "N", IssuedOn = new DateOnly(2026, 1, 1) }));

        Assert.Equal(System.Net.HttpStatusCode.Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task AddComplianceDocAsync_AfterRejection_AllowsNewDocOfSameType()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var staffId = await SeedStaffAsync(db, agency.AgencyId);
        await SeedDocAsync(db, agency.AgencyId, ComplianceDocStatus.Rejected);
        var sut = CreateSut(db);

        var result = await sut.AddComplianceDocAsync(agency.AgencyId, staffId, UserRole.AgencyStaff,
            new ComplianceDocCreateDto { PublicId = "k", DocType = ComplianceDocType.BusinessRegistration, DocNumber = "N", IssuedOn = new DateOnly(2026, 1, 1) });

        Assert.Equal(ComplianceDocStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GetStatusHistoryAsync_ReturnsEntriesNewestFirst_ForAdmin()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var adminId = Guid.NewGuid();
        // History rows join their (FK-guaranteed in Postgres) ChangedByUser, so the actor must exist here.
        db.Users.Add(new User { UserId = adminId, Role = UserRole.Admin, Email = "admin@example.com", PasswordHash = "x", FullName = "Admin One" });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);
        await sut.SuspendAsync(agency.AgencyId, adminId, UserRole.Admin);
        await Task.Delay(5);
        await sut.UpdateStatusAsync(agency.AgencyId, adminId, UserRole.Admin, new UpdateAgencyStatusDto { Status = AgencyStatus.Active, Reason = "Cleared" });

        var history = await sut.GetStatusHistoryAsync(agency.AgencyId, adminId, UserRole.Admin);

        Assert.Equal(2, history.Count);
        Assert.Equal(AgencyStatus.Active, history[0].ToStatus);
        Assert.Equal("Cleared", history[0].Reason);
        Assert.Equal("Admin One", history[0].ChangedByName);
        Assert.Equal(AgencyStatus.Suspended, history[1].ToStatus);
    }

    [Fact]
    public async Task GetStatusHistoryAsync_ByAgencyStaff_ThrowsForbidden()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Active);
        var staffId = await SeedStaffAsync(db, agency.AgencyId);
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.GetStatusHistoryAsync(agency.AgencyId, staffId, UserRole.AgencyStaff));

        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task VerifyAsync_ByAgencyStaffFromADifferentAgency_ThrowsForbidden_AgencyNotOwned()
    {
        var db = CreateContext();
        var agency = await SeedAgencyAsync(db, AgencyStatus.Pending);
        var sut = CreateSut(db);

        var otherStaffId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var otherAgency = new Agency { AgencyId = Guid.NewGuid(), Name = "Other", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15], YardAddress = "Y2", YardLat = 6.8m, YardLng = 79.7m, Status = AgencyStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Agencies.Add(otherAgency);
        db.Users.Add(new User { UserId = otherStaffId, Role = UserRole.AgencyStaff, Email = $"o-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Other Staff", IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.AgencyStaff.Add(new AgencyStaff { UserId = otherStaffId, AgencyId = otherAgency.AgencyId, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.VerifyAsync(agency.AgencyId, otherStaffId, UserRole.AgencyStaff));

        Assert.Equal(ErrorCode.AGENCY_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ForNonExistentAgency_ThrowsNotFound()
    {
        var db = CreateContext();
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(ErrorCode.AGENCY_NOT_FOUND, ex.Code);
    }
}
