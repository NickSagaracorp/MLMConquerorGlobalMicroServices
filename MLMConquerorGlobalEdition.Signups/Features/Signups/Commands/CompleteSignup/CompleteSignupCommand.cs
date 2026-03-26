using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.CompleteSignup;

public record CompleteSignupCommand(string SignupId, CompleteSignupRequest Request) : IRequest<Result<SignupResponse>>;
