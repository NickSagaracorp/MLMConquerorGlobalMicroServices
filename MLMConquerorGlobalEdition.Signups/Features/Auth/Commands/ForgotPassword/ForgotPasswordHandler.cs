using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ForgotPasswordHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(ForgotPasswordCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);

        // Always return success to prevent email enumeration
        if (user is null || !user.IsActive)
            return Result<bool>.Success(true);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // TODO: Send email with reset link containing token.
        // Email format: /auth/reset-password?email={email}&token={Uri.EscapeDataString(token)}
        // Email service integration is wired in Iteration 5 (Billing).
        _ = token; // suppress unused variable warning until email service is wired

        return Result<bool>.Success(true);
    }
}
