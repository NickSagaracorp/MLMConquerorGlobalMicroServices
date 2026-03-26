using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.SignupMember;

public record SignupMemberCommand(MemberSignupRequest Request) : IRequest<Result<SignupResponse>>;
