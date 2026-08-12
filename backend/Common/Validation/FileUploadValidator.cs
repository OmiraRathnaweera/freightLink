using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Structural validation for uploaded files against <see cref="FileUploadPolicy"/> — required,
/// not a blocked extension, within the size limit. Used by <see cref="Services.FileUploadService"/>.
/// </summary>
public static class FileUploadValidator
{
    /// <summary>Validates a single file, throwing on the first failure.</summary>
    /// <param name="file">The uploaded file, or <see langword="null"/>.</param>
    /// <exception cref="ApiException">
    /// 400 <see cref="ErrorCode.FILE_REQUIRED"/> if <paramref name="file"/> is null/empty;
    /// 400 <see cref="ErrorCode.BLOCKED_FILE_TYPE"/> if its extension is not an allowed image/PDF type;
    /// 400 <see cref="ErrorCode.FILE_TOO_LARGE"/> if it exceeds <see cref="FileUploadPolicy.MaxFileBytes"/>.
    /// </exception>
    public static void ValidateOrThrow(IFormFile? file)
    {
        var failure = ValidateCore(file);
        if (failure is { } f)
        {
            throw new ApiException(HttpStatusCode.BadRequest, f.Code, f.Message);
        }
    }

    /// <summary>
    /// Validates a single file without throwing — used by batch uploads so every file's failure
    /// can be collected into one response instead of stopping at the first bad file.
    /// </summary>
    /// <param name="file">The uploaded file, or <see langword="null"/>.</param>
    /// <returns>A validation failure keyed by the file's name, or <see langword="null"/> if valid.</returns>
    public static ValidationErrorItemDto? Validate(IFormFile? file)
    {
        var failure = ValidateCore(file);
        return failure is { } f ? new ValidationErrorItemDto { Field = f.FieldName, Issue = f.Message } : null;
    }

    /// <summary>Runs every structural check once, shared by the throwing and non-throwing entry points above.</summary>
    private static (ErrorCode Code, string FieldName, string Message)? ValidateCore(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return (ErrorCode.FILE_REQUIRED, file?.FileName ?? "file", "A non-empty file is required.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !FileUploadPolicy.AllowedExtensions.Contains(extension))
        {
            return (ErrorCode.BLOCKED_FILE_TYPE, file.FileName, "Only image files (JPG, PNG, GIF, WEBP, BMP, HEIC, TIFF) and PDF files are allowed.");
        }

        if (file.Length > FileUploadPolicy.MaxFileBytes)
        {
            return (ErrorCode.FILE_TOO_LARGE, file.FileName, $"File exceeds the maximum allowed size of {FileUploadPolicy.MaxFileBytes} bytes.");
        }

        return null;
    }
}
