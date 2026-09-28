using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Security;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAssignmentActionTokenService" />
public class AssignmentActionTokenService : IAssignmentActionTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly IAssignmentService _assignmentService;

    public AssignmentActionTokenService(AppDbContext dbContext, IAssignmentService assignmentService)
    {
        _dbContext = dbContext;
        _assignmentService = assignmentService;
    }

    /// <inheritdoc />
    public async Task<AssignmentResponseDto> ConsumeActionTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_OR_EXPIRED_ASSIGNMENT_TOKEN, "This link is invalid or has expired.");
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = AccountTokens.HashToken(rawToken);

        var token = await _dbContext.AssignmentActionTokens
            .Include(t => t.Assignment)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.ConsumedAt is not null || token.ExpiresAt <= now)
        {
            // Fail closed with one generic message - never disambiguate "unknown" vs "expired"
            // vs "already used" to a caller who only ever presents a raw token, not credentials.
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_OR_EXPIRED_ASSIGNMENT_TOKEN, "This link is invalid or has expired.");
        }

        if (token.Assignment.Status != AssignmentStatus.Proposed)
        {
            // Already actioned another way (e.g. the mobile app) since this email was sent -
            // burn both tokens and refuse, rather than double-apply an accept/decline.
            token.ConsumedAt = now;
            token.ConsumedReason = "Assignment was no longer actionable (already responded to another way).";
            await InvalidateSiblingAsync(token, now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.ASSIGNMENT_NO_LONGER_ACTIONABLE, "This proposal has already been responded to.");
        }

        // Burn this token (and its still-active sibling) before applying the action, so a token
        // is single-use even if AcceptAsync/DeclineAsync itself later fails for an unrelated reason.
        token.ConsumedAt = now;
        token.ConsumedReason = "Responded via email action link.";
        await InvalidateSiblingAsync(token, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return token.Action == AssignmentActionType.Accept
            ? await _assignmentService.AcceptAsync(token.AssignmentId, null, token.ActingUserId, UserRole.AgencyStaff, cancellationToken)
            : await _assignmentService.DeclineAsync(
                token.AssignmentId,
                new DeclineAssignmentDto { Reason = "Declined via email action link." },
                token.ActingUserId,
                UserRole.AgencyStaff,
                cancellationToken);
    }

    private async Task InvalidateSiblingAsync(Entities.AssignmentActionToken token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sibling = await _dbContext.AssignmentActionTokens.FirstOrDefaultAsync(
            t => t.AssignmentId == token.AssignmentId
                 && t.AssignmentActionTokenId != token.AssignmentActionTokenId
                 && t.ConsumedAt == null,
            cancellationToken);

        if (sibling is not null)
        {
            sibling.ConsumedAt = now;
            sibling.ConsumedReason = "Superseded by the other action link being used.";
        }
    }
}
