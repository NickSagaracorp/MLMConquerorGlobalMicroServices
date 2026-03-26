using MediatR;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberRank;

/// <summary>
/// Query dispatched by GET /api/v1/external/member/{memberId}/rank.
/// Returns the member's current rank information.
/// </summary>
/// <param name="MemberId">Human-readable member ID (e.g. AMB-000001).</param>
public record GetExternalMemberRankQuery(string MemberId)
    : IRequest<Result<ExternalMemberRankDto>>;
