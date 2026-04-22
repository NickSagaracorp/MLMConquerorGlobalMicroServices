using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculatePresidentialBonus;

public class CalculatePresidentialBonusHandler
    : IRequestHandler<CalculatePresidentialBonusCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculatePresidentialBonusHandler(
        AppDbContext        db,
        IDateTimeProvider   dateTime,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _dateTime    = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculatePresidentialBonusCommand command, CancellationToken ct)
    {
        var now        = _dateTime.Now;
        var monthStart = command.PeriodDate.HasValue
            ? new DateTime(command.PeriodDate.Value.Year, command.PeriodDate.Value.Month, 1)
            : new DateTime(now.Year, now.Month, 1);

        // Presidential Bonus types are identified by LifeTimeRank > 0
        var presidentialTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.LifeTimeRank > 0 && !t.IsPaidOnSignup)
            .ToListAsync(ct);

        if (presidentialTypes.Count == 0)
            return Result<CalculationResultResponse>.Failure(
                "NO_PRESIDENTIAL_TYPES",
                "No active Presidential Bonus commission types configured.");

        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => presidentialTypes.Select(t => t.Id).Contains(e.CommissionTypeId)
                        && e.PeriodDate.HasValue
                        && e.PeriodDate.Value.Year  == monthStart.Year
                        && e.PeriodDate.Value.Month == monthStart.Month, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Presidential Bonus for {monthStart:yyyy-MM} was already calculated.");

        // ── Dual team nodes: each ambassador's own node holds their L/R leg point totals ──
        var dualNodes = await _db.DualTeamTree
            .AsNoTracking()
            .ToListAsync(ct);

        // Map memberId → (leftLegPoints, rightLegPoints)
        var legsByMember = dualNodes
            .GroupBy(n => n.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.First()); // one node per member (their own position in the tree)

        // ── Member lifetime ranks ──
        var rankHistories = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(h => h.RankDefinition)
            .Where(h => !h.IsDeleted)
            .ToListAsync(ct);

        var memberLifetimeRank = rankHistories
            .GroupBy(h => h.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.Max(h => h.RankDefinition?.SortOrder ?? 0));

        var skipped  = new List<string>();
        var earnings = new List<CommissionEarning>();

        foreach (var commType in presidentialTypes)
        {
            var qualifiedMembers = memberLifetimeRank
                .Where(kvp => kvp.Value >= commType.LifeTimeRank)
                .Select(kvp => kvp.Key)
                .ToList();

            if (qualifiedMembers.Count == 0)
            {
                skipped.Add($"{commType.Name}: no members meet LifeTimeRank {commType.LifeTimeRank}.");
                continue;
            }

            foreach (var memberId in qualifiedMembers)
            {
                if (!legsByMember.TryGetValue(memberId, out var node))
                {
                    skipped.Add($"{commType.Name} — {memberId}: not placed in binary tree.");
                    continue;
                }

                var weakLeg = Math.Min(node.LeftLegPoints, node.RightLegPoints);

                if (weakLeg <= 0)
                {
                    skipped.Add($"{commType.Name} — {memberId}: weak leg = 0 (L={node.LeftLegPoints}, R={node.RightLegPoints}).");
                    continue;
                }

                // Commission = weak leg points × percentage rate
                var amount = Math.Round(weakLeg * commType.Percentage / 100m, 2);

                if (amount <= 0)
                {
                    skipped.Add($"{commType.Name} — {memberId}: calculated amount is zero.");
                    continue;
                }

                earnings.Add(new CommissionEarning
                {
                    BeneficiaryMemberId = memberId,
                    CommissionTypeId    = commType.Id,
                    Amount              = amount,
                    Status              = CommissionEarningStatus.Pending,
                    EarnedDate          = now,
                    PaymentDate         = now.AddDays(commType.PaymentDelayDays),
                    PeriodDate          = monthStart,
                    Notes               = $"Presidential Bonus — weak leg: {weakLeg} pts (L={node.LeftLegPoints}, R={node.RightLegPoints}), rate: {commType.Percentage}%",
                    CreatedBy           = _currentUser.UserId,
                    CreationDate        = now,
                    LastUpdateDate      = now
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
            CommissionType        = "PresidentialBonus",
            RecordsCreated        = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate            = monthStart,
            SkippedReasons        = skipped
        });
    }
}
