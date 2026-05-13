using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Universal Spreedly proxy that handles ALL CardProcessor values.
///
/// Per BILLING-RULES §3:
///   - Every member's card is vaulted ONCE at Spreedly as a payment_method_token.
///   - At charge time, the routing engine selects the downstream gateway and passes its
///     Spreedly gateway token here. We call Spreedly with both tokens; Spreedly proxies
///     the charge to the downstream processor.
///   - We do NOT use Spreedly's own routing/adaptive-acceptance features.
///   - The seven per-processor stub classes are replaced by this single proxy.
///
/// Configuration in ApiCredential:
///   - "Spreedly" row: ApiKeyEncrypted = Spreedly environment key (the API auth credential).
///   - One row per CardProcessor (e.g. "NmiSpreedly", "CheckoutEUR", …):
///       SpreedlyGatewayTokenEncrypted = the Spreedly downstream-gateway-token assigned when
///       that processor was provisioned inside the Spreedly dashboard.
///
/// Real HTTP wiring to Spreedly's purchase endpoint is a functional STUB until credentials
/// land. The stub logs the full intended request shape for debuggability.
/// </summary>
public class SpreedlyCardGatewayService : ICardGatewayService
{
    // ICardGatewayService.Processor is returned by the resolver; this service
    // is used for ALL processors, so the DI resolver bypasses this property.
    // We expose a sentinel value; callers should use ICardGatewayResolver.Resolve.
    public CardProcessor Processor => CardProcessor.NmiSpreedly; // nominal; resolver ignores it

    private const string SpreedlyServiceKey = "Spreedly";

    private readonly AppDbContext _db;
    private readonly ILogger<SpreedlyCardGatewayService> _logger;

    public SpreedlyCardGatewayService(
        AppDbContext db,
        ILogger<SpreedlyCardGatewayService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<Result<GatewayChargeResult>> ChargeAsync(
        GatewayChargeRequest req,
        CancellationToken ct = default)
        => await ChargeWithProcessorAsync(req, req.DownstreamProcessor, ct);

    /// <summary>
    /// Core charge path. Accepts an explicit <paramref name="processor"/> so the resolver can
    /// call this for any CardProcessor value without needing per-processor subclasses.
    /// </summary>
    public async Task<Result<GatewayChargeResult>> ChargeWithProcessorAsync(
        GatewayChargeRequest req,
        CardProcessor processor,
        CancellationToken ct = default)
    {
        // 1. Validate member's Spreedly payment_method_token
        if (string.IsNullOrWhiteSpace(req.SpreedlyPaymentMethodToken))
        {
            _logger.LogWarning(
                "[Spreedly] Member {MemberId}: SpreedlyPaymentMethodToken is missing. Cannot charge.",
                req.MemberId);
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_MEMBER_TOKEN_MISSING",
                $"Member {req.MemberId} has no Spreedly payment_method_token. Vault the card through Spreedly before charging.");
        }

        // 2. Resolve the Spreedly environment credential
        var spreedlyCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == SpreedlyServiceKey && !c.IsDeleted, ct);

        if (spreedlyCred is null || !spreedlyCred.IsActive)
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_CREDENTIAL_MISSING",
                "ApiCredential 'Spreedly' is missing or inactive. Configure it via the admin Credentials page.");

        if (string.IsNullOrWhiteSpace(spreedlyCred.ApiKeyEncrypted))
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_CREDENTIAL_INCOMPLETE",
                "ApiCredential 'Spreedly'.ApiKeyEncrypted is not set.");

        // 3. Resolve downstream-gateway-token for the chosen processor
        var processorKey = processor.ToString(); // e.g. "NmiSpreedly", "CheckoutEUR"
        var processorCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == processorKey && !c.IsDeleted, ct);

        if (processorCred is null)
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_DOWNSTREAM_TOKEN_MISSING",
                $"ApiCredential '{processorKey}' not found. " +
                $"Provision the processor in Spreedly and store the resulting gateway token via the admin Credentials page.");

        if (string.IsNullOrWhiteSpace(processorCred.SpreedlyGatewayTokenEncrypted))
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
                $"ApiCredential '{processorKey}'.SpreedlyGatewayTokenEncrypted is not set. " +
                $"After provisioning the gateway in Spreedly, copy the gateway_token into the admin Credentials page.");

        // 4. Log intended request shape (useful before real HTTP wiring lands)
        _logger.LogInformation(
            "[Spreedly][STUB] Charge request — " +
            "Processor: {Processor}, MemberId: {MemberId}, Amount: {Amount} {Currency}, " +
            "PaymentMethodToken: {PmToken}, DownstreamGatewayToken: {GwToken} (masked), " +
            "IsRecurring: {IsRecurring}, NetworkTransactionId: {NtxId}.",
            processor,
            req.MemberId,
            req.Amount,
            req.Currency,
            req.SpreedlyPaymentMethodToken,
            processorCred.SpreedlyGatewayTokenEncrypted![..Math.Min(8, processorCred.SpreedlyGatewayTokenEncrypted.Length)] + "…",
            req.IsRecurring,
            req.NetworkTransactionId ?? "(none)");

        // 5. Stub response — replace with real Spreedly HTTP call when credentials are wired
        //    Spreedly endpoint: POST https://core.spreedly.com/v1/gateways/{gateway_token}/purchase.json
        //    Body: { "transaction": { "payment_method_token": "...", "amount": ..., "currency_code": "...",
        //                             "retain_on_success": true } }
        //    Recurring billing: include "stored_credential" { "initiator": "merchant", "reason": "recurring",
        //                                                       "initial_transaction_id": "<networkTxId>" }
        var simulatedTxId = $"simulated-spreedly-txn-{Guid.NewGuid():N}";

        _logger.LogInformation(
            "[Spreedly][STUB] Simulated success — TxId: {TxId}, Processor: {Processor}.",
            simulatedTxId, processor);

        return Result<GatewayChargeResult>.Success(new GatewayChargeResult
        {
            GatewayTransactionId = simulatedTxId,
            Status               = "simulated_success",
            RawResponse          = $"{{\"stub\":true,\"processor\":\"{processor}\",\"transaction_token\":\"{simulatedTxId}\"}}"
        });
    }

    public async Task<Result<bool>> RefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default)
        => await RefundWithProcessorAsync(gatewayTransactionId, amount, CardProcessor.NmiSpreedly, ct);

    public async Task<Result<bool>> RefundWithProcessorAsync(
        string gatewayTransactionId,
        decimal amount,
        CardProcessor processor,
        CancellationToken ct = default)
    {
        // 1. Resolve Spreedly credential
        var spreedlyCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == SpreedlyServiceKey && !c.IsDeleted, ct);

        if (spreedlyCred is null || !spreedlyCred.IsActive || string.IsNullOrWhiteSpace(spreedlyCred.ApiKeyEncrypted))
            return Result<bool>.Failure(
                "SPREEDLY_CREDENTIAL_MISSING",
                "ApiCredential 'Spreedly' is missing, inactive, or has no ApiKeyEncrypted.");

        // 2. Resolve downstream token
        var processorKey  = processor.ToString();
        var processorCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == processorKey && !c.IsDeleted, ct);

        if (processorCred is null || string.IsNullOrWhiteSpace(processorCred.SpreedlyGatewayTokenEncrypted))
            return Result<bool>.Failure(
                "SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
                $"ApiCredential '{processorKey}'.SpreedlyGatewayTokenEncrypted is not set.");

        _logger.LogInformation(
            "[Spreedly][STUB] Refund — GatewayTxId: {TxId}, Amount: {Amount}, Processor: {Processor}.",
            gatewayTransactionId, amount, processor);

        // Spreedly endpoint: POST https://core.spreedly.com/v1/transactions/{transaction_token}/credit.json
        return Result<bool>.Success(true);
    }
}
