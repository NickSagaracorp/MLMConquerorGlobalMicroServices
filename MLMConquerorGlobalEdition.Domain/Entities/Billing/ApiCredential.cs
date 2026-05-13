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

    public bool IsActive { get; set; } = true;
}
