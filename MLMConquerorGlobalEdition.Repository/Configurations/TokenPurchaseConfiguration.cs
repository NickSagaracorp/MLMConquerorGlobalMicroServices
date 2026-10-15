using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class TokenPurchaseConfiguration : IEntityTypeConfiguration<TokenPurchase>
{
    public void Configure(EntityTypeBuilder<TokenPurchase> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.GrantedProductIds).IsRequired().HasMaxLength(400);
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(64); // TokenPurchaseRules.IdempotencyKeyMaxLength
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 4);
        builder.Property(x => x.PaymentMethod).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.CreditCardId).HasMaxLength(450);
        builder.Property(x => x.PaymentHistoryId).HasMaxLength(450);
        builder.Property(x => x.FailureCode).HasMaxLength(64);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Ignore(x => x.IsReservation);

        // La idempotencia vive en la base, no solo en el código.
        builder.HasIndex(x => new { x.MemberId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.MemberId, x.PeriodKey, x.Status });
        builder.HasIndex(x => x.Status);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
