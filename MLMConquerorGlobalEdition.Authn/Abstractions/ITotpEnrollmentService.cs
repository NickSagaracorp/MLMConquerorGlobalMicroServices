using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Authn.Abstractions;

/// <summary>
/// Enrolamiento TOTP sobre una aplicación de autenticación (Google Authenticator, Authy,
/// 1Password…). El TOTP en sí no se implementa aquí: lo trae ASP.NET Identity a través de
/// <c>AddDefaultTokenProviders</c>, que registra <c>AuthenticatorTokenProvider</c>. Esta
/// clase solo orquesta ese proveedor y genera el QR en proceso con QRCoder.
/// </summary>
public interface ITotpEnrollmentService
{
    /// <summary>
    /// Genera (o regenera) la clave del autenticador para el usuario y devuelve el URI
    /// <c>otpauth://</c> junto con su QR, listos para que el usuario escanee con su
    /// aplicación. El 2FA no queda activo todavía: eso lo hace <see cref="ConfirmAsync"/>
    /// una vez que el usuario demuestra que la clave quedó bien sincronizada.
    /// </summary>
    Task<Result<TotpEnrollment>> BeginAsync(ApplicationUser user, CancellationToken ct = default);

    /// <summary>
    /// Verifica el primer código que produce la aplicación del usuario. Si es válido, activa
    /// el 2FA y fija el canal preferido a Authenticator; si no, no toca nada.
    /// </summary>
    /// <remarks>
    /// <see cref="SharedKernel"/> no tiene un <c>Result</c> sin genérico; se usa
    /// <see cref="Result{T}"/> de <c>bool</c> igual que en el resto de la librería.
    /// </remarks>
    Task<Result<bool>> ConfirmAsync(ApplicationUser user, string code, CancellationToken ct = default);

    /// <summary>Desactiva el 2FA y borra la clave del autenticador y la marca de enrolamiento.</summary>
    Task<Result<bool>> ResetAsync(ApplicationUser user, CancellationToken ct = default);
}
