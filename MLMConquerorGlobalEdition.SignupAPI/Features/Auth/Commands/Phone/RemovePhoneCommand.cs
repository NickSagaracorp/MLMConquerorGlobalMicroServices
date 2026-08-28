using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;

/// <param name="UserId">Sale de las claims del token de acceso: no hay cuerpo que manipular.</param>
public record RemovePhoneCommand(string UserId) : IRequest<Result<bool>>;
