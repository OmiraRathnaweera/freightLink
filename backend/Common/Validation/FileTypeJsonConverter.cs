using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Serializes/deserializes <see cref="FileType"/> as its member name only. The plain
/// <see cref="JsonStringEnumConverter"/> applied via <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c>
/// defaults to <c>allowIntegerValues: true</c>, which converts any raw JSON number straight to the
/// enum's underlying value with no membership check — an undefined value like <c>99</c> binds
/// successfully instead of failing model validation. Disabling integer values here means a non-string
/// <c>fileType</c> (including a numeric one, defined or not) fails deserialization and surfaces as the
/// standard 400 <c>VALIDATION_ERROR</c> envelope via the existing invalid-model-state pipeline, instead
/// of silently persisting a meaningless enum value.
/// </summary>
public sealed class FileTypeJsonConverter : JsonStringEnumConverter<FileType>
{
    /// <summary>Creates the converter with integer values disabled.</summary>
    public FileTypeJsonConverter() : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
