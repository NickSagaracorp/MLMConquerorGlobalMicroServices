using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class SubscriptionBillingStateConfiguration : IEntityTypeConfiguration<SubscriptionBillingState>
{
    public void Configure(EntityTypeBuilder<SubscriptionBillingState> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RowVersion).IsRowVersion();
        // Plan K (K1): anulable. Los estados de cuota anual no tienen suscripción.
        builder.Property(x => x.MembershipSubscriptionId).HasMaxLength(100);
        builder.Property(x => x.Purpose).HasConversion<int>().HasDefaultValue(BillingStatePurpose.Membership);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SubscriptionBillingStates_Purpose",
            "([Purpose] = 1 AND [MembershipSubscriptionId] IS NOT NULL) OR ([Purpose] = 2 AND [MembershipSubscriptionId] IS NULL)"));
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastAttemptOutcome).HasMaxLength(50);
        builder.Property(x => x.LastFailureReason).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Primary scheduling index: daily sweep query filters on Status + NextAttemptDate
        builder.HasIndex(x => new { x.Status, x.NextAttemptDate });
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.MembershipSubscriptionId).IsUnique().HasFilter("[MembershipSubscriptionId] IS NOT NULL");

        // Plan K (K1): un solo estado de cuota anual por miembro. Incluye los borrados lógicos: quien da
        // de alta estados los busca con IgnoreQueryFilters.
        builder.HasIndex(x => x.MemberId, "UX_SubscriptionBillingStates_AnnualFeeMember")
               .IsUnique()
               .HasFilter("[Purpose] = 2");
        builder.HasIndex(x => x.RecurringBillingPlanId);

        // ShardKey: database-assigned monotonic identity used for batch shard partitioning.
        // ValueGeneratedOnAdd ensures the DB sets it via IDENTITY; we never set it from code.
        builder.Property(x => x.ShardKey).ValueGeneratedOnAdd();
        builder.HasIndex(x => x.ShardKey).IsUnique();

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Plan)
               .WithMany()
               .HasForeignKey(x => x.RecurringBillingPlanId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
