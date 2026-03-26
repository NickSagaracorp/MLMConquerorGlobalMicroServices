using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.SignupAmbassador;

public record SignupAmbassadorCommand(AmbassadorSignupRequest Request) : IRequest<Result<SignupResponse>>;
