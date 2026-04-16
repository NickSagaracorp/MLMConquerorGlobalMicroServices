using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

public static class ProductsSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Products.AnyAsync())
        {
            logger.LogDebug("Products already exist. Skipping seed.");
            return;
        }

        var now = DateTime.UtcNow;

        var products = new List<Product>
        {
            // ── Corporate enrollment fee (shown in the Enrollment section) ───────
            new()
            {
                Id                 = Guid.NewGuid().ToString(),
                Name               = "Enrollment Fee",
                Description        = "One-time activation fee to join as a Lifestyle Ambassador.",
                ImageUrl           = string.Empty,
                ThemeClass         = string.Empty,
                SetupFee           = 49.00m,
                MonthlyFee         = 0m,
                IsActive           = true,
                CorporateFee       = true,
                JoinPageMembership = false,
                CreatedBy          = "SYSTEM",
                CreationDate       = now,
                LastUpdateDate     = now
            },

            // ── Membership plans (shown in Select Your Membership) ────────────
            new()
            {
                Id                 = Guid.NewGuid().ToString(),
                Name               = "Guest Pass",
                Description        = "Access to the platform with limited benefits. Great to get started and explore.",
                ImageUrl           = string.Empty,
                ThemeClass         = "theme-product-guest",
                SetupFee           = 0m,
                MonthlyFee         = 29.99m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = true,
                OldSystemProductId = 1,
                CreatedBy          = "SYSTEM",
                CreationDate       = now,
                LastUpdateDate     = now
            },
            new()
            {
                Id                 = Guid.NewGuid().ToString(),
                Name               = "VIP Membership",
                Description        = "Full access to all travel benefits and exclusive member discounts.",
                ImageUrl           = string.Empty,
                ThemeClass         = "theme-product-vip",
                SetupFee           = 0m,
                MonthlyFee         = 59.99m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = true,
                OldSystemProductId = 2,
                CreatedBy          = "SYSTEM",
                CreationDate       = now,
                LastUpdateDate     = now
            },
            new()
            {
                Id                 = Guid.NewGuid().ToString(),
                Name               = "Elite Membership",
                Description        = "Premium tier with priority support, higher commissions, and elite travel perks.",
                ImageUrl           = string.Empty,
                ThemeClass         = "theme-product-elite",
                SetupFee           = 0m,
                MonthlyFee         = 99.99m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = true,
                OldSystemProductId = 3,
                CreatedBy          = "SYSTEM",
                CreationDate       = now,
                LastUpdateDate     = now
            },
            new()
            {
                Id                 = Guid.NewGuid().ToString(),
                Name               = "Turbo Elite",
                Description        = "Top-tier membership with maximum benefits, fastest rank advancement, and VIP concierge.",
                ImageUrl           = string.Empty,
                ThemeClass         = "theme-product-turbo",
                SetupFee           = 0m,
                MonthlyFee         = 149.99m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = true,
                OldSystemProductId = 4,
                CreatedBy          = "SYSTEM",
                CreationDate       = now,
                LastUpdateDate     = now
            }
        };

        await db.Products.AddRangeAsync(products);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} products.", products.Count);
    }
}
