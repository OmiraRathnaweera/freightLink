using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Agency;

/// <summary>
/// Replaces an existing compliance document's file/number/dates in place (e.g. re-uploading after a
/// rejection, or renewing an expiring document). <c>DocType</c> is not included here — it is fixed by
/// the target row's id and is never changed by a replace.
/// </summary>
public class ComplianceDocUpdateDto
{
    [Required]
    public string PublicId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string DocNumber { get; set; } = string.Empty;

    public DateOnly IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }
}
