using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class RecurringBillingBatchShardConfiguration : IEntityTypeConfiguration<RecurringBillingBatchShard>
{
    public void Configure(EntityTypeBuilder<RecurringBillingBatchShard> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.AssignedWorkerKey).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<int>();

        // Worker lookup: find shards by batch + shard index
        builder.HasIndex(x => new { x.BatchId, x.ShardIndex }).IsUnique();
        // Status sweep: aggregator checks if all shards are Done
        builder.HasIndex(x => new { x.BatchId, x.Status });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
