using System.Security.Cryptography;
using System.Text;
using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService       = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTeamMyTeam;

/// <summary>
/// BizCenter dual-tree "my team" endpoint. Delegates to the shared
/// <see cref="IDualTeamService"/>. Do NOT add query logic here.
///
/// Caches each (member, page, page-size, filter-fingerprint) tuple under the
/// <see cref="CacheKeys.DualTeamMyTeam"/> pattern for 5 minutes — the
/// downline shape and points are expensive to compute (multi-table join with
/// MemberStatistics + DualTeamTree + ranks) and rarely change minute-by-minute.
/// Callers can pass <c>BypassCache = true</c> via the controller's
/// <c>?bypassCache=true</c> query param to force a fresh read.
/// </summary>
public class GetDualTeamMyTeamHandler
    : IRequestHandler<GetDualTeamMyTeamQuery, Result<PagedResult<DualTeamMyTeamMemberDto>>>
{
    private readonly IDualTeamService    _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService       _cache;

    public GetDualTeamMyTeamHandler(
        IDualTeamService    service,
        ICurrentUserService currentUser,
        ICacheService       cache)
    {
        _service     = service;
        _currentUser = currentUser;
        _cache       = cache;
    }

    public async Task<Result<PagedResult<DualTeamMyTeamMemberDto>>> Handle(
        GetDualTeamMyTeamQuery request, CancellationToken ct)
    {
        var memberId    = _currentUser.MemberId;
        var fingerprint = BuildFilterFingerprint(request.Search, request.From, request.To);
        var cacheKey    = CacheKeys.DualTeamMyTeam(memberId, request.Page, request.PageSize, fingerprint);

        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<PagedResult<DualTeamMyTeamMemberDto>>(cacheKey, ct);
            if (cached is not null) return Result<PagedResult<DualTeamMyTeamMemberDto>>.Success(cached);
        }

        var view = await _service.GetMyTeamAsync(
            memberId, request.Page, request.PageSize,
            request.Search, request.From, request.To, ct);

        var mapped = new PagedResult<DualTeamMyTeamMemberDto>
        {
            TotalCount = view.TotalCount,
            Page       = view.Page,
            PageSize   = view.PageSize,
            Items      = view.Items.Select(MapToDto).ToList()
        };

        await _cache.SetAsync(cacheKey, mapped, CacheKeys.DualTeamMyTeamTtl, ct);
        return Result<PagedResult<DualTeamMyTeamMemberDto>>.Success(mapped);
    }

    /// <summary>
    /// Short, deterministic fingerprint of search + date filters so cache keys
    /// stay readable in Redis but each filter combo gets its own slot. SHA256
    /// truncated to 12 hex chars is enough collision protection for cache.
    /// </summary>
    private static string BuildFilterFingerprint(string? search, DateTime? from, DateTime? to)
    {
        var raw = $"{search ?? ""}|{from?.ToString("o") ?? ""}|{to?.ToString("o") ?? ""}";
        if (raw == "||") return "none";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes.AsSpan(0, 6)).ToLowerInvariant();
    }

    private static DualTeamMyTeamMemberDto MapToDto(DualTeamMyTeamMemberView v) => new()
    {
        MemberId             = v.MemberId,
        FullName             = v.FullName,
        Email                = v.Email,
        Phone                = v.Phone,
        Country              = v.Country,
        Level                = v.Level,
        Leg                  = v.Leg,
        EnrollDate           = v.EnrollDate,
        SponsorMemberId      = v.SponsorMemberId,
        SponsorFullName      = v.SponsorFullName,
        DualUplineMemberId   = v.DualUplineMemberId,
        DualUplineFullName   = v.DualUplineFullName,
        AccountStatus        = v.AccountStatus,
        MembershipStatus     = v.MembershipStatus,
        IsQualified          = v.IsQualified,
        MembershipLevelName  = v.MembershipLevelName,
        CurrentRankName      = v.CurrentRankName,
        RankDate             = v.RankDate,
        LifetimeRankName     = v.LifetimeRankName,
        NextRankPercent      = v.NextRankPercent,
        QualificationPoints  = v.QualificationPoints,
        EnrollmentTeamPoints = v.EnrollmentTeamPoints,
        LeftTeamPoints       = v.LeftTeamPoints,
        RightTeamPoints      = v.RightTeamPoints
    };
}
