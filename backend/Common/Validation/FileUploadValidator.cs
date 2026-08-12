using System.Net;
using System.Text;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Structural validation for uploaded files against <see cref="FileUploadPolicy"/> — required, not
/// a blocked extension, within the size limit, and its actual bytes match what the extension claims
/// (a magic-number sniff, since the extension alone is client-supplied and trivially spoofed).
/// Used by <see cref="Services.FileUploadService"/>.
/// </summary>
public static class FileUploadValidator
{
    /// <summary>Validates a single file, throwing on the first failure.</summary>
    /// <param name="file">The uploaded file, or <see langword="null"/>.</param>
    /// <exception cref="ApiException">
    /// 400 <see cref="ErrorCode.FILE_REQUIRED"/> if <paramref name="file"/> is null/empty;
    /// 400 <see cref="ErrorCode.BLOCKED_FILE_TYPE"/> if its extension is not an allowed image/PDF
    /// type, or its content doesn't match that extension's expected signature;
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

    /// <summary>Number of leading bytes read to check a file's signature — enough for every format in <see cref="ContentMatchesExtension"/>.</summary>
    private const int SignatureBufferSize = 16;

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

        if (!HasMatchingContentSignature(file, extension))
        {
            return (ErrorCode.BLOCKED_FILE_TYPE, file.FileName, "The file's content does not match its extension.");
        }

        return null;
    }

    /// <summary>
    /// Sniffs the file's leading bytes and checks them against the signature expected for its
    /// (already-allowed) extension — an extension alone is client-supplied and trivially spoofed by
    /// renaming an arbitrary file, so this confirms the actual bytes match before the stream is
    /// handed to storage.
    /// </summary>
    private static bool HasMatchingContentSignature(IFormFile file, string extension)
    {
        var stream = file.OpenReadStream();
        var header = new byte[SignatureBufferSize];
        var totalRead = 0;

        try
        {
            int read;
            while (totalRead < header.Length && (read = stream.Read(header, totalRead, header.Length - totalRead)) > 0)
            {
                totalRead += read;
            }
        }
        finally
        {
            // The same IFormFile is read again by the storage layer once validation passes — reset
            // so that read starts from the beginning rather than where this sniff left off.
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }

        return ContentMatchesExtension(extension, header, totalRead);
    }

    /// <summary>Checks leading bytes against the magic-number signature(s) known for each allowed extension.</summary>
    private static bool ContentMatchesExtension(string extension, byte[] header, int bytesRead)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => StartsWith(header, bytesRead, 0xFF, 0xD8, 0xFF),
            ".png" => StartsWith(header, bytesRead, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".gif" => StartsWithAscii(header, bytesRead, "GIF87a") || StartsWithAscii(header, bytesRead, "GIF89a"),
            ".bmp" => StartsWithAscii(header, bytesRead, "BM"),
            ".pdf" => StartsWithAscii(header, bytesRead, "%PDF"),
            ".tif" or ".tiff" => StartsWith(header, bytesRead, 0x49, 0x49, 0x2A, 0x00) || StartsWith(header, bytesRead, 0x4D, 0x4D, 0x00, 0x2A),
            // RIFF????WEBP: "RIFF" at offset 0, "WEBP" at offset 8.
            ".webp" => bytesRead >= 12 && StartsWithAscii(header, bytesRead, "RIFF") && StartsWithAscii(header[8..], bytesRead - 8, "WEBP"),
            // ISO base media file format "ftyp" box, at offset 4 (offsets 0-3 are the box size).
            ".heic" or ".heif" => bytesRead >= 8 && StartsWithAscii(header[4..], bytesRead - 4, "ftyp"),
            _ => false // unreachable: the extension was already confirmed to be in FileUploadPolicy.AllowedExtensions
        };
    }

    private static bool StartsWithAscii(byte[] header, int bytesRead, string ascii) =>
        StartsWith(header, bytesRead, Encoding.ASCII.GetBytes(ascii));

    private static bool StartsWith(byte[] header, int bytesRead, params byte[] signature)
    {
        if (bytesRead < signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (header[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }
}
