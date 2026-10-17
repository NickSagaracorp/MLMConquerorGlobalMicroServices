using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class AnnualFeePaymentConfiguration : IEntityTypeConfiguration<AnnualFeePayment>
{
    public void Configure(EntityTypeBuilder<AnnualFeePayment> builder)
    {
        builder.ToTable("AnnualFeePayments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        // Id de miembro sin FK a MemberProfiles: 100 (como SubscriptionBillingStates). Ids de entidades: 450.
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SubscriptionBillingStateId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.PaymentHistoryId).HasMaxLength(450);

        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Method).HasConversion<int>();
        builder.Property(x => x.Source).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.TokenCode).HasMaxLength(AnnualFeePayment.TokenCodeMaxLength);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(TokenPurchaseRules.IdempotencyKeyMaxLength);
        builder.Property(x => x.FailureCode).HasMaxLength(AnnualFeePayment.FailureCodeMaxLength);
        builder.Property(x => x.Notes).HasMaxLength(AnnualFeePayment.NotesMaxLength);
        // 256, lo que mide un correo: el actor puede ser un email y no solo un id.
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(256);
        builder.Ignore(x => x.IsLive);

        // La idempotencia y el "un cobro vivo por ciclo" viven en la base, no solo en el código (K10).
        builder.HasIndex(x => new { x.MemberId, x.IdempotencyKey }, "UX_AnnualFeePayments_Member_IdempotencyKey")
               .IsUnique()
               .HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(x => new { x.SubscriptionBillingStateId, x.CycleDueDate }, "UX_AnnualFeePayments_LivePerCycle")
               .IsUnique()
               .HasFilter("[Status] IN (1, 2, 4)");
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreationDate });
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });

        builder.HasOne<SubscriptionBillingState>()
               .WithMany()
               .HasForeignKey(x => x.SubscriptionBillingStateId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
