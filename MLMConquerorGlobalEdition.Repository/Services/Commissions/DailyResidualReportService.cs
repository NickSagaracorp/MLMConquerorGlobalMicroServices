using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <inheritdoc />
public sealed class DailyResidualReportService : IDailyResidualReportService
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize     = 100;
    public const int MaxYears        = 15;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public DailyResidualReportService(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<DailyResidualHistoryView> GetHistoryAsync(
        string memberId, int? year, int? month, int page, int pageSize, CancellationToken ct = default)
    {
        var now   = _clock.Now;
        var years = await AvailableYearsAsync(memberId, now, ct);

        var y    = year is int requested && years.Contains(requested) ? requested : now.Year;
        int? m   = month is >= 1 and <= 12 ? month : null;
        var from = new DateTime(y, m ?? 1, 1);
        var to   = m is null ? from.AddYears(1) : from.AddMonths(1);

        pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);
        page     = Math.Max(1, page);

        var query = _db.DailyResidualEarnings.AsNoTracking()
            .Where(e => e.BeneficiaryMemberId == memberId
                     && e.EarnedDate >= from
                     && e.EarnedDate <  to
                     && e.Status     != CommissionEarningStatus.Cancelled);

        var sums = await query
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var items = await query
            .OrderByDescending(e => e.EarnedDate).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new DailyResidualRowView
            {
                Id             = e.Id,
                EarnedDate     = e.EarnedDate,
                Amount         = e.Amount,
                Status         = e.Status.ToString(),
                PaymentDate    = e.PaymentDate,
                EligiblePoints = e.EligibleDualTeamPoints == null && e.EligibleEnrollmentTeamPoints == null
                    ? null
                    : (int?)((e.EligibleDualTeamPoints ?? 0) + (e.EligibleEnrollmentTeamPoints ?? 0)),
                PaymentComment = e.PaymentComment
            })
            .ToListAsync(ct);

        var paid    = sums.Where(s => s.Status == CommissionEarningStatus.Paid).Sum(s => s.Amount);
        var pending = sums.Where(s => s.Status == CommissionEarningStatus.Pending).Sum(s => s.Amount);

        return new DailyResidualHistoryView
        {
            Items          = items,
            TotalCount     = sums.Sum(s => s.Count),
            Page           = page,
            PageSize       = pageSize,
            Year           = y,
            Month          = m,
            PeriodPaid     = paid,
            PeriodPending  = pending,
            PeriodTotal    = paid + pending,
            AvailableYears = years
        };
    }

    /// <summary>Del año de alta del miembro (15 como mucho) al año del reloj del servidor, descendente.</summary>
    private async Task<List<int>> AvailableYearsAsync(string memberId, DateTime now, CancellationToken ct)
    {
        var enrolled = await _db.MemberProfiles.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => (DateTime?)p.EnrollDate)
            .FirstOrDefaultAsync(ct);

        var start = Math.Clamp(enrolled?.Year ?? now.Year, now.Year - MaxYears + 1, now.Year);
        return Enumerable.Range(start, now.Year - start + 1).Reverse().ToList();
    }
}
