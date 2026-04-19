using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.BackfillFsbCountdowns;

public class BackfillFsbCountdownsHandler : IRequestHandler<BackfillFsbCountdownsCommand, Result<BackfillFsbCountdownsResult>>
{
    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;

    public BackfillFsbCountdownsHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db       = db;
        _dateTime = dateTime;
    }

    public async Task<Result<BackfillFsbCountdownsResult>> Handle(
        BackfillFsbCountdownsCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Load all ambassadors that have no countdown record yet
        var existingMemberIds = await _db.CommissionCountDowns
            .AsNoTracking()
            .Select(c => c.MemberId)
            .ToHashSetAsync(ct);

        var ambassadors = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberType == MemberType.Ambassador && !existingMemberIds.Contains(m.UserId))
            .Select(m => new { m.UserId, m.MemberId, m.EnrollDate, m.Email })
            .ToListAsync(ct);

        if (ambassadors.Count == 0)
            return Result<BackfillFsbCountdownsResult>.Success(new BackfillFsbCountdownsResult
            {
                Created = 0,
                Skipped = 0,
                Details = new() { "All ambassadors already have FSB countdown records." }
            });

        // Load MemberProfile navigation objects needed by the required Member property
        var userIds = ambassadors.Select(a => a.UserId).ToList();
        var profiles = await _db.MemberProfiles
            .Where(m => userIds.Contains(m.UserId))
            .ToDictionaryAsync(m => m.UserId, ct);

        var toCreate = ambassadors
            .Where(a => profiles.ContainsKey(a.UserId))
            .Select(a =>
            {
                var enrollDate = a.EnrollDate;
                return new MemberCommissionCountDown
                {
                    MemberId                     = a.UserId,
                    Member                       = profiles[a.UserId],
                    FastStartBonus1Start         = enrollDate,
                    FastStartBonus1End           = enrollDate.AddDays(7),
                    FastStartBonus1ExtendedStart = enrollDate,
                    FastStartBonus1ExtendedEnd   = enrollDate.AddDays(14),
                    FastStartBonus2Start         = enrollDate.AddDays(7),
                    FastStartBonus2End           = enrollDate.AddDays(14),
                    FastStartBonus3Start         = enrollDate.AddDays(14),
                    FastStartBonus3End           = enrollDate.AddDays(21),
                    CreatedBy                    = "admin-backfill",
                    CreationDate                 = now,
                    LastUpdateDate               = now
                };
            }).ToList();

        await _db.CommissionCountDowns.AddRangeAsync(toCreate, ct);
        await _db.SaveChangesAsync(ct);

        var details = ambassadors
            .Select(a => $"{a.MemberId} — EnrollDate: {a.EnrollDate:yyyy-MM-dd} | W1: {a.EnrollDate:MM/dd} – {a.EnrollDate.AddDays(7):MM/dd} | Extended: {a.EnrollDate:MM/dd} – {a.EnrollDate.AddDays(14):MM/dd}")
            .ToList();

        return Result<BackfillFsbCountdownsResult>.Success(new BackfillFsbCountdownsResult
        {
            Created = toCreate.Count,
            Skipped = 0,
            Details = details
        });
    }
}
