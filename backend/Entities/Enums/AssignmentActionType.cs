namespace FreightLink.Api.Entities.Enums;

/// <summary>Which action an <see cref="AssignmentActionToken"/> performs when consumed.
/// Named to match <c>IAssignmentService.AcceptAsync</c>/<c>DeclineAsync</c> (not the
/// Shipper-side Approve/Reject/Revise vocabulary, which is a separate decision entirely).</summary>
public enum AssignmentActionType
{
    Accept,
    Decline
}
