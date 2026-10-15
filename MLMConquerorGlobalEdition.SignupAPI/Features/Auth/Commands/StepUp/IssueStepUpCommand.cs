using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record IssueStepUpCommand(string UserId, StepUpChallengeRequest Request)
    : IRequest<Result<StepUpChallengeResponse>>;
