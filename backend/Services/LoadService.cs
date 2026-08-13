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
    /// <summary>Bounded retry count for a <see cref="GenerateReferenceCode"/> collision on <c>uq_load_reference</c> before <see cref="CreateAsync"/> gives up.</summary>
    private const int MaxReferenceCodeGenerationAttempts = 5;

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
        ValidateDistinctPoints(request.PickupLat!.Value, request.PickupLng!.Value, request.DropoffLat!.Value, request.DropoffLng!.Value);

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
            PickupLat = request.PickupLat!.Value,
            PickupLng = request.PickupLng!.Value,
            DropoffAddress = request.DropoffAddress,
            DropoffLat = request.DropoffLat!.Value,
            DropoffLng = request.DropoffLng!.Value,
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

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_load_reference" })
            {
                // Defense-in-depth against the GUID-derived ReferenceCode colliding — astronomically
                // unlikely given GenerateReferenceCode's full-GUID entropy, but retried a bounded
                // number of times with a freshly generated code (the failed insert leaves `load`
                // tracked as Added, so SaveChangesAsync can simply be retried) rather than failing an
                // otherwise-valid create request on the first collision.
                if (attempt >= MaxReferenceCodeGenerationAttempts)
                {
                    throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_REFERENCE_CODE_CONFLICT, "Could not generate a unique load reference code; please retry.");
                }

                load.ReferenceCode = GenerateReferenceCode();
            }
        }

        return MapToResponse(load);
    }

    /// <inheritdoc />
    public async Task<LoadResponseDto> GetByIdAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.AsNoTracking().FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (currentUserRole != UserRole.Admin && load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
        }

        return MapToResponse(load);
    }

    /// <inheritdoc />
    public async Task<PagedLoadResponseDto> GetListAsync(LoadListQueryDto query, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Widened to long before multiplying: (page - 1) * pageSize as plain int arithmetic
        // silently overflows and wraps negative for a large-but-otherwise-valid page (e.g.
        // page = int.MaxValue), which Queryable.Skip(int) would then reject with an unhandled
        // ArgumentOutOfRangeException (a 500) instead of this controlled 400. Skip's signature is
        // int-only regardless of what PostgreSQL's own OFFSET could address, so int.MaxValue is the
        // real ceiling here, not an arbitrary business limit.
        var skip = (long)(page - 1) * pageSize;
        if (skip > int.MaxValue)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.LOAD_PAGE_OUT_OF_RANGE, "The requested page/pageSize combination is out of range.");
        }

        // A non-Admin caller is always scoped to their own loads, regardless of what the query
        // requested — this is what prevents one Shipper from reading another's loads by simply
        // passing a different shipperUserId filter.
        var effectiveShipperUserId = currentUserRole == UserRole.Admin ? query.ShipperUserId : currentUserId;

        var loads = _dbContext.Loads.AsNoTracking();

        if (effectiveShipperUserId is { } shipperUserId)
        {
            loads = loads.Where(l => l.ShipperUserId == shipperUserId);
        }

        if (query.Status is { } status)
        {
            loads = loads.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // EF.Functions.ILike would be the more idiomatic Npgsql translation, but it throws at
            // runtime under the InMemory provider this project's unit tests run against (it has no
            // client-evaluation fallback, unlike EF.Functions.Like) — .ToLower().Contains() stays
            // both InMemory-compatible and, on Postgres, an exact match for the
            // ix_load_*_search_trgm expression indexes in AddLoadSearchTrigramIndexes (see
            // LoadConfiguration), which are built on the same lower(column) expression this
            // generates so the planner can actually use them instead of a sequential scan.
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
            .Skip((int)skip)
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
    public async Task<LoadResponseDto> UpdateAsync(Guid loadId, Guid currentUserId, UpdateLoadDto request, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
        }

        if (!LoadStatusTransitionRules.CanEdit(load.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_LOAD_STATUS_TRANSITION, $"A load in status '{load.Status}' cannot be edited.");
        }

        ValidatePickupWindow(request.PickupWindowStart, request.PickupWindowEnd);
        ValidateDistinctPoints(request.PickupLat!.Value, request.PickupLng!.Value, request.DropoffLat!.Value, request.DropoffLng!.Value);

        load.CargoDescription = request.CargoDescription;
        load.WeightKg = request.WeightKg;
        load.VolumeM3 = request.VolumeM3;
        load.PickupAddress = request.PickupAddress;
        load.PickupLat = request.PickupLat!.Value;
        load.PickupLng = request.PickupLng!.Value;
        load.DropoffAddress = request.DropoffAddress;
        load.DropoffLat = request.DropoffLat!.Value;
        load.DropoffLng = request.DropoffLng!.Value;
        load.PickupWindowStart = request.PickupWindowStart;
        load.PickupWindowEnd = request.PickupWindowEnd;

        await SaveChangesWithConcurrencyCheckAsync(cancellationToken);

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

        if (load.ShipperUserId != cancelledByUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
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

        // A single SaveChangesAsync = a single transaction: the status change and its history row
        // either both land or neither does.
        await SaveChangesWithConcurrencyCheckAsync(cancellationToken);

        return MapToResponse(load);
    }

    /// <summary>
    /// Saves pending changes, translating a concurrent write caught by <c>Load</c>'s xmin
    /// concurrency token (see <c>LoadConfiguration</c>) into a client-facing 409 instead of an
    /// unhandled <see cref="DbUpdateConcurrencyException"/>.
    /// </summary>
    private async Task SaveChangesWithConcurrencyCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_CONCURRENCY_CONFLICT, "This load was modified by another request. Please reload and try again.");
        }
    }

    /// <summary>Throws if <paramref name="end"/> is not after <paramref name="start"/> (mirrors <c>ck_load_window</c>).</summary>
    private static void ValidatePickupWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_PICKUP_WINDOW, "PickupWindowEnd must be after PickupWindowStart.");
        }
    }

    /// <summary>
    /// Throws if the pickup and dropoff coordinates are identical (mirrors <c>ck_load_distinct_points</c>),
    /// so this fails with a client-facing 400 instead of only surfacing as an unhandled DB CHECK
    /// violation (500) on <c>SaveChangesAsync</c>.
    /// </summary>
    private static void ValidateDistinctPoints(decimal pickupLat, decimal pickupLng, decimal dropoffLat, decimal dropoffLng)
    {
        if (pickupLat == dropoffLat && pickupLng == dropoffLng)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.LOAD_PICKUP_DROPOFF_IDENTICAL, "Pickup and dropoff coordinates must not be identical.");
        }
    }

    /// <summary>
    /// Applies the requested sort, falling back to <c>createdAt desc</c> for an unrecognized
    /// <paramref name="sortBy"/>. Every branch appends <c>LoadId</c> as a secondary sort key, in the
    /// same direction as the primary key, since none of the three primary keys are unique —
    /// without a tiebreaker, rows sharing a primary-sort value could be ordered differently between
    /// the count query and the page query (or between two pages of the same request), silently
    /// duplicating or dropping rows at a page boundary.
    /// </summary>
    private static IQueryable<Load> ApplySort(IQueryable<Load> loads, string? sortBy, string? sortDir)
    {
        var ascending = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLowerInvariant() switch
        {
            "pickupwindowstart" => ascending
                ? loads.OrderBy(l => l.PickupWindowStart).ThenBy(l => l.LoadId)
                : loads.OrderByDescending(l => l.PickupWindowStart).ThenByDescending(l => l.LoadId),
            "weightkg" => ascending
                ? loads.OrderBy(l => l.WeightKg).ThenBy(l => l.LoadId)
                : loads.OrderByDescending(l => l.WeightKg).ThenByDescending(l => l.LoadId),
            _ => ascending
                ? loads.OrderBy(l => l.CreatedAt).ThenBy(l => l.LoadId)
                : loads.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.LoadId)
        };
    }

    /// <summary>
    /// Generates a human-scannable load reference code: a fixed <c>LD-</c> prefix followed by a full
    /// GUID's worth of hex digits (128 bits of entropy) rather than a truncated slice of one, so a
    /// collision on <c>uq_load_reference</c> stays vanishingly unlikely even before
    /// <see cref="MaxReferenceCodeGenerationAttempts"/>' retry budget is considered.
    /// </summary>
    private static string GenerateReferenceCode() => $"LD-{Guid.NewGuid():N}".ToUpperInvariant();

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
