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

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;

/// <summary>
/// Cierra el enrolamiento con el primer código de la aplicación del usuario y, si cuadra, emite
/// los tokens de acceso. El usuario acaba de demostrar los dos factores en la misma sesión
/// —contraseña en el login que dio el token de enrolamiento, TOTP aquí—, así que mandarlo de
/// vuelta a iniciar sesión solo añadiría una pantalla sin añadir ninguna garantía.
/// </summary>
public class ConfirmEnrollmentHandler : IRequestHandler<ConfirmEnrollmentCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly IJwtService                    _jwt;
    private readonly IDateTimeProvider              _dateTime;
    private readonly AppDbContext                   _db;
    private readonly IChallengeTokenService         _challenges;
    private readonly ITotpEnrollmentService         _enrollment;

    public ConfirmEnrollmentHandler(
        UserManager<ApplicationUser> userManager,
        IJwtService                  jwt,
        IDateTimeProvider            dateTime,
        AppDbContext                 db,
        IChallengeTokenService       challenges,
        ITotpEnrollmentService       enrollment)
    {
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
        _db          = db;
        _challenges  = challenges;
        _enrollment  = enrollment;
    }

    public async Task<Result<AuthResponse>> Handle(ConfirmEnrollmentCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // Mismo propósito que en Begin: un token de login no puede terminar un enrolamiento, y
        // aquí importa aún más porque el final de este camino son tokens de acceso.
        var validation = _challenges.Validate(req.EnrollmentToken, TwoFactorPurpose.Enrollment);
        if (!validation.IsSuccess)
            return Result<AuthResponse>.Failure(validation.ErrorCode!, validation.Error!);

        var user = await _userManager.FindByIdAsync(validation.Value!.UserId);
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Account is no longer active.");

        // La librería activa el 2FA y fija el canal preferido solo si el código es válido. Si
        // no lo es, no toca nada y este handler se va sin emitir ni un token: no hay camino por
        // el que un enrolamiento fallido termine en una sesión abierta.
        var confirmed = await _enrollment.ConfirmAsync(user, req.Code, ct);
        if (!confirmed.IsSuccess)
            return Result<AuthResponse>.Failure(confirmed.ErrorCode!, confirmed.Error!);

        var roles      = await _userManager.GetRolesAsync(user);
        var memberId   = user.MemberProfileId ?? string.Empty;
        var memberType = roles.Contains("Ambassador") ? "Ambassador"
                       : roles.Contains("Member")     ? "Member"
                       : "Staff";

        var defaultLanguage = string.IsNullOrEmpty(memberId)
            ? null
            : await _db.MemberProfiles.AsNoTracking()
                .Where(m => m.MemberId == memberId)
                .Select(m => m.DefaultLanguage)
                .FirstOrDefaultAsync(ct);

        var accessToken  = _jwt.GenerateAccessToken(
            user.Id, memberId, user.Email!, roles,
            defaultLanguage: string.IsNullOrEmpty(defaultLanguage) ? null : defaultLanguage);
        var refreshToken = _jwt.GenerateRefreshToken();
        var now          = _dateTime.Now;

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
