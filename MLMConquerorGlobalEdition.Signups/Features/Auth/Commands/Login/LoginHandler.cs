using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Signups.DTOs.Auth;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService                  _jwt;
    private readonly IDateTimeProvider            _dateTime;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        IJwtService jwt,
        IDateTimeProvider dateTime)
    {
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
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

        var roles      = await _userManager.GetRolesAsync(user);
        var memberId   = user.MemberProfileId ?? string.Empty;
        var memberType = roles.Contains("Ambassador") ? "Ambassador"
                       : roles.Contains("Member")     ? "Member"
                       : "Staff";

        var accessToken  = _jwt.GenerateAccessToken(user.Id, memberId, user.Email!, roles);
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
