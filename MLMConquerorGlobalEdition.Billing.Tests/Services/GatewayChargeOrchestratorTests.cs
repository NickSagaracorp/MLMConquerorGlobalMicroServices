using FluentAssertions;
using Hangfire;
using Hangfire.States;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class GatewayChargeOrchestratorTests
{
    private static readonly GatewayRoutingContext DefaultCtx = new()
    {
        OperationType        = BillingOperationType.Payment,
        CardBrand            = CardBrand.Visa,
        CardholderCountryIso = "US",
        AmountUsd            = 100m,
        MemberId             = "member-1"
    };

    private static readonly OrchestratorChargeRequest DefaultChargeReq = new()
    {
        MemberId             = "member-1",
        TokenizedCardRef     = "tok_abc",
        NetworkTransactionId = "ntxn_abc",
        Description          = "Test charge",
        OrderId              = "order-1",
        IsRecurring          = true
    };

    private static GatewayAttemptPlan Step(CardProcessor proc, int idx = 0, int delay = 0, decimal amount = 100m) =>
        new()
        {
            CardProcessor       = proc,
            PresentmentCurrency = "USD",
            Amount              = amount,
            FallbackStepIndex   = idx,
            DelayMinutes        = delay
        };

    private static GatewayRoutingPlan Plan(params GatewayAttemptPlan[] steps) =>
        new() { RouteBucketKey = "test-bucket", Steps = steps };

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Mock<ICardGatewayResolver> GatewayResolverReturning(
        CardProcessor proc, Result<GatewayChargeResult> chargeResult)
    {
        var gwMock = new Mock<ICardGatewayService>();
        gwMock.Setup(g => g.Processor).Returns(proc);
        gwMock.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(chargeResult);

        var resolverMock = new Mock<ICardGatewayResolver>();
        resolverMock.Setup(r => r.Resolve(proc)).Returns(gwMock.Object);
        return resolverMock;
    }

    private static GatewayChargeOrchestrator CreateOrchestrator(
        Microsoft.EntityFrameworkCore.DbContext db,
        ICardGatewayResolver resolver,
        IBackgroundJobClient? hangfire = null)
    {
        var hf = hangfire ?? new Mock<IBackgroundJobClient>().Object;
        var dtMock = new Mock<IDateTimeProvider>();
        dtMock.Setup(d => d.Now).Returns(DateTime.UtcNow);
        var logMock = new Mock<Microsoft.Extensions.Logging.ILogger<GatewayChargeOrchestrator>>();

        return new GatewayChargeOrchestrator(
            (MLMConquerorGlobalEdition.Repository.Context.AppDbContext)db,
            resolver,
            hf,
            dtMock.Object,
            logMock.Object);
    }

    // ── Primary success ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenPrimarySucceeds_ReturnsSuccessAndPersistsPaymentHistory()
    {
        using var db = TestDbContextFactory.Create();
        var chargeResult = Result<GatewayChargeResult>.Success(new GatewayChargeResult
        {
            GatewayTransactionId = "txn-123",
            Status               = "captured"
        });
        var resolver = GatewayResolverReturning(CardProcessor.NmiSpreedly, chargeResult);
        var orchestrator = CreateOrchestrator(db, resolver.Object);
        var plan = Plan(Step(CardProcessor.NmiSpreedly));

        var result = await orchestrator.ExecuteAsync(plan, DefaultCtx, DefaultChargeReq);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Success");
        result.Value.GatewayTransactionId.Should().Be("txn-123");
        db.PaymentHistories.Should().HaveCount(1);
        db.GatewayChargeAttempts.Should().HaveCount(1);
    }

    // ── Primary fails, fallback succeeds ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenPrimaryFailsFallbackSucceeds_ReturnsFallbackSuccess()
    {
        using var db = TestDbContextFactory.Create();
        var failResult    = Result<GatewayChargeResult>.Failure("DECLINED", "Declined");
        var successResult = Result<GatewayChargeResult>.Success(new GatewayChargeResult
        {
            GatewayTransactionId = "txn-fallback",
            Status               = "captured"
        });

        var gw1 = new Mock<ICardGatewayService>();
        gw1.Setup(g => g.Processor).Returns(CardProcessor.NmiSpreedly);
        gw1.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(failResult);

        var gw2 = new Mock<ICardGatewayService>();
        gw2.Setup(g => g.Processor).Returns(CardProcessor.NmiDirect);
        gw2.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(successResult);

        var resolver = new Mock<ICardGatewayResolver>();
        resolver.Setup(r => r.Resolve(CardProcessor.NmiSpreedly)).Returns(gw1.Object);
        resolver.Setup(r => r.Resolve(CardProcessor.NmiDirect)).Returns(gw2.Object);

        var orchestrator = CreateOrchestrator(db, resolver.Object);
        var plan = Plan(Step(CardProcessor.NmiSpreedly, 0), Step(CardProcessor.NmiDirect, 1));

        var result = await orchestrator.ExecuteAsync(plan, DefaultCtx, DefaultChargeReq);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayTransactionId.Should().Be("txn-fallback");
        result.Value.ProcessorUsed.Should().Be(CardProcessor.NmiDirect.ToString());
    }

    // ── All steps fail → exhaustion error ────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenAllStepsFail_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var failResult = Result<GatewayChargeResult>.Failure("DECLINED", "All declined");
        var resolver = GatewayResolverReturning(CardProcessor.NmiSpreedly, failResult);
        var orchestrator = CreateOrchestrator(db, resolver.Object);
        var plan = Plan(Step(CardProcessor.NmiSpreedly, 0), Step(CardProcessor.NmiSpreedly, 1));

        var result = await orchestrator.ExecuteAsync(plan, DefaultCtx, DefaultChargeReq);

        result.IsSuccess.Should().BeFalse();
    }

    // ── Delayed fallback step → Hangfire enqueue + Scheduled status ────────

    [Fact]
    public async Task ExecuteAsync_WhenFallbackHasDelay_EnqueuesHangfireJobAndReturnsScheduled()
    {
        using var db = TestDbContextFactory.Create();
        var failResult = Result<GatewayChargeResult>.Failure("DECLINED", "Primary declined");
        var resolver = GatewayResolverReturning(CardProcessor.NmiSpreedly, failResult);

        var hangfireMock = new Mock<IBackgroundJobClient>();
        hangfireMock
            .Setup(h => h.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<IState>()))
            .Returns("job-id-1");

        var orchestrator = CreateOrchestrator(db, resolver.Object, hangfireMock.Object);

        // Plan: primary NmiSpreedly (immediate) + delayed fallback CheckoutUS (60 min)
        var plan = Plan(
            Step(CardProcessor.NmiSpreedly, 0, delay: 0),
            Step(CardProcessor.CheckoutUS,  1, delay: 60));

        var result = await orchestrator.ExecuteAsync(plan, DefaultCtx, DefaultChargeReq);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Scheduled");
        hangfireMock.Verify(
            h => h.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<IState>()),
            Times.Once);
    }

    // ── Charge attempt is logged for each gateway step ────────────────────

    [Fact]
    public async Task ExecuteAsync_LogsGatewayChargeAttempt_ForEachAttemptedStep()
    {
        using var db = TestDbContextFactory.Create();
        var chargeResult = Result<GatewayChargeResult>.Success(new GatewayChargeResult
        {
            GatewayTransactionId = "txn-ok",
            Status               = "captured"
        });
        var resolver = GatewayResolverReturning(CardProcessor.NmiSpreedly, chargeResult);
        var orchestrator = CreateOrchestrator(db, resolver.Object);
        var plan = Plan(Step(CardProcessor.NmiSpreedly));

        await orchestrator.ExecuteAsync(plan, DefaultCtx, DefaultChargeReq);

        db.GatewayChargeAttempts.Should().HaveCount(1);
        var attempt = db.GatewayChargeAttempts.First();
        attempt.Outcome.Should().Be("Success");
        attempt.MemberId.Should().Be("member-1");
        attempt.CardProcessor.Should().Be(CardProcessor.NmiSpreedly);
    }

    // ── Regression: the built GatewayChargeRequest must carry the step's ──
    // ── processor, the vaulted token, and any raw-card/retain-on-success ──
    // ── details through to the gateway — previously these were dropped. ──

    [Fact]
    public async Task ExecuteAsync_PassesDownstreamProcessorAndTokenAndRawCardToGateway()
    {
        using var db = TestDbContextFactory.Create();
        GatewayChargeRequest? captured = null;

        var gwMock = new Mock<ICardGatewayService>();
        gwMock.Setup(g => g.Processor).Returns(CardProcessor.CheckoutEUR);
        gwMock.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
              .Callback<GatewayChargeRequest, CancellationToken>((req, _) => captured = req)
              .ReturnsAsync(Result<GatewayChargeResult>.Success(new GatewayChargeResult
              {
                  GatewayTransactionId = "txn-captured",
                  Status               = "captured"
              }));

        var resolver = new Mock<ICardGatewayResolver>();
        resolver.Setup(r => r.Resolve(CardProcessor.CheckoutEUR)).Returns(gwMock.Object);

        var orchestrator = CreateOrchestrator(db, resolver.Object);
        var plan = Plan(Step(CardProcessor.CheckoutEUR));

        var chargeReq = new OrchestratorChargeRequest
        {
            MemberId         = "member-1",
            TokenizedCardRef = "spm_existing_token",
            Description      = "Test charge",
            OrderId          = "order-1",
            RawCard          = new RawCardDetails { FirstName = "Jane", LastName = "Doe", Number = "4111111111111111", Month = 12, Year = 2030, Cvv = "123" },
            RetainOnSuccess  = true
        };

        await orchestrator.ExecuteAsync(plan, DefaultCtx, chargeReq);

        captured.Should().NotBeNull();
        captured!.DownstreamProcessor.Should().Be(CardProcessor.CheckoutEUR);
        captured.SpreedlyPaymentMethodToken.Should().Be("spm_existing_token");
        captured.RawCard.Should().NotBeNull();
        captured.RawCard!.Number.Should().Be("4111111111111111");
        captured.RetainOnSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenGatewayVaultsNewToken_PropagatesTokenToOrchestratorResult()
    {
        using var db = TestDbContextFactory.Create();
        var chargeResult = Result<GatewayChargeResult>.Success(new GatewayChargeResult
        {
            GatewayTransactionId       = "txn-vaulted",
            Status                     = "captured",
            SpreedlyPaymentMethodToken = "spm_newly_vaulted"
        });
        var resolver = GatewayResolverReturning(CardProcessor.NmiSpreedly, chargeResult);
        var orchestrator = CreateOrchestrator(db, resolver.Object);
        var plan = Plan(Step(CardProcessor.NmiSpreedly));

        var result = await orchestrator.ExecuteAsync(plan, DefaultCtx, DefaultChargeReq);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SpreedlyPaymentMethodToken.Should().Be("spm_newly_vaulted");
    }
}
