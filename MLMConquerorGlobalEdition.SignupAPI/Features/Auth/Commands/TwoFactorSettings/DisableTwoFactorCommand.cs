using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;

/// <param name="UserId">Sale de las claims del token de acceso: no hay cuerpo que manipular.</param>
public record DisableTwoFactorCommand(string UserId) : IRequest<Result<bool>>;
