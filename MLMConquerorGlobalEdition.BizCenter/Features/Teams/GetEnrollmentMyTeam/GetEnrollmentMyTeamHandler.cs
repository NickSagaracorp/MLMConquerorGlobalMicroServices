using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentMyTeam;

public class GetEnrollmentMyTeamHandler
    : IRequestHandler<GetEnrollmentMyTeamQuery, Result<PagedResult<EnrollmentMyTeamMemberDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetEnrollmentMyTeamHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<EnrollmentMyTeamMemberDto>>> Handle(
        GetEnrollmentMyTeamQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // 1. Resolve HierarchyPath for the current member
        var myNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);

        if (myNode is null)
            return Result<PagedResult<EnrollmentMyTeamMemberDto>>.Success(
                new PagedResult<EnrollmentMyTeamMemberDto>());

        var pathPrefix = myNode.HierarchyPath;

        // 2. All downline member IDs via materialized path
        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix))
            .Select(g => new { g.MemberId, g.Level })
            .ToListAsync(ct);

        var downlineIds = downlineMemberIds.Select(x => x.MemberId).ToList();
        var levelMap    = downlineMemberIds.ToDictionary(x => x.MemberId, x => x.Level);

        if (!downlineIds.Any())
            return Result<PagedResult<EnrollmentMyTeamMemberDto>>.Success(
                new PagedResult<EnrollmentMyTeamMemberDto>());

        // 3. Member profiles
        var profileQuery = _db.MemberProfiles
            .AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            profileQuery = profileQuery.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s)  ||
                (m.Email != null && m.Email.ToLower().Contains(s)));
        }

        if (request.From.HasValue)
            profileQuery = profileQuery.Where(m => m.EnrollDate >= request.From.Value);
        if (request.To.HasValue)
            profileQuery = profileQuery.Where(m => m.EnrollDate <= request.To.Value);

        var totalCount = await profileQuery.CountAsync(ct);

        var profiles = await profileQuery
            .OrderByDescending(m => m.EnrollDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new
            {
                m.MemberId, m.FirstName, m.LastName, m.Email, m.Phone,
                m.Country, m.EnrollDate, m.SponsorMemberId,
                AccountStatus = m.Status.ToString()
            })
            .ToListAsync(ct);

        var pageIds = profiles.Select(p => p.MemberId).ToList();

        // 4. Active memberships
        var subscriptions = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => pageIds.Contains(s.MemberId) &&
                        s.SubscriptionStatus != MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipStatus.Cancelled)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        var subMap = subscriptions
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.First());

        // 5. Latest rank and lifetime rank
        var rankHistories = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => pageIds.Contains(r.MemberId))
            .ToListAsync(ct);

        var currentRankMap = rankHistories
            .GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First());

        var lifetimeRankMap = rankHistories
            .GroupBy(r => r.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.RankDefinition?.SortOrder ?? 0).First());

        // 6. Dual tree nodes (for upline and left/right points)
        var dualNodes = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => pageIds.Contains(d.MemberId))
            .ToListAsync(ct);

        var dualMap = dualNodes.ToDictionary(d => d.MemberId);

        // 7. Member statistics (for enrollment team points + qualification points)
        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToListAsync(ct);

        var statsMap = stats.ToDictionary(s => s.MemberId);

        // 8. Last successful payment date per member
        var lastPayments = await _db.PaymentHistories
            .AsNoTracking()
            .Where(p => pageIds.Contains(p.MemberId) &&
                        p.TransactionStatus == MLMConquerorGlobalEdition.Domain.Entities.Orders.PaymentHistoryTransactionStatus.Captured)
            .GroupBy(p => p.MemberId)
            .Select(g => new { MemberId = g.Key, LastDate = g.Max(p => p.ProcessedAt) })
            .ToListAsync(ct);

        var lastPaymentMap = lastPayments.ToDictionary(x => x.MemberId, x => x.LastDate);

        // 10. Sponsor name resolution
        var sponsorIds = profiles
            .Where(p => !string.IsNullOrEmpty(p.SponsorMemberId))
            .Select(p => p.SponsorMemberId!)
            .Distinct()
            .ToList();

        var dualUplineIds = dualNodes
            .Where(d => !string.IsNullOrEmpty(d.ParentMemberId))
            .Select(d => d.ParentMemberId!)
            .Distinct()
            .ToList();

        var allResolveIds = sponsorIds.Union(dualUplineIds).ToList();

        var nameMap = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => allResolveIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        // 11. Next-rank percent: all rank definitions sorted by SortOrder
        var allRanks = await _db.RankDefinitions
            .AsNoTracking()
            .Include(r => r.Requirements)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        // 12. Assemble DTOs
        var items = profiles.Select(p =>
        {
            subMap.TryGetValue(p.MemberId, out var sub);
            currentRankMap.TryGetValue(p.MemberId, out var currentRank);
            lifetimeRankMap.TryGetValue(p.MemberId, out var lifetimeRank);
            dualMap.TryGetValue(p.MemberId, out var dual);
            statsMap.TryGetValue(p.MemberId, out var stat);
            lastPaymentMap.TryGetValue(p.MemberId, out var lastPayDate);

            nameMap.TryGetValue(p.SponsorMemberId ?? "", out var sponsorName);
            nameMap.TryGetValue(dual?.ParentMemberId ?? "", out var uplineName);

            var membershipStatus = sub is null ? "None"
                : sub.SubscriptionStatus.ToString();

            var isQualified = sub?.SubscriptionStatus ==
                              MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipStatus.Active;

            // Compute next rank %
            var currentSortOrder = currentRank?.RankDefinition?.SortOrder ?? 0;
            var nextRank = allRanks.FirstOrDefault(r => r.SortOrder > currentSortOrder);
            int nextRankPct = 0;
            if (nextRank is not null)
            {
                var req = nextRank.Requirements.FirstOrDefault(r => r.LevelNo == 0);
                if (req is not null && req.TeamPoints > 0)
                {
                    var currentPoints = (stat?.DualTeamPoints ?? 0);
                    nextRankPct = Math.Min(100, (int)(currentPoints * 100.0 / req.TeamPoints));
                }
            }
            else if (currentRank is not null)
            {
                nextRankPct = 100; // already at highest rank
            }

            return new EnrollmentMyTeamMemberDto
            {
                MemberId            = p.MemberId,
                FullName            = $"{p.FirstName} {p.LastName}",
                Email               = p.Email,
                Phone               = p.Phone,
                Country             = p.Country,
                Level               = levelMap.TryGetValue(p.MemberId, out var lvl) ? lvl : 0,
                EnrollDate          = p.EnrollDate,
                SponsorMemberId     = p.SponsorMemberId,
                SponsorFullName     = sponsorName,
                DualUplineMemberId  = dual?.ParentMemberId,
                DualUplineFullName  = uplineName,
                AccountStatus       = p.AccountStatus,
                MembershipStatus    = membershipStatus,
                IsQualified         = isQualified,
                MembershipLevelName = sub?.MembershipLevel?.Name,
                CurrentRankName     = currentRank?.RankDefinition?.Name,
                RankDate            = currentRank?.AchievedAt,
                LifetimeRankName    = lifetimeRank?.RankDefinition?.Name,
                NextRankPercent     = nextRankPct,
                QualificationPoints = stat?.PersonalPoints ?? 0,
                EnrollmentTeamPoints= stat?.EnrollmentPoints ?? 0,
                LeftTeamPoints      = dual?.LeftLegPoints ?? 0,
                RightTeamPoints     = dual?.RightLegPoints ?? 0,
                SuspensionDate      = sub?.HoldDate,
                CancellationDate    = sub?.CancellationDate,
                LastPaymentDate     = lastPayDate
            };
        }).ToList();

        return Result<PagedResult<EnrollmentMyTeamMemberDto>>.Success(
            new PagedResult<EnrollmentMyTeamMemberDto>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = request.Page,
                PageSize   = request.PageSize
            });
    }
}
