using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;

/// <summary>
/// Códigos de error tal y como los lee el diálogo de step-up. Parte de
/// <see cref="TwoFactorMessages"/> y solo cambia lo que ahí está redactado para otro momento.
/// </summary>
/// <remarks>
/// POR QUÉ NO SIRVE EL TEXTO COMPARTIDO. <c>TwoFactor.Error.InvalidChallenge</c> termina en "vuelve
/// a iniciar sesión", la salida correcta durante un login. Dentro del diálogo la persona YA tiene
/// sesión y la salida es otra: pedir un código nuevo con el botón de reenvío. Mandarla a cerrar
/// sesión por un código gastado sería un callejón sin salida. Mismo patrón que
/// <see cref="AccountMessages"/>, que también redacta aparte lo que no le cuadra a su pantalla.
///
/// CODE_EXPIRED y TOO_MANY_ATTEMPTS comparten texto con INVALID_CHALLENGE porque, para quien está
/// delante del diálogo, los tres significan lo mismo y piden lo mismo: este código ya no sirve,
/// pide otro.
/// </remarks>
public static class StepUpMessages
{
    public static string ErrorKeyFor(string? errorCode) =>
        errorCode?.Trim().ToUpperInvariant() switch
        {
            "INVALID_CHALLENGE" or "CODE_EXPIRED" or "TOO_MANY_ATTEMPTS" => "StepUp.ChallengeInvalid",
            StepUpGrant.ImpersonationNotAllowedErrorCode => "StepUp.ImpersonationNotAllowed",
            _ => TwoFactorMessages.ErrorKeyFor(errorCode)
        };
}
