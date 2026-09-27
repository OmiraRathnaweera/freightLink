using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Payload for <c>POST /api/v1/invoices/{id}/payment-proof</c> — links an already-uploaded file
/// (from <c>POST /api/v1/files/single</c>) to an invoice as the Shipper's proof-of-payment receipt.
/// Only references the upload by <see cref="PublicId"/>, mirroring <c>AttachLoadFileDto</c>.
/// </summary>
public class UploadPaymentProofDto
{
    /// <summary>Cloudinary public id of an already-uploaded file, as returned by <c>POST /api/v1/files/single</c>.</summary>
    [Required]
    public string PublicId { get; set; } = string.Empty;
}
