using FreightLink.Api.DTOs.Assignments;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Consumes single-use email Accept/Decline action tokens (minted by
/// AssignmentService.ConfirmMatchAsync) and applies the resulting decision through the
/// normal <see cref="IAssignmentService.AcceptAsync"/>/<see cref="IAssignmentService.DeclineAsync"/>
/// path, so the existing retry-cascade/notification logic there applies unchanged.
/// </summary>
public interface IAssignmentActionTokenService
{
    /// <summary>
    /// Hashes and looks up <paramref name="rawToken"/>, failing closed (a generic invalid/expired
    /// error, no distinction leaked) if it is unknown, expired, or already consumed. If the
    /// underlying assignment was already accepted/declined another way since the token was
    /// issued, both this token and its still-active sibling are invalidated and a 409 is thrown
    /// without ever calling Accept/DeclineAsync. Otherwise marks this token (and its sibling)
    /// consumed and applies the action.
    /// </summary>
    Task<AssignmentResponseDto> ConsumeActionTokenAsync(string rawToken, CancellationToken cancellationToken = default);
}
