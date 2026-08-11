namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Regex patterns shared between DTO-level <c>[RegularExpression]</c> validation and server-side
/// checks (e.g. admin seeding), kept in exactly one place so they can never drift from the matching
/// Postgres CHECK constraints on <c>Users</c> (<c>ck_user_email_format</c>, <c>ck_user_phone_e164</c>,
/// see <c>Data/Configurations/UserConfiguration.cs</c>). A request that satisfies these patterns is
/// guaranteed to also satisfy the DB CHECK, so no valid-looking request should ever reach Postgres
/// and fail as an unhandled 500.
/// </summary>
public static class AuthPatterns
{
    /// <summary>Matches <c>ck_user_email_format</c> exactly.</summary>
    public const string EmailPattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";

    /// <summary>Matches <c>ck_user_phone_e164</c> exactly (mandatory leading '+').</summary>
    public const string PhonePattern = @"^\+[1-9]\d{1,14}$";
}
