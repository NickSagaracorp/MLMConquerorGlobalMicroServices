using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResetPassword;

/// <summary>
/// Redime el enlace del correo de recuperación y fija la contraseña nueva.
/// </summary>
/// <remarks>
/// La cuenta se resuelve por <c>UserId</c> si viene y por <c>Email</c> si no. Los dos clientes de
/// hoy mandan cosas distintas —el componente de SharedComponents postea <c>UserId</c>, la pantalla
/// de BizCenterWeb postea <c>Email</c>— y el enlace del correo nuevo lleva <c>userId</c>, que es
/// el que no deja la dirección del usuario en el historial ni en la cabecera <c>Referer</c>.
///
/// Aquí sí se falla explícitamente, al contrario que en <c>ForgotPasswordHandler</c>: quien llega
/// con un enlace en la mano no está sondeando qué correos están registrados.
/// </remarks>
public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ResetPasswordHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(ResetPasswordCommand command, CancellationToken ct)
    {
        var req = command.Request;

        var user = string.IsNullOrWhiteSpace(req.UserId)
            ? await _userManager.FindByEmailAsync(req.Email)
            : await _userManager.FindByIdAsync(req.UserId);

        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "No active account found for this link.");

        // El enlace del correo trae el token en base64url, igual que el de confirmación de
        // dirección: el token crudo de Identity lleva '+', '/' y '=', que una query string
        // corrompe. Se decodifica aquí, que es el otro extremo de esa codificación.
        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
        }
        catch (FormatException)
        {
            // Enlace truncado o manipulado. Error de dominio, no una FormatException que suba
            // como 500.
            return Result<bool>.Failure("INVALID_TOKEN", "The reset link is not valid.");
        }

        var result = await _userManager.ResetPasswordAsync(user, token, req.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Failure("PASSWORD_RESET_FAILED", errors);
        }

        // Invalidate all refresh tokens on password reset
        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
