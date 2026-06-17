using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMemberStats;

/// <summary>
/// Computes the four headline counters that sit above the AdminWeb Members
/// grid: total active members, new signups in the last 24h, cancellations
/// in the last 24h, and binary-tree placements in the last 24h. Cached for
/// ~30 seconds keyed at <c>admin:member-stats</c> so repeated page loads /
/// "Refresh" clicks don't hammer the DB. Bypass with <c>BypassCache = true</c>.
/// </summary>
public class GetMemberStatsHandler : IRequestHandler<GetMemberStatsQuery, Result<MemberStatsDto>>
{
    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService     _cache;

    public GetMemberStatsHandler(AppDbContext db, IDateTimeProvider dateTime, ICacheService cache)
    {
        _db       = db;
        _dateTime = dateTime;
        _cache    = cache;
    }

    public async Task<Result<MemberStatsDto>> Handle(
        GetMemberStatsQuery request, CancellationToken ct)
    {
        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<MemberStatsDto>(CacheKeys.AdminMemberStats, ct);
            if (cached is not null) return Result<MemberStatsDto>.Success(cached);
        }

        // IDateTimeProvider per CLAUDE.md — never DateTime.UtcNow directly.
        var now           = _dateTime.Now;
        var windowStart   = now.AddHours(-24);

        // Cancellation signal — see XML doc on MemberStatsDto.CancellationsLast24Hours
        // for the full rationale. Anything in MemberStatusHistory in the last 24h
        // whose NewStatus is one of the three "off" states counts as a cancellation.
        var cancellationStatuses = new[]
        {
            MemberAccountStatus.Inactive,
            MemberAccountStatus.Suspended,
            MemberAccountStatus.Terminated
        };

        var totalActive = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.Status == MemberAccountStatus.Active)
            .CountAsync(ct);

        var newSignups = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.EnrollDate >= windowStart)
            .CountAsync(ct);

        var cancellations = await _db.MemberStatusHistories
            .AsNoTracking()
            .Where(h => h.ChangedAt >= windowStart && cancellationStatuses.Contains(h.NewStatus))
            .CountAsync(ct);

        var placements = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => d.CreationDate >= windowStart)
            .CountAsync(ct);

        var dto = new MemberStatsDto
        {
            TotalActive              = totalActive,
            NewSignupsLast24Hours    = newSignups,
            CancellationsLast24Hours = cancellations,
            PlacementsLast24Hours    = placements
        };

        await _cache.SetAsync(CacheKeys.AdminMemberStats, dto, CacheKeys.AdminMemberStatsTtl, ct);
        return Result<MemberStatsDto>.Success(dto);
    }
}
