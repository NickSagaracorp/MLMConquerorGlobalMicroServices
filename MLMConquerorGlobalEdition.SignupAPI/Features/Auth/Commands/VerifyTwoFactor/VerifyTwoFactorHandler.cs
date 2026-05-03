using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.VerifyTwoFactor;

public class VerifyTwoFactorHandler : IRequestHandler<VerifyTwoFactorCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly IJwtService                    _jwt;
    private readonly IDateTimeProvider              _dateTime;
    private readonly AppDbContext                   _db;
    private readonly ITwoFactorChallengeService     _twoFactor;

    public VerifyTwoFactorHandler(
        UserManager<ApplicationUser> userManager,
        IJwtService                  jwt,
        IDateTimeProvider            dateTime,
        AppDbContext                 db,
        ITwoFactorChallengeService   twoFactor)
    {
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
        _db          = db;
        _twoFactor   = twoFactor;
    }

    public async Task<Result<AuthResponse>> Handle(VerifyTwoFactorCommand command, CancellationToken ct)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Code) || req.Code.Length != 6 || !req.Code.All(char.IsDigit))
            return Result<AuthResponse>.Failure("INVALID_CODE", "The verification code is invalid.");

        var validation = _twoFactor.ValidateChallenge(req.ChallengeToken);
        if (!validation.IsSuccess)
            return Result<AuthResponse>.Failure(validation.ErrorCode!, validation.Error!);

        var claims = validation.Value!;

        // Constant-time compare on the SHA-256 hash so timing differences do
        // not leak whether a near-miss code was numerically close to the real one.
        var inputHash = _twoFactor.HashCode(req.Code);
        if (!CryptographicEquals(inputHash, claims.CodeHash))
            return Result<AuthResponse>.Failure("INVALID_CODE", "The verification code is invalid.");

        var user = await _userManager.FindByIdAsync(claims.UserId);
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Account is no longer active.");

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

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string HashToken(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
