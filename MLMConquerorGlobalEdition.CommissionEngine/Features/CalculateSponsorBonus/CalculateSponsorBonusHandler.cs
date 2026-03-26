using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateSponsorBonus;

public class CalculateSponsorBonusHandler
    : IRequestHandler<CalculateSponsorBonusCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateSponsorBonusHandler(
        AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateSponsorBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // ── Load order ────────────────────────────────────────────────────────
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == command.OrderId
                                   && o.MemberId == command.NewMemberId
                                   && o.Status == Domain.Entities.Orders.OrderStatus.Completed, ct);

        if (order is null)
            return Result<CalculationResultResponse>.Failure(
                "ORDER_NOT_FOUND",
                $"Completed order '{command.OrderId}' for member '{command.NewMemberId}' not found.");

        // ── Load new member's sponsor ─────────────────────────────────────────
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == command.NewMemberId, ct);

        if (member is null)
            return Result<CalculationResultResponse>.Failure(
                "MEMBER_NOT_FOUND", $"Member '{command.NewMemberId}' not found.");

        if (string.IsNullOrEmpty(member.SponsorMemberId))
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "SponsorBonus",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = now,
                SkippedReasons = new() { "Member has no sponsor — no sponsor bonus to award." }
            });

        // ── Resolve membership level from the order's product ─────────────────
        // The Member Bonus amount differs by tier (VIP=$20, Elite=$40, Turbo=$80).
        // CommissionType.LevelNo matches MembershipLevel.Id.
        // Lifestyle Ambassador (LevelNo=1) does not trigger a Member Bonus.
        var membershipLevelId = await (
            from od in _db.OrderDetails.AsNoTracking()
            join p in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where od.OrderId == command.OrderId && p.MembershipLevelId.HasValue
            select p.MembershipLevelId!.Value
        ).FirstOrDefaultAsync(ct);

        if (membershipLevelId <= 1)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "SponsorBonus",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = now,
                SkippedReasons = new()
                {
                    "Lifestyle Ambassador signup does not trigger a Member Bonus."
                }
            });

        // ── Select the tier-specific sponsor bonus commission type ────────────
        var commType = await _db.CommissionTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.IsSponsorBonus && t.LevelNo == membershipLevelId, ct);

        if (commType is null)
            return Result<CalculationResultResponse>.Failure(
                "NO_SPONSOR_BONUS_TYPE",
                $"No active sponsor bonus commission type configured for membership level {membershipLevelId}.");

        // ── Idempotency ───────────────────────────────────────────────────────
        var alreadyExists = await _db.CommissionEarnings
            .AnyAsync(e => e.SourceOrderId == command.OrderId
                        && e.CommissionTypeId == commType.Id
                        && e.BeneficiaryMemberId == member.SponsorMemberId, ct);

        if (alreadyExists)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "SponsorBonus",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = now,
                SkippedReasons = new()
                {
                    $"Sponsor bonus for order '{command.OrderId}' already recorded."
                }
            });

        // ── Calculate and persist ─────────────────────────────────────────────
        // Use FixedAmount when set (comp plan: VIP=$20, Elite=$40, Turbo=$80).
        var amount = commType.FixedAmount
            ?? Math.Round(order.TotalAmount * commType.Percentage / 100, 2);

        var earning = new CommissionEarning
        {
            BeneficiaryMemberId = member.SponsorMemberId,
            SourceMemberId = command.NewMemberId,
            SourceOrderId = command.OrderId,
            CommissionTypeId = commType.Id,
            Amount = amount,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = now,
            PaymentDate = now.AddDays(commType.PaymentDelayDays),
            PeriodDate = now.Date,
            CreatedBy = _currentUser.UserId,
            CreationDate = now,
            LastUpdateDate = now
        };

        await _db.CommissionEarnings.AddAsync(earning, ct);
        await _db.SaveChangesAsync(ct);

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType = "SponsorBonus",
            RecordsCreated = 1,
            TotalAmountCalculated = amount,
            PeriodDate = now
        });
    }
}
