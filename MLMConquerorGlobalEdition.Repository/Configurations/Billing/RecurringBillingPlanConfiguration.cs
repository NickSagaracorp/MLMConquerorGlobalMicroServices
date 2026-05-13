using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class RecurringBillingPlanConfiguration : IEntityTypeConfiguration<RecurringBillingPlan>
{
    public void Configure(EntityTypeBuilder<RecurringBillingPlan> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RetryCadenceDays).IsRequired().HasMaxLength(100);
        builder.Property(x => x.FixedAmountOverride).HasPrecision(18, 4);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.CycleType);

        builder.HasMany(x => x.PlanProducts)
               .WithOne(pp => pp.Plan)
               .HasForeignKey(pp => pp.RecurringBillingPlanId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
