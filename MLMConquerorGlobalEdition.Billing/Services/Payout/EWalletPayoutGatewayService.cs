using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

/// <summary>
/// i-payout (eWallet) payout gateway. Talks to the legacy ws_JsonAdapter.aspx endpoint —
/// a JSON-over-HTTP wrapper around i-payout's ws_eWallet.asmx SOAP service. There is no
/// official public spec for this endpoint; the request/response shape below was reverse
/// engineered from i-payout's own SOAP operation directory (ws_eWallet.asmx?op=...) and a
/// community client library, NOT from i-payout-provided documentation. In particular:
///   - The m_Code == 0 "success" convention is ASSUMED (matches the common legacy-gateway
///     pattern and the SOAP field naming) but has not been confirmed against a live response.
///   - Every call logs the full raw response at Information level specifically so the first
///     real sandbox calls can be used to correct this if the assumption is wrong.
/// Every request is POST JSON: { "fn": "<operation>", "MerchantGUID", "MerchantPassword", ...}.
/// An account identifier containing "TEST_TRANSPORT_FAIL" short-circuits before the HTTP call,
/// for exercising the transport-failure path without depending on the sandbox being reachable.
/// </summary>
public class EWalletPayoutGatewayService : IPayoutGatewayService
{
    public WalletType GatewayType => WalletType.eWallet;

    private readonly HttpClient _httpClient;
    private readonly IPayoutOptions _options;
    private readonly ILogger<EWalletPayoutGatewayService> _logger;

    public EWalletPayoutGatewayService(
        HttpClient httpClient, IOptions<IPayoutOptions> options, ILogger<EWalletPayoutGatewayService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<PayoutAccountResult>> SubscribeAccountAsync(PayoutAccountContext ctx, CancellationToken ct = default)
    {
        // eWallet_RegisterUser rejects requests missing required profile fields (confirmed against
        // the sandbox: "First Name is Required" with m_Code -1). Send whatever PayoutAccountContext
        // was given; callers that only have an email will still hit the same rejection until they
        // populate FirstName/LastName too.
        var parameters = new Dictionary<string, object?>
        {
            ["UserName"] = ctx.AccountIdentifier,
            ["EmailAddress"] = ctx.Email ?? ctx.AccountIdentifier,
            ["DefaultCurrency"] = "USD"
        };
        if (!string.IsNullOrWhiteSpace(ctx.FirstName)) parameters["FirstName"] = ctx.FirstName;
        if (!string.IsNullOrWhiteSpace(ctx.LastName)) parameters["LastName"] = ctx.LastName;
        if (!string.IsNullOrWhiteSpace(ctx.Address1)) parameters["Address1"] = ctx.Address1;
        if (!string.IsNullOrWhiteSpace(ctx.City)) parameters["City"] = ctx.City;
        if (!string.IsNullOrWhiteSpace(ctx.State)) parameters["State"] = ctx.State;
        if (!string.IsNullOrWhiteSpace(ctx.ZipCode)) parameters["ZipCode"] = ctx.ZipCode;
        if (!string.IsNullOrWhiteSpace(ctx.CountryIso2)) parameters["Country2xFormat"] = ctx.CountryIso2;
        if (!string.IsNullOrWhiteSpace(ctx.PhoneNumber)) parameters["PhoneNumber"] = ctx.PhoneNumber;
        if (ctx.DateOfBirth.HasValue) parameters["DateOfBirth"] = ctx.DateOfBirth.Value.ToString("yyyy-MM-dd");

        var call = await CallAsync("eWallet_RegisterUser", parameters, ct);

        if (!call.TransportOk)
            return Result<PayoutAccountResult>.Failure("IPAYOUT_TRANSPORT_ERROR", call.Error!);

        if (call.Code != 0)
            return Result<PayoutAccountResult>.Failure(
                call.Code?.ToString() ?? "IPAYOUT_UNKNOWN_CODE", call.Text ?? "eWallet_RegisterUser failed");

        return Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists = true, GatewayCode = call.Code.ToString(), GatewayMessage = call.Text
        });
    }

    public async Task<Result<PayoutAccountResult>> ValidateAccountAsync(PayoutAccountContext ctx, CancellationToken ct = default)
    {
        var call = await CallAsync("eWallet_CheckIfUserNameExists", new Dictionary<string, object?>
        {
            ["UserName"] = ctx.AccountIdentifier
        }, ct);

        if (!call.TransportOk)
            return Result<PayoutAccountResult>.Failure("IPAYOUT_TRANSPORT_ERROR", call.Error!);

        // ASSUMPTION: m_Code == 0 means the username exists; any other code is treated as
        // "does not exist yet" rather than a hard failure, since that's the legitimate negative
        // answer this call exists to give (mirrors the pre-HTTP stub's always-Success contract).
        return Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists = call.Code == 0, GatewayCode = call.Code?.ToString() ?? "UNKNOWN", GatewayMessage = call.Text
        });
    }

    public async Task<Result<PayoutBalanceResult>> GetBalanceAsync(string memberId, string accountIdentifier, CancellationToken ct = default)
    {
        var call = await CallAsync("eWallet_GetBalance", new Dictionary<string, object?>
        {
            ["UserName"] = accountIdentifier
        }, ct);

        if (!call.TransportOk)
            return Result<PayoutBalanceResult>.Failure("IPAYOUT_TRANSPORT_ERROR", call.Error!);

        if (call.Code != 0)
            return Result<PayoutBalanceResult>.Failure(
                call.Code?.ToString() ?? "IPAYOUT_UNKNOWN_CODE", call.Text ?? "eWallet_GetBalance failed");

        var balance = TryGetProperty(call.Root, "Balance", out var balanceEl) && balanceEl.TryGetDecimal(out var b) ? b : 0m;
        var currency = TryGetProperty(call.Root, "CurrencyCode", out var currencyEl) ? currencyEl.GetString() ?? "USD" : "USD";

        return Result<PayoutBalanceResult>.Success(new PayoutBalanceResult
        {
            Balance = balance, Currency = currency, GatewayCode = call.Code.ToString(), GatewayMessage = call.Text
        });
    }

    public async Task<Result<PayoutTransferResult>> DisburseAsync(PayoutTransferContext ctx, CancellationToken ct = default)
    {
        if (ctx.AccountIdentifier.Contains("TEST_TRANSPORT_FAIL", StringComparison.OrdinalIgnoreCase))
            return Result<PayoutTransferResult>.Failure("SIM_TRANSPORT_FAIL", "Forced transport failure for testing");

        var call = await CallAsync("eWallet_MakePayoutRequest", new Dictionary<string, object?>
        {
            ["PartnerBatchID"] = ctx.Reference,
            ["PoolID"] = string.Empty,
            ["CurrencyCode"] = "USD",
            ["arrAccounts"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["UserName"] = ctx.AccountIdentifier,
                    ["Amount"] = ctx.AmountUsd,
                    ["Comments"] = ctx.Reference
                }
            }
        }, ct);

        if (!call.TransportOk)
            return Result<PayoutTransferResult>.Failure("IPAYOUT_TRANSPORT_ERROR", call.Error!);

        if (call.Code != 0)
            return Result<PayoutTransferResult>.Failure(
                call.Code?.ToString() ?? "IPAYOUT_UNKNOWN_CODE", call.Text ?? "eWallet_MakePayoutRequest failed");

        // Confirmed against a live sandbox response: LogTransactionID comes back 0 while
        // TransactionRefID carries the real, non-zero id — prefer it, falling back to
        // LogTransactionID only if TransactionRefID is itself absent/zero.
        var gatewayTxnId = TryGetNonZeroLong(call.Root, "TransactionRefID", out var refId)
            ? refId.ToString()
            : TryGetNonZeroLong(call.Root, "LogTransactionID", out var logId)
                ? logId.ToString()
                : $"ipayout-{Guid.NewGuid():N}";

        return Result<PayoutTransferResult>.Success(new PayoutTransferResult
        {
            GatewayTransactionId = gatewayTxnId, GatewayCode = call.Code.ToString(), GatewayMessage = call.Text
        });
    }

    /// <summary>
    /// Looks up transaction status by the merchant reference we passed as "Comments" on
    /// DisburseAsync — i-payout's eWallet_FindTransaction is queried by MerchantReferenceID,
    /// LogTransactionID or MerchantBatchID. We only track our own Reference (PayoutAttempt.Id),
    /// so this assumes i-payout echoes "Comments" back as MerchantReferenceID; unconfirmed.
    /// </summary>
    public async Task<Result<PayoutTransferStatusResult>> GetTransferStatusAsync(string reference, CancellationToken ct = default)
    {
        var call = await CallAsync("eWallet_FindTransaction", new Dictionary<string, object?>
        {
            ["MerchantReferenceID"] = reference
        }, ct);

        if (!call.TransportOk)
            return Result<PayoutTransferStatusResult>.Failure("IPAYOUT_TRANSPORT_ERROR", call.Error!);

        if (call.Code != 0)
            return Result<PayoutTransferStatusResult>.Success(new PayoutTransferStatusResult
            {
                State = PayoutTransferState.NotFound, GatewayCode = call.Code?.ToString() ?? "UNKNOWN", GatewayMessage = call.Text
            });

        string? tranStatus = null;
        if (TryGetProperty(call.Root, "ArrTransactionInfo", out var arr) && arr.ValueKind == JsonValueKind.Array
            && arr.GetArrayLength() > 0 && TryGetProperty(arr[0], "TranStatus", out var statusEl))
        {
            tranStatus = statusEl.GetString();
        }

        var state = tranStatus switch
        {
            { } s when s.Contains("fail", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("reject", StringComparison.OrdinalIgnoreCase) => PayoutTransferState.Failed,
            { } s when s.Contains("complet", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("success", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("paid", StringComparison.OrdinalIgnoreCase) => PayoutTransferState.Succeeded,
            _ => PayoutTransferState.Unknown // conservative default — reconciliation sweep leaves it Pending and retries
        };

        return Result<PayoutTransferStatusResult>.Success(new PayoutTransferStatusResult
        {
            State = state, GatewayTransactionId = reference, GatewayCode = call.Code.ToString(), GatewayMessage = call.Text
        });
    }

    private sealed record IPayoutCallResult(bool TransportOk, JsonElement Root, int? Code, string? Text, string? Error);

    private async Task<IPayoutCallResult> CallAsync(string fn, Dictionary<string, object?> parameters, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>(parameters)
        {
            ["fn"] = fn,
            ["MerchantGUID"] = _options.MerchantId,
            ["MerchantPassword"] = _options.Password
        };

        string raw;
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.PostAsJsonAsync(_options.BaseUrl, body, ct);
            raw = await httpResponse.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "[i-payout] {Fn}: transport error calling {BaseUrl}", fn, _options.BaseUrl);
            return new IPayoutCallResult(false, default, null, null, ex.Message);
        }

        // Logged at Information (not Debug) on purpose — this is the calibration signal for the
        // m_Code assumptions documented on the class. Do not remove until confirmed against
        // several real sandbox responses per operation.
        _logger.LogInformation(
            "[i-payout] {Fn}: HTTP {StatusCode} — raw response: {Raw}", fn, (int)httpResponse.StatusCode, raw);

        if (!httpResponse.IsSuccessStatusCode)
            return new IPayoutCallResult(false, default, null, null, $"HTTP {(int)httpResponse.StatusCode}");

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return new IPayoutCallResult(false, default, null, null, $"Invalid JSON response: {ex.Message}");
        }

        // Confirmed against a live sandbox response: the actual payload is wrapped one level
        // deep, e.g. {"response":{"m_Code":-1,"m_Text":"First Name is Required",...}}. Unwrap it
        // so every field lookup below (and every caller's Balance/AccStatus/ArrTransactionInfo
        // lookup) reads from the right level. Fall back to the raw root if some other operation
        // ever returns a flat (unwrapped) body.
        var envelope = TryGetProperty(root, "response", out var responseEl) && responseEl.ValueKind == JsonValueKind.Object
            ? responseEl
            : root;

        int? code = null;
        if (TryGetProperty(envelope, "m_Code", out var codeEl))
        {
            code = codeEl.ValueKind == JsonValueKind.Number
                ? codeEl.GetInt32()
                : int.TryParse(codeEl.GetString(), out var c) ? c : null;
        }

        var text = TryGetProperty(envelope, "m_Text", out var textEl) ? textEl.GetString() : null;

        return new IPayoutCallResult(true, envelope, code, text, null);
    }

    private static bool TryGetNonZeroLong(JsonElement obj, string name, out long value)
    {
        if (TryGetProperty(obj, name, out var el) && el.ValueKind == JsonValueKind.Number
            && el.TryGetInt64(out var n) && n != 0)
        {
            value = n;
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>i-payout's JSON casing is unconfirmed (m_Code vs m_code seen in different sources) — match case-insensitively.</summary>
    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
