using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusWeekStats;

public class GetBoostBonusWeekStatsHandler : IRequestHandler<GetBoostBonusWeekStatsQuery, Result<BoostBonusWeekStatsDto>>
{
    private const int BoostBonusCategoryId = 4;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public GetBoostBonusWeekStatsHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<BoostBonusWeekStatsDto>> Handle(GetBoostBonusWeekStatsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var today    = _dateTime.UtcNow;

        // ISO week: Monday = start of week
        var dayOfWeek  = (int)today.DayOfWeek;
        var daysToMon  = dayOfWeek == 0 ? -6 : -(dayOfWeek - 1);
        var weekStart  = today.AddDays(daysToMon).Date;
        var weekEnd    = weekStart.AddDays(6).Date;

        var weekLabel = $"Week of {weekStart:MMM d} - {weekEnd:MMM d}";

        var earningsThisWeek = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.EarnedDate >= weekStart
                     && c.EarnedDate <= weekEnd.AddDays(1))
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == BoostBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => ct2.Name)
            .ToListAsync(ct);

        var goldCount     = earningsThisWeek.Count(n => n.Contains("Gold",     StringComparison.OrdinalIgnoreCase));
        var platinumCount = earningsThisWeek.Count(n => n.Contains("Platinum", StringComparison.OrdinalIgnoreCase));

        var dto = new BoostBonusWeekStatsDto
        {
            WeekLabel      = weekLabel,
            GoldCount      = goldCount,
            GoldTarget     = 36,
            PlatinumCount  = platinumCount,
            PlatinumTarget = 72
        };

        return Result<BoostBonusWeekStatsDto>.Success(dto);
    }
}
