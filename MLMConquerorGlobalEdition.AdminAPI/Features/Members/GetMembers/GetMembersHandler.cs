using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;

/// <summary>
/// Paginated admin member list with sponsor / dual-upline / membership / rank
/// joins. Heavy multi-table query — cached for 2 minutes per page+filter so
/// rapid pagination and back-and-forth navigation hits the in-process / Redis
/// cache instead of re-running the joins. Pass <c>BypassCache = true</c>
/// (controller surfaces it as <c>?bypassCache=true</c>) when the admin clicks
/// "Refresh" and expects fresh data.
/// </summary>
public class GetMembersHandler : IRequestHandler<GetMembersQuery, Result<PagedResult<AdminMemberDto>>>
{
    private readonly AppDbContext  _db;
    private readonly ICacheService _cache;

    public GetMembersHandler(AppDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<Result<PagedResult<AdminMemberDto>>> Handle(
        GetMembersQuery request, CancellationToken cancellationToken)
    {
        var fingerprint = BuildFilterFingerprint(request.StatusFilter, request.SponsorId, request.SearchTerm);
        var cacheKey    = CacheKeys.AdminMembers(request.Page.Page, request.Page.PageSize, fingerprint);

        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<PagedResult<AdminMemberDto>>(cacheKey, cancellationToken);
            if (cached is not null) return Result<PagedResult<AdminMemberDto>>.Success(cached);
        }

        var query = _db.MemberProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<MemberAccountStatus>(request.StatusFilter, true, out var statusEnum))
        {
            query = query.Where(m => m.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.SponsorId))
        {
            query = query.Where(m => m.SponsorMemberId == request.SponsorId);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(m => m.FirstName.ToLower().Contains(term) ||
                                      m.LastName.ToLower().Contains(term) ||
                                      m.MemberId.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageItems = await query
            .OrderByDescending(m => m.CreationDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(m => new
            {
                m.MemberId, m.FirstName, m.LastName, m.Phone, m.Email,
                m.Country, m.Status, m.MemberType, m.EnrollDate,
                m.SponsorMemberId, m.ReplicateSiteSlug, m.CreationDate
            })
            .ToListAsync(cancellationToken);

        var memberIds  = pageItems.Select(m => m.MemberId).ToList();
        var sponsorIds = pageItems.Select(m => m.SponsorMemberId).Where(id => id != null).Distinct().ToList();

        // Resolve sponsor (enrollment tree) full names
        var sponsorNames = await _db.MemberProfiles.AsNoTracking()
            .Where(s => sponsorIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, FullName = s.FirstName + " " + s.LastName })
            .ToDictionaryAsync(s => s.MemberId, s => s.FullName.Trim(), cancellationToken);

        // Resolve dual team parent (upline in binary tree)
        var dualParentMap = await _db.DualTeamTree.AsNoTracking()
            .Where(d => memberIds.Contains(d.MemberId) && d.ParentMemberId != null)
            .Select(d => new { d.MemberId, d.ParentMemberId })
            .ToDictionaryAsync(d => d.MemberId, d => d.ParentMemberId!, cancellationToken);

        var dualParentIds = dualParentMap.Values.Distinct().ToList();
        var dualParentNames = await _db.MemberProfiles.AsNoTracking()
            .Where(p => dualParentIds.Contains(p.MemberId))
            .Select(p => new { p.MemberId, FullName = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(p => p.MemberId, p => p.FullName.Trim(), cancellationToken);

        // Resolve most recent active subscription (level name + expiration date) per member
        var subs = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => memberIds.Contains(s.MemberId))
            .OrderByDescending(s => s.CreationDate)
            .Select(s => new
            {
                s.MemberId,
                LevelName  = s.MembershipLevel != null ? s.MembershipLevel.Name : null,
                s.EndDate,
                s.SubscriptionStatus
            })
            .ToListAsync(cancellationToken);

        var subMap = subs
            .GroupBy(s => s.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => (int)s.SubscriptionStatus == 1 ? 1 : 0)
                       .ThenByDescending(s => s.EndDate)
                       .First());

        // Resolve lifetime rank (highest SortOrder ever achieved)
        var ranks = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => memberIds.Contains(r.MemberId))
            .Select(r => new { r.MemberId, RankName = r.RankDefinition != null ? r.RankDefinition.Name : null, SortOrder = r.RankDefinition != null ? r.RankDefinition.SortOrder : 0 })
            .ToListAsync(cancellationToken);

        var rankMap = ranks
            .GroupBy(r => r.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.SortOrder).First().RankName);

        var items = pageItems.Select(m =>
        {
            subMap.TryGetValue(m.MemberId, out var sub);
            return new AdminMemberDto
            {
                MemberId            = m.MemberId,
                FirstName           = m.FirstName,
                LastName            = m.LastName,
                Phone               = m.Phone,
                Email               = m.Email,
                Country             = m.Country,
                Status              = m.Status.ToString(),
                MemberType          = m.MemberType.ToString(),
                EnrollDate          = m.EnrollDate,
                ExpirationDate      = sub?.EndDate,
                SponsorMemberId         = m.SponsorMemberId,
                SponsorFullName         = m.SponsorMemberId != null && sponsorNames.TryGetValue(m.SponsorMemberId, out var n) ? n : null,
                DualTeamParentMemberId  = dualParentMap.TryGetValue(m.MemberId, out var pid) ? pid : null,
                DualTeamUplineFullName  = dualParentMap.TryGetValue(m.MemberId, out var upId) && dualParentNames.TryGetValue(upId, out var un) ? un : null,
                ReplicateSiteSlug   = m.ReplicateSiteSlug,
                MembershipLevelName = sub?.LevelName,
                LifetimeRankName    = rankMap.TryGetValue(m.MemberId, out var rk) ? rk : null,
                CreationDate        = m.CreationDate
            };
        }).ToList();

        var result = new PagedResult<AdminMemberDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        };

        await _cache.SetAsync(cacheKey, result, CacheKeys.AdminMembersTtl, cancellationToken);
        return Result<PagedResult<AdminMemberDto>>.Success(result);
    }

    /// <summary>
    /// Short, deterministic fingerprint of the filter trio so each combination
    /// gets its own cache slot without bloating the key with the raw search
    /// string. SHA256 truncated to 12 hex chars is plenty for cache buckets.
    /// </summary>
    private static string BuildFilterFingerprint(string? status, string? sponsor, string? search)
    {
        var raw = $"{status ?? ""}|{sponsor ?? ""}|{search ?? ""}";
        if (raw == "||") return "none";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes.AsSpan(0, 6)).ToLowerInvariant();
    }
}
