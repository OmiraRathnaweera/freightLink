namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Regex patterns shared between DTO-level <c>[RegularExpression]</c> validation and
/// server-side service guards for Invoice-related fields, kept in exactly one place so they
/// can never drift from the matching Postgres CHECK constraints on <c>Invoices</c>
/// (see <c>Data/Configurations/InvoiceConfiguration.cs</c>). A request that satisfies
/// these patterns is guaranteed to also satisfy the DB CHECK, so no valid-looking request
/// should ever reach Postgres and fail as an unhandled 500.
/// </summary>
public static class InvoicePatterns
{
    /// <summary>
    /// Matches <c>ck_invoice_currency</c> exactly: exactly three uppercase ASCII letters.
    /// Applied <em>after</em> the service normalises the raw input via
    /// <c>.Trim().ToUpperInvariant()</c>, so lower-case inputs submitted by the caller are
    /// up-cased before the DB constraint fires — but the DTO-level
    /// <c>[RegularExpression]</c> attribute runs <em>before</em> the service and therefore
    /// validates the raw (already model-bound) value.
    ///
    /// <para>
    /// Because the DTO default is <c>"LKR"</c> and the service normalises via
    /// <c>ToUpperInvariant()</c>, callers who send lower-case codes (e.g. <c>"usd"</c>) will
    /// fail the DTO regex. This is intentional: the API contract requires ISO-4217 uppercase
    /// codes; the service normalisation exists only as a secondary safety net, not a
    /// permissive input channel.
    /// </para>
    /// </summary>
    public const string CurrencyCodePattern = @"^[A-Z]{3}$";
}
