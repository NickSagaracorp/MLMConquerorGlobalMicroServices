using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusSummary;

public class GetFastStartBonusSummaryHandler : IRequestHandler<GetFastStartBonusSummaryQuery, Result<CommissionBonusSummaryDto>>
{
    private const int FastStartBonusCategoryId = 2;

    private readonly AppDbContext                              _db;
    private readonly MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService _currentUser;
    private readonly MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider   _dateTime;

    public GetFastStartBonusSummaryHandler(
        AppDbContext db,
        MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService currentUser,
        MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<CommissionBonusSummaryDto>> Handle(GetFastStartBonusSummaryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var now      = _dateTime.Now;

        // Aggregate FSB earnings (count + total)
        var earningsAgg = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { c.Amount, ct2.TriggerOrder })
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), TotalAmount = g.Sum(x => x.Amount) })
            .FirstOrDefaultAsync(ct);

        // Load the member's FSB countdown record via UserId
        var memberUserId = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(ct);

        var countdown = memberUserId != default
            ? await _db.CommissionCountDowns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MemberId == memberUserId, ct)
            : null;

        // Build per-window earnings lookup (TriggerOrder 1/2/3 = window number)
        var earnedByWindow = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status != CommissionEarningStatus.Cancelled)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { ct2.TriggerOrder, c.Amount })
            .GroupBy(x => x.TriggerOrder)
            .Select(g => new { WindowNumber = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        List<FsbWindowDto>? windows = null;
        if (countdown is not null)
        {
            // Normal windows: W1=7d, W2=7d, W3=7d
            var normalDefs = new[]
            {
                new { Num = 1, Start = countdown.FastStartBonus1Start,  End = countdown.FastStartBonus1End  },
                new { Num = 2, Start = countdown.FastStartBonus2Start,  End = countdown.FastStartBonus2End  },
                new { Num = 3, Start = countdown.FastStartBonus3Start,  End = countdown.FastStartBonus3End  },
            };

            // Extended/promo: only Window 1 Extended (14d), Windows 2 & 3 same as normal
            var promoDefs = new[]
            {
                new { Num = 1, Start = countdown.FastStartBonus1ExtendedStart, End = countdown.FastStartBonus1ExtendedEnd },
                new { Num = 2, Start = countdown.FastStartBonus2Start,         End = countdown.FastStartBonus2End         },
                new { Num = 3, Start = countdown.FastStartBonus3Start,         End = countdown.FastStartBonus3End         },
            };

            FsbWindowDto BuildWindow(int num, bool isPromo, DateTime start, DateTime end)
            {
                var earned      = earnedByWindow.FirstOrDefault(e => e.WindowNumber == num);
                var isCompleted = now > end;
                var isActive    = !isCompleted && now >= start && now <= end;
                return new FsbWindowDto
                {
                    WindowNumber = num,
                    IsPromo      = isPromo,
                    Amount       = earned?.Amount ?? 0m,
                    IsCompleted  = isCompleted,
                    IsActive     = isActive,
                    StartDate    = start,
                    EndDate      = end
                };
            }

            windows = normalDefs.Select(w => BuildWindow(w.Num, false, w.Start, w.End))
                .Concat(promoDefs.Select(w => BuildWindow(w.Num, true, w.Start, w.End)))
                .ToList();
        }

        return Result<CommissionBonusSummaryDto>.Success(new CommissionBonusSummaryDto
        {
            Count       = earningsAgg?.Count       ?? 0,
            TotalAmount = earningsAgg?.TotalAmount ?? 0m,
            Windows     = windows
        });
    }
}
