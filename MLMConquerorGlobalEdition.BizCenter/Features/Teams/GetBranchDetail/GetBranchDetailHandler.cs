using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetBranchDetail;

public class GetBranchDetailHandler
    : IRequestHandler<GetBranchDetailQuery, Result<BranchDetailDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetBranchDetailHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<BranchDetailDto>> Handle(
        GetBranchDetailQuery request, CancellationToken ct)
    {
        var memberId       = _currentUser.MemberId;
        var branchMemberId = request.BranchMemberId;

        // ── 1. Verify the branch member is a direct child of the current user ────
        var branchNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == branchMemberId &&
                                      g.ParentMemberId == memberId, ct);

        if (branchNode is null)
            return Result<BranchDetailDto>.Failure(
                "BRANCH_NOT_FOUND", "Branch not found or does not belong to you.");

        // ── 2. Load branch member name + points ──────────────────────────────────
        var branchProfile = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == branchMemberId)
            .Select(m => new { m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);

        var branchStats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => s.MemberId == branchMemberId)
            .Select(s => s.EnrollmentPoints)
            .FirstOrDefaultAsync(ct);

        // ── 3. All downline IDs via materialized path ────────────────────────────
        var pathPrefix = branchNode.HierarchyPath;
        var branchLevel = branchNode.Level;

        var downlineNodes = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix) && g.MemberId != branchMemberId)
            .Select(g => new { g.MemberId, g.Level })
            .ToListAsync(ct);

        var downlineIds = downlineNodes.Select(x => x.MemberId).ToList();
        var levelMap    = downlineNodes.ToDictionary(x => x.MemberId, x => x.Level - branchLevel);

        if (!downlineIds.Any())
        {
            return Result<BranchDetailDto>.Success(new BranchDetailDto
            {
                BranchMemberId   = branchMemberId,
                BranchMemberName = branchProfile is not null
                    ? $"{branchProfile.FirstName} {branchProfile.LastName}" : branchMemberId,
                TotalPoints      = branchStats
            });
        }

        // ── 4. Load member profiles for all downline ─────────────────────────────
        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId))
            .Select(m => new
            {
                m.MemberId, m.FirstName, m.LastName,
                m.MemberType, AccountStatus = m.Status.ToString()
            })
            .ToListAsync(ct);

        var allIds = profiles.Select(p => p.MemberId).ToList();

        // ── 5. Active subscriptions ──────────────────────────────────────────────
        var subscriptions = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => allIds.Contains(s.MemberId) &&
                        s.SubscriptionStatus != MembershipStatus.Cancelled)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        var subMap = subscriptions
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.First());

        // ── 6. Statistics (enrollment points) ───────────────────────────────────
        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => allIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);

        var statsMap = stats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        // ── 7. Assemble ──────────────────────────────────────────────────────────
        var ambassadors = profiles
            .Where(p => p.MemberType == MemberType.Ambassador)
            .OrderBy(p => levelMap.TryGetValue(p.MemberId, out var l) ? l : 0)
            .ThenBy(p => p.FirstName)
            .Select((p, idx) =>
            {
                subMap.TryGetValue(p.MemberId, out var sub);
                var membershipStatus = sub is null ? "None" : sub.SubscriptionStatus.ToString();
                var isQualified = sub?.SubscriptionStatus == MembershipStatus.Active;

                return new BranchAmbassadorItemDto
                {
                    SeqNo               = idx + 1,
                    Level               = levelMap.TryGetValue(p.MemberId, out var lv) ? lv : 0,
                    FullName            = $"{p.FirstName} {p.LastName}",
                    AccountStatus       = p.AccountStatus,
                    MembershipStatus    = membershipStatus,
                    IsQualified         = isQualified,
                    MembershipLevelName = sub?.MembershipLevel?.Name,
                    EnrollmentPoints    = statsMap.TryGetValue(p.MemberId, out var pts) ? pts : 0
                };
            }).ToList();

        var customers = profiles
            .Where(p => p.MemberType == MemberType.ExternalMember)
            .OrderBy(p => levelMap.TryGetValue(p.MemberId, out var l) ? l : 0)
            .ThenBy(p => p.FirstName)
            .Select((p, idx) =>
            {
                subMap.TryGetValue(p.MemberId, out var sub);
                var membershipStatus = sub is null ? "None" : sub.SubscriptionStatus.ToString();

                return new BranchCustomerItemDto
                {
                    SeqNo               = idx + 1,
                    Level               = levelMap.TryGetValue(p.MemberId, out var lv) ? lv : 0,
                    FullName            = $"{p.FirstName} {p.LastName}",
                    MembershipStatus    = membershipStatus,
                    MembershipLevelName = sub?.MembershipLevel?.Name,
                    EnrollmentPoints    = statsMap.TryGetValue(p.MemberId, out var pts) ? pts : 0
                };
            }).ToList();

        return Result<BranchDetailDto>.Success(new BranchDetailDto
        {
            BranchMemberId   = branchMemberId,
            BranchMemberName = branchProfile is not null
                ? $"{branchProfile.FirstName} {branchProfile.LastName}" : branchMemberId,
            TotalPoints      = branchStats,
            Ambassadors      = ambassadors,
            Customers        = customers
        });
    }
}
