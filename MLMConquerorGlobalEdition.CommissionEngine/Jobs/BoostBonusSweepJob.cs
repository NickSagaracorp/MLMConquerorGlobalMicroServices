using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;

namespace MLMConquerorGlobalEdition.CommissionEngine.Jobs;

/// <summary>
/// HangFire recurring job — every 5 minutes.
/// Checks the last <see cref="LookbackWeeks"/> completed ISO weeks and triggers
/// the Boost Bonus calculation for any week that was missed (e.g., if the Sunday
/// 3 AM job did not run).  The current in-progress week is intentionally skipped —
/// it will be finalized by <see cref="BoostBonusJob"/> on Sunday.
///
/// Idempotent: <see cref="CalculateBoostBonusHandler"/> returns ALREADY_CALCULATED
/// when a week was already processed, so re-triggering a done week is always safe.
/// </summary>
public class BoostBonusSweepJob
{
    private const int LookbackWeeks = 4;

    private readonly IMediator                    _mediator;
    private readonly IDateTimeProvider            _dateTime;
    private readonly ILogger<BoostBonusSweepJob> _logger;

    public BoostBonusSweepJob(
        IMediator mediator,
        IDateTimeProvider dateTime,
        ILogger<BoostBonusSweepJob> logger)
    {
        _mediator = mediator;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = _dateTime.Now;

        // Monday of the current (still-running) week — never calculate this one.
        var daysToMon        = (int)now.DayOfWeek == 0 ? -6 : -((int)now.DayOfWeek - 1);
        var currentWeekStart = now.Date.AddDays(daysToMon);

        for (int i = 1; i <= LookbackWeeks; i++)
        {
            var weekStart = currentWeekStart.AddDays(-7 * i);

            var result = await _mediator.Send(new CalculateBoostBonusCommand(weekStart), ct);

            if (result.IsSuccess)
            {
                if (result.Value!.RecordsCreated > 0)
                    _logger.LogInformation(
                        "BoostBonusSweep: backfilled week {Week} — {Count} record(s), ${Total:F2}.",
                        weekStart.ToString("yyyy-MM-dd"),
                        result.Value.RecordsCreated,
                        result.Value.TotalAmountCalculated);
            }
            else if (result.ErrorCode is not ("ALREADY_CALCULATED" or "NO_BOOST_TYPES"))
            {
                _logger.LogWarning(
                    "BoostBonusSweep: week {Week} failed — [{Code}] {Error}",
                    weekStart.ToString("yyyy-MM-dd"),
                    result.ErrorCode,
                    result.Error);
            }
        }
    }
}
