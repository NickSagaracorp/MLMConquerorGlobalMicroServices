using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResendTwoFactor;

public class ResendTwoFactorHandler : IRequestHandler<ResendTwoFactorCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly AppDbContext                   _db;
    private readonly IChallengeTokenService         _challenges;
    private readonly ITwoFactorService              _twoFactor;

    public ResendTwoFactorHandler(
        UserManager<ApplicationUser> userManager,
        AppDbContext                 db,
        IChallengeTokenService       challenges,
        ITwoFactorService            twoFactor)
    {
        _userManager = userManager;
        _db          = db;
        _challenges  = challenges;
        _twoFactor   = twoFactor;
    }

    public async Task<Result<AuthResponse>> Handle(ResendTwoFactorCommand command, CancellationToken ct)
    {
        // allowExpired: quien tiene un código ya vencido debe poder pedir otro sin volver a
        // escribir su contraseña. La firma se sigue verificando; lo que se relaja es la
        // vigencia, acotada por ResendGraceWindow. Se valida contra IChallengeTokenService y no
        // contra ITwoFactorService porque VerifyAsync no admite expirados: allí redimir un
        // challenge vencido sería justo lo que hay que impedir.
        //
        // El propósito es Login: el botón de reenviar vive en la pantalla del challenge de
        // inicio de sesión, y un token de enrolamiento o de step-up no debe servir para pedir
        // códigos de login.
        var validation = _challenges.Validate(
            command.Request.ChallengeToken, TwoFactorPurpose.Login, allowExpired: true);

        if (!validation.IsSuccess)
            return Result<AuthResponse>.Failure(validation.ErrorCode!, validation.Error!);

        var claims = validation.Value!;
        var user = await _userManager.FindByIdAsync(claims.UserId);
        if (user is null || !user.IsActive || !user.TwoFactorEnabled)
            return Result<AuthResponse>.Failure("INVALID_CHALLENGE", "Challenge token is invalid.");

        // Con Authenticator no hay nada que reenviar: el código lo genera la aplicación del
        // usuario en su teléfono, y nosotros nunca lo mandamos. Sin este corte, el botón de
        // reenviar emitiría un challenge nuevo, gastaría cupo de emisiones y no enviaría nada,
        // dejando al usuario esperando un mensaje que no existe.
        if (claims.Channel == TwoFactorChannel.Authenticator)
            return Result<AuthResponse>.Failure(
                "CHANNEL_UNAVAILABLE",
                "El código lo genera su aplicación de autenticación; no hay nada que reenviar.");

        var memberId = user.MemberProfileId ?? string.Empty;
        var defaultLanguage = string.IsNullOrEmpty(memberId)
            ? null
            : await _db.MemberProfiles.AsNoTracking()
                .Where(m => m.MemberId == memberId)
                .Select(m => m.DefaultLanguage)
                .FirstOrDefaultAsync(ct);

        // Se reenvía por el mismo canal que emitió el challenge original, no por el preferido
        // del usuario: "reenviar" es repetir el envío que el usuario está esperando. Si los dos
        // discrepan, dejarlo al preferido podría mandar el código a otro sitio — o a ninguno.
        var issued = await _twoFactor.IssueAsync(
            user, TwoFactorPurpose.Login,
            forcedChannel: claims.Channel, languageCode: defaultLanguage, ct: ct);

        // Igual que en el login: un código que no salió no devuelve challenge. El error del
        // transporte —o el tope de emisiones— se propaga tal cual.
        if (!issued.IsSuccess)
            return Result<AuthResponse>.Failure(issued.ErrorCode!, issued.Error!);

#pragma warning disable CS0618 // MaskedEmail sigue rellenándose: es el contrato de los clientes de hoy.
        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId            = user.Id,
            Email             = user.Email!,
            RequiresTwoFactor = true,
            ChallengeToken    = issued.Value!.ChallengeToken,
            Channel           = issued.Value.Channel,
            MaskedTarget      = issued.Value.MaskedTarget,
            MaskedEmail       = issued.Value.Channel == TwoFactorChannel.Email
                                    ? issued.Value.MaskedTarget : null
        });
#pragma warning restore CS0618
    }
}
