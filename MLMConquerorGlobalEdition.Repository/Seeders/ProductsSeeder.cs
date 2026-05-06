using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

/// <summary>
/// Idempotent fallback seeder for the Products catalog.
///
/// Mirrors the canonical data inserted by migrations
/// AddProductSeeds + AddProductJoinPageFlags + RestoreEliteProduct + SetVipJoinPageMembership.
/// Uses the same hardcoded GUIDs so migrations and this seeder converge on identical rows.
///
/// JoinPageMembership = true on Travel Advantage VIP / Elite / Turbo
/// CorporateFee       = true on Subscription (annual ambassador fee)
/// </summary>
public static class ProductsSeeder
{
    public const string GuestMemberId = "00000001-prod-0000-0000-000000000001";
    public const string VipId         = "00000002-prod-0000-0000-000000000002";
    public const string EliteId       = "00000003-prod-0000-0000-000000000003";
    public const string TurboId       = "00000004-prod-0000-0000-000000000004";
    public const string SubscriptionId      = "00000005-prod-0000-0000-000000000005";
    public const string MonthlySubscriptionId = "00000006-prod-0000-0000-000000000006";

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var seedDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

        var canonical = new List<Product>
        {
            new()
            {
                Id                 = GuestMemberId,
                Name               = "Travel Advantage Guest Member",
                Description        = "Free guest access to the Travel Advantage platform. No qualification points. No commissions triggered. Upgrade required to earn full benefits.",
                ImageUrl           = string.Empty,
                ThemeClass         = string.Empty,
                SetupFee           = 0m,
                MonthlyFee         = 0m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = false,
                OldSystemProductId = 1,
                MembershipLevelId  = null,
                QualificationPoins = 0,
                CreatedBy          = "seed",
                CreationDate       = seedDate,
                LastUpdateDate     = seedDate
            },
            new()
            {
                Id                 = VipId,
                Name               = "Travel Advantage VIP",
                Description        = "Entry-level Travel Advantage membership. Earns 3 qualification points per billing cycle. Triggers VIP Member Bonus ($20) and all standard enrollment commissions.",
                ImageUrl           = string.Empty,
                ThemeClass         = "theme-product-vip",
                SetupFee           = 0m,
                MonthlyFee         = 40m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = true,
                OldSystemProductId = 2,
                MembershipLevelId  = 2,
                QualificationPoins = 3,
                CreatedBy          = "seed",
                CreationDate       = seedDate,
                LastUpdateDate     = seedDate
            },
            new()
            {
                Id                 = EliteId,
                Name               = "Travel Advantage Elite",
                Description        = "Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.",
                ImageUrl           = string.Empty,
                ThemeClass         = "theme-product-elite",
                SetupFee           = 0m,
                MonthlyFee         = 99m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = true,
                OldSystemProductId = 3,
                MembershipLevelId  = 3,
                QualificationPoins = 6,
                CreatedBy          = "seed",
                CreationDate       = seedDate,
                LastUpdateDate     = seedDate
            },
            new()
            {
                Id                 = TurboId,
                Name               = "Travel Advantage Turbo",
                Description        = "Premium Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Turbo Member Bonus ($80), full commissions, and Builder Bonus Turbo program.",
                ImageUrl           = string.Empty,
                ThemeClass         = "theme-product-turbo",
                SetupFee           = 0m,
                MonthlyFee         = 199m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = true,
                OldSystemProductId = 4,
                MembershipLevelId  = 4,
                QualificationPoins = 6,
                CreatedBy          = "seed",
                CreationDate       = seedDate,
                LastUpdateDate     = seedDate
            },
            new()
            {
                Id                 = SubscriptionId,
                Name               = "Subscription",
                Description        = "Annual ambassador business fee. Operational/administrative product. Does not earn qualification points and does not trigger commissions.",
                ImageUrl           = string.Empty,
                ThemeClass         = string.Empty,
                SetupFee           = 99m,
                MonthlyFee         = 0m,
                AnnualPrice        = 99m,
                IsActive           = true,
                CorporateFee       = true,
                JoinPageMembership = false,
                OldSystemProductId = 5,
                MembershipLevelId  = 1,
                QualificationPoins = 0,
                CreatedBy          = "seed",
                CreationDate       = seedDate,
                LastUpdateDate     = seedDate
            },
            new()
            {
                Id                 = MonthlySubscriptionId,
                Name               = "Monthly Subscription",
                Description        = "Generic recurring monthly subscription. Operational/administrative product. Does not earn qualification points and does not trigger commissions.",
                ImageUrl           = string.Empty,
                ThemeClass         = string.Empty,
                SetupFee           = 0m,
                MonthlyFee         = 0m,
                IsActive           = true,
                CorporateFee       = false,
                JoinPageMembership = false,
                OldSystemProductId = 6,
                MembershipLevelId  = null,
                QualificationPoins = 0,
                CreatedBy          = "seed",
                CreationDate       = seedDate,
                LastUpdateDate     = seedDate
            }
        };

        var existingIds = await db.Products
            .Select(p => p.Id)
            .ToListAsync();
        var existingSet = existingIds.ToHashSet();

        var toAdd = canonical.Where(p => !existingSet.Contains(p.Id)).ToList();
        if (toAdd.Count > 0)
        {
            await db.Products.AddRangeAsync(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} products.", toAdd.Count);
        }

        // Idempotent flag patch — restore JoinPageMembership / CorporateFee
        // on any pre-existing rows that drifted (e.g., manual SQL edits or
        // older seed runs that pre-dated the flag migrations).
        var idsNeedingPatch = canonical
            .Where(c => c.JoinPageMembership || c.CorporateFee)
            .Select(c => c.Id)
            .ToList();

        var rows = await db.Products
            .Where(p => idsNeedingPatch.Contains(p.Id))
            .ToListAsync();

        var patched = 0;
        foreach (var row in rows)
        {
            var canon = canonical.First(c => c.Id == row.Id);
            if (row.JoinPageMembership != canon.JoinPageMembership ||
                row.CorporateFee       != canon.CorporateFee       ||
                row.MembershipLevelId  != canon.MembershipLevelId)
            {
                row.JoinPageMembership = canon.JoinPageMembership;
                row.CorporateFee       = canon.CorporateFee;
                row.MembershipLevelId  = canon.MembershipLevelId;
                patched++;
            }
        }

        if (patched > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Patched JoinPage/Corporate flags on {Count} products.", patched);
        }
    }
}
