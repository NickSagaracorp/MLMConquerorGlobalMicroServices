using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;

/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record VerifyPhoneCommand(string UserId, VerifyPhoneRequest Request) : IRequest<Result<bool>>;
