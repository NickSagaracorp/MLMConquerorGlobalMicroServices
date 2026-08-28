using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
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

    public BeginEnrollmentHandler(
        UserManager<ApplicationUser> userManager,
        IChallengeTokenService       challenges,
        ITotpEnrollmentService       enrollment)
    {
        _userManager = userManager;
        _challenges  = challenges;
        _enrollment  = enrollment;
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

        var begun = await _enrollment.BeginAsync(user, ct);
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
