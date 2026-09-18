using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Serializes/deserializes <see cref="EvidenceType"/> as its member name only. Mirrors
/// <see cref="FileTypeJsonConverter"/> exactly, for the same reason: the plain
/// <see cref="JsonStringEnumConverter"/> applied via <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c>
/// defaults to <c>allowIntegerValues: true</c>, which converts any raw JSON number straight to the
/// enum's underlying value with no membership check — an undefined value like <c>99</c> binds
/// successfully instead of failing model validation. Disabling integer values here means a non-string
/// <c>evidenceType</c> (including a numeric one, defined or not) fails deserialization and surfaces as
/// the standard 400 <c>VALIDATION_ERROR</c> envelope via the existing invalid-model-state pipeline,
/// instead of silently persisting a meaningless enum value.
///
/// <para>
/// Applied to <c>UploadTripEvidenceDto.EvidenceType</c> specifically (not <c>ChangeTripStatusDto.TargetStatus</c>,
/// which uses the plain converter, matching <c>ChangeLoadStatusDto.Status</c>'s existing precedent) because
/// this field's closest analog in the codebase is <c>AttachLoadFileDto.FileType</c> — both classify an
/// already-uploaded, PublicId-referenced resource — and that field already receives this hardening.
/// </para>
/// </summary>
public sealed class EvidenceTypeJsonConverter : JsonStringEnumConverter<EvidenceType>
{
    /// <summary>Creates the converter with integer values disabled.</summary>
    public EvidenceTypeJsonConverter() : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}