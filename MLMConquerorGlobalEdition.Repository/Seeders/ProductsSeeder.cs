using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

public static class ProductsSeeder
{
    // MembershipLevelId assignments: 1=Guest Pass, 2=VIP, 3=Elite, 4=Turbo
    private static readonly Dictionary<string, int> MembershipLevelByProductName = new()
    {
        ["Guest Pass"]       = 1,
        ["VIP Membership"]   = 2,
        ["Elite Membership"] = 3,
        ["Turbo Elite"]      = 4
    };

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var now = DateTime.UtcNow;

        if (await db.Products.AnyAsync())
        {
            // Patch any existing products that are missing MembershipLevelId.
            // Required because the original seeder didn't set this field, which
            // prevents commission engines from matching products to membership tiers.
            var existing = await db.Products
                .Where(p => MembershipLevelByProductName.Keys.Contains(p.Name)
                         && p.MembershipLevelId == null)
                .ToListAsync();

            if (existing.Count > 0)
            {
                foreach (var p in existing)
                    if (MembershipLevelByProductName.TryGetValue(p.Name, out var lvl))
                        p.MembershipLevelId = lvl;

                await db.SaveChangesAsync();
                logger.LogInformation("Patched MembershipLevelId on {Count} existing products.", existing.Count);
            }
            else
            {
                logger.LogDebug("Products already exist and are up to date. Skipping seed.");
            }

            return;
        }

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
                MembershipLevelId  = 1,
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
                MembershipLevelId  = 2,
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
                MembershipLevelId  = 3,
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
                MembershipLevelId  = 4,
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
