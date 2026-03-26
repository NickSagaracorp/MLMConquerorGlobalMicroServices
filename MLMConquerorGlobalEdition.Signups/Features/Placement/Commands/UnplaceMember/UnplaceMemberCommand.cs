using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.UnplaceMember;

public record UnplaceMemberCommand(string MemberId, string RequestedByMemberId) : IRequest<Result<bool>>;
