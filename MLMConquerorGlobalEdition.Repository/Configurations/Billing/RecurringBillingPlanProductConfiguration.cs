using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class RecurringBillingPlanProductConfiguration : IEntityTypeConfiguration<RecurringBillingPlanProduct>
{
    public void Configure(EntityTypeBuilder<RecurringBillingPlanProduct> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Each product can appear in at most one active plan (no FK enforcement, just index for lookup)
        builder.HasIndex(x => new { x.RecurringBillingPlanId, x.ProductId }).IsUnique();
        builder.HasIndex(x => x.ProductId);
    }
}
