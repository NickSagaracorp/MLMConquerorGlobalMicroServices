using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

public interface IPayQuickerSettingsProvider
{
    Task<Result<PayQuickerSettings>> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Arma <see cref="PayQuickerSettings"/> leyendo los selectores que el admin configuró en
/// PaymentGatewayInfo y la fila de ApiCredential que le corresponde.
///
/// Falla explícito y temprano: si falta la credencial, el ClientId o el secreto, devuelve
/// un error con código en vez de dejar que la llamada muera con un 401 opaco más adelante.
/// </summary>
public class PayQuickerSettingsProvider : IPayQuickerSettingsProvider
{
    private readonly AppDbContext       _db;
    private readonly IEncryptionService _crypto;

    public PayQuickerSettingsProvider(AppDbContext db, IEncryptionService crypto)
    {
        _db     = db;
        _crypto = crypto;
    }

    public async Task<Result<PayQuickerSettings>> GetAsync(CancellationToken ct = default)
    {
        var gateway = await _db.PaymentGateways
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.WalletType == WalletType.PayQuicker, ct);

        if (gateway is null)
            return Result<PayQuickerSettings>.Failure(
                "PAYQUICKER_NOT_CONFIGURED",
                "No PaymentGatewayInfo row exists for PayQuicker.");

        var version = string.IsNullOrWhiteSpace(gateway.ApiVersion) ? "V2" : gateway.ApiVersion!.Trim().ToUpperInvariant();
        if (version is not ("V1" or "V2"))
            return Result<PayQuickerSettings>.Failure(
                "PAYQUICKER_BAD_VERSION",
                $"PayQuicker ApiVersion '{gateway.ApiVersion}' is not supported. Use 'V1' or 'V2'.");

        var environment = string.IsNullOrWhiteSpace(gateway.Environment) ? "Sandbox" : gateway.Environment!.Trim();
        var serviceKey  = $"PayQuicker{version}";

        var cred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == serviceKey && c.Environment == environment, ct);

        if (cred is null)
            return Result<PayQuickerSettings>.Failure(
                "PAYQUICKER_NO_CREDENTIAL",
                $"No ApiCredential found for ServiceKey '{serviceKey}' in environment '{environment}'.");

        if (!cred.IsActive)
            return Result<PayQuickerSettings>.Failure(
                "PAYQUICKER_CREDENTIAL_INACTIVE",
                $"ApiCredential '{serviceKey}' ({environment}) is marked inactive.");

        if (string.IsNullOrWhiteSpace(cred.ApiKeyEncrypted) || string.IsNullOrWhiteSpace(cred.SecretKeyEncrypted))
            return Result<PayQuickerSettings>.Failure(
                "PAYQUICKER_INCOMPLETE_CREDENTIAL",
                $"ApiCredential '{serviceKey}' ({environment}) is missing the client id and/or client secret.");

        var baseUrl = (cred.BaseUrl ?? DefaultBaseUrl(version, environment)).TrimEnd('/');

        // v2 emite el token bajo la misma base de la API; v1 usa un host de identidad
        // aparte, que convive con la base de API en el mismo dominio salvo el subdominio.
        var tokenUrl = version == "V2"
            ? $"{baseUrl}/auth/connect/token"
            : $"{baseUrl.Replace("platform.", "identity.")}/core/connect/token";

        var scopes = version == "V2"
            ? "api readonly modify"
            : "api useraccount_balance useraccount_debit useraccount_payment useraccount_invitation";

        try
        {
            return Result<PayQuickerSettings>.Success(new PayQuickerSettings
            {
                ApiVersion          = version,
                Environment         = environment,
                BaseUrl             = baseUrl,
                ClientId            = _crypto.Decrypt(cred.ApiKeyEncrypted!),
                ClientSecret        = _crypto.Decrypt(cred.SecretKeyEncrypted!),
                FundingAccountToken = NormalizeFundingAccount(Decrypt(cred.MerchantIdEncrypted), version),
                ProgramToken        = Decrypt(cred.AdditionalSecretEncrypted),
                TokenUrl            = tokenUrl,
                Scopes              = scopes
            });
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            // Pasa cuando el valor guardado lleva el prefijo ENC: pero no es un payload de
            // Data Protection válido — por ejemplo si se cargó como texto plano prefijado, o
            // si lo cifró un host con OTRO key ring. Se devuelve un error accionable en vez
            // de dejar escapar una excepción de criptografía desde un job de payout.
            return Result<PayQuickerSettings>.Failure(
                "PAYQUICKER_CREDENTIAL_UNDECRYPTABLE",
                $"The stored PayQuicker {version} credential for '{environment}' could not be decrypted. " +
                "Re-enter it in Admin → Billing → API Credentials. If it was saved by another service, " +
                "confirm both hosts share the Data Protection key ring.");
        }
    }

    /// <summary>
    /// La MISMA cuenta de fondeo se escribe distinto en cada version de la API:
    ///
    ///   v1  d2ffd5ee9d3945d7936a9648e706ccc6           (GUID pelado, sin guiones)
    ///   v2  acct-d2ffd5ee-9d39-45d7-936a-9648e706ccc6  (prefijo acct- y con guiones)
    ///
    /// Cargar la forma equivocada no da un error entendible: el GET de invitaciones no valida
    /// el formato y simplemente no encuentra nada, y el POST responde 500 con "An error
    /// occurred while processing your request". Se normaliza a la forma que espera la version
    /// seleccionada en vez de hacer que alguien pierda una tarde con ese 500.
    /// </summary>
    public static string? NormalizeFundingAccount(string? token, string version)
    {
        if (string.IsNullOrWhiteSpace(token)) return token;

        var trimmed = token.Trim();
        var bare    = trimmed.StartsWith("acct-", StringComparison.OrdinalIgnoreCase)
            ? trimmed["acct-".Length..]
            : trimmed;

        // Si no es un GUID reconocible se devuelve tal cual: puede ser un identificador con
        // otra forma y no corresponde adivinar.
        if (!Guid.TryParse(bare, out var parsed)) return trimmed;

        return version == "V2"
            ? $"acct-{parsed:D}"   // con guiones
            : parsed.ToString("N"); // pelado
    }

    private string? Decrypt(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : _crypto.Decrypt(value);

    private static string DefaultBaseUrl(string version, string environment)
    {
        var isProd = environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
        return version switch
        {
            "V2" => isProd ? "https://api.payquicker.io/api/v2"
                           : "https://api.sandbox.payquicker.io/api/v2",
            _    => isProd ? "https://platform.mypayquicker.com"
                           : "https://platform.mypayquicker.build"
        };
    }
}
