using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class GatewayRouterTests
{
    // ── Test helpers ───────────────────────────────────────────────────────

    private static GatewayRouter CreateRouter(
        AppDbContext db,
        IGatewaySplitSelector? splitSelector = null,
        ICurrencyConversionService? currencyConversion = null)
    {
        if (splitSelector is null)
        {
            var splMock = new Mock<IGatewaySplitSelector>();
            splMock
                .Setup(s => s.PickAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<GatewayRoutingRuleSplit>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, IReadOnlyList<GatewayRoutingRuleSplit> splits, CancellationToken _) =>
                    SharedKernel.Result<CardProcessor>.Success(splits[0].CardProcessor));
            splitSelector = splMock.Object;
        }

        if (currencyConversion is null)
        {
            var ccMock = new Mock<ICurrencyConversionService>();
            ccMock
                .Setup(c => c.ConvertAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal amt, string _, decimal _, CancellationToken _) =>
                    SharedKernel.Result<decimal>.Success(amt));
            currencyConversion = ccMock.Object;
        }

        return new GatewayRouter(db, splitSelector, currencyConversion);
    }

    private static GatewayRoutingContext MakeCtx(
        BillingOperationType op = BillingOperationType.Payment,
        CardBrand brand = CardBrand.Visa,
        string country = "US",
        decimal amount = 100m) =>
        new()
        {
            OperationType        = op,
            CardBrand            = brand,
            CardholderCountryIso = country,
            AmountUsd            = amount,
            MemberId             = "member-1"
        };

    private static GatewayRoutingRule RuleWithSplit(
        BillingOperationType op, CardProcessor proc,
        string? iso = null, int? groupId = null, bool catchAll = false, CardBrand? brand = null)
    {
        var rule = new GatewayRoutingRule
        {
            OperationType  = op,
            CardBrand      = brand,
            IsoCountryCode = iso,
            CountryGroupId = groupId,
            IsCatchAll     = catchAll,
            IsActive       = true,
            Splits         = new List<GatewayRoutingRuleSplit>
            {
                new() { CardProcessor = proc, WeightPercent = 100, SortOrder = 1 }
            }
        };
        return rule;
    }

    // ── Admin override bypass ──────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenAdminOverride_ReturnsSingleStepWithOverrideProcessor()
    {
        using var db = TestDbContextFactory.Create();
        var router = CreateRouter(db);
        var ctx = new GatewayRoutingContext
        {
            OperationType = BillingOperationType.Payment,
            CardBrand     = CardBrand.Visa,
            AdminOverride = CardProcessor.Shift4,
            AmountUsd     = 50m,
            MemberId      = "m1"
        };

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Steps.Should().HaveCount(1);
        result.Value.Steps[0].CardProcessor.Should().Be(CardProcessor.Shift4);
        result.Value.Steps[0].PresentmentCurrency.Should().Be("USD");
        result.Value.RouteBucketKey.Should().Be("admin-override");
    }

    // ── No matching rule → failure ─────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenNoRuleFound_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var router = CreateRouter(db);
        var ctx = MakeCtx(country: "ZZ");

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_ROUTING_RULE");
    }

    // ── Exact country rule wins over catch-all ─────────────────────────────

    [Fact]
    public async Task ResolveAsync_ExactCountryRule_WinsOverCatchAll()
    {
        using var db = TestDbContextFactory.Create();
        // Exact country rule → NmiSpreedly
        db.GatewayRoutingRules.Add(RuleWithSplit(BillingOperationType.Payment, CardProcessor.NmiSpreedly, iso: "US"));
        // Catch-all rule → CheckoutUS (should not be chosen)
        db.GatewayRoutingRules.Add(RuleWithSplit(BillingOperationType.Payment, CardProcessor.CheckoutUS, catchAll: true));
        await db.SaveChangesAsync();

        var router = CreateRouter(db);
        var ctx = MakeCtx(country: "US");

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Steps[0].CardProcessor.Should().Be(CardProcessor.NmiSpreedly);
    }

    // ── Country-group rule wins over catch-all ─────────────────────────────

    [Fact]
    public async Task ResolveAsync_CountryGroupRule_WinsOverCatchAll()
    {
        using var db = TestDbContextFactory.Create();

        // Country group 1 contains DE
        db.CountryGroupCountries.Add(new CountryGroupCountry
            { CountryGroupId = 1, IsoCountryCode = "DE" });

        // Group rule → CheckoutEUR
        db.GatewayRoutingRules.Add(RuleWithSplit(BillingOperationType.Payment, CardProcessor.CheckoutEUR, groupId: 1));
        // Catch-all → NmiDirect (should lose)
        db.GatewayRoutingRules.Add(RuleWithSplit(BillingOperationType.Payment, CardProcessor.NmiDirect, catchAll: true));
        await db.SaveChangesAsync();

        var router = CreateRouter(db);
        var ctx = MakeCtx(country: "DE");

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Steps[0].CardProcessor.Should().Be(CardProcessor.CheckoutEUR);
    }

    // ── Fallback chain appended to plan ───────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenFallbackRulesExist_AppendsFallbackSteps()
    {
        using var db = TestDbContextFactory.Create();
        db.GatewayRoutingRules.Add(RuleWithSplit(BillingOperationType.Payment, CardProcessor.NmiSpreedly, catchAll: true));
        db.GatewayFallbackRules.AddRange(
            new GatewayFallbackRule
            {
                OperationType     = BillingOperationType.Payment,
                PrimaryProcessor  = CardProcessor.NmiSpreedly,
                StepOrder         = 1,
                NextProcessor     = CardProcessor.NmiDirect,
                DelayMinutes      = 0,
                ForceUsdOnFallback = true
            },
            new GatewayFallbackRule
            {
                OperationType     = BillingOperationType.Payment,
                PrimaryProcessor  = CardProcessor.NmiSpreedly,
                StepOrder         = 2,
                NextProcessor     = CardProcessor.StripeEms,
                DelayMinutes      = 0,
                ForceUsdOnFallback = false
            }
        );
        await db.SaveChangesAsync();

        var router = CreateRouter(db);
        var ctx = MakeCtx(country: "AU");

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Steps.Should().HaveCount(3);
        result.Value.Steps[0].CardProcessor.Should().Be(CardProcessor.NmiSpreedly);
        result.Value.Steps[1].CardProcessor.Should().Be(CardProcessor.NmiDirect);
        result.Value.Steps[2].CardProcessor.Should().Be(CardProcessor.StripeEms);
    }

    // ── ForceUsdOnFallback: NMI steps use USD ─────────────────────────────

    [Fact]
    public async Task ResolveAsync_FallbackWithForceUsd_HasUsdCurrency()
    {
        using var db = TestDbContextFactory.Create();

        var currPolicyMock = new Mock<ICurrencyConversionService>();
        currPolicyMock
            .Setup(c => c.ConvertAsync(100m, "EUR", 2m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedKernel.Result<decimal>.Success(92m));

        // Rule with EUR currency policy
        var policy = new CurrencyPolicy
        {
            PresentmentCurrency = "EUR",
            MarkupPercent       = 2m,
            IsActive            = true
        };
        db.CurrencyPolicies.Add(policy);
        await db.SaveChangesAsync();

        var rule = RuleWithSplit(BillingOperationType.Payment, CardProcessor.CheckoutEUR, catchAll: true);
        rule.CurrencyPolicyId = policy.Id;
        rule.CurrencyPolicy   = policy;
        db.GatewayRoutingRules.Add(rule);

        db.GatewayFallbackRules.Add(new GatewayFallbackRule
        {
            OperationType      = BillingOperationType.Payment,
            PrimaryProcessor   = CardProcessor.CheckoutEUR,
            StepOrder          = 1,
            NextProcessor      = CardProcessor.NmiSpreedly,
            DelayMinutes       = 0,
            ForceUsdOnFallback = true    // NMI step → USD
        });
        await db.SaveChangesAsync();

        var router = CreateRouter(db, currencyConversion: currPolicyMock.Object);
        var ctx = MakeCtx(country: "FR", amount: 100m);

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        // Primary should be EUR (92m converted)
        result.Value!.Steps[0].PresentmentCurrency.Should().Be("EUR");
        result.Value.Steps[0].Amount.Should().Be(92m);
        // Fallback should be USD
        result.Value.Steps[1].PresentmentCurrency.Should().Be("USD");
        result.Value.Steps[1].Amount.Should().Be(100m);
    }

    // ── Delayed fallback step has correct DelayMinutes ────────────────────

    [Fact]
    public async Task ResolveAsync_DelayedFallback_StepHasCorrectDelayMinutes()
    {
        using var db = TestDbContextFactory.Create();
        db.GatewayRoutingRules.Add(RuleWithSplit(BillingOperationType.Payment, CardProcessor.NmiSpreedly, catchAll: true));
        db.GatewayFallbackRules.Add(new GatewayFallbackRule
        {
            OperationType      = BillingOperationType.Payment,
            PrimaryProcessor   = CardProcessor.NmiSpreedly,
            StepOrder          = 10,
            NextProcessor      = CardProcessor.CheckoutUS,
            DelayMinutes       = 60,
            ForceUsdOnFallback = true
        });
        await db.SaveChangesAsync();

        var router = CreateRouter(db);
        var ctx = MakeCtx(country: "US");

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        var delayedStep = result.Value!.Steps.First(s => s.DelayMinutes > 0);
        delayedStep.DelayMinutes.Should().Be(60);
        delayedStep.CardProcessor.Should().Be(CardProcessor.CheckoutUS);
    }

    // ── Inactive rule is ignored ───────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenRuleIsInactive_IgnoresIt()
    {
        using var db = TestDbContextFactory.Create();
        var inactiveRule = RuleWithSplit(BillingOperationType.Payment, CardProcessor.NmiSpreedly, catchAll: true);
        inactiveRule.IsActive = false;
        db.GatewayRoutingRules.Add(inactiveRule);
        await db.SaveChangesAsync();

        var router = CreateRouter(db);
        var ctx = MakeCtx();

        var result = await router.ResolveAsync(ctx);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_ROUTING_RULE");
    }
}
