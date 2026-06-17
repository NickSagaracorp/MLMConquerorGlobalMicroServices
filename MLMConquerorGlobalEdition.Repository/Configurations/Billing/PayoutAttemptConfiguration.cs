using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class PayoutAttemptConfiguration : IEntityTypeConfiguration<PayoutAttempt>
{
    public void Configure(EntityTypeBuilder<PayoutAttempt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayoutAccountSnapshot).IsRequired().HasMaxLength(500);
        builder.Property(x => x.PayoutAccountMetaSnapshot).HasMaxLength(2000);
        builder.Property(x => x.AmountUsd).HasPrecision(18, 2);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(20);
        builder.Property(x => x.GatewayTransactionId).HasMaxLength(500);
        builder.Property(x => x.GatewayErrorCode).HasMaxLength(100);
        builder.Property(x => x.GatewayErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.PayoutBatchId).HasMaxLength(50);
        builder.Property(x => x.ReceiptUrl).HasMaxLength(1000);
        builder.Property(x => x.ReceiptSha256).HasMaxLength(64);
        builder.Property(x => x.ReceiptPrevHash).HasMaxLength(64);
        builder.Property(x => x.ReceiptAnchorRef).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.MemberId, x.ProcessDateUtc });
        builder.HasIndex(x => new { x.Outcome, x.ProcessDateUtc });
        builder.HasIndex(x => x.PayoutBatchId);
    }
}
