using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

/// <summary>
/// Seeds the gateway routing engine catalog tables:
///   - PaymentGatewayCatalog (one row per CardProcessor)
///   - CountryGroup + CountryGroupCountry (Europe, LatinAmerica, RussiaBloc)
///   - GatewayRoutingRule + GatewayRoutingRuleSplit (full routing matrix)
///   - CurrencyPolicy (EUR/CAD/AUD/GBP +2 %)
///   - GatewayFallbackRule (signup, recurring, authorization chains)
///   - ApiCredential (inactive placeholder per gateway + currency converter)
/// All seeded rows use Id 0 / generated keys — idempotent guard is
/// "if table is already seeded, skip". Run after EF migrations.
/// </summary>
public static class GatewayRoutingSeeder
{
    private const string Actor = "seed";

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        await SeedGatewayCatalogAsync(db, logger);
        await SeedCountryGroupsAsync(db, logger);
        await SeedCurrencyPoliciesAsync(db, logger);
        await SeedRoutingRulesAsync(db, logger);
        await SeedFallbackRulesAsync(db, logger);
        await SeedApiCredentialPlaceholdersAsync(db, logger);
    }

    // ── 1. Processor catalog ─────────────────────────────────────────────

    private static async Task SeedGatewayCatalogAsync(AppDbContext db, ILogger logger)
    {
        if (await db.GatewayCatalog.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var entries = new[]
        {
            new PaymentGatewayCatalog { Processor = CardProcessor.NmiSpreedly,   DisplayName = "NMI (Spreedly Vault)", IsActive = true, SupportsRefund = true,  SupportsRecurring = true,  CreatedBy = Actor, CreationDate = now },
            new PaymentGatewayCatalog { Processor = CardProcessor.NmiDirect,     DisplayName = "NMI Direct",           IsActive = true, SupportsRefund = true,  SupportsRecurring = false, CreatedBy = Actor, CreationDate = now },
            new PaymentGatewayCatalog { Processor = CardProcessor.CheckoutEUR,   DisplayName = "Checkout.com EUR",     IsActive = true, SupportsRefund = true,  SupportsRecurring = true,  CreatedBy = Actor, CreationDate = now },
            new PaymentGatewayCatalog { Processor = CardProcessor.CheckoutUS,    DisplayName = "Checkout.com US",      IsActive = true, SupportsRefund = true,  SupportsRecurring = true,  CreatedBy = Actor, CreationDate = now },
            new PaymentGatewayCatalog { Processor = CardProcessor.CheckoutUsLlc, DisplayName = "Checkout US LLC",      IsActive = true, SupportsRefund = true,  SupportsRecurring = false, CreatedBy = Actor, CreationDate = now },
            new PaymentGatewayCatalog { Processor = CardProcessor.Shift4,        DisplayName = "Shift4",               IsActive = true, SupportsRefund = true,  SupportsRecurring = true,  CreatedBy = Actor, CreationDate = now },
            new PaymentGatewayCatalog { Processor = CardProcessor.StripeEms,     DisplayName = "Stripe EMS",           IsActive = true, SupportsRefund = true,  SupportsRecurring = true,  CreatedBy = Actor, CreationDate = now },
        };

        db.GatewayCatalog.AddRange(entries);
        await db.SaveChangesAsync();
        logger.LogInformation("GatewayRoutingSeeder: {Count} gateway catalog rows seeded.", entries.Length);
    }

    // ── 2. Country groups ────────────────────────────────────────────────

    private static async Task SeedCountryGroupsAsync(AppDbContext db, ILogger logger)
    {
        if (await db.CountryGroups.AnyAsync()) return;

        var now = DateTime.UtcNow;

        // ── Europe ────────────────────────────────────────────────────────
        var europe = new CountryGroup { Code = "EUROPE", Name = "Europe", CreatedBy = Actor, CreationDate = now };
        db.CountryGroups.Add(europe);
        await db.SaveChangesAsync();

        var europeCodes = new[]
        {
            "AL","AD","AT","BY","BE","BA","BG","HR","CY","CZ","DK","EE","FI","FR","DE",
            "GI","GR","HU","IS","IE","IT","LV","LI","LT","LU","MK","MT","MD","MC","ME",
            "NL","NO","PL","PT","RO","SM","RS","SK","SI","ES","SE","CH","UA","GB","VA",
            "AX","FO","GL","GG","IM","JE","SJ","XK"
        };
        db.CountryGroupCountries.AddRange(europeCodes.Select(c => new CountryGroupCountry
        {
            CountryGroupId = europe.Id, IsoCountryCode = c, CreatedBy = Actor, CreationDate = now
        }));

        // ── Latin America ─────────────────────────────────────────────────
        var latam = new CountryGroup { Code = "LATINAMERICA", Name = "Latin America", CreatedBy = Actor, CreationDate = now };
        db.CountryGroups.Add(latam);
        await db.SaveChangesAsync();

        var latamCodes = new[]
        {
            "MX","GT","BZ","HN","SV","NI","CR","PA",
            "CU","JM","HT","DO","PR","TT","BB","LC","VC","GD","AG","KN","DM","AW","CW","SX","BQ","AI","VG","KY","TC","MS","GP","MQ","BL","MF","PM",
            "CO","VE","GY","SR","BR","EC","PE","BO","PY","CL","AR","UY","FK","GF"
        };
        db.CountryGroupCountries.AddRange(latamCodes.Select(c => new CountryGroupCountry
        {
            CountryGroupId = latam.Id, IsoCountryCode = c, CreatedBy = Actor, CreationDate = now
        }));

        // ── Russia Bloc ───────────────────────────────────────────────────
        var russia = new CountryGroup { Code = "RUSSIABLOC", Name = "Russia Bloc", CreatedBy = Actor, CreationDate = now };
        db.CountryGroups.Add(russia);
        await db.SaveChangesAsync();

        var russiaCodes = new[] { "RU", "AZ", "AM", "GE", "KZ", "KG", "TJ", "TM", "UZ", "BY" };
        db.CountryGroupCountries.AddRange(russiaCodes.Select(c => new CountryGroupCountry
        {
            CountryGroupId = russia.Id, IsoCountryCode = c, CreatedBy = Actor, CreationDate = now
        }));

        await db.SaveChangesAsync();
        logger.LogInformation("GatewayRoutingSeeder: country groups seeded (Europe, LatinAmerica, RussiaBloc).");
    }

    // ── 3. Currency policies ─────────────────────────────────────────────

    private static async Task SeedCurrencyPoliciesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.CurrencyPolicies.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var policies = new[]
        {
            new CurrencyPolicy { PresentmentCurrency = "EUR", MarkupPercent = 2m, IsActive = true, Description = "European Union presentment currency", CreatedBy = Actor, CreationDate = now },
            new CurrencyPolicy { PresentmentCurrency = "GBP", MarkupPercent = 2m, IsActive = true, Description = "United Kingdom presentment currency",  CreatedBy = Actor, CreationDate = now },
            new CurrencyPolicy { PresentmentCurrency = "CAD", MarkupPercent = 2m, IsActive = true, Description = "Canada presentment currency",           CreatedBy = Actor, CreationDate = now },
            new CurrencyPolicy { PresentmentCurrency = "AUD", MarkupPercent = 2m, IsActive = true, Description = "Australia presentment currency",         CreatedBy = Actor, CreationDate = now },
        };

        db.CurrencyPolicies.AddRange(policies);
        await db.SaveChangesAsync();
        logger.LogInformation("GatewayRoutingSeeder: {Count} currency policies seeded.", policies.Length);
    }

    // ── 4. Routing rules ─────────────────────────────────────────────────

    private static async Task SeedRoutingRulesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.GatewayRoutingRules.AnyAsync()) return;

        var now = DateTime.UtcNow;

        // Load group/policy IDs seeded above
        var europeId  = (await db.CountryGroups.FirstAsync(x => x.Code == "EUROPE")).Id;
        var latamId   = (await db.CountryGroups.FirstAsync(x => x.Code == "LATINAMERICA")).Id;
        var russiaId  = (await db.CountryGroups.FirstAsync(x => x.Code == "RUSSIABLOC")).Id;
        var eurPolicyId = (await db.CurrencyPolicies.FirstAsync(x => x.PresentmentCurrency == "EUR")).Id;
        var gbpPolicyId = (await db.CurrencyPolicies.FirstAsync(x => x.PresentmentCurrency == "GBP")).Id;
        var cadPolicyId = (await db.CurrencyPolicies.FirstAsync(x => x.PresentmentCurrency == "CAD")).Id;
        var audPolicyId = (await db.CurrencyPolicies.FirstAsync(x => x.PresentmentCurrency == "AUD")).Id;

        // Helper to build rules; splits are added as navigation children
        void AddRule(
            BillingOperationType op,
            CardBrand? brand,
            string? iso, int? groupId, bool isCatchAll,
            int? currencyPolicyId,
            IEnumerable<(CardProcessor proc, decimal pct, int order)> splits)
        {
            var rule = new GatewayRoutingRule
            {
                OperationType  = op,
                CardBrand      = brand,
                IsoCountryCode = iso,
                CountryGroupId = groupId,
                IsCatchAll     = isCatchAll,
                CurrencyPolicyId = currencyPolicyId,
                IsActive       = true,
                CreatedBy      = Actor,
                CreationDate   = now,
            };
            foreach (var (proc, pct, ord) in splits)
                rule.Splits.Add(new GatewayRoutingRuleSplit { CardProcessor = proc, WeightPercent = pct, SortOrder = ord, CreatedBy = Actor, CreationDate = now });
            db.GatewayRoutingRules.Add(rule);
        }

        // ── We seed rules for BOTH operation types with the same routing matrix ──

        foreach (var op in new[] { BillingOperationType.CardAuthorization, BillingOperationType.Payment })
        {
            // ── Amex → CheckoutEUR 100 % everywhere ──────────────────────
            AddRule(op, CardBrand.Amex, null, null, true, eurPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 100m, 1) });

            // ── Maestro → StripeEms 100 % ────────────────────────────────
            AddRule(op, CardBrand.Maestro, null, null, true, null,
                new[] { (CardProcessor.StripeEms, 100m, 1) });

            // ── Bancontact → StripeEms 100 % ─────────────────────────────
            AddRule(op, CardBrand.Bancontact, null, null, true, null,
                new[] { (CardProcessor.StripeEms, 100m, 1) });

            // ── JCB → StripeEms 100 % ────────────────────────────────────
            AddRule(op, CardBrand.Jcb, null, null, true, null,
                new[] { (CardProcessor.StripeEms, 100m, 1) });

            // ── Visa/MC exact-country rules ───────────────────────────────

            // USA → CheckoutUS 40 / NmiSpreedly 60
            AddRule(op, CardBrand.Visa, "US", null, false, null,
                new[] { (CardProcessor.CheckoutUS, 40m, 1), (CardProcessor.NmiSpreedly, 60m, 2) });
            AddRule(op, CardBrand.MasterCard, "US", null, false, null,
                new[] { (CardProcessor.CheckoutUS, 40m, 1), (CardProcessor.NmiSpreedly, 60m, 2) });

            // Canada → CheckoutUS 100 (CAD currency)
            AddRule(op, CardBrand.Visa, "CA", null, false, cadPolicyId,
                new[] { (CardProcessor.CheckoutUS, 100m, 1) });
            AddRule(op, CardBrand.MasterCard, "CA", null, false, cadPolicyId,
                new[] { (CardProcessor.CheckoutUS, 100m, 1) });

            // UK → CheckoutEUR 100 (GBP)
            AddRule(op, CardBrand.Visa, "GB", null, false, gbpPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 100m, 1) });
            AddRule(op, CardBrand.MasterCard, "GB", null, false, gbpPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 100m, 1) });

            // Australia → CheckoutEUR 100 (AUD)
            AddRule(op, CardBrand.Visa, "AU", null, false, audPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 100m, 1) });
            AddRule(op, CardBrand.MasterCard, "AU", null, false, audPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 100m, 1) });

            // South Korea → NmiSpreedly 100
            AddRule(op, CardBrand.Visa, "KR", null, false, null,
                new[] { (CardProcessor.NmiSpreedly, 100m, 1) });
            AddRule(op, CardBrand.MasterCard, "KR", null, false, null,
                new[] { (CardProcessor.NmiSpreedly, 100m, 1) });

            // Japan → NmiSpreedly 100
            AddRule(op, CardBrand.Visa, "JP", null, false, null,
                new[] { (CardProcessor.NmiSpreedly, 100m, 1) });
            AddRule(op, CardBrand.MasterCard, "JP", null, false, null,
                new[] { (CardProcessor.NmiSpreedly, 100m, 1) });

            // ── Visa/MC country-group rules ───────────────────────────────

            // Europe → CheckoutEUR 60 / Shift4 40
            AddRule(op, CardBrand.Visa, null, europeId, false, eurPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 60m, 1), (CardProcessor.Shift4, 40m, 2) });
            AddRule(op, CardBrand.MasterCard, null, europeId, false, eurPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 60m, 1), (CardProcessor.Shift4, 40m, 2) });

            // RussiaBloc → CheckoutEUR 50 / Shift4 50
            AddRule(op, CardBrand.Visa, null, russiaId, false, eurPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 50m, 1), (CardProcessor.Shift4, 50m, 2) });
            AddRule(op, CardBrand.MasterCard, null, russiaId, false, eurPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 50m, 1), (CardProcessor.Shift4, 50m, 2) });

            // LatinAmerica → NmiSpreedly 50 / CheckoutUsLlc 50
            AddRule(op, CardBrand.Visa, null, latamId, false, null,
                new[] { (CardProcessor.NmiSpreedly, 50m, 1), (CardProcessor.CheckoutUsLlc, 50m, 2) });
            AddRule(op, CardBrand.MasterCard, null, latamId, false, null,
                new[] { (CardProcessor.NmiSpreedly, 50m, 1), (CardProcessor.CheckoutUsLlc, 50m, 2) });

            // ── Visa/MC global catch-all → CheckoutEUR 100 ───────────────
            AddRule(op, CardBrand.Visa, null, null, true, eurPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 100m, 1) });
            AddRule(op, CardBrand.MasterCard, null, null, true, eurPolicyId,
                new[] { (CardProcessor.CheckoutEUR, 100m, 1) });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("GatewayRoutingSeeder: routing rules seeded.");
    }

    // ── 5. Fallback rules ────────────────────────────────────────────────

    private static async Task SeedFallbackRulesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.GatewayFallbackRules.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var rules = new List<GatewayFallbackRule>();

        void Add(BillingOperationType op, CardProcessor primary, int step, CardProcessor next, int delay, bool forceUsd) =>
            rules.Add(new GatewayFallbackRule
            {
                OperationType    = op,
                PrimaryProcessor = primary,
                StepOrder        = step,
                NextProcessor    = next,
                DelayMinutes     = delay,
                ForceUsdOnFallback = forceUsd,
                CreatedBy        = Actor,
                CreationDate     = now,
            });

        // ── Signup (Payment) fallback chains ─────────────────────────────
        // NMI → NmiDirect → CheckoutUS → Stripe(EMS)
        Add(BillingOperationType.Payment, CardProcessor.NmiSpreedly, 1, CardProcessor.NmiDirect,  0, true);
        Add(BillingOperationType.Payment, CardProcessor.NmiSpreedly, 2, CardProcessor.CheckoutUS, 0, true);
        Add(BillingOperationType.Payment, CardProcessor.NmiSpreedly, 3, CardProcessor.StripeEms,  0, false);

        // CheckoutUS → NmiSpreedly → Stripe(EMS)
        Add(BillingOperationType.Payment, CardProcessor.CheckoutUS, 1, CardProcessor.NmiSpreedly, 0, true);
        Add(BillingOperationType.Payment, CardProcessor.CheckoutUS, 2, CardProcessor.StripeEms,   0, false);

        // CheckoutEUR → NmiSpreedly → Stripe(EMS)
        Add(BillingOperationType.Payment, CardProcessor.CheckoutEUR, 1, CardProcessor.NmiSpreedly, 0, true);
        Add(BillingOperationType.Payment, CardProcessor.CheckoutEUR, 2, CardProcessor.StripeEms,   0, false);

        // Shift4 → NmiSpreedly → StripeEms
        Add(BillingOperationType.Payment, CardProcessor.Shift4, 1, CardProcessor.NmiSpreedly, 0, true);
        Add(BillingOperationType.Payment, CardProcessor.Shift4, 2, CardProcessor.StripeEms,   0, false);

        // CheckoutUsLlc → NmiSpreedly → StripeEms
        Add(BillingOperationType.Payment, CardProcessor.CheckoutUsLlc, 1, CardProcessor.NmiSpreedly, 0, true);
        Add(BillingOperationType.Payment, CardProcessor.CheckoutUsLlc, 2, CardProcessor.StripeEms,   0, false);

        // StripeEms (no further fallback defined — it is the last resort)

        // ── Recurring USA/Canada (Payment) — delayed fallback ─────────────
        // NMI → CheckoutUS @ 60 min
        Add(BillingOperationType.Payment, CardProcessor.NmiSpreedly,  10, CardProcessor.CheckoutUS,    60, true);
        // CheckoutUS → NMI @ 60 min
        Add(BillingOperationType.Payment, CardProcessor.CheckoutUS,   10, CardProcessor.NmiSpreedly,   60, true);

        // ── Authorization fallback chains ─────────────────────────────────
        // NMI → CheckoutUS → Stripe(EMS)
        Add(BillingOperationType.CardAuthorization, CardProcessor.NmiSpreedly, 1, CardProcessor.CheckoutUS, 0, true);
        Add(BillingOperationType.CardAuthorization, CardProcessor.NmiSpreedly, 2, CardProcessor.StripeEms,  0, false);

        // CheckoutEUR → Stripe(EMS)
        Add(BillingOperationType.CardAuthorization, CardProcessor.CheckoutEUR, 1, CardProcessor.StripeEms, 0, false);

        // CheckoutUS → StripeEms
        Add(BillingOperationType.CardAuthorization, CardProcessor.CheckoutUS, 1, CardProcessor.StripeEms, 0, false);

        db.GatewayFallbackRules.AddRange(rules);
        await db.SaveChangesAsync();
        logger.LogInformation("GatewayRoutingSeeder: {Count} fallback rules seeded.", rules.Count);
    }

    // ── 6. API credential placeholders ───────────────────────────────────
    // Empty, inactive rows — one per gateway service key plus the currency
    // converter — so the admin UI lists them out of the box. The operator
    // fills in BaseUrl / keys and flips IsActive from the UI; no secrets are
    // seeded here.

    private static async Task SeedApiCredentialPlaceholdersAsync(AppDbContext db, ILogger logger)
    {
        if (await db.ApiCredentials.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var placeholders = new[]
        {
            new ApiCredential { ServiceKey = "NmiSpreedly",          Environment = "Production", BaseUrl = "https://core.spreedly.com",          IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
            new ApiCredential { ServiceKey = "NmiDirect",            Environment = "Production", BaseUrl = "https://secure.networkmerchants.com", IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
            new ApiCredential { ServiceKey = "CheckoutEUR",          Environment = "Production", BaseUrl = "https://api.checkout.com",            IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
            new ApiCredential { ServiceKey = "CheckoutUS",           Environment = "Production", BaseUrl = "https://api.checkout.com",            IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
            new ApiCredential { ServiceKey = "CheckoutUsLlc",        Environment = "Production", BaseUrl = "https://api.checkout.com",            IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
            new ApiCredential { ServiceKey = "Shift4",               Environment = "Production", BaseUrl = "https://api.shift4.com",               IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
            new ApiCredential { ServiceKey = "StripeEms",            Environment = "Production", BaseUrl = "https://api.stripe.com",               IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
            new ApiCredential { ServiceKey = "CurrencyConverterApi", Environment = "Production", BaseUrl = "https://api.currconv.com",             IsActive = false, CreatedBy = Actor, CreationDate = now, LastUpdateDate = now },
        };

        db.ApiCredentials.AddRange(placeholders);
        await db.SaveChangesAsync();
        logger.LogInformation("GatewayRoutingSeeder: {Count} API credential placeholders seeded.", placeholders.Length);
    }
}
