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

        // Plan K (K2): como mucho un plan de cuota anual.
        builder.Property(x => x.IsAnnualFeePlan).IsRequired().HasDefaultValue(false);
        builder.HasIndex(x => x.IsAnnualFeePlan, "UX_RecurringBillingPlans_AnnualFee")
               .IsUnique()
               .HasFilter("[IsAnnualFeePlan] = 1");
        builder.HasIndex(x => x.CycleType);

        builder.HasMany(x => x.PlanProducts)
               .WithOne(pp => pp.Plan)
               .HasForeignKey(pp => pp.RecurringBillingPlanId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
