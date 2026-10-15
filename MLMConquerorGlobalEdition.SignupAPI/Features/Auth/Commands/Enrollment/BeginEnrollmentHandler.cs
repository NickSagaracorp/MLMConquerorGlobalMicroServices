using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;

/// <summary>
/// Abre el enrolamiento TOTP: devuelve la clave compartida, el URI <c>otpauth://</c> y su QR.
/// El 2FA no queda activo aquí — eso lo hace <see cref="ConfirmEnrollmentHandler"/> cuando el
/// usuario demuestra que su aplicación quedó sincronizada.
/// </summary>
public class BeginEnrollmentHandler : IRequestHandler<BeginEnrollmentCommand, Result<EnrollmentResponse>>
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly IChallengeTokenService         _challenges;
    private readonly ITotpEnrollmentService         _enrollment;
    private readonly ISecurityFactorNotifier        _notifier;

    public BeginEnrollmentHandler(
        UserManager<ApplicationUser> userManager,
        IChallengeTokenService       challenges,
        ITotpEnrollmentService       enrollment,
        ISecurityFactorNotifier      notifier)
    {
        _userManager = userManager;
        _challenges  = challenges;
        _enrollment  = enrollment;
        _notifier    = notifier;
    }

    public async Task<Result<EnrollmentResponse>> Handle(BeginEnrollmentCommand command, CancellationToken ct)
    {
        // El token de enrolamiento es toda la credencial de este endpoint, así que el propósito
        // se comprueba de verdad: un challenge de login no sirve para enrolarse. Sin esa
        // separación, el código que abre una sesión abriría también la configuración del
        // segundo factor, que es exactamente lo que ese factor tiene que proteger.
        var validation = _challenges.Validate(
            command.Request.EnrollmentToken, TwoFactorPurpose.Enrollment);

        if (!validation.IsSuccess)
            return Result<EnrollmentResponse>.Failure(validation.ErrorCode!, validation.Error!);

        var user = await _userManager.FindByIdAsync(validation.Value!.UserId);
        if (user is null || !user.IsActive)
            return Result<EnrollmentResponse>.Failure("INVALID_CREDENTIALS", "Account is no longer active.");

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
