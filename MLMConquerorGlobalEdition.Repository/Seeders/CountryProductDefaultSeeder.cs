using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Configurations;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

/// <summary>
/// Seeds default CountryProduct mappings on first run.
/// Countries in VipAllowedIso2 → VIP + Elite + Turbo + Subscription.
/// All other active countries     → Elite + Turbo + Subscription only.
///
/// Also ensures all VIP-allowed countries are active and present in the DB.
/// Idempotent: only seeds countries that have ZERO mappings. Admin-configured
/// mappings are never overwritten.
/// </summary>
public static class CountryProductDefaultSeeder
{
    /// <summary>
    /// ISO-2 codes whose members can choose the VIP product on signup.
    /// Lower-purchasing-power markets where VIP is the accessible tier.
    /// </summary>
    private static readonly HashSet<string> VipAllowedIso2 = new(StringComparer.OrdinalIgnoreCase)
    {
        "ZA","SR","KE","BW","DZ","AO","BJ","BF","BI","CM","TD","KM","CG","CI",
        "DJ","CD","EG","GQ","GA","GH","GN","GW","MG","ML","MR","MU","MA","NA",
        "NG","RW","SN","SL","TG","TN","UG","ZM","ZW","TZ","CV","SC","DM","GY",
        "LC","PR","NE","TT","BZ","PH"
    };

    /// <summary>
    /// Countries that are in VipAllowedIso2 but absent from the CountriesSeeder.
    /// These are inserted if they don't exist in the DB.
    /// </summary>
    private static readonly List<Country> MissingVipCountries = new()
    {
        new Country
        {
            Iso2 = "CI", Iso3 = "CIV", NameEn = "Côte d'Ivoire", NameNative = "Côte d'Ivoire",
            DefaultLanguageCode = "fr", FlagEmoji = "🇨🇮", PhoneCode = "+225",
            IsActive = true, SortOrder = 0, CreatedBy = "seeder", CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new Country
        {
            Iso2 = "PR", Iso3 = "PRI", NameEn = "Puerto Rico", NameNative = "Puerto Rico",
            DefaultLanguageCode = "es", FlagEmoji = "🇵🇷", PhoneCode = "+1-787",
            IsActive = true, SortOrder = 0, CreatedBy = "seeder", CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }
    };

    public static async Task SeedAsync(AppDbContext db, ILogger logger,
        CancellationToken ct = default)
    {
        // ── Step 1: Insert missing VIP countries (CI, PR) ─────────────────────
        var existingIso2s = await db.Countries
            .AsNoTracking()
            .Select(c => c.Iso2)
            .ToListAsync(ct);

        var existingIso2Set = new HashSet<string>(existingIso2s, StringComparer.OrdinalIgnoreCase);

        var countriesToInsert = MissingVipCountries
            .Where(c => !existingIso2Set.Contains(c.Iso2))
            .ToList();

        if (countriesToInsert.Count > 0)
        {
            await db.Countries.AddRangeAsync(countriesToInsert, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "[CountryProductDefaultSeeder] Inserted {Count} missing VIP countries: {Codes}.",
                countriesToInsert.Count,
                string.Join(", ", countriesToInsert.Select(c => c.Iso2)));
        }

        // ── Step 2: Activate all VIP-allowed countries that are inactive ───────
        var inactiveVipCountries = await db.Countries
            .Where(c => VipAllowedIso2.Contains(c.Iso2) && !c.IsActive)
            .ToListAsync(ct);

        if (inactiveVipCountries.Count > 0)
        {
            foreach (var country in inactiveVipCountries)
                country.IsActive = true;

            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "[CountryProductDefaultSeeder] Activated {Count} VIP-allowed countries: {Codes}.",
                inactiveVipCountries.Count,
                string.Join(", ", inactiveVipCountries.Select(c => c.Iso2)));
        }

        // ── Step 3: Resolve product IDs ────────────────────────────────────────
        var vipId          = ProductConfiguration.VipId;
        var eliteId        = ProductConfiguration.EliteId;
        var turboId        = ProductConfiguration.TurboId;
        var subscriptionId = ProductConfiguration.SubscriptionId;

        var existingProductIds = await db.Products
            .AsNoTracking()
            .Where(p => new[] { vipId, eliteId, turboId, subscriptionId }.Contains(p.Id) && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (existingProductIds.Count == 0)
        {
            logger.LogWarning("[CountryProductDefaultSeeder] Products not found — skipping mappings.");
            return;
        }

        // ── Step 4: Seed mappings for countries with zero existing mappings ─────
        var activeCountries = await db.Countries
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Iso2 })
            .ToListAsync(ct);

        if (activeCountries.Count == 0)
        {
            logger.LogInformation("[CountryProductDefaultSeeder] No active countries found.");
            return;
        }

        var countriesWithMappings = await db.CountryProducts
            .AsNoTracking()
            .Select(cp => cp.CountryId)
            .Distinct()
            .ToListAsync(ct);

        var countriesWithMappingsSet = new HashSet<int>(countriesWithMappings);

        var newMappings = new List<CountryProduct>();
        var now   = DateTime.UtcNow;
        const string actor = "seeder";

        foreach (var country in activeCountries)
        {
            if (countriesWithMappingsSet.Contains(country.Id))
                continue; // Already has admin-configured mappings — skip

            bool allowVip = VipAllowedIso2.Contains(country.Iso2);

            var productsForCountry = new List<string> { eliteId, turboId, subscriptionId };

            if (allowVip && existingProductIds.Contains(vipId))
                productsForCountry.Add(vipId);

            foreach (var productId in productsForCountry.Where(existingProductIds.Contains))
            {
                newMappings.Add(new CountryProduct
                {
                    CountryId    = country.Id,
                    ProductId    = productId,
                    IsActive     = true,
                    CreatedBy    = actor,
                    CreationDate = now
                });
            }
        }

        if (newMappings.Count > 0)
        {
            await db.CountryProducts.AddRangeAsync(newMappings, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "[CountryProductDefaultSeeder] Seeded {Count} country-product mappings for {Countries} countries.",
                newMappings.Count,
                newMappings.Select(m => m.CountryId).Distinct().Count());
        }
        else
        {
            logger.LogInformation("[CountryProductDefaultSeeder] All active countries already have mappings.");
        }
    }
}
