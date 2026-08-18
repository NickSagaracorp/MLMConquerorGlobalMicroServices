using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Stores gateway / third-party API credentials. Secrets are ALWAYS stored encrypted
/// (prefix "ENC:"). Any attempt to store a plain-text secret throws WalletPasswordStorageException.
/// </summary>
public class ApiCredential : AuditChangesStringKey
{
    /// <summary>
    /// Logical name of the service. E.g. "NmiSpreedly", "CheckoutEUR", "CurrencyConverterApi".
    /// </summary>
    public string ServiceKey    { get; set; } = string.Empty;

    /// <summary>"Production" | "Sandbox"</summary>
    public string Environment   { get; set; } = "Production";

    public string? BaseUrl      { get; set; }

    // ── Encrypted secret fields ────────────────────────────────────────────

    private string? _apiKeyEncrypted;
    public string? ApiKeyEncrypted
    {
        get => _apiKeyEncrypted;
        set
        {
            if (value is not null && !value.StartsWith("ENC:"))
                throw new WalletPasswordStorageException();
            _apiKeyEncrypted = value;
        }
    }

    private string? _secretKeyEncrypted;
    public string? SecretKeyEncrypted
    {
        get => _secretKeyEncrypted;
        set
        {
            if (value is not null && !value.StartsWith("ENC:"))
                throw new WalletPasswordStorageException();
            _secretKeyEncrypted = value;
        }
    }

    private string? _merchantIdEncrypted;
    public string? MerchantIdEncrypted
    {
        get => _merchantIdEncrypted;
        set
        {
            if (value is not null && !value.StartsWith("ENC:"))
                throw new WalletPasswordStorageException();
            _merchantIdEncrypted = value;
        }
    }

    private string? _additionalSecretEncrypted;
    public string? AdditionalSecretEncrypted
    {
        get => _additionalSecretEncrypted;
        set
        {
            if (value is not null && !value.StartsWith("ENC:"))
                throw new WalletPasswordStorageException();
            _additionalSecretEncrypted = value;
        }
    }

    /// <summary>
    /// The Spreedly downstream-gateway-token for this processor.
    /// When Spreedly is provisioned with a downstream gateway (e.g. NMI, Checkout.com),
    /// Spreedly assigns a gateway token that must be passed on every charge request so
    /// Spreedly knows which downstream processor to route to.
    /// Stored encrypted (ENC:...) like all secrets. Set/rotated via the admin credentials UI.
    /// </summary>
    private string? _spreedlyGatewayTokenEncrypted;
    public string? SpreedlyGatewayTokenEncrypted
    {
        get => _spreedlyGatewayTokenEncrypted;
        set
        {
            if (value is not null && !value.StartsWith("ENC:"))
                throw new WalletPasswordStorageException();
            _spreedlyGatewayTokenEncrypted = value;
        }
    }

    // ── Portal administrativo del proveedor ────────────────────────────────
    // Distinto de BaseUrl: BaseUrl es el endpoint de la API que consume el sistema;
    // esto es el sitio web donde una PERSONA entra a revisar cuentas y transacciones.
    // Va por ambiente porque el portal de sandbox y el de producción son distintos.

    /// <summary>URL del portal administrativo del proveedor (no es la API).</summary>
    public string? PortalUrl { get; set; }

    private string? _portalUsernameEncrypted;
    /// <summary>
    /// Usuario del portal. Cifrado igual que los secretos: aunque un usuario no sea
    /// tan sensible como una contraseña, filtrarlo entrega la mitad de la credencial.
    /// </summary>
    public string? PortalUsernameEncrypted
    {
        get => _portalUsernameEncrypted;
        set
        {
            if (value is not null && !value.StartsWith("ENC:"))
                throw new WalletPasswordStorageException();
            _portalUsernameEncrypted = value;
        }
    }

    private string? _portalPasswordEncrypted;
    /// <summary>
    /// Contraseña del portal. OJO: es una credencial HUMANA compartida — no deja rastro
    /// de quién entró y rotarla obliga a avisar a todo el equipo. Si el proveedor ofrece
    /// cuentas individuales o SSO, es preferible dejar esto nulo y usar aquello.
    /// </summary>
    public string? PortalPasswordEncrypted
    {
        get => _portalPasswordEncrypted;
        set
        {
            if (value is not null && !value.StartsWith("ENC:"))
                throw new WalletPasswordStorageException();
            _portalPasswordEncrypted = value;
        }
    }

    public bool IsActive { get; set; } = true;
}
