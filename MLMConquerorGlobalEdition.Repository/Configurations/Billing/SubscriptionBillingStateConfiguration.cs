using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class SubscriptionBillingStateConfiguration : IEntityTypeConfiguration<SubscriptionBillingState>
{
    public void Configure(EntityTypeBuilder<SubscriptionBillingState> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.MembershipSubscriptionId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastAttemptOutcome).HasMaxLength(50);
        builder.Property(x => x.LastFailureReason).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Primary scheduling index: daily sweep query filters on Status + NextAttemptDate
        builder.HasIndex(x => new { x.Status, x.NextAttemptDate });
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.MembershipSubscriptionId).IsUnique();
        builder.HasIndex(x => x.RecurringBillingPlanId);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Plan)
               .WithMany()
               .HasForeignKey(x => x.RecurringBillingPlanId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
