using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateMatchingBonus;

public class CalculateMatchingBonusHandler
    : IRequestHandler<CalculateMatchingBonusCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateMatchingBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateMatchingBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;
        var periodDate = (command.PeriodDate ?? now).Date;

        // Matching bonus types: ResidualBased + ResidualOverCommissionType > 0
        var matchingTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.ResidualBased && t.ResidualOverCommissionType > 0)
            .ToListAsync(ct);

        if (matchingTypes.Count == 0)
            return Result<CalculationResultResponse>.Failure(
                "NO_MATCHING_TYPES", "No active Matching Bonus commission types configured.");

        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => matchingTypes.Select(t => t.Id).Contains(e.CommissionTypeId)
                        && e.PeriodDate.HasValue && e.PeriodDate.Value.Date == periodDate, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Matching Bonus for period {periodDate:yyyy-MM-dd} was already calculated.");

        var skipped = new List<string>();
        var earnings = new List<CommissionEarning>();

        foreach (var matchType in matchingTypes)
        {
            // Load direct downline earnings for the target commission type on this period
            var downlineEarnings = await _db.CommissionEarnings
                .AsNoTracking()
                .Where(e => e.CommissionTypeId == matchType.ResidualOverCommissionType
                         && e.PeriodDate.HasValue && e.PeriodDate.Value.Date == periodDate
                         && !e.IsDeleted)
                .ToListAsync(ct);

            if (downlineEarnings.Count == 0)
            {
                skipped.Add($"{matchType.Name}: no base commissions found for type {matchType.ResidualOverCommissionType} on {periodDate:yyyy-MM-dd}.");
                continue;
            }

            // Group by beneficiary, then find their sponsor
            var downlineByMember = downlineEarnings
                .GroupBy(e => e.BeneficiaryMemberId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            var memberIds = downlineByMember.Keys.ToList();
            var sponsorMap = await _db.MemberProfiles
                .AsNoTracking()
                .Where(m => memberIds.Contains(m.MemberId) && m.SponsorMemberId != null)
                .Select(m => new { m.MemberId, m.SponsorMemberId })
                .ToDictionaryAsync(m => m.MemberId, m => m.SponsorMemberId!, ct);

            // Aggregate matching bonus per sponsor
            var sponsorTotals = new Dictionary<string, decimal>();
            foreach (var (downlineMemberId, earnedAmount) in downlineByMember)
            {
                if (!sponsorMap.TryGetValue(downlineMemberId, out var sponsorId)) continue;
                sponsorTotals.TryAdd(sponsorId, 0);
                sponsorTotals[sponsorId] += earnedAmount;
            }

            foreach (var (sponsorId, baseAmount) in sponsorTotals)
            {
                var matchAmount = Math.Round(baseAmount * (decimal)matchType.ResidualPercentage / 100, 2);
                if (matchAmount <= 0) continue;

                // Idempotency check per sponsor
                var exists = await _db.CommissionEarnings
                    .AnyAsync(e => e.BeneficiaryMemberId == sponsorId
                                && e.CommissionTypeId == matchType.Id
                                && e.PeriodDate.HasValue && e.PeriodDate.Value.Date == periodDate, ct);
                if (exists) continue;

                earnings.Add(new CommissionEarning
                {
                    BeneficiaryMemberId = sponsorId,
                    CommissionTypeId = matchType.Id,
                    Amount = matchAmount,
                    Status = CommissionEarningStatus.Pending,
                    EarnedDate = now,
                    PaymentDate = now.AddDays(matchType.PaymentDelayDays),
                    PeriodDate = periodDate,
                    Notes = $"Matching {matchType.ResidualPercentage}% on ${baseAmount:F2} downline earnings",
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
            CommissionType = "MatchingBonus",
            RecordsCreated = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate = periodDate,
            SkippedReasons = skipped
        });
    }
}
