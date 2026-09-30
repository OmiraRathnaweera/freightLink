using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Common.Validation;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Payload for <c>POST /api/v1/trips/{id}/evidence</c> — links an already-uploaded file (from
/// <c>POST /api/v1/files/single</c>) to a trip as proof-of-pickup or proof-of-delivery. Same
/// "reference by public id, don't re-upload" pattern as <see cref="Files.AttachLoadFileDto"/>. Per
/// the role matrix (API contract Section 4.4), the (not-yet-implemented) service layer must reject
/// an <see cref="EvidenceType"/> that doesn't match the caller's role — AgencyStaff may only submit
/// <c>PickupProof</c>, Driver may only submit <c>DeliveryProof</c> — this DTO only carries the data,
/// it does not enforce that rule itself.
/// </summary>
public class UploadTripEvidenceDto
{
    /// <summary>Cloudinary public id of an already-uploaded file, as returned by <c>POST /api/v1/files/single</c>.</summary>
    [Required]
    public string PublicId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is pickup or delivery proof. Nullable so <see cref="RequiredAttribute"/> actually
    /// rejects an omitted JSON field instead of silently binding it to the enum's default member
    /// (mirrors <see cref="Files.AttachLoadFileDto.FileType"/>). Uses <see cref="EvidenceTypeJsonConverter"/>
    /// (not the plain <c>JsonStringEnumConverter</c>) for the same reason <c>AttachLoadFileDto.FileType</c>
    /// does — see that converter's XML docs — since this field is that DTO's closest structural analog.
    /// </summary>
    [Required]
    [JsonConverter(typeof(EvidenceTypeJsonConverter))]
    public EvidenceType? EvidenceType { get; set; }

    /// <summary>Optional GPS latitude captured at the moment of evidence capture.</summary>
    [Range(-90, 90)]
    public decimal? CapturedLat { get; set; }

    /// <summary>Optional GPS longitude captured at the moment of evidence capture.</summary>
    [Range(-180, 180)]
    public decimal? CapturedLng { get; set; }
}