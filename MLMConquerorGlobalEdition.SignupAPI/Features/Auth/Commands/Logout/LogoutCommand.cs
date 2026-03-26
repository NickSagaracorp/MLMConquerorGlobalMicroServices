using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Logout;

public record LogoutCommand(string UserId) : IRequest<Result<bool>>;
