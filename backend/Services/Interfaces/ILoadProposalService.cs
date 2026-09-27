using FreightLink.Api.DTOs.LoadProposals;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Manual, non-AI-matching load proposals: an Agency bids a price directly on a <c>Posted</c> load,
/// and the Shipper reviews and accepts one — independent of, and running alongside, the multi-agent
/// matching pipeline. Accepting a proposal creates a normal <c>Assignment</c> so trip dispatch works
/// unmodified afterward.
/// </summary>
public interface ILoadProposalService
{
    /// <summary>Submits a new proposal from the caller's agency on a posted load.</summary>
    Task<LoadProposalResponseDto> CreateAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CreateLoadProposalDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists proposals for a load: the owning Shipper or an Admin sees every proposal; an Agency
    /// Staff caller sees only their own agency's proposal(s).
    /// </summary>
    Task<List<LoadProposalResponseDto>> GetListAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every proposal across all of the caller's own loads (Shipper only) — the aggregate feed
    /// backing the Shipper's "Load Proposals" page, unlike <see cref="GetListAsync"/>'s single-load scope.
    /// </summary>
    Task<List<LoadProposalResponseDto>> GetListForShipperAsync(Guid shipperUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a pending proposal (Shipper only): creates an Assignment for the proposing agency at
    /// the proposed price, advances the load to Matched, and auto-rejects every other pending
    /// proposal on the same load.
    /// </summary>
    Task<LoadProposalResponseDto> AcceptAsync(Guid loadId, Guid proposalId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>Rejects a pending proposal (Shipper only), optionally with a reason.</summary>
    Task<LoadProposalResponseDto> RejectAsync(Guid loadId, Guid proposalId, Guid currentUserId, UserRole currentUserRole, RespondLoadProposalDto? request, CancellationToken cancellationToken = default);

    /// <summary>Withdraws the caller's own pending proposal (Agency Staff only).</summary>
    Task<LoadProposalResponseDto> WithdrawAsync(Guid loadId, Guid proposalId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);
}
