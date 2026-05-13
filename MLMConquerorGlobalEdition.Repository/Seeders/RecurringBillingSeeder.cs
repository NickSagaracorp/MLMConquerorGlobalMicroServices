using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

/// <summary>
/// Seeds the recurring billing engine catalog rows:
///   - GlobalParameter: DailyResidualConsolidationMinimum = 100
///   - TokenType (one per recurring product) + TokenTypeProduct links
///   - RecurringBillingPlan "Travel Advantage" (Elite/VIP/Turbo)
///   - RecurringBillingPlan "Lifestyle Ambassador (Annual)"
///
/// Decision: Per-product token types on RecurringBillingPlanProduct.TokenTypeIdOverride.
/// Travel Advantage covers three products (Elite/VIP/Turbo) that each get their own TokenType;
/// the override on RecurringBillingPlanProduct ensures the correct token is issued per product.
/// RecurringBillingPlan.TokenTypeId is set to the Elite token as a fallback (in case a state row
/// is processed without a matching PlanProduct — should not happen in practice).
/// Lifestyle Ambassador uses a single TokenType at plan level.
///
/// Idempotent: per-row "skip if exists" checks ensure re-runs are safe.
/// </summary>
public static class RecurringBillingSeeder
{
    private const string Actor = "seed";

    // ── Product name fragments (case-insensitive contains match) ────────────
    private const string EliteFragment   = "elite";
    private const string VipFragment     = "vip";
    private const string TurboFragment   = "turbo";
    private const string LifestyleFragment = "lifestyle";

    // ── Token type names ─────────────────────────────────────────────────────
    private const string EliteTokenName     = "Travel Advantage Elite Subscription";
    private const string VipTokenName       = "Travel Advantage VIP Subscription";
    private const string TurboTokenName     = "Travel Advantage Turbo Subscription";
    private const string LifestyleTokenName = "Lifestyle Ambassador Subscription";

    // ── Plan names ────────────────────────────────────────────────────────────
    private const string TravelPlanName     = "Travel Advantage";
    private const string LifestylePlanName  = "Lifestyle Ambassador (Annual)";

    // ── Parameter key ─────────────────────────────────────────────────────────
    private const string ConsolidationMinimumKey = "DailyResidualConsolidationMinimum";

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var now = DateTime.UtcNow;

        // 1. GlobalParameter: DailyResidualConsolidationMinimum
        await SeedGlobalParameterAsync(db, logger, now);

        // 2. Token types (one per recurring product)
        var (eliteTokenId, vipTokenId, turboTokenId, lifestyleTokenId) =
            await SeedTokenTypesAsync(db, logger, now);

        // 3. RecurringBillingPlan — Travel Advantage (Every30Days)
        await SeedTravelAdvantagePlanAsync(db, logger, now,
            eliteTokenId, vipTokenId, turboTokenId);

        // 4. RecurringBillingPlan — Lifestyle Ambassador (Annual)
        await SeedLifestylePlanAsync(db, logger, now, lifestyleTokenId);
    }

    // ── 1. Global Parameter ────────────────────────────────────────────────────

    private static async Task SeedGlobalParameterAsync(AppDbContext db, ILogger logger, DateTime now)
    {
        if (await db.GlobalParameters.AnyAsync(p => p.Key == ConsolidationMinimumKey))
        {
            logger.LogInformation("RecurringBillingSeeder: GlobalParameter '{Key}' already exists — skipped.", ConsolidationMinimumKey);
            return;
        }

        db.GlobalParameters.Add(new GlobalParameter
        {
            Key          = ConsolidationMinimumKey,
            Value        = "100",
            Description  = "Minimum pending daily-residual balance (USD) required before consolidation into a CommissionEarning credit.",
            CreatedBy    = Actor,
            CreationDate = now
        });

        await db.SaveChangesAsync();
        logger.LogInformation("RecurringBillingSeeder: GlobalParameter '{Key}' seeded.", ConsolidationMinimumKey);
    }

    // ── 2. Token Types ─────────────────────────────────────────────────────────

    private static async Task<(int elite, int vip, int turbo, int lifestyle)> SeedTokenTypesAsync(
        AppDbContext db, ILogger logger, DateTime now)
    {
        var eliteTokenId     = await EnsureTokenTypeAsync(db, logger, now, EliteTokenName,     "Travel Advantage Elite recurring subscription token.", EliteFragment,     TokenCategory.Monthly);
        var vipTokenId       = await EnsureTokenTypeAsync(db, logger, now, VipTokenName,       "Travel Advantage VIP recurring subscription token.",   VipFragment,       TokenCategory.Monthly);
        var turboTokenId     = await EnsureTokenTypeAsync(db, logger, now, TurboTokenName,     "Travel Advantage Turbo recurring subscription token.", TurboFragment,     TokenCategory.Monthly);
        var lifestyleTokenId = await EnsureTokenTypeAsync(db, logger, now, LifestyleTokenName, "Lifestyle Ambassador annual recurring subscription token.", LifestyleFragment, TokenCategory.Annual);

        return (eliteTokenId, vipTokenId, turboTokenId, lifestyleTokenId);
    }

    private static async Task<int> EnsureTokenTypeAsync(
        AppDbContext db, ILogger logger, DateTime now,
        string tokenTypeName, string description, string productNameFragment,
        TokenCategory category)
    {
        // Check if token type already exists
        var existing = await db.TokenTypes
            .Include(t => t.ProductLinks)
            .FirstOrDefaultAsync(t => t.Name == tokenTypeName);

        if (existing is not null)
        {
            logger.LogInformation("RecurringBillingSeeder: TokenType '{Name}' already exists (Id={Id}).", tokenTypeName, existing.Id);
            await EnsureTokenTypeProductLinkAsync(db, logger, now, existing, productNameFragment);
            return existing.Id;
        }

        var tokenType = new TokenType
        {
            Name         = tokenTypeName,
            Description  = description,
            IsGuestPass  = false,
            IsActive     = true,
            Category     = category,
            CreatedBy    = Actor,
            CreationDate = now
        };
        db.TokenTypes.Add(tokenType);
        await db.SaveChangesAsync();

        logger.LogInformation("RecurringBillingSeeder: TokenType '{Name}' created (Id={Id}).", tokenTypeName, tokenType.Id);

        await EnsureTokenTypeProductLinkAsync(db, logger, now, tokenType, productNameFragment);
        return tokenType.Id;
    }

    private static async Task EnsureTokenTypeProductLinkAsync(
        AppDbContext db, ILogger logger, DateTime now,
        TokenType tokenType, string productNameFragment)
    {
        // Find the product by name fragment
        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name.ToLower().Contains(productNameFragment) && p.IsActive && !p.IsDeleted);

        if (product is null)
        {
            logger.LogWarning(
                "RecurringBillingSeeder: No active product found matching '{Fragment}' for TokenType '{Name}'. " +
                "The token type was created but has no product link — wire it via the admin UI.",
                productNameFragment, tokenType.Name);
            return;
        }

        // Check if link already exists
        var linkExists = await db.TokenTypeProducts
            .AnyAsync(tp => tp.TokenTypeId == tokenType.Id && tp.ProductId == product.Id);

        if (linkExists)
            return;

        db.TokenTypeProducts.Add(new TokenTypeProduct
        {
            TokenTypeId    = tokenType.Id,
            ProductId      = product.Id,
            Role           = TokenProductRole.Granted,
            QuantityGranted = 1,
            CreatedBy      = Actor,
            CreationDate   = now
        });

        await db.SaveChangesAsync();
        logger.LogInformation(
            "RecurringBillingSeeder: TokenTypeProduct link created — TokenType '{Token}' → Product '{Product}'.",
            tokenType.Name, product.Name);
    }

    // ── 3. Travel Advantage Plan ───────────────────────────────────────────────

    private static async Task SeedTravelAdvantagePlanAsync(
        AppDbContext db, ILogger logger, DateTime now,
        int eliteTokenId, int vipTokenId, int turboTokenId)
    {
        if (await db.RecurringBillingPlans.AnyAsync(p => p.Name == TravelPlanName))
        {
            logger.LogInformation("RecurringBillingSeeder: Plan '{Name}' already exists — skipped.", TravelPlanName);
            return;
        }

        // Resolve products
        var eliteProduct   = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Name.ToLower().Contains(EliteFragment)   && p.IsActive && !p.IsDeleted);
        var vipProduct     = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Name.ToLower().Contains(VipFragment)     && p.IsActive && !p.IsDeleted);
        var turboProduct   = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Name.ToLower().Contains(TurboFragment)   && p.IsActive && !p.IsDeleted);

        var plan = new RecurringBillingPlan
        {
            Name                      = TravelPlanName,
            CycleType                 = RecurringCycleType.Every30Days,
            RetryCadenceDays          = "1,2,2,2,2,2",
            OnAllRetriesFail          = RecurringFailurePolicy.RetryOnMonthlyAnniversary,
            StopAfterUnbilledDays     = 90,
            PayFromCommissionBalanceFirst = true,
            TokenTypeId               = eliteTokenId, // fallback plan-level token
            FixedAmountOverride       = null,         // use Product.MonthlyFee
            IsActive                  = true,
            CreatedBy                 = Actor,
            CreationDate              = now
        };

        // Add product links (with per-product token type overrides)
        if (eliteProduct is not null)
            plan.PlanProducts.Add(new RecurringBillingPlanProduct
            {
                ProductId          = eliteProduct.Id,
                TokenTypeIdOverride = eliteTokenId,
                CreatedBy          = Actor,
                CreationDate       = now
            });
        else
            logger.LogWarning("RecurringBillingSeeder: Elite product not found — plan has no Elite link.");

        if (vipProduct is not null)
            plan.PlanProducts.Add(new RecurringBillingPlanProduct
            {
                ProductId          = vipProduct.Id,
                TokenTypeIdOverride = vipTokenId,
                CreatedBy          = Actor,
                CreationDate       = now
            });
        else
            logger.LogWarning("RecurringBillingSeeder: VIP product not found — plan has no VIP link.");

        if (turboProduct is not null)
            plan.PlanProducts.Add(new RecurringBillingPlanProduct
            {
                ProductId          = turboProduct.Id,
                TokenTypeIdOverride = turboTokenId,
                CreatedBy          = Actor,
                CreationDate       = now
            });
        else
            logger.LogWarning("RecurringBillingSeeder: Turbo product not found — plan has no Turbo link.");

        db.RecurringBillingPlans.Add(plan);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "RecurringBillingSeeder: Plan '{Name}' created (Id={Id}) with {Count} product link(s).",
            TravelPlanName, plan.Id, plan.PlanProducts.Count);
    }

    // ── 4. Lifestyle Ambassador Plan ───────────────────────────────────────────

    private static async Task SeedLifestylePlanAsync(
        AppDbContext db, ILogger logger, DateTime now, int lifestyleTokenId)
    {
        if (await db.RecurringBillingPlans.AnyAsync(p => p.Name == LifestylePlanName))
        {
            logger.LogInformation("RecurringBillingSeeder: Plan '{Name}' already exists — skipped.", LifestylePlanName);
            return;
        }

        var lifestyleProduct = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name.ToLower().Contains(LifestyleFragment) && p.IsActive && !p.IsDeleted);

        var plan = new RecurringBillingPlan
        {
            Name                      = LifestylePlanName,
            CycleType                 = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays          = "1,1,1,2,2,5,5",
            OnAllRetriesFail          = RecurringFailurePolicy.MarkExpired,
            StopAfterUnbilledDays     = null,  // no auto-stop for annual
            PayFromCommissionBalanceFirst = true,
            TokenTypeId               = lifestyleTokenId,
            FixedAmountOverride       = null,  // use Product.AnnualPrice
            IsActive                  = true,
            CreatedBy                 = Actor,
            CreationDate              = now
        };

        if (lifestyleProduct is not null)
            plan.PlanProducts.Add(new RecurringBillingPlanProduct
            {
                ProductId           = lifestyleProduct.Id,
                TokenTypeIdOverride = null,  // use plan-level token type
                CreatedBy           = Actor,
                CreationDate        = now
            });
        else
            logger.LogWarning("RecurringBillingSeeder: Lifestyle Ambassador product not found — plan has no product link.");

        db.RecurringBillingPlans.Add(plan);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "RecurringBillingSeeder: Plan '{Name}' created (Id={Id}) with {Count} product link(s).",
            LifestylePlanName, plan.Id, plan.PlanProducts.Count);
    }
}
