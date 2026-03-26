using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupAmbassador;

public record SignupAmbassadorCommand(AmbassadorSignupRequest Request) : IRequest<Result<SignupResponse>>;
