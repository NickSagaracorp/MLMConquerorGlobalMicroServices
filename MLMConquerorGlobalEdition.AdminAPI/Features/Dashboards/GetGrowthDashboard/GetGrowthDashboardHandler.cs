using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetGrowthDashboard;

public class GetGrowthDashboardHandler : IRequestHandler<GetGrowthDashboardQuery, Result<GrowthDashboardDto>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public GetGrowthDashboardHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<GrowthDashboardDto>> Handle(
        GetGrowthDashboardQuery request, CancellationToken cancellationToken)
    {
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

        return Result<GrowthDashboardDto>.Success(dto);
    }
}
