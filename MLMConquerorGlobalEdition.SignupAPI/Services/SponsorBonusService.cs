using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public class SponsorBonusService : ISponsorBonusService
{
    private readonly AppDbContext _db;

    public SponsorBonusService(AppDbContext db) => _db = db;

    public async Task ComputeAsync(
        string? sponsorMemberId,
        string newMemberId,
        string orderId,
        decimal orderTotal,
        string createdBy,
        DateTime now,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sponsorMemberId)) return;

        // Resolve membership level from the order's product so we pick the correct tier.
        // CommissionType.LevelNo matches MembershipLevel.Id: 2=VIP, 3=Elite, 4=Turbo.
        // Lifestyle Ambassador (LevelNo=1) has no Member Bonus.
        var membershipLevelId = await (
            from od in _db.OrderDetails.AsNoTracking()
            join p in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where od.OrderId == orderId && p.MembershipLevelId.HasValue
            select p.MembershipLevelId!.Value
        ).FirstOrDefaultAsync(ct);

        if (membershipLevelId <= 1) return; // no bonus for Lifestyle Ambassador

        var commType = await _db.CommissionTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.IsSponsorBonus && t.LevelNo == membershipLevelId, ct);

        if (commType is null) return;

        // Idempotency guard
        var alreadyExists = await _db.CommissionEarnings
            .AnyAsync(e => e.SourceOrderId == orderId
                        && e.CommissionTypeId == commType.Id
                        && e.BeneficiaryMemberId == sponsorMemberId, ct);
        if (alreadyExists) return;

        // Use FixedAmount when set (VIP=$20, Elite=$40, Turbo=$80); fall back to percentage.
        var amount = commType.FixedAmount
            ?? Math.Round(orderTotal * commType.Percentage / 100, 2);
        if (amount <= 0) return;

        await _db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = sponsorMemberId,
            SourceMemberId = newMemberId,
            SourceOrderId = orderId,
            CommissionTypeId = commType.Id,
            Amount = amount,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = now,
            PaymentDate = now.AddDays(commType.PaymentDelayDays),
            PeriodDate = now.Date,
            CreatedBy = createdBy,
            CreationDate = now,
            LastUpdateDate = now
        }, ct);
    }

    public async Task TryReverseAsync(
        string cancelledMemberId,
        string signupOrderId,
        string? reason,
        DateTime now,
        string actorId,
        CancellationToken ct)
    {
        // Find the sponsor bonus commission type IDs so we can locate the original earning
        var sponsorBonusTypeIds = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsSponsorBonus)
            .Select(t => new { t.Id, t.ReverseId, t.PaymentDelayDays })
            .ToListAsync(ct);

        if (sponsorBonusTypeIds.Count == 0) return;

        var typeIds = sponsorBonusTypeIds.Select(t => t.Id).ToList();

        // The earning's SourceMemberId = the cancelled member; SourceOrderId = their signup order
        var earning = await _db.CommissionEarnings
            .FirstOrDefaultAsync(e => e.SourceOrderId == signupOrderId
                                   && e.SourceMemberId == cancelledMemberId
                                   && typeIds.Contains(e.CommissionTypeId), ct);

        if (earning is null) return;

        var reverseNote = reason?.Trim().Length > 0
            ? reason
            : "Cancellation within 14-day window.";

        if (earning.Status == CommissionEarningStatus.Pending)
        {
            // Domain method — marks as Cancelled and sets Notes
            earning.Cancel(reverseNote);
            earning.LastUpdateBy = actorId;
        }
        else if (earning.Status == CommissionEarningStatus.Paid)
        {
            var originalType = sponsorBonusTypeIds.First(t => t.Id == earning.CommissionTypeId);
            if (originalType.ReverseId == 0) return; // no reversal type configured

            var reverseType = await _db.CommissionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == originalType.ReverseId && t.IsActive, ct);
            if (reverseType is null) return;

            // Idempotency: skip if a reversal was already created for this order
            var reversalExists = await _db.CommissionEarnings
                .AnyAsync(e => e.SourceOrderId == signupOrderId
                            && e.SourceMemberId == cancelledMemberId
                            && e.CommissionTypeId == originalType.ReverseId, ct);
            if (reversalExists) return;

            await _db.CommissionEarnings.AddAsync(new CommissionEarning
            {
                BeneficiaryMemberId = earning.BeneficiaryMemberId,
                SourceMemberId = cancelledMemberId,
                SourceOrderId = signupOrderId,
                CommissionTypeId = originalType.ReverseId,
                Amount = -earning.Amount,   // negative amount = deduction from sponsor balance
                Status = CommissionEarningStatus.Pending,
                EarnedDate = now,
                PaymentDate = now.AddDays(reverseType.PaymentDelayDays),
                PeriodDate = now.Date,
                Notes = $"Reversal of sponsor bonus (earning {earning.Id}) — {reverseNote}",
                CreatedBy = actorId,
                CreationDate = now,
                LastUpdateDate = now
            }, ct);
        }
        // Status = Cancelled → already reversed, skip silently
    }
}
