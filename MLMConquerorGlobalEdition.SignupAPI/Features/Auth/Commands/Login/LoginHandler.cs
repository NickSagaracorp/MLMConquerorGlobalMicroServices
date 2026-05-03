using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly IJwtService                    _jwt;
    private readonly IDateTimeProvider              _dateTime;
    private readonly AppDbContext                   _db;
    private readonly ITwoFactorChallengeService     _twoFactor;
    private readonly IEmailService                  _email;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        IJwtService                  jwt,
        IDateTimeProvider            dateTime,
        AppDbContext                 db,
        ITwoFactorChallengeService   twoFactor,
        IEmailService                email)
    {
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
        _db          = db;
        _twoFactor   = twoFactor;
        _email       = email;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var req  = command.Request;
        var user = await _userManager.FindByEmailAsync(req.Email);

        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, req.Password);
        if (!passwordValid)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");

        // Read MemberProfile.DefaultLanguage once — used either to localize the
        // 2FA code email below or to embed in the access-token claims.
        var memberId = user.MemberProfileId ?? string.Empty;
        var defaultLanguage = string.IsNullOrEmpty(memberId)
            ? null
            : await _db.MemberProfiles.AsNoTracking()
                .Where(m => m.MemberId == memberId)
                .Select(m => m.DefaultLanguage)
                .FirstOrDefaultAsync(ct);

        // Two-factor branch — when TFA is enabled, do NOT issue access/refresh
        // tokens. Issue a 5-minute JWT challenge that carries the SHA-256 of
        // a freshly generated 6-digit code, send the code by email, and let
        // the client redeem it via /api/v1/auth/two-factor/verify.
        if (user.TwoFactorEnabled)
        {
            var code      = _twoFactor.GenerateCode();
            var codeHash  = _twoFactor.HashCode(code);
            var challenge = _twoFactor.IssueChallenge(user.Id, user.Email!, codeHash);

            await _email.SendAsync(
                toEmail:      user.Email!,
                toName:       user.Email!,
                languageCode: string.IsNullOrEmpty(defaultLanguage) ? "en" : defaultLanguage,
                eventType:    NotificationEvents.TwoFactorCode,
                variables:    new Dictionary<string, string>
                {
                    ["Code"]             = code,
                    ["ExpiresInMinutes"] = ((int)_twoFactor.ChallengeLifetime.TotalMinutes).ToString()
                },
                ct: ct);

            return Result<AuthResponse>.Success(new AuthResponse
            {
                UserId            = user.Id,
                Email             = user.Email!,
                RequiresTwoFactor = true,
                ChallengeToken    = challenge,
                MaskedEmail       = _twoFactor.MaskEmail(user.Email!)
            });
        }

        var roles      = await _userManager.GetRolesAsync(user);
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
