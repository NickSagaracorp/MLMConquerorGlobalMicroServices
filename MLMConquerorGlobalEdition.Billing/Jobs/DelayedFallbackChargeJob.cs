using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Hangfire one-off delayed job — queue "billing".
/// Executes a single fallback charge step after a configured delay
/// (e.g. 60 minutes for recurring USA/Canada retry).
/// Enqueued by GatewayChargeOrchestrator when a step has DelayMinutes > 0.
/// </summary>
[Queue("billing")]
public class DelayedFallbackChargeJob
{
    private readonly AppDbContext          _db;
    private readonly ICardGatewayResolver  _gatewayResolver;
    private readonly IDateTimeProvider     _dateTime;
    private readonly ILogger<DelayedFallbackChargeJob> _logger;

    public DelayedFallbackChargeJob(
        AppDbContext db,
        ICardGatewayResolver gatewayResolver,
        IDateTimeProvider dateTime,
        ILogger<DelayedFallbackChargeJob> logger)
    {
        _db              = db;
        _gatewayResolver = gatewayResolver;
        _dateTime        = dateTime;
        _logger          = logger;
    }

    public async Task ExecuteAsync(
        string routeBucketKey,
        string memberId,
        int    processorOrdinal,
        decimal amount,
        string currency,
        int    fallbackStepIndex,
        string? orderId,
        string? tokenizedCardRef,
        string? networkTransactionId,
        string  description,
        CancellationToken ct = default)
    {
        var processor = (CardProcessor)processorOrdinal;
        var now       = _dateTime.Now;

        _logger.LogInformation(
            "DelayedFallbackChargeJob: executing fallback step {Step} via {Proc} for member {MemberId}.",
            fallbackStepIndex, processor, memberId);

        ICardGatewayService gateway;
        try { gateway = _gatewayResolver.Resolve(processor); }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "DelayedFallbackChargeJob: gateway {P} not registered.", processor);
            return;
        }

        var req = new GatewayChargeRequest
        {
            MemberId             = memberId,
            Amount               = amount,
            Currency             = currency,
            Description          = description,
            TokenizedCardRef     = tokenizedCardRef,
            NetworkTransactionId = networkTransactionId,
            IsRecurring          = true
        };

        var result = await gateway.ChargeAsync(req, ct);

        var attempt = new GatewayChargeAttempt
        {
            RouteBucketKey       = routeBucketKey,
            CardProcessor        = processor,
            FallbackStepIndex    = fallbackStepIndex,
            PresentmentCurrency  = currency,
            OriginalAmountUsd    = amount, // USD since recurring fallback forces USD
            ConvertedAmount      = amount,
            Outcome              = result.IsSuccess ? "Success" : "Failed",
            GatewayTransactionId = result.IsSuccess ? result.Value?.GatewayTransactionId : null,
            FailureReason        = result.IsSuccess ? null : result.Error,
            AttemptedAtUtc       = now,
            CompletedAtUtc       = now,
            MemberId             = memberId,
            OperationType        = BillingOperationType.Payment,
            CardBrand            = CardBrand.Other, // brand not available at delayed-retry time
            CreatedBy            = "billing-engine",
            CreationDate         = now
        };

        if (result.IsSuccess)
        {
            var payment = new PaymentHistory
            {
                OrderId              = orderId ?? string.Empty,
                MemberId             = memberId,
                Amount               = amount,
                GatewayName          = processor.ToString(),
                GatewayTransactionId = result.Value!.GatewayTransactionId,
                TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
                ProcessedAt          = now,
                CreationDate         = now,
                CreatedBy            = "billing-engine",
                LastUpdateDate       = now,
                LastUpdateBy         = "billing-engine"
            };
            _db.PaymentHistories.Add(payment);
            attempt.PaymentHistoryId = payment.Id;

            _logger.LogInformation(
                "DelayedFallbackChargeJob: success via {Proc}, txId={TxId}.",
                processor, result.Value.GatewayTransactionId);
        }
        else
        {
            _logger.LogWarning(
                "DelayedFallbackChargeJob: failed via {Proc}. Error: {Err}",
                processor, result.Error);
        }

        _db.GatewayChargeAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
    }
}
