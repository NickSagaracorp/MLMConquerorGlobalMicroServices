using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class GatewayChargeAttemptConfiguration : IEntityTypeConfiguration<GatewayChargeAttempt>
{
    public void Configure(EntityTypeBuilder<GatewayChargeAttempt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RouteBucketKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PresentmentCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.OriginalAmountUsd).HasPrecision(18, 4);
        builder.Property(x => x.ConvertedAmount).HasPrecision(18, 4);
        builder.Property(x => x.ExchangeRateUsed).HasPrecision(18, 8);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(20);
        builder.Property(x => x.GatewayTransactionId).HasMaxLength(500);
        builder.Property(x => x.PaymentHistoryId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // Spec: index on PaymentHistoryId
        builder.HasIndex(x => x.PaymentHistoryId);
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.AttemptedAtUtc);
    }
}
