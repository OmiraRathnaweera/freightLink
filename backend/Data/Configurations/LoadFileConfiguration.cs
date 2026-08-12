using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class LoadFileConfiguration : IEntityTypeConfiguration<LoadFile>
{
    public void Configure(EntityTypeBuilder<LoadFile> builder)
    {
        builder.ToTable("LoadFiles");

        builder.HasKey(x => x.FileId);
        builder.Property(x => x.FileId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.Load)
            .WithMany(l => l.Files)
            .HasForeignKey(x => x.LoadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UploadedFile)
            .WithMany()
            .HasForeignKey(x => x.UploadedFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UploadedFileId).IsUnique().HasDatabaseName("uq_loadfile_uploadedfileid");
    }
}
