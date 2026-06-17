using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;

/// <summary>
/// Paginated admin member list with sponsor / dual-upline / membership / rank joins.
/// All columns — including the derived name columns — are projected into a single
/// queryable BEFORE paging, so the grid's search, per-column filters and sorting run
/// server-side across the WHOLE member set (not just the loaded page). Cached for
/// 2 minutes per page+filter+sort+search combination; pass <c>BypassCache = true</c>
/// (surfaced as <c>?bypassCache=true</c>) when the admin clicks "Refresh".
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

    /// <summary>
    /// Maximum rows per page the admin members listing will return. Higher values
    /// explode the multi-subquery projection cost and overload the Syncfusion grid.
    /// Mirrored by the AdminWeb Members page-size options.
    /// </summary>
    private const int MaxPageSize = 100;

    /// <summary>String columns the free-text search box matches against.</summary>
    private static readonly string[] SearchableFields =
    {
        nameof(AdminMemberDto.MemberId),
        nameof(AdminMemberDto.FirstName),
        nameof(AdminMemberDto.LastName),
        nameof(AdminMemberDto.Email),
        nameof(AdminMemberDto.Phone),
        nameof(AdminMemberDto.Country),
        nameof(AdminMemberDto.ReplicateSiteSlug),
        nameof(AdminMemberDto.SponsorFullName),
        nameof(AdminMemberDto.DualTeamUplineFullName),
        nameof(AdminMemberDto.MembershipLevelName),
        nameof(AdminMemberDto.LifetimeRankName),
    };

    public async Task<Result<PagedResult<AdminMemberDto>>> Handle(
        GetMembersQuery request, CancellationToken cancellationToken)
    {
        // Build the grid request from the query. Default to a deterministic
        // newest-first sort when the caller supplies no explicit sort.
        var grid = new GridDataRequest
        {
            Page     = Math.Max(1, request.Page.Page),
            PageSize = request.Page.PageSize,
            Search   = request.SearchTerm,
            Sorts    = request.Sorts is { Count: > 0 }
                ? request.Sorts
                : new List<GridSort> { new() { Field = nameof(AdminMemberDto.CreationDate), Direction = "desc" } },
            Filters  = request.Filters ?? new List<GridFilter>()
        };

        var fingerprint = BuildFilterFingerprint(request.StatusFilter, request.SponsorId, grid);
        var cacheKey    = CacheKeys.AdminMembers(grid.Page, Math.Clamp(grid.PageSize <= 0 ? 20 : grid.PageSize, 1, MaxPageSize), fingerprint);

        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<PagedResult<AdminMemberDto>>(cacheKey, cancellationToken);
            if (cached is not null) return Result<PagedResult<AdminMemberDto>>.Success(cached);
        }

        var baseQuery = _db.MemberProfiles.AsNoTracking();

        // Explicit status / sponsor pre-filters (the AdminWeb status dropdown and
        // sponsor drill-down) are applied before projection so they compose with
        // any grid column filters the user also sets.
        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<MemberAccountStatus>(request.StatusFilter, true, out var statusEnum))
        {
            baseQuery = baseQuery.Where(m => m.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.SponsorId))
        {
            baseQuery = baseQuery.Where(m => m.SponsorMemberId == request.SponsorId);
        }

        // Single projection: derived name columns are resolved via correlated
        // subqueries so the grid can filter and sort on them server-side.
        var projected = baseQuery.Select(m => new AdminMemberDto
        {
            MemberId   = m.MemberId,
            FirstName  = m.FirstName,
            LastName   = m.LastName,
            Phone      = m.Phone,
            Email      = m.Email,
            Country    = m.Country,
            Status     = m.Status.ToString(),
            MemberType = m.MemberType.ToString(),
            EnrollDate = m.EnrollDate,

            ExpirationDate = _db.MembershipSubscriptions
                .Where(s => s.MemberId == m.MemberId)
                .OrderByDescending(s => (int)s.SubscriptionStatus == 1)
                .ThenByDescending(s => s.EndDate)
                .Select(s => (DateTime?)s.EndDate)
                .FirstOrDefault(),

            SponsorMemberId = m.SponsorMemberId,
            SponsorFullName = _db.MemberProfiles
                .Where(s => s.MemberId == m.SponsorMemberId)
                .Select(s => (s.FirstName + " " + s.LastName).Trim())
                .FirstOrDefault(),

            DualTeamParentMemberId = _db.DualTeamTree
                .Where(d => d.MemberId == m.MemberId)
                .Select(d => d.ParentMemberId)
                .FirstOrDefault(),
            DualTeamUplineFullName = _db.DualTeamTree
                .Where(d => d.MemberId == m.MemberId && d.ParentMemberId != null)
                .Select(d => _db.MemberProfiles
                    .Where(p => p.MemberId == d.ParentMemberId)
                    .Select(p => (p.FirstName + " " + p.LastName).Trim())
                    .FirstOrDefault())
                .FirstOrDefault(),

            ReplicateSiteSlug = m.ReplicateSiteSlug,

            MembershipLevelName = _db.MembershipSubscriptions
                .Where(s => s.MemberId == m.MemberId)
                .OrderByDescending(s => (int)s.SubscriptionStatus == 1)
                .ThenByDescending(s => s.EndDate)
                .Select(s => s.MembershipLevel != null ? s.MembershipLevel.Name : null)
                .FirstOrDefault(),

            LifetimeRankName = _db.MemberRankHistories
                .Where(r => r.MemberId == m.MemberId)
                .OrderByDescending(r => r.RankDefinition!.SortOrder)
                .Select(r => r.RankDefinition!.Name)
                .FirstOrDefault(),

            CreationDate = m.CreationDate
        });

        var result = await projected.ToGridResultAsync(grid, SearchableFields, MaxPageSize, cancellationToken);

        await _cache.SetAsync(cacheKey, result, CacheKeys.AdminMembersTtl, cancellationToken);
        return Result<PagedResult<AdminMemberDto>>.Success(result);
    }

    /// <summary>
    /// Deterministic fingerprint of the full filter/sort/search state so each grid
    /// view gets its own cache slot. SHA256 truncated to 12 hex chars is plenty.
    /// </summary>
    private static string BuildFilterFingerprint(string? status, string? sponsor, GridDataRequest grid)
    {
        var sortsRaw   = string.Join(";", grid.Sorts.Select(s => $"{s.Field}:{s.Direction}"));
        var filtersRaw = string.Join(";", grid.Filters.Select(f => $"{f.Field}:{f.Operator}:{f.Value}:{f.Logic}"));
        var raw        = $"{status ?? ""}|{sponsor ?? ""}|{grid.Search ?? ""}|{sortsRaw}|{filtersRaw}";
        if (raw == "||||") return "none";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes.AsSpan(0, 6)).ToLowerInvariant();
    }
}
