using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;

public record AdminPlaceMemberCommand(
    string MemberToPlaceId,
    string TargetParentMemberId,
    string Side
) : IRequest<Result<string>>;
