using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly IJwtService                    _jwt;
    private readonly IDateTimeProvider              _dateTime;
    private readonly AppDbContext                   _db;
    private readonly ITwoFactorService              _twoFactor;
    private readonly IConfiguration                 _config;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        IJwtService                  jwt,
        IDateTimeProvider            dateTime,
        AppDbContext                 db,
        ITwoFactorService            twoFactor,
        IConfiguration               config)
    {
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
        _db          = db;
        _twoFactor   = twoFactor;
        _config      = config;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var req  = command.Request;
        var user = await _userManager.FindByEmailAsync(req.Email);

        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");

        // Lockout de Identity: configurado en Program.cs (5 intentos / 15 min) pero hasta
        // ahora nunca invocado desde aquí, así que el contador no subía y el bloqueo no
        // ocurría. Este es el camino de login de AdminWeb y BizCenterWeb.
        if (await _userManager.IsLockedOutAsync(user))
            return Result<AuthResponse>.Failure("ACCOUNT_LOCKED", "Account is temporarily locked.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, req.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        // Read MemberProfile.DefaultLanguage once — used either to localize the
        // 2FA code email below or to embed in the access-token claims. Es null para el
        // personal, que no tiene MemberProfile; IssueAsync acepta null y cae a "en".
        var memberId = user.MemberProfileId ?? string.Empty;
        var defaultLanguage = string.IsNullOrEmpty(memberId)
            ? null
            : await _db.MemberProfiles.AsNoTracking()
                .Where(m => m.MemberId == memberId)
                .Select(m => m.DefaultLanguage)
                .FirstOrDefaultAsync(ct);

        // Los roles se resuelven aquí, antes de las dos ramas de dos factores: la de
        // enrolamiento obligatorio decide justo sobre ellos.
        var roles = await _userManager.GetRolesAsync(user);

        var mandatoryRoles = _config.GetSection("Auth:TwoFactor:MandatoryRoles").Get<string[]>() ?? [];
        var requiresTwoFactor = roles.Any(r => mandatoryRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

        // Rol que exige 2FA pero sin configurar: no hay tokens de acceso hasta enrolarse.
        // El token de enrolamiento no abre ningún endpoint de negocio, asi que el usuario
        // queda atrapado en esa pantalla en vez de poder navegar el portal a medias.
        if (requiresTwoFactor && !user.TwoFactorEnabled)
        {
            return Result<AuthResponse>.Success(new AuthResponse
            {
                UserId             = user.Id,
                Email              = user.Email!,
                RequiresEnrollment = true,
                EnrollmentToken    = _twoFactor.IssueEnrollmentToken(user)
            });
        }

        // Two-factor branch — when TFA is enabled, do NOT issue access/refresh tokens.
        // La emisión y el despacho del código son de la librería Authn: elige el canal
        // preferido del usuario, envía por él y solo devuelve el challenge si el envío salió.
        if (user.TwoFactorEnabled)
        {
            var issued = await _twoFactor.IssueAsync(
                user, TwoFactorPurpose.Login, languageCode: defaultLanguage, ct: ct);

            // Un código que no llegó no puede convertirse en una sesión abierta: el error del
            // transporte se propaga para que la interfaz pueda ofrecer otro canal.
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

        var memberType = roles.Contains("Ambassador") ? "Ambassador"
                       : roles.Contains("Member")     ? "Member"
                       : "Staff";

        var accessToken  = _jwt.GenerateAccessToken(
            user.Id, memberId, user.Email!, roles,
            defaultLanguage: string.IsNullOrEmpty(defaultLanguage) ? null : defaultLanguage);
        var refreshToken = _jwt.GenerateRefreshToken();
        var now          = _dateTime.Now;

        // Store hashed refresh token
        user.RefreshToken       = HashToken(refreshToken);
        user.RefreshTokenExpiry = now.Add(_jwt.RefreshTokenExpiry);
        user.LastLoginAt        = now;
        await _userManager.UpdateAsync(user);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId       = user.Id,
            MemberId     = memberId,
            Email        = user.Email!,
            MemberType   = memberType,
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            TokenExpiry  = now.Add(_jwt.AccessTokenExpiry),
            Roles        = roles
        });
    }

    private static string HashToken(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
