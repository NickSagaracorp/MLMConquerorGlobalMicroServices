using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Loyalty points earned per billing cycle per product.
/// Only Travel Advantage products earn loyalty points.
///
///   Travel Advantage VIP    — 3 pts/cycle
///   Travel Advantage Elite  — 6 pts/cycle
///   Travel Advantage Turbo  — 6 pts/cycle
///
/// RequiredSuccessfulPayments = 1: points lock until the first successful
/// renewal payment is confirmed (prevents awarding points on chargebacks).
/// </summary>
public class ProductLoyaltyPointsSettingConfiguration : IEntityTypeConfiguration<ProductLoyaltyPointsSetting>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ProductLoyaltyPointsSetting> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.PointsPerUnit).HasColumnType("decimal(10,2)");
        builder.HasIndex(x => x.ProductId).IsUnique();

        builder.HasData(

            new ProductLoyaltyPointsSetting
            {
                Id = 1,
                ProductId                   = ProductConfiguration.VipId,
                PointsPerUnit               = 3m,
                RequiredSuccessfulPayments  = 1,
                IsActive                    = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new ProductLoyaltyPointsSetting
            {
                Id = 2,
                ProductId                   = ProductConfiguration.EliteId,
                PointsPerUnit               = 6m,
                RequiredSuccessfulPayments  = 1,
                IsActive                    = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new ProductLoyaltyPointsSetting
            {
                Id = 3,
                ProductId                   = ProductConfiguration.TurboId,
                PointsPerUnit               = 6m,
                RequiredSuccessfulPayments  = 1,
                IsActive                    = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            }
        );
    }
}
