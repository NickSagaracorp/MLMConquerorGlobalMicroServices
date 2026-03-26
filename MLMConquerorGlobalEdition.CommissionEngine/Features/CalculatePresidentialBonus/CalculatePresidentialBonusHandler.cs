using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculatePresidentialBonus;

public class CalculatePresidentialBonusHandler
    : IRequestHandler<CalculatePresidentialBonusCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculatePresidentialBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculatePresidentialBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;
        var monthStart = command.PeriodDate.HasValue
            ? new DateTime(command.PeriodDate.Value.Year, command.PeriodDate.Value.Month, 1)
            : new DateTime(now.Year, now.Month, 1);

        // Presidential types: LifeTimeRank > 0 means a minimum lifetime rank is required
        var presidentialTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.LifeTimeRank > 0 && !t.IsPaidOnSignup)
            .ToListAsync(ct);

        if (presidentialTypes.Count == 0)
            return Result<CalculationResultResponse>.Failure(
                "NO_PRESIDENTIAL_TYPES", "No active Presidential Bonus commission types configured.");

        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => presidentialTypes.Select(t => t.Id).Contains(e.CommissionTypeId)
                        && e.PeriodDate.HasValue
                        && e.PeriodDate.Value.Year == monthStart.Year
                        && e.PeriodDate.Value.Month == monthStart.Month, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Presidential Bonus for {monthStart:yyyy-MM} was already calculated.");

        // Get all ambassador member IDs with their highest achieved rank sort order
        var memberRanks = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(h => h.RankDefinition)
            .Where(h => !h.IsDeleted)
            .GroupBy(h => h.MemberId)
            .Select(g => new
            {
                MemberId = g.Key,
                HighestSortOrder = g.Max(h => h.RankDefinition!.SortOrder)
            })
            .ToDictionaryAsync(x => x.MemberId, x => x.HighestSortOrder, ct);

        // Total month network volume (sum of all completed orders this month)
        var monthEnd = monthStart.AddMonths(1);
        var monthVolume = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Status == Domain.Entities.Orders.OrderStatus.Completed
                     && o.OrderDate >= monthStart && o.OrderDate < monthEnd)
            .SumAsync(o => (decimal?)o.TotalAmount ?? 0, ct);

        var skipped = new List<string>();
        var earnings = new List<CommissionEarning>();

        foreach (var commType in presidentialTypes)
        {
            // Qualify members whose lifetime rank sort order >= required rank
            var qualifiedMembers = memberRanks
                .Where(kvp => kvp.Value >= commType.LifeTimeRank)
                .Select(kvp => kvp.Key)
                .ToList();

            if (qualifiedMembers.Count == 0)
            {
                skipped.Add($"{commType.Name}: no members meet LifeTimeRank {commType.LifeTimeRank}.");
                continue;
            }

            // Split monthly volume equally among qualified members (or use Percentage of volume)
            var amountPerMember = qualifiedMembers.Count > 0
                ? Math.Round(monthVolume * commType.Percentage / 100 / qualifiedMembers.Count, 2)
                : 0;

            if (amountPerMember <= 0)
            {
                skipped.Add($"{commType.Name}: calculated amount is zero (monthly volume={monthVolume}).");
                continue;
            }

            foreach (var memberId in qualifiedMembers)
            {
                earnings.Add(new CommissionEarning
                {
                    BeneficiaryMemberId = memberId,
                    CommissionTypeId = commType.Id,
                    Amount = amountPerMember,
                    Status = CommissionEarningStatus.Pending,
                    EarnedDate = now,
                    PaymentDate = now.AddDays(commType.PaymentDelayDays),
                    PeriodDate = monthStart,
                    Notes = $"Presidential Bonus pool share ({qualifiedMembers.Count} members, pool=${monthVolume * commType.Percentage / 100:F2})",
                    CreatedBy = _currentUser.UserId,
                    CreationDate = now,
                    LastUpdateDate = now
                });
            }
        }

        if (earnings.Count > 0)
        {
            await _db.CommissionEarnings.AddRangeAsync(earnings, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType = "PresidentialBonus",
            RecordsCreated = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate = monthStart,
            SkippedReasons = skipped
        });
    }
}
