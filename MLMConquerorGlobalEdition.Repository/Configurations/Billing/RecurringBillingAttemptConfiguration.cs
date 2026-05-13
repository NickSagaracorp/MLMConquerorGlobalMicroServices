using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class RecurringBillingAttemptConfiguration : IEntityTypeConfiguration<RecurringBillingAttempt>
{
    public void Configure(EntityTypeBuilder<RecurringBillingAttempt> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriptionBillingStateId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProductId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.PaymentHistoryId).HasMaxLength(100);
        builder.Property(x => x.OrderId).HasMaxLength(100);
        builder.Property(x => x.CommissionDeductionEarningId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // Audit queries: filter by member, outcome, date range
        builder.HasIndex(x => new { x.MemberId, x.AttemptedAt });
        builder.HasIndex(x => new { x.Outcome, x.AttemptedAt });
        builder.HasIndex(x => x.SubscriptionBillingStateId);
    }
}
