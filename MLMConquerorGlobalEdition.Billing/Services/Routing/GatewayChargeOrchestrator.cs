using Hangfire;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// Executes the routing plan step-by-step:
///   - Primary attempt first (FallbackStepIndex=0).
///   - On failure: advance to next step.
///   - If the next step has DelayMinutes > 0: enqueue DelayedFallbackChargeJob
///     and return Scheduled (do NOT block).
///   - On success: write PaymentHistory + GatewayChargeAttempt(Success).
///   - If all steps exhausted: return failure with last gateway error.
/// </summary>
public class GatewayChargeOrchestrator : IGatewayChargeOrchestrator
{
    private readonly AppDbContext          _db;
    private readonly ICardGatewayResolver  _gatewayResolver;
    private readonly IBackgroundJobClient  _hangfire;
    private readonly IDateTimeProvider     _dateTime;
    private readonly ILogger<GatewayChargeOrchestrator> _logger;

    public GatewayChargeOrchestrator(
        AppDbContext db,
        ICardGatewayResolver gatewayResolver,
        IBackgroundJobClient hangfire,
        IDateTimeProvider dateTime,
        ILogger<GatewayChargeOrchestrator> logger)
    {
        _db              = db;
        _gatewayResolver = gatewayResolver;
        _hangfire        = hangfire;
        _dateTime        = dateTime;
        _logger          = logger;
    }

    public async Task<Result<OrchestratorChargeResult>> ExecuteAsync(
        GatewayRoutingPlan plan,
        GatewayRoutingContext ctx,
        OrchestratorChargeRequest chargeReq,
        CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        string? lastError     = null;
        string? lastErrorCode = null;

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];

            // If this step has a delay and it is NOT the first step (primary), schedule it
            if (step.DelayMinutes > 0 && i > 0)
            {
                var jobId = _hangfire.Schedule<DelayedFallbackChargeJob>(
                    job => job.ExecuteAsync(
                        plan.RouteBucketKey,
                        chargeReq.MemberId,
                        (int)step.CardProcessor,
                        step.Amount,
                        step.PresentmentCurrency,
                        step.FallbackStepIndex,
                        chargeReq.OrderId,
                        chargeReq.TokenizedCardRef,
                        chargeReq.NetworkTransactionId,
                        chargeReq.Description,
                        CancellationToken.None),
                    TimeSpan.FromMinutes(step.DelayMinutes));

                // Log the scheduled attempt
                await LogAttemptAsync(
                    plan.RouteBucketKey, step, ctx, chargeReq.MemberId,
                    "Scheduled", null, null, now, ct);

                return Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
                {
                    Status         = "Scheduled",
                    ProcessorUsed  = step.CardProcessor.ToString(),
                    AmountCharged  = step.Amount,
                    CurrencyCharged = step.PresentmentCurrency,
                    ScheduledJobId = jobId,
                    PaymentHistoryId = string.Empty,
                    GatewayTransactionId = string.Empty
                });
            }

            // Attempt charge
            ICardGatewayService gateway;
            try { gateway = _gatewayResolver.Resolve(step.CardProcessor); }
            catch (InvalidOperationException ex)
            {
                lastError     = ex.Message;
                lastErrorCode = "GATEWAY_NOT_REGISTERED";
                _logger.LogError(ex, "Orchestrator: gateway {P} not registered.", step.CardProcessor);
                continue;
            }

            var req = new GatewayChargeRequest
            {
                MemberId                   = chargeReq.MemberId,
                Amount                     = step.Amount,
                Currency                   = step.PresentmentCurrency,
                Description                = chargeReq.Description,
                TokenizedCardRef           = chargeReq.TokenizedCardRef,
                NetworkTransactionId       = chargeReq.NetworkTransactionId,
                IsRecurring                = chargeReq.IsRecurring,
                DownstreamProcessor        = step.CardProcessor,
                SpreedlyPaymentMethodToken = chargeReq.TokenizedCardRef,
                RawCard                    = chargeReq.RawCard,
                RetainOnSuccess            = chargeReq.RetainOnSuccess
            };

            var chargeResult = await gateway.ChargeAsync(req, ct);

            if (!chargeResult.IsSuccess)
            {
                lastError     = chargeResult.Error;
                lastErrorCode = chargeResult.ErrorCode;

                await LogAttemptAsync(
                    plan.RouteBucketKey, step, ctx, chargeReq.MemberId,
                    "Failed", null, chargeResult.Error, now, ct);

                _logger.LogWarning(
                    "Orchestrator: step {Step}/{Total} failed via {Proc}. Error: {Err}",
                    i + 1, plan.Steps.Count, step.CardProcessor, chargeResult.Error);

                continue;
            }

            // SUCCESS
            var txId = chargeResult.Value!.GatewayTransactionId;

            // Write PaymentHistory
            var payment = new PaymentHistory
            {
                OrderId              = chargeReq.OrderId ?? string.Empty,
                MemberId             = chargeReq.MemberId,
                Amount               = step.Amount,
                GatewayName          = step.CardProcessor.ToString(),
                GatewayTransactionId = txId,
                TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
                ProcessedAt          = now,
                CreationDate         = now,
                CreatedBy            = "billing-engine",
                LastUpdateDate       = now,
                LastUpdateBy         = "billing-engine"
            };
            _db.PaymentHistories.Add(payment);

            await LogAttemptAsync(
                plan.RouteBucketKey, step, ctx, chargeReq.MemberId,
                "Success", txId, null, now, ct, payment.Id);

            await _db.SaveChangesAsync(ct);

            return Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
            {
                PaymentHistoryId           = payment.Id,
                GatewayTransactionId       = txId,
                ProcessorUsed              = step.CardProcessor.ToString(),
                AmountCharged              = step.Amount,
                CurrencyCharged            = step.PresentmentCurrency,
                Status                     = "Success",
                SpreedlyPaymentMethodToken = chargeResult.Value!.SpreedlyPaymentMethodToken
            });
        }

        // All steps exhausted
        return Result<OrchestratorChargeResult>.Failure(
            lastErrorCode ?? "ALL_GATEWAYS_FAILED",
            lastError     ?? "All gateway attempts were exhausted.");
    }

    private async Task LogAttemptAsync(
        string routeBucketKey,
        GatewayAttemptPlan step,
        GatewayRoutingContext ctx,
        string memberId,
        string outcome,
        string? txId,
        string? failureReason,
        DateTime now,
        CancellationToken ct,
        string? paymentHistoryId = null)
    {
        _db.GatewayChargeAttempts.Add(new GatewayChargeAttempt
        {
            RouteBucketKey       = routeBucketKey,
            CardProcessor        = step.CardProcessor,
            FallbackStepIndex    = step.FallbackStepIndex,
            PresentmentCurrency  = step.PresentmentCurrency,
            OriginalAmountUsd    = ctx.AmountUsd,
            ConvertedAmount      = step.Amount,
            Outcome              = outcome,
            GatewayTransactionId = txId,
            PaymentHistoryId     = paymentHistoryId,
            FailureReason        = failureReason,
            AttemptedAtUtc       = now,
            CompletedAtUtc       = outcome != "Scheduled" ? now : null,
            MemberId             = memberId,
            OperationType        = ctx.OperationType,
            CardBrand            = ctx.CardBrand,
            CreatedBy            = "billing-engine",
            CreationDate         = now
        });

        await _db.SaveChangesAsync(ct);
    }
}
