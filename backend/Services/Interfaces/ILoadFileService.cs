using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Links already-uploaded files (see <see cref="IFileUploadService"/>) to a <c>Load</c>. Owns no
/// storage/Cloudinary logic itself — purely the metadata linkage and its ownership rules.
/// </summary>
public interface ILoadFileService
{
    /// <summary>Attaches an already-uploaded file to a load. Shipper (own) only.</summary>
    /// <param name="loadId">The load to attach the file to.</param>
    /// <param name="currentUserId">The authenticated caller's user id.</param>
    /// <param name="request">The upload reference and its classification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created attachment, joined with the underlying upload's storage details.</returns>
    Task<LoadFileResponseDto> AttachAsync(Guid loadId, Guid currentUserId, AttachLoadFileDto request, CancellationToken cancellationToken = default);

    /// <summary>Lists a load's attached files. Shipper (own) or Admin.</summary>
    /// <param name="loadId">The load whose files to list.</param>
    /// <param name="currentUserId">The authenticated caller's user id.</param>
    /// <param name="currentUserRole">The authenticated caller's role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Every file attached to the load, most recently attached first.</returns>
    Task<List<LoadFileResponseDto>> ListAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>Detaches a file from a load. Shipper (own) only. Does not delete the underlying upload.</summary>
    /// <param name="loadId">The load the file is attached to.</param>
    /// <param name="fileId">The attachment's own id (not the underlying upload's id).</param>
    /// <param name="currentUserId">The authenticated caller's user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DetachAsync(Guid loadId, Guid fileId, Guid currentUserId, CancellationToken cancellationToken = default);
}
