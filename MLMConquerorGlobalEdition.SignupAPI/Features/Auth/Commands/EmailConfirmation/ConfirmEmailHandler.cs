using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;

/// <summary>
/// Redime el enlace del correo de confirmación.
/// </summary>
/// <remarks>
/// Aquí sí se falla explícitamente, al contrario que en <see cref="SendEmailConfirmationHandler"/>:
/// quien llega con un userId y un token ya tiene el enlace en la mano, no está sondeando qué
/// correos están registrados. Devolver siempre éxito solo serviría para que el usuario cuyo
/// enlace caducó se quedara mirando una pantalla que dice que todo fue bien.
/// </remarks>
public class ConfirmEmailHandler : IRequestHandler<ConfirmEmailCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ConfirmEmailHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(ConfirmEmailCommand command, CancellationToken ct)
    {
        var req  = command.Request;
        var user = await _userManager.FindByIdAsync(req.UserId);

        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "No active account found for this link.");

        // Idempotente: reabrir el enlace del correo —o que el antivirus del cliente lo visite
        // antes que el usuario— no debe dar error. Hay que cortar antes de llamar a Identity:
        // el token ya se consumió y ConfirmEmailAsync lo rechazaría.
        if (user.EmailConfirmed)
            return Result<bool>.Success(true);

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
        }
        catch (FormatException)
        {
            // Enlace truncado o manipulado. Error de dominio, no una FormatException que suba
            // como 500.
            return Result<bool>.Failure("INVALID_TOKEN", "The confirmation link is not valid.");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Failure("EMAIL_CONFIRMATION_FAILED", errors);
        }

        return Result<bool>.Success(true);
    }
}
