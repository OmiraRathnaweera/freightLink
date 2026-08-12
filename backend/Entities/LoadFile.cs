using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// Links a <see cref="Load"/> to a file already stored via the shared upload infrastructure
/// (<see cref="UploadedFile"/>), classifying why it's attached. Storage/media details (URL, format,
/// size, content type, uploader) live on <see cref="UploadedFile"/> and are not duplicated here.
/// </summary>
public class LoadFile
{
    public Guid FileId { get; set; }
    public Guid LoadId { get; set; }
    public Guid UploadedFileId { get; set; }
    public FileType FileType { get; set; }
    public DateTimeOffset AttachedAt { get; set; }

    public Load Load { get; set; } = null!;
    public UploadedFile UploadedFile { get; set; } = null!;
}
