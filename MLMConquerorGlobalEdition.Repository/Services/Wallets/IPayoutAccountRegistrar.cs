using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Wallets;

/// <summary>
/// Da de alta la cuenta del miembro en el proveedor de payout cuando registra o cambia su
/// método de cobro.
///
/// Existe como abstracción angosta a propósito. Las implementaciones de gateway viven en el
/// proyecto Billing, que referencia a Repository — no al revés. Sin este contrato,
/// MemberWalletService no podría llamarlas sin invertir la dependencia.
///
/// También mantiene chico el radio de impacto: BizCenter necesita dar de alta cuentas pero
/// no necesita el orquestador de payouts, los recibos ni la exportación de lotes. Registra
/// sólo AddPayoutGatewayClients y con eso alcanza.
/// </summary>
public interface IPayoutAccountRegistrar
{
    Task<PayoutAccountRegistrationResult> RegisterAsync(
        PayoutAccountRegistrationRequest request, CancellationToken ct = default);
}

public sealed class PayoutAccountRegistrationRequest
{
    public required string     MemberId          { get; init; }
    public required WalletType WalletType        { get; init; }
    public required string     AccountIdentifier { get; init; }

    /// <summary>
    /// Email de contacto del miembro. PayQuicker lo necesita para direccionar
    /// (programUserId + email) e i-Payout para crear la cuenta.
    /// </summary>
    public string? Email     { get; init; }
    public string? FirstName { get; init; }
    public string? LastName  { get; init; }
}

public sealed class PayoutAccountRegistrationResult
{
    /// <summary>Si el proveedor aceptó el alta (o si no hacía falta ninguna).</summary>
    public required bool Success { get; init; }

    /// <summary>
    /// True cuando el gateway no requiere alta — hoy sólo Crypto: el usuario provee una
    /// dirección de wallet y no hay cuenta que crear del lado del proveedor.
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>
    /// Identificador que ASIGNÓ el proveedor, si difiere del que mandó el usuario.
    /// i-Payout devuelve su propio UserName; PayQuicker devuelve la clave de invitación.
    /// Cuando viene, es el que hay que persistir en la wallet.
    /// </summary>
    public string? AssignedAccountIdentifier { get; init; }

    public string? GatewayCode    { get; init; }
    public string? GatewayMessage { get; init; }

    // Para el rastro de auditoría en MemberWalletApiLog.
    public string Endpoint     { get; init; } = string.Empty;
    public string RequestBody  { get; init; } = string.Empty;
    public string ResponseBody { get; init; } = string.Empty;
    public long   DurationMs   { get; init; }
}
