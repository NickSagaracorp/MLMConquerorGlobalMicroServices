using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Security;

/// <summary>
/// Bitácora de eventos de seguridad de autenticación: 2FA de login, enrolamiento, alta de
/// teléfono, cambios de contraseña, resets por administrador, step-up y cambios de política.
/// Un solo lugar donde mirar cuando hay que reconstruir qué pasó con una cuenta.
/// </summary>
public class AuthSecurityEvent : AuditChangesLongKey
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>Desnormalizado a propósito: sobrevive a la baja de la cuenta.</summary>
    public string UserEmail { get; set; } = string.Empty;

    public AuthEventType EventType { get; set; }
    public AuthEventOutcome Outcome { get; set; }

    /// <summary>Null en eventos que no son de step-up.</summary>
    public string? OperationKey { get; set; }

    public TwoFactorChannel? Channel { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestPath { get; set; }

    /// <summary>Identificador del challenge, para correlacionar emisión con verificación.</summary>
    public string? ChallengeJti { get; set; }
}
