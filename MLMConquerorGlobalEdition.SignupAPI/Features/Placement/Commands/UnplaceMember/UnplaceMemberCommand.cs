using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.UnplaceMember;

public record UnplaceMemberCommand(string MemberId, string RequestedByMemberId) : IRequest<Result<bool>>;
