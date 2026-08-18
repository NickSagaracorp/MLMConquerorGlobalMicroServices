using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

/// <summary>
/// Operaciones de PayQuicker que necesita el payout gateway, expresadas SIN atarse a una
/// versión de API. Hay una implementación por versión (V1 / V2) y
/// <see cref="PayQuickerClientFactory"/> elige cuál según el selector del admin.
///
/// El contrato está redactado en términos del dominio (programUserId + email) porque
/// nuestro programa es "hosted portal": el destinatario se identifica así y no por un
/// token de cuenta. v2 lo soporta nativamente; v1 usa su userCompanyAssignedUniqueKey,
/// que es el mismo dato con otro nombre.
/// </summary>
public interface IPayQuickerClient
{
    /// <summary>"V1" | "V2".</summary>
    string Version { get; }

    /// <summary>Da de alta al usuario en el programa (invitación). Idempotente por programUserId.</summary>
    Task<Result<PayQuickerAccountResult>> CreateInvitationAsync(
        PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default);

    /// <summary>Confirma que el usuario existe y en qué estado de registro está.</summary>
    Task<Result<PayQuickerAccountResult>> GetAccountAsync(
        PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default);

    /// <summary>Saldo disponible del usuario en la moneda pedida.</summary>
    Task<Result<decimal>> GetBalanceAsync(
        string programUserId, string currency, PayQuickerSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Envía un pago. <paramref name="request"/>.ClientPaymentRef es la clave de
    /// idempotencia: PayQuicker rechaza un ref repetido, que es justo lo que queremos si
    /// el orquestador reintenta.
    /// </summary>
    Task<Result<PayQuickerTransferResult>> SendPaymentAsync(
        PayQuickerPaymentRequest request, PayQuickerSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Estado autoritativo de un pago, buscado por su ClientPaymentRef. Es lo que usa el
    /// sweep de reconciliación para responder "¿la plata salió?" tras un crash.
    /// </summary>
    Task<Result<PayQuickerTransferStatus>> GetTransferStatusAsync(
        string clientPaymentRef, PayQuickerSettings settings, CancellationToken ct = default);
}

public sealed class PayQuickerAccountRequest
{
    public required string ProgramUserId { get; init; }
    public required string Email         { get; init; }
    public string? FirstName { get; init; }
    public string? LastName  { get; init; }
    public string  Language  { get; init; } = "en-US";

    /// <summary>
    /// Si PayQuicker debe mandarle el mail de bienvenida al usuario.
    /// OJO: la doc de v2 describe este flag como "held for notification until a subsequent
    /// call releases the invitation", lectura que sugiere lo CONTRARIO al nombre. Está
    /// pendiente de confirmar contra sandbox antes de habilitar el gateway en producción.
    /// </summary>
    public bool NotifyUser { get; init; } = true;

    public bool IssueCard { get; init; }
}

public sealed class PayQuickerAccountResult
{
    public bool Exists { get; init; }

    /// <summary>
    /// Valor a persistir como AccountIdentifier. En v2 es el <c>key</c> de la invitación
    /// (el que viaja en la URL de bienvenida), NO el <c>token</c> invt-…; en v1 es el
    /// invitationKey. Se eligió el mismo campo semántico en ambas para que un cambio de
    /// versión no invalide lo ya guardado.
    /// </summary>
    public string? InvitationKey { get; init; }

    public string? Status         { get; init; }
    public string? GatewayCode    { get; init; }
    public string? GatewayMessage { get; init; }
}

public sealed class PayQuickerPaymentRequest
{
    public required string  ProgramUserId   { get; init; }
    public required string  Email           { get; init; }
    public required decimal AmountUsd       { get; init; }

    /// <summary>Máximo 50 caracteres — límite del contrato de PayQuicker.</summary>
    public required string  ClientPaymentRef { get; init; }

    public string  Purpose        { get; init; } = "BONUS";
    public string  AcceptanceMode { get; init; } = "AUTO_ACCEPT";
    public string? Memo           { get; init; }
    public string? Note           { get; init; }
}

public sealed class PayQuickerTransferResult
{
    public required string GatewayTransactionId { get; init; }
    public string? GatewayCode    { get; init; }
    public string? GatewayMessage { get; init; }
}

public sealed class PayQuickerTransferStatus
{
    public PayoutTransferState State { get; init; }
    public string? GatewayTransactionId { get; init; }
    public string? GatewayCode    { get; init; }
    public string? GatewayMessage { get; init; }
}
