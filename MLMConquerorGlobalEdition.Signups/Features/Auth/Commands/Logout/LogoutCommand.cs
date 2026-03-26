using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.Logout;

public record LogoutCommand(string UserId) : IRequest<Result<bool>>;
