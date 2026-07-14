using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

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
/// Configuration precedence for the master Spreedly credential (environment key / access
/// secret / base URL):
///   1. ApiCredential row (ServiceKey="Spreedly") — admin-managed, encrypted at rest. Preferred
///      in production since it can be rotated without a redeploy.
///   2. The "Spreedly" section in appsettings.json (BaseUrl / EnvironmentKey / AccessSecret) —
///      convenient for local development. Used only for whichever of the two fields the DB
///      row didn't provide.
///
/// The per-processor downstream-gateway-token (e.g. "NmiSpreedly", "CheckoutEUR") always comes
/// from its own ApiCredential row's SpreedlyGatewayTokenEncrypted — there's one per processor,
/// so appsettings isn't a practical place for those.
/// </summary>
public class SpreedlyCardGatewayService : ICardGatewayService
{
    // ICardGatewayService.Processor is returned by the resolver; this service
    // is used for ALL processors, so the DI resolver bypasses this property.
    // We expose a sentinel value; callers should use ICardGatewayResolver.Resolve.
    public CardProcessor Processor => CardProcessor.NmiSpreedly; // nominal; resolver ignores it

    private const string SpreedlyServiceKey = "Spreedly";
    private const string DefaultBaseUrl     = "https://core.spreedly.com";
    private const string ConfigPlaceholderPrefix = "REPLACE_WITH_";

    private readonly AppDbContext          _db;
    private readonly IHttpClientFactory    _httpClientFactory;
    private readonly IEncryptionService    _encryption;
    private readonly IConfiguration        _configuration;
    private readonly ILogger<SpreedlyCardGatewayService> _logger;

    public SpreedlyCardGatewayService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryption,
        IConfiguration configuration,
        ILogger<SpreedlyCardGatewayService> logger)
    {
        _db                = db;
        _httpClientFactory = httpClientFactory;
        _encryption        = encryption;
        _configuration     = configuration;
        _logger            = logger;
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
        if (string.IsNullOrWhiteSpace(req.SpreedlyPaymentMethodToken) && req.RawCard is null)
        {
            _logger.LogWarning(
                "[Spreedly] Member {MemberId}: no SpreedlyPaymentMethodToken and no RawCard. Cannot charge.",
                req.MemberId);
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_MEMBER_TOKEN_MISSING",
                $"Member {req.MemberId} has neither a Spreedly payment_method_token nor raw card details to charge.");
        }

        var authResult = await ResolveAuthAsync(processor, ct);
        if (!authResult.IsSuccess)
            return Result<GatewayChargeResult>.Failure(authResult.ErrorCode!, authResult.Error!);

        var (baseUrl, environmentKey, accessSecret, downstreamGatewayToken) = authResult.Value!;

        var transaction = new Dictionary<string, object?>
        {
            ["amount"]        = (int)Math.Round(req.Amount * 100m, MidpointRounding.AwayFromZero),
            ["currency_code"] = req.Currency,
            ["description"]   = req.Description
        };

        if (!string.IsNullOrWhiteSpace(req.SpreedlyPaymentMethodToken))
        {
            transaction["payment_method_token"] = req.SpreedlyPaymentMethodToken;
        }
        else
        {
            var card = req.RawCard!;
            transaction["credit_card"] = new Dictionary<string, object?>
            {
                ["first_name"]         = card.FirstName,
                ["last_name"]          = card.LastName,
                ["number"]             = card.Number,
                ["month"]              = card.Month,
                ["year"]               = card.Year,
                ["verification_value"] = card.Cvv
            };
            transaction["retain_on_success"] = req.RetainOnSuccess;
        }

        if (req.IsRecurring && !string.IsNullOrWhiteSpace(req.NetworkTransactionId))
        {
            transaction["stored_credential"] = new Dictionary<string, object?>
            {
                ["initiator"]              = "merchant",
                ["reason"]                 = "recurring",
                ["initial_transaction_id"] = req.NetworkTransactionId
            };
        }

        var url  = $"{baseUrl}/v1/gateways/{downstreamGatewayToken}/purchase.json";
        var body = new Dictionary<string, object?> { ["transaction"] = transaction };

        try
        {
            using var response = await SendAsync(HttpMethod.Post, url, body, environmentKey, accessSecret, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("transaction", out var txEl))
                return Result<GatewayChargeResult>.Failure(
                    "SPREEDLY_BAD_RESPONSE", $"Spreedly purchase response missing 'transaction'. HTTP {(int)response.StatusCode}.");

            var succeeded = txEl.TryGetProperty("succeeded", out var succEl) && succEl.GetBoolean();
            var message   = txEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
            var txToken   = txEl.TryGetProperty("token", out var tokEl) ? tokEl.GetString() ?? string.Empty : string.Empty;

            if (!succeeded)
            {
                _logger.LogWarning(
                    "[Spreedly] Charge declined — Processor: {Processor}, MemberId: {MemberId}, Message: {Message}.",
                    processor, req.MemberId, message);
                return Result<GatewayChargeResult>.Failure("SPREEDLY_DECLINED", message ?? "Card was declined.");
            }

            string? vaultedToken = null;
            if (txEl.TryGetProperty("payment_method", out var pmEl) &&
                pmEl.TryGetProperty("token", out var pmTokEl))
            {
                vaultedToken = pmTokEl.GetString();
            }

            _logger.LogInformation(
                "[Spreedly] Charge succeeded — TxId: {TxId}, Processor: {Processor}, MemberId: {MemberId}.",
                txToken, processor, req.MemberId);

            return Result<GatewayChargeResult>.Success(new GatewayChargeResult
            {
                GatewayTransactionId       = txToken,
                Status                     = "succeeded",
                RawResponse                = json,
                SpreedlyPaymentMethodToken = vaultedToken
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "[Spreedly] Charge request failed for member {MemberId}.", req.MemberId);
            return Result<GatewayChargeResult>.Failure("SPREEDLY_REQUEST_FAILED", $"Spreedly request failed: {ex.Message}");
        }
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
        var authResult = await ResolveAuthAsync(processor, ct);
        if (!authResult.IsSuccess)
            return Result<bool>.Failure(authResult.ErrorCode!, authResult.Error!);

        var (baseUrl, environmentKey, accessSecret, _) = authResult.Value!;

        var url  = $"{baseUrl}/v1/transactions/{gatewayTransactionId}/credit.json";
        var body = new Dictionary<string, object?>
        {
            ["transaction"] = new Dictionary<string, object?>
            {
                ["amount"] = (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)
            }
        };

        try
        {
            using var response = await SendAsync(HttpMethod.Post, url, body, environmentKey, accessSecret, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var succeeded = doc.RootElement.TryGetProperty("transaction", out var txEl) &&
                            txEl.TryGetProperty("succeeded", out var succEl) && succEl.GetBoolean();

            if (!succeeded)
            {
                var message = doc.RootElement.TryGetProperty("transaction", out var t) &&
                              t.TryGetProperty("message", out var m) ? m.GetString() : null;
                _logger.LogWarning(
                    "[Spreedly] Refund failed — GatewayTxId: {TxId}, Processor: {Processor}, Message: {Message}.",
                    gatewayTransactionId, processor, message);
                return Result<bool>.Failure("SPREEDLY_REFUND_FAILED", message ?? "Refund was not accepted by Spreedly.");
            }

            _logger.LogInformation(
                "[Spreedly] Refund succeeded — GatewayTxId: {TxId}, Amount: {Amount}, Processor: {Processor}.",
                gatewayTransactionId, amount, processor);
            return Result<bool>.Success(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "[Spreedly] Refund request failed for transaction {TxId}.", gatewayTransactionId);
            return Result<bool>.Failure("SPREEDLY_REQUEST_FAILED", $"Spreedly request failed: {ex.Message}");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, Dictionary<string, object?> body,
        string environmentKey, string accessSecret, CancellationToken ct)
    {
        var client  = _httpClientFactory.CreateClient("Spreedly");
        using var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{environmentKey}:{accessSecret}")));

        return await client.SendAsync(request, ct);
    }

    private async Task<Result<(string BaseUrl, string EnvironmentKey, string AccessSecret, string DownstreamGatewayToken)>>
        ResolveAuthAsync(CardProcessor processor, CancellationToken ct)
    {
        var spreedlyCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == SpreedlyServiceKey && !c.IsDeleted, ct);

        string? environmentKey = null;
        string? accessSecret   = null;
        string? baseUrl        = null;

        if (spreedlyCred is not null && spreedlyCred.IsActive)
        {
            baseUrl = spreedlyCred.BaseUrl;

            try
            {
                if (!string.IsNullOrWhiteSpace(spreedlyCred.ApiKeyEncrypted))
                    environmentKey = _encryption.Decrypt(spreedlyCred.ApiKeyEncrypted);
                if (!string.IsNullOrWhiteSpace(spreedlyCred.SecretKeyEncrypted))
                    accessSecret = _encryption.Decrypt(spreedlyCred.SecretKeyEncrypted);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "[Spreedly] Failed to decrypt master credential.");
                return Result<(string, string, string, string)>.Failure(
                    "SPREEDLY_CREDENTIAL_DECRYPT_FAILED",
                    "Could not decrypt Spreedly credentials. They may have been encrypted by a service outside the shared key ring.");
            }
        }

        // Fall back to the "Spreedly" section in appsettings.json for whichever field the DB
        // row didn't provide — convenient for local/dev environments without an admin-configured row.
        environmentKey ??= ReadConfigValue("Spreedly:EnvironmentKey");
        accessSecret   ??= ReadConfigValue("Spreedly:AccessSecret");
        baseUrl        ??= ReadConfigValue("Spreedly:BaseUrl");

        if (string.IsNullOrWhiteSpace(environmentKey) && string.IsNullOrWhiteSpace(accessSecret))
            return Result<(string, string, string, string)>.Failure(
                "SPREEDLY_CREDENTIAL_MISSING",
                "Spreedly environment key and access secret are not configured. Set them via the admin " +
                "Credentials page (ApiCredential 'Spreedly') or the 'Spreedly' section in appsettings.json.");

        if (string.IsNullOrWhiteSpace(environmentKey) || string.IsNullOrWhiteSpace(accessSecret))
            return Result<(string, string, string, string)>.Failure(
                "SPREEDLY_CREDENTIAL_INCOMPLETE",
                "Spreedly environment key or access secret is missing. Both must be set via the admin " +
                "Credentials page or the 'Spreedly' section in appsettings.json.");

        var processorKey = processor.ToString(); // e.g. "NmiSpreedly", "CheckoutEUR"
        var processorCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == processorKey && !c.IsDeleted, ct);

        string? downstreamGatewayToken = null;
        if (processorCred is not null && !string.IsNullOrWhiteSpace(processorCred.SpreedlyGatewayTokenEncrypted))
        {
            try
            {
                downstreamGatewayToken = _encryption.Decrypt(processorCred.SpreedlyGatewayTokenEncrypted);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "[Spreedly] Failed to decrypt downstream gateway token for processor {Processor}.", processor);
                return Result<(string, string, string, string)>.Failure(
                    "SPREEDLY_CREDENTIAL_DECRYPT_FAILED",
                    "Could not decrypt the Spreedly downstream gateway token. It may have been encrypted by a service outside the shared key ring.");
            }
        }

        // Fall back to a single "Spreedly:DefaultGatewayToken" in appsettings.json — convenient
        // for local/dev testing against one Spreedly Test Gateway regardless of which processor
        // the routing engine picked. Production should configure a real token per processor via
        // the admin Credentials page instead.
        downstreamGatewayToken ??= ReadConfigValue("Spreedly:DefaultGatewayToken");

        if (string.IsNullOrWhiteSpace(downstreamGatewayToken))
        {
            if (processorCred is null)
                return Result<(string, string, string, string)>.Failure(
                    "SPREEDLY_DOWNSTREAM_TOKEN_MISSING",
                    $"ApiCredential '{processorKey}' not found. Provision the processor in Spreedly and store " +
                    "the resulting gateway token via the admin Credentials page, or set 'Spreedly:DefaultGatewayToken' " +
                    "in appsettings.json for local testing.");

            return Result<(string, string, string, string)>.Failure(
                "SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
                $"ApiCredential '{processorKey}'.SpreedlyGatewayTokenEncrypted is not set. After provisioning the " +
                "gateway in Spreedly, copy the gateway_token into the admin Credentials page, or set " +
                "'Spreedly:DefaultGatewayToken' in appsettings.json for local testing.");
        }

        return Result<(string, string, string, string)>.Success(
            (baseUrl ?? DefaultBaseUrl, environmentKey, accessSecret, downstreamGatewayToken));
    }

    /// <summary>Reads a config value, treating an un-replaced "REPLACE_WITH_..." placeholder as absent.</summary>
    private string? ReadConfigValue(string key)
    {
        var value = _configuration[key];
        return string.IsNullOrWhiteSpace(value) || value.StartsWith(ConfigPlaceholderPrefix, StringComparison.Ordinal)
            ? null
            : value;
    }
}
