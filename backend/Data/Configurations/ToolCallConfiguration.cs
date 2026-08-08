using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class ToolCallConfiguration : IEntityTypeConfiguration<ToolCall>
{
    public void Configure(EntityTypeBuilder<ToolCall> builder)
    {
        builder.HasKey(x => x.ToolCallId);
        builder.Property(x => x.ToolCallId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RequestJson).HasColumnType("jsonb");
        builder.Property(x => x.ResponseJson).HasColumnType("jsonb");

        builder.HasOne(x => x.AgentStep)
            .WithMany(s => s.ToolCalls)
            .HasForeignKey(x => x.AgentStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
