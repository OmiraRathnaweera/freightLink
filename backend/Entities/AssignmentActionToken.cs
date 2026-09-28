using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// A single-use email Accept/Decline action link (see AssignmentService.ConfirmMatchAsync,
/// which mints an Accept+Decline pair per proposal, and AssignmentActionTokenService, which
/// consumes them). Only the SHA-256 hash of the raw token is ever persisted, mirroring the
/// account-token pattern in AuthService (Common/Security/AccountTokens.cs).
/// </summary>
public class AssignmentActionToken
{
    public Guid AssignmentActionTokenId { get; set; }
    public Guid AssignmentId { get; set; }
    public AssignmentActionType Action { get; set; }
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The AgencyStaff user this link acts as when consumed - the person the
    /// proposal email was actually sent to, not whoever happens to click the link.</summary>
    public Guid ActingUserId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public string? ConsumedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public User ActingUser { get; set; } = null!;
}
