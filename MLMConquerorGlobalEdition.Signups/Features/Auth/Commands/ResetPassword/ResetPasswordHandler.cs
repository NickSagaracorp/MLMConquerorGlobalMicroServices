using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.ResetPassword;

public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ResetPasswordHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(ResetPasswordCommand command, CancellationToken ct)
    {
        var req  = command.Request;
        var user = await _userManager.FindByEmailAsync(req.Email);

        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "No active account found for this email.");

        var result = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
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
