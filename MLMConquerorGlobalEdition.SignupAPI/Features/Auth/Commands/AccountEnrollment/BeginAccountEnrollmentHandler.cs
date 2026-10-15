using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;

/// <summary>
/// Abre el enrolamiento TOTP desde una sesión ya iniciada: devuelve la clave compartida, el URI
/// <c>otpauth://</c> y su QR. El 2FA no queda activo aquí — eso lo hace
/// <see cref="ConfirmAccountEnrollmentHandler"/> con el primer código.
/// </summary>
/// <remarks>
/// Es el hermano autenticado de <c>BeginEnrollmentHandler</c>. Aquel exige un
/// <c>EnrollmentToken</c>, que solo emite el login cuando el rol obliga a enrolarse; un usuario
/// que ya entró no tiene ninguno, así que por aquel camino no podía activar ni volver a enrolar
/// su autenticador. Se separan en dos rutas en vez de hacer uno híbrido: un endpoint que acepta
/// token de enrolamiento <i>o</i> sesión tiene dos modelos de autenticación conviviendo, y quien
/// lo lea después tiene que sostener los dos a la vez para saber quién puede llegar hasta ahí.
///
/// <see cref="ITotpEnrollmentService.BeginAsync"/> se reutiliza tal cual y ya hace lo que hace
/// falta: es idempotente mientras el enrolamiento sigue abierto —dos llamadas seguidas devuelven
/// la misma clave, así que recargar la pantalla no invalida el QR que el usuario acaba de
/// escanear— y genera una clave nueva cuando ya había un autenticador enrolado, que es
/// exactamente lo que tiene que pasar al re-enrolar.
/// </remarks>
public class BeginAccountEnrollmentHandler
    : IRequestHandler<BeginAccountEnrollmentCommand, Result<EnrollmentResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITotpEnrollmentService       _enrollment;
    private readonly ISecurityFactorNotifier      _notifier;

    public BeginAccountEnrollmentHandler(
        UserManager<ApplicationUser> userManager,
        ITotpEnrollmentService       enrollment,
        ISecurityFactorNotifier      notifier)
    {
        _userManager = userManager;
        _enrollment  = enrollment;
        _notifier    = notifier;
    }

    public async Task<Result<EnrollmentResponse>> Handle(
        BeginAccountEnrollmentCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<EnrollmentResponse>.Failure("USER_NOT_FOUND", "User not found.");

        // Sobre un autenticador ya enrolado, BeginAsync no abre un alta: REEMPLAZA la clave, y el
        // autenticador que el dueño lleva en el móvil deja de valer en ese momento. Es un cambio de
        // canal de sus códigos tan real como cambiar el teléfono, así que se le avisa igual —si no
        // fue él, es lo único que le dice que alguien con su sesión está reapuntando su 2FA—. Se
        // mira ANTES de llamar, y se avisa aunque BeginAsync devuelva fallo: la clave vieja se
        // resetea antes de que nada pueda fallar. En un alta primera no hay nada que se pierda y
        // no se avisa. El aviso vive aquí y no en Authn para no darle a Authn una dependencia más.
        var reenrolling = user.TwoFactorEnrolledAt is not null;

        var begun = await _enrollment.BeginAsync(user, ct);

        if (reenrolling)
            await _notifier.NotifyAsync(user, SecurityFactorChange.Authenticator, ct: ct);

        if (!begun.IsSuccess)
            return Result<EnrollmentResponse>.Failure(begun.ErrorCode!, begun.Error!);

        return Result<EnrollmentResponse>.Success(new EnrollmentResponse
        {
            SharedKey        = begun.Value!.SharedKey,
            AuthenticatorUri = begun.Value.AuthenticatorUri,
            QrCodePngDataUri = begun.Value.QrCodePngDataUri
        });
    }
}
