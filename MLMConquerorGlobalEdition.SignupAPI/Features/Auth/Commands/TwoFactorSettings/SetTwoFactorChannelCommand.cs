using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;

/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record SetTwoFactorChannelCommand(string UserId, SetTwoFactorChannelRequest Request)
    : IRequest<Result<bool>>;
