using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

/// <summary>EF Core mapping for <see cref="UploadedFile"/>.</summary>
public class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
{
    /// <summary>Configures the <c>UploadedFiles</c> table.</summary>
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        builder.ToTable("UploadedFiles");

        builder.HasKey(x => x.FileId);
        builder.Property(x => x.FileId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.UploadedByUser)
            .WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("uq_uploadedfile_publicid");
    }
}
