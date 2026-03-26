using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateFastStartBonus;

public class CalculateFastStartBonusHandler
    : IRequestHandler<CalculateFastStartBonusCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateFastStartBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateFastStartBonusCommand command, CancellationToken ct)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == command.OrderId && o.Status == Domain.Entities.Orders.OrderStatus.Completed, ct);

        if (order is null)
            return Result<CalculationResultResponse>.Failure(
                "ORDER_NOT_FOUND", $"Completed order '{command.OrderId}' not found.");

        // Verify this product triggers Fast Start Bonus
        var triggersFsb = await (
            from od in _db.OrderDetails.AsNoTracking()
            join pc in _db.ProductCommissions.AsNoTracking() on od.ProductId equals pc.ProductId.ToString()
            where od.OrderId == command.OrderId && pc.TriggerFastStartBonus
            select 1
        ).AnyAsync(ct);

        if (!triggersFsb)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "FastStartBonus",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = _dateTime.Now,
                SkippedReasons = new() { "No products in this order trigger Fast Start Bonus." }
            });

        // Load FSB commission types only (IsPaidOnSignup=true, NOT IsSponsorBonus).
        // TriggerOrder encodes the window number: 1=Window1 (14d), 2=Window2 (7d), 3=Window3 (7d).
        var fsbTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsPaidOnSignup && !t.IsSponsorBonus)
            .OrderBy(t => t.LevelNo)
            .ToListAsync(ct);

        if (fsbTypes.Count == 0)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "FastStartBonus",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = _dateTime.Now,
                SkippedReasons = new() { "No active Fast Start Bonus commission types configured." }
            });

        var now = _dateTime.Now;
        var skipped = new List<string>();
        var earnings = new List<CommissionEarning>();

        // Walk sponsor chain up to the max level defined in commission types
        var maxLevel = fsbTypes.Max(t => t.LevelNo);
        var currentMemberId = command.BuyerMemberId;

        for (int level = 1; level <= maxLevel; level++)
        {
            var currentMember = await _db.MemberProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MemberId == currentMemberId, ct);

            if (currentMember is null || string.IsNullOrEmpty(currentMember.SponsorMemberId)) break;

            var sponsorMemberId = currentMember.SponsorMemberId;

            // Find the FSB commission type that applies at this level
            var commType = fsbTypes.FirstOrDefault(t => t.LevelNo == level)
                        ?? fsbTypes.FirstOrDefault(t => t.LevelNo == 0); // fallback: LevelNo=0 means all levels

            if (commType is null) { currentMemberId = sponsorMemberId; continue; }

            // Determine which FSB window the sponsor is currently in
            var countdown = await _db.CommissionCountDowns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MemberId.ToString() == sponsorMemberId, ct);

            if (countdown is null)
            {
                skipped.Add($"Level {level}: sponsor {sponsorMemberId} has no FSB countdown record.");
                currentMemberId = sponsorMemberId;
                continue;
            }

            // TriggerOrder on CommissionType encodes the window: 1=Window1, 2=Window2, 3=Window3
            int activeWindow = GetActiveWindowNumber(now, countdown);
            if (activeWindow == 0)
            {
                skipped.Add($"Level {level}: sponsor {sponsorMemberId} — all FSB windows expired.");
                currentMemberId = sponsorMemberId;
                continue;
            }

            // Re-select the commission type matching the active window
            var windowType = fsbTypes.FirstOrDefault(t => t.LevelNo == level && t.TriggerOrder == activeWindow)
                          ?? fsbTypes.FirstOrDefault(t => t.LevelNo == level && t.TriggerOrder == 0)
                          ?? commType; // fallback to original match

            // Check for duplicate (idempotent)
            var alreadyExists = await _db.CommissionEarnings
                .AnyAsync(e => e.SourceOrderId == command.OrderId
                            && e.CommissionTypeId == windowType.Id
                            && e.BeneficiaryMemberId == sponsorMemberId, ct);

            if (alreadyExists)
            {
                skipped.Add($"Level {level}: FSB for order {command.OrderId} already recorded for {sponsorMemberId}.");
                currentMemberId = sponsorMemberId;
                continue;
            }

            // FixedAmount takes precedence over Percentage calculation
            var amount = windowType.FixedAmount
                         ?? Math.Round(order.TotalAmount * windowType.Percentage / 100, 2);
            var earning = BuildEarning(sponsorMemberId, command.BuyerMemberId, command.OrderId,
                windowType, amount, now);
            earnings.Add(earning);

            currentMemberId = sponsorMemberId;
        }

        if (earnings.Count > 0)
        {
            await _db.CommissionEarnings.AddRangeAsync(earnings, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType = "FastStartBonus",
            RecordsCreated = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate = now,
            SkippedReasons = skipped
        });
    }

    /// <summary>Returns the active FSB window number (1, 2, or 3), or 0 if no window is open.</summary>
    private static int GetActiveWindowNumber(DateTime now, Domain.Entities.Commission.MemberCommissionCountDown countdown)
    {
        if (now >= countdown.FastStartBonus1Start && now <= countdown.FastStartBonus1End) return 1;
        if (now >= countdown.FastStartBonus2Start && now <= countdown.FastStartBonus2End) return 2;
        if (now >= countdown.FastStartBonus3Start && now <= countdown.FastStartBonus3End) return 3;
        return 0;
    }

    private CommissionEarning BuildEarning(
        string beneficiaryMemberId, string sourceMemberId, string orderId,
        Domain.Entities.Commission.CommissionType commType,
        decimal amount, DateTime now)
        => new()
        {
            BeneficiaryMemberId = beneficiaryMemberId,
            SourceMemberId = sourceMemberId,
            SourceOrderId = orderId,
            CommissionTypeId = commType.Id,
            Amount = amount,
            Status = Domain.Enums.CommissionEarningStatus.Pending,
            EarnedDate = now,
            PaymentDate = now.AddDays(commType.PaymentDelayDays),
            PeriodDate = now.Date,
            CreatedBy = _currentUser.UserId,
            CreationDate = now,
            LastUpdateDate = now
        };
}
