using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMemberStats;

/// <summary>
/// Query for the four headline counters that render above the AdminWeb
/// Members grid. <paramref name="BypassCache"/> mirrors the same flag on
/// <see cref="GetMembers.GetMembersQuery"/> — surfaced by the controller as
/// <c>?bypassCache=true</c> so a "Refresh" button click forces fresh counts.
/// </summary>
public record GetMemberStatsQuery(bool BypassCache = false)
    : IRequest<Result<MemberStatsDto>>;
