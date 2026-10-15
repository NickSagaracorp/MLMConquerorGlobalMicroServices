using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// Contrato del permiso de step-up: lo que SignupAPI firma al verificar el código y lo que los
/// demás servicios aceptan en <see cref="HeaderName"/>. Vive aquí para que quien firma y quien
/// valida no puedan divergir.
/// </summary>
/// <remarks>
/// <b>Audiencia propia</b>, por la misma razón que <see cref="ChallengeAudience"/>: los anfitriones
/// validan <c>Jwt:Audience</c>, así que un permiso presentado como token de acceso muere en la
/// validación estándar sin que nadie tenga que acordarse de nada. Lleva además el claim de
/// propósito, que es el segundo cinturón que ya rechazan todos los anfitriones.
/// </remarks>
public static class StepUpGrant
{
    public const string HeaderName        = "X-Step-Up-Token";
    public const string AudienceSuffix    = ".stepup";
    public const string OperationClaim    = "operation_key";
    public const string PurposePrefix     = "step_up_grant:";
    public const string RequiredErrorCode = "STEP_UP_REQUIRED";
    public const string InvalidErrorCode  = "STEP_UP_INVALID";

    /// <summary>
    /// SignupAPI no emite ni canjea un step-up mientras dure la espera tras un cambio de factor
    /// (correo, teléfono, canal o autenticador). Va aquí y no en SignupAPI porque la UI que lo
    /// traduce no referencia SignupAPI.
    /// </summary>
    public const string CooldownErrorCode = "STEP_UP_COOLDOWN";

    /// <summary>
    /// Corta a propósito: el permiso se pide justo antes de reintentar la acción, y dentro de su
    /// vida no se puede revocar. Un solo uso lo garantiza el validador, no la vida.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    public static string AudienceFor(string accessAudience) => accessAudience + AudienceSuffix;

    public static string PurposeFor(string operationKey) => PurposePrefix + operationKey;
}
