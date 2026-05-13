using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// Resolves the ordered gateway attempt plan for a charge request.
///
/// Algorithm (per spec §5):
///   1. Resolve country → group(s).
///   2. Pick the most-specific active routing rule:
///      exact country > country group > brand catch-all > global catch-all.
///   3. If multi-split → IGatewaySplitSelector to pick primary.
///   4. Resolve CurrencyPolicy / convert amount.
///   5. Build fallback chain from GatewayFallbackRule.
///   6. Return GatewayRoutingPlan.
///
/// Admin override: if ctx.AdminOverride is set, bypass routing and use that
/// processor directly with USD amount and no fallback chain.
/// </summary>
public class GatewayRouter : IGatewayRouter
{
    private readonly AppDbContext              _db;
    private readonly IGatewaySplitSelector     _splitSelector;
    private readonly ICurrencyConversionService _currencyConversion;

    public GatewayRouter(
        AppDbContext db,
        IGatewaySplitSelector splitSelector,
        ICurrencyConversionService currencyConversion)
    {
        _db                 = db;
        _splitSelector      = splitSelector;
        _currencyConversion = currencyConversion;
    }

    public async Task<Result<GatewayRoutingPlan>> ResolveAsync(
        GatewayRoutingContext ctx,
        CancellationToken ct = default)
    {
        // ── Admin override ────────────────────────────────────────────────
        if (ctx.AdminOverride.HasValue)
        {
            var overridePlan = new GatewayRoutingPlan
            {
                RouteBucketKey = "admin-override",
                Steps = new[]
                {
                    new GatewayAttemptPlan
                    {
                        CardProcessor       = ctx.AdminOverride.Value,
                        PresentmentCurrency = "USD",
                        Amount              = ctx.AmountUsd,
                        FallbackStepIndex   = 0,
                        DelayMinutes        = 0
                    }
                }
            };
            return Result<GatewayRoutingPlan>.Success(overridePlan);
        }

        // ── 1. Resolve country groups ─────────────────────────────────────
        var countryGroupIds = await _db.CountryGroupCountries
            .Where(c => c.IsoCountryCode == ctx.CardholderCountryIso.ToUpperInvariant())
            .Select(c => c.CountryGroupId)
            .ToListAsync(ct);

        // ── 2. Pick the most-specific routing rule ─────────────────────────
        var rule = await ResolveRuleAsync(ctx, countryGroupIds, ct);
        if (rule is null)
            return Result<GatewayRoutingPlan>.Failure(
                "NO_ROUTING_RULE",
                $"No active routing rule found for OperationType={ctx.OperationType}, " +
                $"CardBrand={ctx.CardBrand}, Country={ctx.CardholderCountryIso}.");

        var splits = rule.Splits.OrderBy(s => s.SortOrder).ToList();
        var bucketKey = BuildBucketKey(ctx.OperationType, ctx.CardBrand, rule);

        // ── 3. Pick primary processor via split selector ───────────────────
        CardProcessor primaryProcessor;
        if (splits.Count == 1)
        {
            primaryProcessor = splits[0].CardProcessor;
            // Still register the pick so the counter stays accurate
            await _splitSelector.PickAsync(bucketKey, splits, ct);
        }
        else
        {
            var pickResult = await _splitSelector.PickAsync(bucketKey, splits, ct);
            if (!pickResult.IsSuccess)
                return Result<GatewayRoutingPlan>.Failure(pickResult.ErrorCode!, pickResult.Error!);
            primaryProcessor = pickResult.Value!;
        }

        // ── 4. Currency conversion for primary ────────────────────────────
        string primaryCurrency = "USD";
        decimal primaryAmount  = ctx.AmountUsd;

        if (rule.CurrencyPolicy is not null && rule.CurrencyPolicy.PresentmentCurrency != "USD")
        {
            var convertResult = await _currencyConversion.ConvertAsync(
                ctx.AmountUsd, rule.CurrencyPolicy.PresentmentCurrency, rule.CurrencyPolicy.MarkupPercent, ct);

            if (convertResult.IsSuccess)
            {
                primaryCurrency = rule.CurrencyPolicy.PresentmentCurrency;
                primaryAmount   = convertResult.Value;
            }
            // On conversion failure, fall through to USD (soft degradation)
        }

        // ── 5. Build fallback chain ────────────────────────────────────────
        var fallbackRules = await _db.GatewayFallbackRules
            .Where(f => f.OperationType == ctx.OperationType
                     && f.PrimaryProcessor == primaryProcessor)
            .OrderBy(f => f.StepOrder)
            .ToListAsync(ct);

        var steps = new List<GatewayAttemptPlan>
        {
            new()
            {
                CardProcessor       = primaryProcessor,
                PresentmentCurrency = primaryCurrency,
                Amount              = primaryAmount,
                FallbackStepIndex   = 0,
                DelayMinutes        = 0
            }
        };

        foreach (var fb in fallbackRules)
        {
            string fbCurrency = "USD";
            decimal fbAmount  = ctx.AmountUsd;

            if (!fb.ForceUsdOnFallback)
            {
                // Stripe-style fallback: keep the primary presentment currency
                fbCurrency = primaryCurrency;
                fbAmount   = primaryAmount;
            }

            steps.Add(new GatewayAttemptPlan
            {
                CardProcessor       = fb.NextProcessor,
                PresentmentCurrency = fbCurrency,
                Amount              = fbAmount,
                FallbackStepIndex   = fb.StepOrder,
                DelayMinutes        = fb.DelayMinutes
            });
        }

        return Result<GatewayRoutingPlan>.Success(new GatewayRoutingPlan
        {
            RouteBucketKey = bucketKey,
            Steps          = steps.AsReadOnly()
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<GatewayRoutingRule?> ResolveRuleAsync(
        GatewayRoutingContext ctx,
        List<int> countryGroupIds,
        CancellationToken ct)
    {
        var candidates = await _db.GatewayRoutingRules
            .Include(r => r.Splits)
            .Include(r => r.CurrencyPolicy)
            .Where(r => r.IsActive
                     && r.OperationType == ctx.OperationType
                     && (r.CardBrand == null || r.CardBrand == ctx.CardBrand))
            .ToListAsync(ct);

        var countryUpper = ctx.CardholderCountryIso.ToUpperInvariant();

        // Priority 1: exact country match + brand match
        var rule = candidates.FirstOrDefault(r =>
            r.IsoCountryCode != null &&
            r.IsoCountryCode.Equals(countryUpper, StringComparison.OrdinalIgnoreCase) &&
            r.CardBrand == ctx.CardBrand);

        if (rule is not null) return rule;

        // Priority 2: exact country match + brand null (catch-all brand, exact country)
        rule = candidates.FirstOrDefault(r =>
            r.IsoCountryCode != null &&
            r.IsoCountryCode.Equals(countryUpper, StringComparison.OrdinalIgnoreCase) &&
            r.CardBrand == null);

        if (rule is not null) return rule;

        // Priority 3: country-group match + brand match
        rule = candidates.FirstOrDefault(r =>
            r.CountryGroupId != null &&
            countryGroupIds.Contains(r.CountryGroupId.Value) &&
            r.CardBrand == ctx.CardBrand);

        if (rule is not null) return rule;

        // Priority 4: country-group match + brand null
        rule = candidates.FirstOrDefault(r =>
            r.CountryGroupId != null &&
            countryGroupIds.Contains(r.CountryGroupId.Value) &&
            r.CardBrand == null);

        if (rule is not null) return rule;

        // Priority 5: brand catch-all (IsCatchAll=false, no country/group) + brand match
        rule = candidates.FirstOrDefault(r =>
            !r.IsCatchAll &&
            r.IsoCountryCode == null &&
            r.CountryGroupId == null &&
            r.CardBrand == ctx.CardBrand);

        if (rule is not null) return rule;

        // Priority 6: global catch-all + brand match
        rule = candidates.FirstOrDefault(r =>
            r.IsCatchAll &&
            r.CardBrand == ctx.CardBrand);

        if (rule is not null) return rule;

        // Priority 7: global catch-all + brand null
        rule = candidates.FirstOrDefault(r =>
            r.IsCatchAll &&
            r.CardBrand == null);

        return rule;
    }

    private static string BuildBucketKey(
        BillingOperationType opType, CardBrand brand, GatewayRoutingRule rule)
    {
        var raw = $"{(int)opType}:{(int)brand}:{rule.IsoCountryCode ?? ""}:{rule.CountryGroupId?.ToString() ?? ""}:{rule.IsCatchAll}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32];
    }
}
