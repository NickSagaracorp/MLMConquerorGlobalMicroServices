using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;

public interface IEWalletClient
{
    Task<Result<string>>  CreateUserAsync(EWalletCreateUserRequest request, CancellationToken ct = default);
    Task<Result<bool>>    UserExistsAsync(string userName, CancellationToken ct = default);
    Task<Result<decimal>> GetBalanceAsync(string userName, string currency, CancellationToken ct = default);
    Task<Result<string>>  LoadAsync(string userName, decimal amount, string merchantReferenceId, string? comments, CancellationToken ct = default);
}

public sealed class EWalletCreateUserRequest
{
    public required string UserName  { get; init; }
    public required string Email     { get; init; }
    public string? FirstName { get; init; }
    public string? LastName  { get; init; }
    public string? Country2  { get; init; }
    public string? City      { get; init; }
    public string? State     { get; init; }
    public string? ZipCode   { get; init; }
    public string? Address1  { get; init; }
    public string? DateOfBirth { get; init; }
}

/// <summary>
/// Cliente HTTP de i-Payout (eWallet).
///
/// Reescrito, no portado: la implementación de MWRLife parsea las respuestas partiendo el
/// JSON por comas y dos puntos (<c>responseString.Split(',')</c> y luego <c>Split(':')</c>),
/// lo que se rompe con cualquier valor que contenga una coma — por ejemplo un m_Text de
/// error o un nombre "Pérez, Juan". Acá se deserializa con DTOs tipados.
/// También arma el JSON por concatenación de strings, sin escapar: un comentario con
/// comillas corrompe el payload. Acá se serializa.
/// </summary>
public class EWalletClient : IEWalletClient
{
    private const string CredentialServiceKey = "EWallet";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory     _httpFactory;
    private readonly AppDbContext           _db;
    private readonly IEncryptionService     _crypto;
    private readonly ILogger<EWalletClient> _logger;

    public EWalletClient(
        IHttpClientFactory httpFactory,
        AppDbContext db,
        IEncryptionService crypto,
        ILogger<EWalletClient> logger)
    {
        _httpFactory = httpFactory;
        _db          = db;
        _crypto      = crypto;
        _logger      = logger;
    }

    public const string HttpClientName = "ewallet";

    public async Task<Result<string>> CreateUserAsync(EWalletCreateUserRequest request, CancellationToken ct = default)
    {
        var cfg = await ResolveAsync(ct);
        if (!cfg.IsSuccess) return Result<string>.Failure(cfg.ErrorCode!, cfg.Error!);

        var payload = new Dictionary<string, object?>
        {
            ["fn"]               = EWalletFunctions.CreateUser,
            ["MerchantGUID"]     = cfg.Value!.MerchantGuid,
            ["MerchantPassword"] = cfg.Value!.MerchantPassword,
            ["UserName"]         = request.UserName,
            ["FirstName"]        = request.FirstName,
            ["LastName"]         = request.LastName,
            ["EmailAddress"]     = request.Email,
            ["Address1"]         = request.Address1,
            ["City"]             = request.City,
            ["State"]            = request.State,
            ["ZipCode"]          = request.ZipCode,
            ["Country2xFormat"]  = request.Country2,
            ["DateOfBirth"]      = request.DateOfBirth
        };

        var result = await PostAsync<EWalletCreateUserResponse>(cfg.Value!, payload, "create user", ct);
        if (!result.IsSuccess) return Result<string>.Failure(result.ErrorCode!, result.Error!);

        var status = result.Value!.Response;
        if (status is null || status.Code < 0)
            return Result<string>.Failure(
                $"EWALLET_{status?.Code ?? -1}",
                $"i-Payout rejected the registration: {status?.Text ?? "no response envelope"}");

        // El UserName que devuelve el gateway es la identidad definitiva de la cuenta.
        // Si no viene, cae al que se pidió — pero se registra, porque significa que el
        // contrato cambió y hay que revisarlo.
        var assigned = result.Value!.TransactionRefId;
        if (string.IsNullOrWhiteSpace(assigned))
        {
            _logger.LogWarning(
                "i-Payout created user {UserName} but returned no TransactionRefID; falling back to the requested user name.",
                request.UserName);
            assigned = request.UserName;
        }

        return Result<string>.Success(assigned!);
    }

    public async Task<Result<bool>> UserExistsAsync(string userName, CancellationToken ct = default)
    {
        var cfg = await ResolveAsync(ct);
        if (!cfg.IsSuccess) return Result<bool>.Failure(cfg.ErrorCode!, cfg.Error!);

        var payload = new Dictionary<string, object?>
        {
            ["fn"]               = EWalletFunctions.CheckIfUserNameExists,
            ["MerchantGUID"]     = cfg.Value!.MerchantGuid,
            ["MerchantPassword"] = cfg.Value!.MerchantPassword,
            ["UserName"]         = userName
        };

        var result = await PostAsync<EWalletResponseEnvelope>(cfg.Value!, payload, "check user", ct);
        if (!result.IsSuccess) return Result<bool>.Failure(result.ErrorCode!, result.Error!);

        // m_Code >= 0 significa que la consulta se resolvió y el usuario existe.
        return Result<bool>.Success((result.Value!.Response?.Code ?? -1) >= 0);
    }

    public async Task<Result<decimal>> GetBalanceAsync(string userName, string currency, CancellationToken ct = default)
    {
        var cfg = await ResolveAsync(ct);
        if (!cfg.IsSuccess) return Result<decimal>.Failure(cfg.ErrorCode!, cfg.Error!);

        var payload = new Dictionary<string, object?>
        {
            ["fn"]               = EWalletFunctions.GetCurrencyBalance,
            ["MerchantGUID"]     = cfg.Value!.MerchantGuid,
            ["MerchantPassword"] = cfg.Value!.MerchantPassword,
            ["UserName"]         = userName,
            ["CurrencyCode"]     = currency
        };

        var result = await PostAsync<EWalletBalanceResponse>(cfg.Value!, payload, "get balance", ct);
        if (!result.IsSuccess) return Result<decimal>.Failure(result.ErrorCode!, result.Error!);

        var status = result.Value!.Response;
        if (status is null || status.Code < 0)
            return Result<decimal>.Failure(
                $"EWALLET_{status?.Code ?? -1}",
                $"i-Payout could not return a balance: {status?.Text ?? "no response envelope"}");

        return Result<decimal>.Success(result.Value!.Balance);
    }

    public async Task<Result<string>> LoadAsync(
        string userName, decimal amount, string merchantReferenceId, string? comments, CancellationToken ct = default)
    {
        var cfg = await ResolveAsync(ct);
        if (!cfg.IsSuccess) return Result<string>.Failure(cfg.ErrorCode!, cfg.Error!);

        var payload = new Dictionary<string, object?>
        {
            ["fn"]               = EWalletFunctions.Load,
            ["MerchantGUID"]     = cfg.Value!.MerchantGuid,
            ["MerchantPassword"] = cfg.Value!.MerchantPassword,
            // PartnerBatchID determinista a partir de nuestra referencia. MWRLife lo armaba
            // con DateTime.Now, así que cada reintento creaba un lote nuevo y perdía la
            // protección contra duplicados.
            ["PartnerBatchID"]   = merchantReferenceId,
            ["PoolID"]           = string.Empty,
            ["arrAccounts"]      = new[]
            {
                new EWalletLoadAccount
                {
                    UserName            = userName,
                    Amount              = amount,
                    Comments            = comments,
                    MerchantReferenceId = merchantReferenceId
                }
            },
            // La pareja que da idempotencia: si la referencia ya se usó, i-Payout rechaza
            // en vez de acreditar de nuevo.
            ["AllowDuplicates"]  = false,
            ["AutoLoad"]         = true,
            ["CurrencyCode"]     = "USD"
        };

        var result = await PostAsync<EWalletLoadResponse>(cfg.Value!, payload, "load account", ct);
        if (!result.IsSuccess) return Result<string>.Failure(result.ErrorCode!, result.Error!);

        var status = result.Value!.Response;
        if (status is null || status.Code < 0)
            return Result<string>.Failure(
                $"EWALLET_{status?.Code ?? -1}",
                $"i-Payout rejected the load: {status?.Text ?? "no response envelope"}");

        // El envoltorio puede venir OK y aun así fallar el ítem individual; hay que mirar los dos.
        var item = result.Value!.Accounts?.FirstOrDefault();
        if (item is not null && item.Code < 0)
            return Result<string>.Failure(
                $"EWALLET_ITEM_{item.Code}",
                $"i-Payout rejected the payout to {userName}: {item.Text}");

        return Result<string>.Success(item?.TransactionRefId ?? merchantReferenceId);
    }

    // ── plomería ────────────────────────────────────────────────────────────

    private sealed record EWalletConfig(string BaseUrl, string MerchantGuid, string MerchantPassword);

    private async Task<Result<EWalletConfig>> ResolveAsync(CancellationToken ct)
    {
        var gateway = await _db.PaymentGateways
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.WalletType == WalletType.eWallet, ct);

        var environment = string.IsNullOrWhiteSpace(gateway?.Environment) ? "Sandbox" : gateway!.Environment!.Trim();

        var cred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == CredentialServiceKey && c.Environment == environment, ct);

        if (cred is null)
            return Result<EWalletConfig>.Failure(
                "EWALLET_NO_CREDENTIAL",
                $"No ApiCredential found for ServiceKey 'EWallet' in environment '{environment}'.");

        if (!cred.IsActive)
            return Result<EWalletConfig>.Failure(
                "EWALLET_CREDENTIAL_INACTIVE",
                $"ApiCredential 'EWallet' ({environment}) is marked inactive.");

        if (string.IsNullOrWhiteSpace(cred.ApiKeyEncrypted) || string.IsNullOrWhiteSpace(cred.SecretKeyEncrypted))
            return Result<EWalletConfig>.Failure(
                "EWALLET_INCOMPLETE_CREDENTIAL",
                $"ApiCredential 'EWallet' ({environment}) is missing the merchant GUID and/or password.");

        try
        {
            return Result<EWalletConfig>.Success(new EWalletConfig(
                cred.BaseUrl ?? "https://api.i-payout.com/eWalletAPI",
                _crypto.Decrypt(cred.ApiKeyEncrypted!),
                _crypto.Decrypt(cred.SecretKeyEncrypted!)));
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            return Result<EWalletConfig>.Failure(
                "EWALLET_CREDENTIAL_UNDECRYPTABLE",
                $"The stored i-Payout credential for '{environment}' could not be decrypted. " +
                "Re-enter it in Admin → Billing → API Credentials. If it was saved by another service, " +
                "confirm both hosts share the Data Protection key ring.");
        }
    }

    private async Task<Result<T>> PostAsync<T>(
        EWalletConfig cfg, Dictionary<string, object?> payload, string operation, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient(HttpClientName);
        var json   = JsonSerializer.Serialize(payload, Json);

        using var request = new HttpRequestMessage(HttpMethod.Post, cfg.BaseUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("i-Payout {Fn} → {Status}", payload["fn"], (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
            return Result<T>.Failure(
                $"EWALLET_HTTP_{(int)response.StatusCode}",
                $"i-Payout {operation} failed ({(int)response.StatusCode}): {Truncate(body)}");

        if (string.IsNullOrWhiteSpace(body))
            return Result<T>.Failure("EWALLET_EMPTY_RESPONSE", $"i-Payout {operation} returned an empty body.");

        try
        {
            var value = JsonSerializer.Deserialize<T>(body, Json);
            return value is null
                ? Result<T>.Failure("EWALLET_MALFORMED_RESPONSE", $"i-Payout {operation} deserialized to null: {Truncate(body)}")
                : Result<T>.Success(value);
        }
        catch (JsonException ex)
        {
            return Result<T>.Failure(
                "EWALLET_MALFORMED_RESPONSE",
                $"i-Payout {operation} returned unparseable JSON ({ex.Message}): {Truncate(body)}");
        }
    }

    private static string Truncate(string s, int max = 500) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");
}
