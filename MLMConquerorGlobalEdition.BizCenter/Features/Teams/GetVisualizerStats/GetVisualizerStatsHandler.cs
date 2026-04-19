using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerStats;

public class GetVisualizerStatsHandler
    : IRequestHandler<GetVisualizerStatsQuery, Result<EnrollmentVisualizerStatsDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisualizerStatsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<EnrollmentVisualizerStatsDto>> Handle(
        GetVisualizerStatsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Use HierarchyPath LIKE pattern to retrieve all downline member IDs in O(1) subtree scan.
        var pattern = "/" + memberId + "/";

        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(pattern))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (!downlineMemberIds.Any())
            return Result<EnrollmentVisualizerStatsDto>.Success(new EnrollmentVisualizerStatsDto());

        // Group by status to avoid loading full profile entities.
        var statusCounts = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => downlineMemberIds.Contains(m.MemberId))
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var qualified   = statusCounts
            .Where(x => x.Status == MemberAccountStatus.Active)
            .Sum(x => x.Count);

        var unqualified = statusCounts
            .Where(x => x.Status == MemberAccountStatus.Inactive
                     || x.Status == MemberAccountStatus.Suspended)
            .Sum(x => x.Count);

        var cancelled   = statusCounts
            .Where(x => x.Status == MemberAccountStatus.Terminated
                     || x.Status == MemberAccountStatus.Pending)
            .Sum(x => x.Count);

        var dto = new EnrollmentVisualizerStatsDto
        {
            TotalMembers     = downlineMemberIds.Count,
            TotalQualified   = qualified,
            TotalUnqualified = unqualified,
            TotalCancelled   = cancelled
        };

        return Result<EnrollmentVisualizerStatsDto>.Success(dto);
    }
}
