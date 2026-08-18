using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.Volet;

public interface IVoletClient
{
    Task<Result<VoletAccountStatus>> ValidateAccountAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Envía dinero. Si la cuenta existe usa sendMoney (intrasistema); si no existe usa
    /// sendMoneyToEmail, que le manda al destinatario un aviso para reclamar los fondos.
    /// Devuelve el id de transacción de Volet.
    /// </summary>
    Task<Result<string>> SendMoneyAsync(
        string email, decimal amount, string currency, string note, CancellationToken ct = default);
}

public sealed record VoletAccountStatus(bool Present, bool IsUserVerified);

/// <summary>
/// Cliente de Volet (ex AdvCash). A diferencia de PayQuicker e i-Payout, Volet NO es REST:
/// es un web service SOAP 1.1. El envelope se arma a mano sobre HttpClient en vez de generar
/// un proxy WCF, para no sumar dependencias ni un paso de scaffolding al build.
///
/// Autenticación: cada llamada lleva un authDTO con apiName, accountEmail y un
/// authenticationToken que es el SHA-256 (hex en MAYÚSCULAS) de una plantilla donde
/// "##datetime##" se reemplaza por la hora UTC en formato yyyyMMdd:HH. O sea, el token ROTA
/// SOLO cada hora; no hay que renovarlo ni cachearlo.
///
/// Contrato tomado del WSDL que usa MWRLife (MerchantWebService.wsdl):
///   targetNamespace      http://wsm.advcash/
///   elementFormDefault   unqualified  → sólo el wrapper de la operación lleva namespace
///   endpoint             https://wallet.advcash.com/wsm/merchantWebService
/// </summary>
public class VoletClient : IVoletClient
{
    public const string HttpClientName = "volet";
    private const string CredentialServiceKey = "Volet";

    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string WsmNs  = "http://wsm.advcash/";

    /// <summary>Marcador que se reemplaza por la hora UTC al calcular el token.</summary>
    private const string DateTimePlaceholder = "##datetime##";

    private readonly IHttpClientFactory   _httpFactory;
    private readonly AppDbContext         _db;
    private readonly IEncryptionService   _crypto;
    private readonly ILogger<VoletClient> _logger;

    public VoletClient(
        IHttpClientFactory httpFactory,
        AppDbContext db,
        IEncryptionService crypto,
        ILogger<VoletClient> logger)
    {
        _httpFactory = httpFactory;
        _db          = db;
        _crypto      = crypto;
        _logger      = logger;
    }

    public async Task<Result<VoletAccountStatus>> ValidateAccountAsync(string email, CancellationToken ct = default)
    {
        var cfg = await ResolveAsync(ct);
        if (!cfg.IsSuccess) return Result<VoletAccountStatus>.Failure(cfg.ErrorCode!, cfg.Error!);

        var body = new XElement(XName.Get("validateAccounts", WsmNs),
            AuthElement(cfg.Value!),
            new XElement("arg1", email));

        var response = await CallAsync(cfg.Value!, body, "validate account", ct);
        if (!response.IsSuccess) return Result<VoletAccountStatus>.Failure(response.ErrorCode!, response.Error!);

        // validateAccountsResponse devuelve un accountPresentDTO por cuenta consultada.
        var first = response.Value!.Descendants("return").FirstOrDefault();
        if (first is null)
            return Result<VoletAccountStatus>.Failure(
                "VOLET_EMPTY_RESPONSE",
                "Volet returned no account result for the address queried.");

        return Result<VoletAccountStatus>.Success(new VoletAccountStatus(
            Present:        ReadBool(first, "present"),
            IsUserVerified: ReadBool(first, "isUserVerified")));
    }

    public async Task<Result<string>> SendMoneyAsync(
        string email, decimal amount, string currency, string note, CancellationToken ct = default)
    {
        var cfg = await ResolveAsync(ct);
        if (!cfg.IsSuccess) return Result<string>.Failure(cfg.ErrorCode!, cfg.Error!);

        // Volet tiene dos formas de pagar y la correcta depende de si la cuenta ya existe:
        //   sendMoney         → transferencia intrasistema, el dinero llega directo.
        //   sendMoneyToEmail  → el destinatario recibe un aviso y reclama los fondos, con lo
        //                       que abre su cuenta en el proceso.
        // Mandar el intrasistema a alguien sin cuenta falla, así que primero se pregunta.
        var status = await ValidateAccountAsync(email, ct);
        if (!status.IsSuccess) return Result<string>.Failure(status.ErrorCode!, status.Error!);

        var operation = status.Value!.Present ? "sendMoney" : "sendMoneyToEmail";

        // El orden de los elementos importa: sendMoneyRequest extiende moneyRequest, así que
        // primero van los campos de la base y después los de la extensión.
        var body = new XElement(XName.Get(operation, WsmNs),
            AuthElement(cfg.Value!),
            new XElement("arg1",
                new XElement("amount",   amount.ToString("0.00", CultureInfo.InvariantCulture)),
                new XElement("currency", currency),
                new XElement("note",     note),
                new XElement("savePaymentTemplate", "false"),
                new XElement("email",    email)));

        var response = await CallAsync(cfg.Value!, body, operation, ct);
        if (!response.IsSuccess) return Result<string>.Failure(response.ErrorCode!, response.Error!);

        var transactionId = response.Value!.Descendants("return").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(transactionId))
            return Result<string>.Failure(
                "VOLET_NO_TRANSACTION_ID",
                $"Volet accepted the {operation} call but returned no transaction id.");

        _logger.LogInformation("Volet {Operation} to {Email} → {TransactionId}", operation, email, transactionId);
        return Result<string>.Success(transactionId!);
    }

    // ── plomería ────────────────────────────────────────────────────────────

    private sealed record VoletConfig(string Endpoint, string ApiName, string AccountEmail, string TokenTemplate);

    private async Task<Result<VoletConfig>> ResolveAsync(CancellationToken ct)
    {
        var gateway = await _db.PaymentGateways.AsNoTracking()
            .FirstOrDefaultAsync(g => g.WalletType == WalletType.Volet, ct);

        var environment = string.IsNullOrWhiteSpace(gateway?.Environment) ? "Production" : gateway!.Environment!.Trim();

        var cred = await _db.ApiCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == CredentialServiceKey && c.Environment == environment, ct);

        if (cred is null)
            return Result<VoletConfig>.Failure(
                "VOLET_NO_CREDENTIAL",
                $"No ApiCredential found for ServiceKey 'Volet' in environment '{environment}'.");

        if (!cred.IsActive)
            return Result<VoletConfig>.Failure(
                "VOLET_CREDENTIAL_INACTIVE",
                $"ApiCredential 'Volet' ({environment}) is marked inactive.");

        // ApiKeyEncrypted           → apiName
        // SecretKeyEncrypted        → plantilla del token (contiene ##datetime##)
        // MerchantIdEncrypted       → accountEmail de la cuenta merchant
        if (string.IsNullOrWhiteSpace(cred.ApiKeyEncrypted)
            || string.IsNullOrWhiteSpace(cred.SecretKeyEncrypted)
            || string.IsNullOrWhiteSpace(cred.MerchantIdEncrypted))
            return Result<VoletConfig>.Failure(
                "VOLET_INCOMPLETE_CREDENTIAL",
                $"ApiCredential 'Volet' ({environment}) needs the API name, the auth-token template and the merchant account email.");

        try
        {
            return Result<VoletConfig>.Success(new VoletConfig(
                cred.BaseUrl ?? "https://wallet.advcash.com/wsm/merchantWebService",
                _crypto.Decrypt(cred.ApiKeyEncrypted!),
                _crypto.Decrypt(cred.MerchantIdEncrypted!),
                _crypto.Decrypt(cred.SecretKeyEncrypted!)));
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            return Result<VoletConfig>.Failure(
                "VOLET_CREDENTIAL_UNDECRYPTABLE",
                $"The stored Volet credential for '{environment}' could not be decrypted. " +
                "Re-enter it in Admin → Billing → API Credentials.");
        }
    }

    private XElement AuthElement(VoletConfig cfg) =>
        new("arg0",
            new XElement("accountEmail",        cfg.AccountEmail),
            new XElement("apiName",             cfg.ApiName),
            new XElement("authenticationToken", CurrentAuthToken(cfg.TokenTemplate)));

    /// <summary>
    /// SHA-256 en hex MAYÚSCULA de la plantilla con la hora UTC sustituida. Rota sola cada
    /// hora, así que no hay token que cachear ni renovar.
    /// </summary>
    public static string CurrentAuthToken(string template, DateTime? utcNow = null)
    {
        var stamp = (utcNow ?? DateTime.UtcNow).ToString("yyyyMMdd:HH", CultureInfo.InvariantCulture);
        var seed  = template.Replace(DateTimePlaceholder, stamp, StringComparison.Ordinal);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
    }

    private async Task<Result<XElement>> CallAsync(
        VoletConfig cfg, XElement body, string operation, CancellationToken ct)
    {
        var envelope = new XDocument(
            new XElement(XName.Get("Envelope", SoapNs),
                new XAttribute(XNamespace.Xmlns + "soap", SoapNs),
                new XElement(XName.Get("Body", SoapNs), body)));

        var client = _httpFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, cfg.Endpoint)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting),
                                        Encoding.UTF8, "text/xml")
        };
        // SOAP 1.1 exige la cabecera SOAPAction; el WSDL la declara vacía.
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        using var response = await client.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("Volet {Operation} → {Status}", operation, (int)response.StatusCode);

        XDocument parsed;
        try
        {
            parsed = XDocument.Parse(raw);
        }
        catch (System.Xml.XmlException ex)
        {
            return Result<XElement>.Failure(
                "VOLET_MALFORMED_RESPONSE",
                $"Volet {operation} returned unparseable XML ({ex.Message}): {Truncate(raw)}");
        }

        // Un soap:Fault llega con HTTP 500, así que se inspecciona ANTES del status code:
        // el faultstring dice el motivo real (NotEnoughMoney, WrongIp, LimitPerTransaction…).
        var fault = parsed.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
        {
            var reason = fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text")?.Value
                         ?? "unspecified SOAP fault";
            return Result<XElement>.Failure($"VOLET_FAULT", $"Volet rejected the {operation} call: {reason}");
        }

        if (!response.IsSuccessStatusCode)
            return Result<XElement>.Failure(
                $"VOLET_HTTP_{(int)response.StatusCode}",
                $"Volet {operation} failed ({(int)response.StatusCode}): {Truncate(raw)}");

        var payload = parsed.Descendants().FirstOrDefault(e => e.Name.LocalName.EndsWith("Response", StringComparison.Ordinal));
        return payload is null
            ? Result<XElement>.Failure("VOLET_EMPTY_RESPONSE", $"Volet {operation} returned no response body: {Truncate(raw)}")
            : Result<XElement>.Success(payload);
    }

    private static bool ReadBool(XElement parent, string name) =>
        bool.TryParse(parent.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value, out var v) && v;

    private static string Truncate(string s, int max = 500) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");
}
