using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class LoadFileConfiguration : IEntityTypeConfiguration<LoadFile>
{
    public void Configure(EntityTypeBuilder<LoadFile> builder)
    {
        builder.ToTable("Files");

        builder.HasKey(x => x.FileId);
        builder.Property(x => x.FileId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.Load)
            .WithMany(l => l.Files)
            .HasForeignKey(x => x.LoadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UploadedByUser)
            .WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StorageKey).IsUnique().HasDatabaseName("uq_file_storagekey");

        builder.ToTable(t => t.HasCheckConstraint("ck_file_size",
            "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760"));
    }
}
