using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class RecurringBillingBatchConfiguration : IEntityTypeConfiguration<RecurringBillingBatch>
{
    public void Configure(EntityTypeBuilder<RecurringBillingBatch> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);
        builder.Property(x => x.Gateway).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();

        // Primary query: find batches by run date + processor
        builder.HasIndex(x => new { x.RunDate, x.Gateway });
        builder.HasIndex(x => x.Status);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Shards)
               .WithOne(s => s.Batch)
               .HasForeignKey(s => s.BatchId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
