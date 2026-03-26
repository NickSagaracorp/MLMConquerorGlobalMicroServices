using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;

public record CompleteSignupCommand(string SignupId, CompleteSignupRequest Request) : IRequest<Result<SignupResponse>>;
