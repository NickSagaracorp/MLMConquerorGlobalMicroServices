using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Commission;

public class CommissionTriggerQueueConfiguration : IEntityTypeConfiguration<CommissionTriggerQueue>
{
    public void Configure(EntityTypeBuilder<CommissionTriggerQueue> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.TriggerType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // Stage 4 query: pending triggers for a batch
        builder.HasIndex(x => new { x.BatchId, x.IsProcessed });
        builder.HasIndex(x => x.MemberId);
    }
}
