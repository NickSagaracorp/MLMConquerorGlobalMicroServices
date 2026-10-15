using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record VerifyStepUpCommand(string UserId, StepUpVerifyRequest Request)
    : IRequest<Result<StepUpGrantResponse>>;
