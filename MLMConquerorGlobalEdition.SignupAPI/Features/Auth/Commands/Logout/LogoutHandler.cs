using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Logout;

public class LogoutHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public LogoutHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(LogoutCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null) return Result<bool>.Success(true); // idempotent

        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
