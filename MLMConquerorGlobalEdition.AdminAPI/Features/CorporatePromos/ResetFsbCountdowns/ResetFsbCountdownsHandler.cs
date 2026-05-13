using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.ResetFsbCountdowns;

/// <summary>
/// Resets the FSB countdown for every eligible ambassador and stamps the
/// promo so the operation can never silently re-fire.
///
/// Eligibility (single pass over MemberCommissionCountDown joined to
/// MemberProfile + CommissionEarnings):
///   • member.Status != Terminated
///   • AND (
///       countdown's FSB3End is in the past (whole window already expired)
///       OR
///       member is still inside the first 14 days of EnrollDate AND has
///       NOT earned any FSB1 commission yet
///     )
///
/// Per-ambassador work, inside one transaction:
///   1. Snapshot the current MemberCommissionCountDown row into
///      MemberCommissionCountDownHistory (Reason = "promo:{id}").
///   2. Rewrite the live row's seven date columns anchored on now:
///         FSB1 [now, +7d]
///         FSB1Extended [now, +14d]
///         FSB2 [+7d, +14d]
///         FSB3 [+14d, +21d]
///   3. Stamp promo.ResetFsbCountdownExecutedAt = now once everything commits.
/// </summary>
public class ResetFsbCountdownsHandler
    : IRequestHandler<ResetFsbCountdownsCommand, Result<ResetFsbCountdownsResponse>>
{
    /// <summary>FSB1 commission category id — mirrors the commission engine constants.</summary>
    private const int FastStartBonusCategoryId = 2;

    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _dateTime;
    private readonly ICurrentUserService _currentUser;

    public ResetFsbCountdownsHandler(
        AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db          = db;
        _dateTime    = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<ResetFsbCountdownsResponse>> Handle(
        ResetFsbCountdownsCommand request, CancellationToken ct)
    {
        var promo = await _db.CorporatePromos
            .FirstOrDefaultAsync(p => p.Id == request.PromoId && !p.IsDeleted, ct);

        if (promo is null)
            return Result<ResetFsbCountdownsResponse>.Failure(
                "PROMO_NOT_FOUND", $"Corporate promo '{request.PromoId}' not found.");

        if (!promo.IsActive)
            return Result<ResetFsbCountdownsResponse>.Failure(
                "PROMO_NOT_ACTIVE", "Activate the promo before running the FSB reset.");

        if (!promo.ResetFsbCountdown)
            return Result<ResetFsbCountdownsResponse>.Failure(
                "RESET_NOT_ENABLED",
                "This promo does not have ResetFsbCountdown enabled.");

        if (promo.ResetFsbCountdownExecutedAt is not null)
            return Result<ResetFsbCountdownsResponse>.Failure(
                "ALREADY_EXECUTED",
                $"FSB reset already executed for this promo at {promo.ResetFsbCountdownExecutedAt:O}.");

        var now            = _dateTime.Now;
        var fourteenDaysAgo = now.AddDays(-14);

        // Pull every active candidate countdown — small enough table to
        // process in memory; heavy enough that we want the date math local.
        var rawCountdowns = await (
            from cd in _db.CommissionCountDowns
            join mp in _db.MemberProfiles
              on cd.MemberId equals mp.UserId
            where mp.Status != MemberAccountStatus.Terminated
            select new { CountDown = cd, Profile = mp }
        ).ToListAsync(ct);

        if (rawCountdowns.Count == 0)
        {
            promo.ResetFsbCountdownExecutedAt = now;
            promo.LastUpdateDate              = now;
            promo.LastUpdateBy                = _currentUser.UserId;
            await _db.SaveChangesAsync(ct);

            return Result<ResetFsbCountdownsResponse>.Success(new ResetFsbCountdownsResponse
            {
                PromoId          = promo.Id,
                AmbassadorsReset = 0,
                ArchivedRows     = 0,
                ExecutedAt       = now
            });
        }

        // FSB1 earnings already paid (any TriggerOrder=1 / Cat 2 row) for the
        // candidate set — used by the "still in first 14 days but no FSB1
        // yet" branch of the eligibility predicate. One round-trip.
        var candidateMemberIds = rawCountdowns.Select(x => x.Profile.MemberId).Distinct().ToList();

        var fsb1EarningMemberIds = await (
            from earning in _db.CommissionEarnings.AsNoTracking()
            join type    in _db.CommissionTypes on earning.CommissionTypeId equals type.Id
            where earning.BeneficiaryMemberId != null
               && candidateMemberIds.Contains(earning.BeneficiaryMemberId!)
               && earning.Status != CommissionEarningStatus.Cancelled
               && type.CommissionCategoryId == FastStartBonusCategoryId
               && type.TriggerOrder         == 1
            select earning.BeneficiaryMemberId!
        ).Distinct().ToHashSetAsync(ct);

        // Build the fresh window once — every candidate gets the same anchors.
        var fsb1Start         = now;
        var fsb1End           = now.AddDays(7);
        var fsb1ExtendedStart = now;
        var fsb1ExtendedEnd   = now.AddDays(14);
        var fsb2Start         = now.AddDays(7);
        var fsb2End           = now.AddDays(14);
        var fsb3Start         = now.AddDays(14);
        var fsb3End           = now.AddDays(21);

        var historiesToInsert = new List<MemberCommissionCountDownHistory>();
        var resetCount        = 0;

        foreach (var row in rawCountdowns)
        {
            var cd = row.CountDown;
            var enrollDate = row.Profile.EnrollDate;
            var hasFsb1    = fsb1EarningMemberIds.Contains(row.Profile.MemberId);

            // Case A — countdown already expired in full.
            var expired = cd.FastStartBonus3End < now;

            // Case B — still inside first 14 days post-enrollment AND no FSB1 yet.
            var withinFirst14DaysAndUnclaimed =
                enrollDate >= fourteenDaysAgo && !hasFsb1;

            if (!expired && !withinFirst14DaysAndUnclaimed) continue;

            historiesToInsert.Add(new MemberCommissionCountDownHistory
            {
                CountDownId                  = cd.Id,
                MemberId                     = cd.MemberId,
                Member                       = row.Profile,
                FastStartBonus1Start         = cd.FastStartBonus1Start,
                FastStartBonus1End           = cd.FastStartBonus1End,
                FastStartBonus1ExtendedStart = cd.FastStartBonus1ExtendedStart,
                FastStartBonus1ExtendedEnd   = cd.FastStartBonus1ExtendedEnd,
                FastStartBonus2Start         = cd.FastStartBonus2Start,
                FastStartBonus2End           = cd.FastStartBonus2End,
                FastStartBonus3Start         = cd.FastStartBonus3Start,
                FastStartBonus3End           = cd.FastStartBonus3End,
                Reason                       = $"promo:{promo.Id}",
                CreationDate                 = now,
                CreatedBy                    = _currentUser.UserId
            });

            cd.FastStartBonus1Start         = fsb1Start;
            cd.FastStartBonus1End           = fsb1End;
            cd.FastStartBonus1ExtendedStart = fsb1ExtendedStart;
            cd.FastStartBonus1ExtendedEnd   = fsb1ExtendedEnd;
            cd.FastStartBonus2Start         = fsb2Start;
            cd.FastStartBonus2End           = fsb2End;
            cd.FastStartBonus3Start         = fsb3Start;
            cd.FastStartBonus3End           = fsb3End;
            cd.LastUpdateDate               = now;
            cd.LastUpdateBy                 = _currentUser.UserId;

            resetCount++;
        }

        if (historiesToInsert.Count > 0)
            await _db.CommissionCountDownHistories.AddRangeAsync(historiesToInsert, ct);

        promo.ResetFsbCountdownExecutedAt = now;
        promo.LastUpdateDate              = now;
        promo.LastUpdateBy                = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<ResetFsbCountdownsResponse>.Success(new ResetFsbCountdownsResponse
        {
            PromoId          = promo.Id,
            AmbassadorsReset = resetCount,
            ArchivedRows     = historiesToInsert.Count,
            ExecutedAt       = now
        });
    }
}
