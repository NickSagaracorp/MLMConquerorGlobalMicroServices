using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;

/// <summary>
/// Places a sponsored member into a specific node of the Dual Team tree.
/// Enforces all 6 structural rules plus ambassador time/opportunity rules.
/// </summary>
public record PlaceMemberCommand(
    string MemberToPlaceId,
    string TargetParentMemberId,
    string Side              // "Left" | "Right"
) : IRequest<Result<PlaceMemberResult>>;

public record PlaceMemberResult(
    string MemberId,
    string FullName,
    string TargetParentMemberId,
    string Side,
    int    OpportunitiesRemaining
);
