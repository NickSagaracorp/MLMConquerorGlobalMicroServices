using MediatR;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StopImpersonation;

public class StopImpersonationHandler : IRequestHandler<StopImpersonationCommand, Result<bool>>
{
    private readonly ICurrentUserService              _currentUser;
    private readonly ILogger<StopImpersonationHandler> _logger;

    public StopImpersonationHandler(
        ICurrentUserService currentUser,
        ILogger<StopImpersonationHandler> logger)
    {
        _currentUser = currentUser;
        _logger      = logger;
    }

    public Task<Result<bool>> Handle(StopImpersonationCommand command, CancellationToken ct)
    {
        // Impersonation is stateless (JWT-based). The client discards the impersonation token
        // and reverts to the original admin token. Server-side we record the audit event.
        _logger.LogInformation(
            "Admin {UserId} ended impersonation session.",
            command.AdminUserId);

        return Task.FromResult(Result<bool>.Success(true));
    }
}
