using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Commission;

public class PointDeltaEventConfiguration : IEntityTypeConfiguration<PointDeltaEvent>
{
    public void Configure(EntityTypeBuilder<PointDeltaEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProductId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EventType).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();

        // Aggregator query: batch-scoped pending events
        builder.HasIndex(x => new { x.BatchId, x.Status });
        // Member-scoped queries for debugging
        builder.HasIndex(x => x.MemberId);
    }
}
