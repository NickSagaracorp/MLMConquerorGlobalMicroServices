using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StopImpersonation;

/// <summary>
/// Stops impersonation. Impersonation is stateless (JWT-based), so stopping it is a
/// client-side operation: the client discards the impersonation token and reverts to the
/// original admin token. This command exists solely to record the audit log server-side.
/// </summary>
public record StopImpersonationCommand(string AdminUserId) : IRequest<Result<bool>>;
