namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

/// <summary>
/// Configuración de PayQuicker ya resuelta y descifrada para una llamada.
///
/// Sale de combinar dos filas:
///   PaymentGatewayInfo(WalletType.PayQuicker) → ApiVersion + Environment (los selectores del admin)
///   ApiCredential(ServiceKey = "PayQuicker{ApiVersion}", Environment)  → URLs y secretos
///
/// Nunca se cachea: los secretos se descifran por llamada y se descartan.
/// </summary>
public sealed class PayQuickerSettings
{
    /// <summary>"V1" | "V2".</summary>
    public required string ApiVersion { get; init; }

    /// <summary>"Sandbox" | "Production" | "Test".</summary>
    public required string Environment { get; init; }

    /// <summary>Base de la API REST, sin barra final.</summary>
    public required string BaseUrl { get; init; }

    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Cuenta de fondeo de la compañía (acct-… en v2, fundingAccountPublicId en v1).
    /// Es el origen del dinero en cada pago.
    /// </summary>
    public string? FundingAccountToken { get; init; }

    /// <summary>Token del programa (prog-…). Sólo v2, obligatorio para crear invitaciones.</summary>
    public string? ProgramToken { get; init; }

    /// <summary>
    /// URL del endpoint de token OAuth2. Difiere por versión:
    ///   v1 → https://identity.…/core/connect/token   (host de identidad propio)
    ///   v2 → {BaseUrl}/auth/connect/token
    /// </summary>
    public required string TokenUrl { get; init; }

    /// <summary>
    /// Scopes del client-credentials. También difieren: los scopes de v1 (useraccount_*)
    /// devuelven invalid_scope con credenciales v2.
    /// </summary>
    public required string Scopes { get; init; }

    /// <summary>
    /// Clave de caché del token de acceso. Incluye versión y ambiente para que un token
    /// de sandbox nunca se reuse contra producción.
    /// </summary>
    public string TokenCacheKey => $"payquicker:{ApiVersion}:{Environment}:{ClientId}";
}
