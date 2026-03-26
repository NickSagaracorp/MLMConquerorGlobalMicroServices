using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Commission trigger rules per product.
/// Only Travel Advantage products (VIP, Elite, Turbo) trigger commissions.
/// Guest Member, Subscription, and Monthly Subscription have no commission entry.
///
/// Turbo product additionally enables the Turbo builder-bonus program
/// (TriggerBuilderBonusTurbo = TriggerSponsorBonusTurbo = true).
/// </summary>
public class ProductCommissionConfiguration : IEntityTypeConfiguration<ProductCommission>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ProductCommission> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).IsRequired();

        builder.HasIndex(x => x.ProductId).IsUnique();

        builder.HasOne(x => x.Product)
            .WithMany(p => p.ProductCommissions)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(

            // Travel Advantage VIP — standard commissions, no Turbo program
            new ProductCommission
            {
                Id = 1,
                ProductId                 = ProductConfiguration.VipId,
                TriggerSponsorBonus       = true,
                TriggerBuilderBonus       = true,
                TriggerSponsorBonusTurbo  = false,
                TriggerBuilderBonusTurbo  = false,
                TriggerFastStartBonus     = true,
                TriggerBoostBonus         = true,
                CarBonusEligible          = true,
                PresidentialBonusEligible = true,
                EligibleMembershipResidual = true,
                EligibleDailyResidual      = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // Travel Advantage Elite — standard commissions, no Turbo program
            new ProductCommission
            {
                Id = 2,
                ProductId                 = ProductConfiguration.EliteId,
                TriggerSponsorBonus       = true,
                TriggerBuilderBonus       = true,
                TriggerSponsorBonusTurbo  = false,
                TriggerBuilderBonusTurbo  = false,
                TriggerFastStartBonus     = true,
                TriggerBoostBonus         = true,
                CarBonusEligible          = true,
                PresidentialBonusEligible = true,
                EligibleMembershipResidual = true,
                EligibleDailyResidual      = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // Travel Advantage Turbo — full commissions + Turbo builder-bonus program
            new ProductCommission
            {
                Id = 3,
                ProductId                 = ProductConfiguration.TurboId,
                TriggerSponsorBonus       = true,
                TriggerBuilderBonus       = true,
                TriggerSponsorBonusTurbo  = true,
                TriggerBuilderBonusTurbo  = true,
                TriggerFastStartBonus     = true,
                TriggerBoostBonus         = true,
                CarBonusEligible          = true,
                PresidentialBonusEligible = true,
                EligibleMembershipResidual = true,
                EligibleDailyResidual      = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            }
        );
    }
}
