using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupMember;

public record SignupMemberCommand(MemberSignupRequest Request) : IRequest<Result<SignupResponse>>;
