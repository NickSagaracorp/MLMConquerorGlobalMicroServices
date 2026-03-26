using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.RefreshToken;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService                  _jwt;
    private readonly IDateTimeProvider            _dateTime;

    public RefreshTokenHandler(
        UserManager<ApplicationUser> userManager,
        IJwtService jwt,
        IDateTimeProvider dateTime)
    {
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var hashedToken = HashToken(command.Token);
        var now         = _dateTime.Now;

        // Find user by hashed refresh token
        var user = _userManager.Users
            .FirstOrDefault(u => u.RefreshToken == hashedToken);

        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("INVALID_REFRESH_TOKEN", "Refresh token is invalid.");

        if (user.RefreshTokenExpiry < now)
            return Result<AuthResponse>.Failure("REFRESH_TOKEN_EXPIRED", "Refresh token has expired. Please log in again.");

        var roles        = await _userManager.GetRolesAsync(user);
        var memberId     = user.MemberProfileId ?? string.Empty;
        var memberType   = roles.Contains("Ambassador") ? "Ambassador"
                         : roles.Contains("Member")     ? "Member"
                         : "Staff";

        var newAccessToken  = _jwt.GenerateAccessToken(user.Id, memberId, user.Email!, roles);
        var newRefreshToken = _jwt.GenerateRefreshToken();

        user.RefreshToken       = HashToken(newRefreshToken);
        user.RefreshTokenExpiry = now.Add(_jwt.RefreshTokenExpiry);
        await _userManager.UpdateAsync(user);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId       = user.Id,
            MemberId     = memberId,
            Email        = user.Email!,
            MemberType   = memberType,
            AccessToken  = newAccessToken,
            RefreshToken = newRefreshToken,
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
