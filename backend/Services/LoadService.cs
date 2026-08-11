using System.Net;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="ILoadService" />
public class LoadService : ILoadService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Creates the load service with its DB context.</summary>
    public LoadService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<LoadResponseDto> CreateAsync(Guid shipperUserId, CreateLoadDto request, CancellationToken cancellationToken = default)
    {
        ValidatePickupWindow(request.PickupWindowStart, request.PickupWindowEnd);

        var initialStatus = request.PostImmediately ? LoadStatus.Posted : LoadStatus.Draft;
        if (!LoadStatusTransitionRules.CanCreateAs(initialStatus))
        {
            // Unreachable with the current two-outcome PostImmediately mapping, but keeps the
            // creatable-status decision centralized in LoadStatusTransitionRules rather than an
            // inline literal that could silently drift from it.
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_LOAD_STATUS_TRANSITION, $"A load cannot be created directly into status '{initialStatus}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperUserId,
            ReferenceCode = GenerateReferenceCode(),
            CargoDescription = request.CargoDescription,
            WeightKg = request.WeightKg,
            VolumeM3 = request.VolumeM3,
            PickupAddress = request.PickupAddress,
            PickupLat = request.PickupLat,
            PickupLng = request.PickupLng,
            DropoffAddress = request.DropoffAddress,
            DropoffLat = request.DropoffLat,
            DropoffLng = request.DropoffLng,
            PickupWindowStart = request.PickupWindowStart,
            PickupWindowEnd = request.PickupWindowEnd,
            Status = initialStatus,
            CreatedAt = now,
            UpdatedAt = now
        };

        var historyRow = new LoadStatusHistory
        {
            LoadStatusHistoryId = Guid.NewGuid(),
            LoadId = load.LoadId,
            ChangedByUserId = shipperUserId,
            FromStatus = null,
            ToStatus = initialStatus,
            Reason = null,
            ChangedAt = now
        };

        _dbContext.Loads.Add(load);
        _dbContext.LoadStatusHistories.Add(historyRow);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_load_reference" })
        {
            // Defense-in-depth against the GUID-derived ReferenceCode colliding — astronomically
            // unlikely, but mirrors AuthService's own unique-violation-race handling rather than
            // letting it surface as an unhandled 500.
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_REFERENCE_CODE_CONFLICT, "Could not generate a unique load reference code; please retry.");
        }

        return MapToResponse(load);
    }

    /// <inheritdoc />
    public async Task<LoadResponseDto> GetByIdAsync(Guid loadId, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.AsNoTracking().FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        return MapToResponse(load);
    }

    /// <inheritdoc />
    public async Task<PagedLoadResponseDto> GetListAsync(LoadListQueryDto query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var loads = _dbContext.Loads.AsNoTracking();

        if (query.ShipperUserId is { } shipperUserId)
        {
            loads = loads.Where(l => l.ShipperUserId == shipperUserId);
        }

        if (query.Status is { } status)
        {
            loads = loads.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            loads = loads.Where(l =>
                l.CargoDescription.ToLower().Contains(term) ||
                l.ReferenceCode.ToLower().Contains(term) ||
                l.PickupAddress.ToLower().Contains(term) ||
                l.DropoffAddress.ToLower().Contains(term));
        }

        if (query.CreatedFrom is { } createdFrom)
        {
            loads = loads.Where(l => l.CreatedAt >= createdFrom);
        }

        if (query.CreatedTo is { } createdTo)
        {
            loads = loads.Where(l => l.CreatedAt <= createdTo);
        }

        loads = ApplySort(loads, query.SortBy, query.SortDir);

        var totalItems = await loads.CountAsync(cancellationToken);

        var pageOfLoads = await loads
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedLoadResponseDto
        {
            Items = pageOfLoads.Select(MapToListItem).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    /// <inheritdoc />
    public async Task<LoadResponseDto> UpdateAsync(Guid loadId, UpdateLoadDto request, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (!LoadStatusTransitionRules.CanEdit(load.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_LOAD_STATUS_TRANSITION, $"A load in status '{load.Status}' cannot be edited.");
        }

        ValidatePickupWindow(request.PickupWindowStart, request.PickupWindowEnd);

        load.CargoDescription = request.CargoDescription;
        load.WeightKg = request.WeightKg;
        load.VolumeM3 = request.VolumeM3;
        load.PickupAddress = request.PickupAddress;
        load.PickupLat = request.PickupLat;
        load.PickupLng = request.PickupLng;
        load.DropoffAddress = request.DropoffAddress;
        load.DropoffLat = request.DropoffLat;
        load.DropoffLng = request.DropoffLng;
        load.PickupWindowStart = request.PickupWindowStart;
        load.PickupWindowEnd = request.PickupWindowEnd;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(load);
    }

    /// <inheritdoc />
    public async Task<LoadResponseDto> CancelAsync(Guid loadId, Guid cancelledByUserId, CancelLoadDto request, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (!LoadStatusTransitionRules.CanCancel(load.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_LOAD_STATUS_TRANSITION, $"A load in status '{load.Status}' cannot be cancelled.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            // Proactively enforces LoadStatusHistory's ck_lsh_cancel_reason CHECK, which the
            // InMemory test provider doesn't evaluate — without this, a missing reason would only
            // ever be caught against a real Postgres database.
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.LOAD_CANCEL_REASON_REQUIRED, "A reason is required when cancelling a load.");
        }

        var fromStatus = load.Status;
        var now = DateTimeOffset.UtcNow;
        load.Status = LoadStatus.Cancelled;

        _dbContext.LoadStatusHistories.Add(new LoadStatusHistory
        {
            LoadStatusHistoryId = Guid.NewGuid(),
            LoadId = load.LoadId,
            ChangedByUserId = cancelledByUserId,
            FromStatus = fromStatus,
            ToStatus = LoadStatus.Cancelled,
            Reason = request.Reason,
            ChangedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(load);
    }

    /// <summary>Throws if <paramref name="end"/> is not after <paramref name="start"/> (mirrors <c>ck_load_window</c>).</summary>
    private static void ValidatePickupWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_PICKUP_WINDOW, "PickupWindowEnd must be after PickupWindowStart.");
        }
    }

    /// <summary>Applies the requested sort, falling back to <c>createdAt desc</c> for an unrecognized <paramref name="sortBy"/>.</summary>
    private static IQueryable<Load> ApplySort(IQueryable<Load> loads, string? sortBy, string? sortDir)
    {
        var ascending = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLowerInvariant() switch
        {
            "pickupwindowstart" => ascending ? loads.OrderBy(l => l.PickupWindowStart) : loads.OrderByDescending(l => l.PickupWindowStart),
            "weightkg" => ascending ? loads.OrderBy(l => l.WeightKg) : loads.OrderByDescending(l => l.WeightKg),
            _ => ascending ? loads.OrderBy(l => l.CreatedAt) : loads.OrderByDescending(l => l.CreatedAt)
        };
    }

    /// <summary>Generates a short, GUID-derived, human-scannable load reference code.</summary>
    private static string GenerateReferenceCode() => $"LD-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>Maps a <see cref="Load"/> entity to its full wire-facing representation.</summary>
    private static LoadResponseDto MapToResponse(Load load) => new()
    {
        LoadId = load.LoadId,
        ShipperUserId = load.ShipperUserId,
        ReferenceCode = load.ReferenceCode,
        CargoDescription = load.CargoDescription,
        WeightKg = load.WeightKg,
        VolumeM3 = load.VolumeM3,
        PickupAddress = load.PickupAddress,
        PickupLat = load.PickupLat,
        PickupLng = load.PickupLng,
        DropoffAddress = load.DropoffAddress,
        DropoffLat = load.DropoffLat,
        DropoffLng = load.DropoffLng,
        PickupWindowStart = load.PickupWindowStart,
        PickupWindowEnd = load.PickupWindowEnd,
        EstimatedPrice = load.EstimatedPrice,
        Status = load.Status.ToString(),
        // WorkflowRunId requires joining AgentWorkflowRun/Assignment, which belongs to a different
        // component (Component D) — left null here rather than implemented out of scope.
        WorkflowRunId = null,
        CreatedAt = load.CreatedAt,
        UpdatedAt = load.UpdatedAt
    };

    /// <summary>Maps a <see cref="Load"/> entity to its lightweight list-row representation.</summary>
    private static LoadListItemDto MapToListItem(Load load) => new()
    {
        LoadId = load.LoadId,
        ReferenceCode = load.ReferenceCode,
        CargoDescription = load.CargoDescription,
        WeightKg = load.WeightKg,
        PickupAddress = load.PickupAddress,
        DropoffAddress = load.DropoffAddress,
        PickupWindowStart = load.PickupWindowStart,
        PickupWindowEnd = load.PickupWindowEnd,
        EstimatedPrice = load.EstimatedPrice,
        Status = load.Status.ToString(),
        CreatedAt = load.CreatedAt
    };
}
