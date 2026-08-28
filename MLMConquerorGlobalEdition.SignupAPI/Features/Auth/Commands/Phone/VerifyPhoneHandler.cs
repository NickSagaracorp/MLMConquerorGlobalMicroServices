using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;

/// <summary>
/// Redime el código SMS del alta y marca el teléfono como confirmado. Es el único sitio que pone
/// <c>TwoFactorPhoneConfirmed = true</c>: a partir de aquí el canal SMS existe para esa cuenta.
/// </summary>
public class VerifyPhoneHandler : IRequestHandler<VerifyPhoneCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITwoFactorService            _twoFactor;

    public VerifyPhoneHandler(UserManager<ApplicationUser> userManager, ITwoFactorService twoFactor)
    {
        _userManager = userManager;
        _twoFactor   = twoFactor;
    }

    public async Task<Result<bool>> Handle(VerifyPhoneCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        if (string.IsNullOrWhiteSpace(user.TwoFactorPhoneEncrypted))
            return Result<bool>.Failure(
                "PHONE_NOT_FOUND", "No hay ningún teléfono pendiente de confirmar en esta cuenta.");

        var req = command.Request;

        // Enrollment, el mismo propósito con el que se emitió: un challenge de login no puede
        // confirmar un teléfono. La librería aplica además el límite de intentos y quema el
        // challenge al agotarlos.
        var verified = await _twoFactor.VerifyAsync(
            req.ChallengeToken, req.Code, TwoFactorPurpose.Enrollment, ct: ct);

        if (!verified.IsSuccess)
            return Result<bool>.Failure(verified.ErrorCode!, verified.Error!);

        var claims = verified.Value!;

        // El challenge tiene que ser del mismo usuario que trae el token de acceso. Sin esta
        // comprobación, un challenge ajeno confirmaría el teléfono de esta cuenta con un código
        // que nadie mandó a este número.
        if (!string.Equals(claims.UserId, command.UserId, StringComparison.Ordinal))
            return Result<bool>.Failure("INVALID_CHALLENGE", "Este challenge no es de esta cuenta.");

        // Y tiene que venir por SMS. El token que abre el enrolamiento TOTP comparte propósito, y
        // sin este corte serviría para marcar un teléfono como confirmado con un código de la
        // aplicación de autenticación, sin que nadie hubiera recibido nunca el mensaje.
        if (claims.Channel != TwoFactorChannel.Sms)
            return Result<bool>.Failure(
                "INVALID_CHALLENGE", "Este challenge no corresponde a la verificación de un teléfono.");

        user.TwoFactorPhoneConfirmed = true;
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
