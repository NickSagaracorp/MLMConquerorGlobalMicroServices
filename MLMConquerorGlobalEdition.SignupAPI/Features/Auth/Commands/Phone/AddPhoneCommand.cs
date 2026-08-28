using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;

/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record AddPhoneCommand(string UserId, AddPhoneRequest Request)
    : IRequest<Result<PhoneChallengeResponse>>;
