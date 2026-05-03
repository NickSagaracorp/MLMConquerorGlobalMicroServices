using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetGrowthDashboard;

/// <summary>
/// Growth executive dashboard. Member count + signup velocity. Cached
/// globally for 3 minutes; pass <c>BypassCache = true</c> to recompute.
/// </summary>
public class GetGrowthDashboardHandler : IRequestHandler<GetGrowthDashboardQuery, Result<GrowthDashboardDto>>
{
    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService     _cache;

    public GetGrowthDashboardHandler(AppDbContext db, IDateTimeProvider dateTime, ICacheService cache)
    {
        _db       = db;
        _dateTime = dateTime;
        _cache    = cache;
    }

    public async Task<Result<GrowthDashboardDto>> Handle(
        GetGrowthDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<GrowthDashboardDto>(CacheKeys.AdminGrowthDashboard, cancellationToken);
            if (cached is not null) return Result<GrowthDashboardDto>.Success(cached);
        }

        var now = _dateTime.Now;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfWeek = now.AddDays(-7);

        var totalMembers = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var newThisMonth = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.EnrollDate >= startOfMonth, cancellationToken);

        var newThisWeek = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.EnrollDate >= startOfWeek, cancellationToken);

        var dto = new GrowthDashboardDto
        {
            TotalMembers = totalMembers,
            NewMembersThisMonth = newThisMonth,
            NewMembersThisWeek = newThisWeek
        };

        await _cache.SetAsync(CacheKeys.AdminGrowthDashboard, dto, CacheKeys.AdminGrowthDashboardTtl, cancellationToken);
        return Result<GrowthDashboardDto>.Success(dto);
    }
}
