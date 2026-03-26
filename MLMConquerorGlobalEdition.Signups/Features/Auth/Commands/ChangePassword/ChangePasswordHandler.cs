using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.ChangePassword;

public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangePasswordHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        var req  = command.Request;
        var user = await _userManager.FindByIdAsync(command.UserId);

        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Failure("PASSWORD_CHANGE_FAILED", errors);
        }

        // Invalidate refresh tokens on password change
        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
