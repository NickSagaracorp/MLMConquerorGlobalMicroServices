using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;

public class CalculateBoostBonusHandler
    : IRequestHandler<CalculateBoostBonusCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateBoostBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateBoostBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;
        // Week starts on Sunday
        var weekStart = command.PeriodDate.HasValue
            ? command.PeriodDate.Value.Date
            : now.Date.AddDays(-(int)now.DayOfWeek);

        // Boost types: TriggerOrder > 0, not signup/residual, not sponsor bonus.
        // Sorted by NewMembers descending so the first qualifying type is always the highest tier.
        // Per comp plan: if Gold AND Platinum both qualify, pay Platinum only.
        var boostTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && !t.IsPaidOnSignup && !t.ResidualBased
                     && !t.IsSponsorBonus && t.TriggerOrder > 0)
            .OrderByDescending(t => t.NewMembers)
            .ToListAsync(ct);

        if (boostTypes.Count == 0)
            return Result<CalculationResultResponse>.Failure(
                "NO_BOOST_TYPES", "No active Boost Bonus commission types configured.");

        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => boostTypes.Select(t => t.Id).Contains(e.CommissionTypeId)
                        && e.PeriodDate.HasValue && e.PeriodDate.Value.Date == weekStart, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Boost Bonus for week starting {weekStart:yyyy-MM-dd} was already calculated.");

        var weekEnd = weekStart.AddDays(7);

        var ambassadors = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberType == MemberType.Ambassador
                     && m.Status == Domain.Entities.Member.MemberAccountStatus.Active)
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        var dualNodes = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => ambassadors.Contains(d.MemberId))
            .ToDictionaryAsync(d => d.MemberId, ct);

        // Count new members enrolled this week per sponsor (for NewMembers threshold)
        var newMemberCounts = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.EnrollDate >= weekStart && m.EnrollDate < weekEnd
                     && ambassadors.Contains(m.SponsorMemberId!))
            .GroupBy(m => m.SponsorMemberId!)
            .Select(g => new { SponsorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SponsorId, x => x.Count, ct);

        var skipped = new List<string>();
        var earnings = new List<CommissionEarning>();

        foreach (var memberId in ambassadors)
        {
            dualNodes.TryGetValue(memberId, out var node);
            var teamPoints = node is not null
                ? Math.Min(node.LeftLegPoints, node.RightLegPoints) * 2
                : 0;
            newMemberCounts.TryGetValue(memberId, out var newMembers);

            // Find the HIGHEST tier the ambassador qualifies for (Gold or Platinum, never both).
            // boostTypes is already sorted by NewMembers descending.
            var qualifyingType = boostTypes.FirstOrDefault(commType =>
                (commType.TeamPoints  == 0 || teamPoints  >= commType.TeamPoints) &&
                (commType.NewMembers  == 0 || newMembers  >= commType.NewMembers));

            if (qualifyingType is null)
            {
                skipped.Add($"{memberId}: did not meet any Boost Bonus threshold (teamPts={teamPoints}, newMembers={newMembers}).");
                continue;
            }

            var amount = qualifyingType.FixedAmount
                         ?? Math.Round(teamPoints * qualifyingType.Percentage / 100, 2);
            if (amount <= 0)
            {
                skipped.Add($"{memberId}: calculated amount is 0 for {qualifyingType.Name}.");
                continue;
            }

            earnings.Add(new CommissionEarning
            {
                BeneficiaryMemberId = memberId,
                CommissionTypeId = qualifyingType.Id,
                Amount = amount,
                Status = CommissionEarningStatus.Pending,
                EarnedDate = now,
                PaymentDate = now.AddDays(qualifyingType.PaymentDelayDays),
                PeriodDate = weekStart,
                CreatedBy = _currentUser.UserId,
                CreationDate = now,
                LastUpdateDate = now
            });
        }

        if (earnings.Count > 0)
        {
            await _db.CommissionEarnings.AddRangeAsync(earnings, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType = "BoostBonus",
            RecordsCreated = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate = weekStart,
            SkippedReasons = skipped
        });
    }
}
