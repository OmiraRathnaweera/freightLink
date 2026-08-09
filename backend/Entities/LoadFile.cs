using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class LoadFile
{
    public Guid FileId { get; set; }
    public Guid LoadId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public FileType FileType { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    public Load Load { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
