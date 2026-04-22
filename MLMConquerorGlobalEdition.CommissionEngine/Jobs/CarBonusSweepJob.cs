using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateCarBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;

namespace MLMConquerorGlobalEdition.CommissionEngine.Jobs;

/// <summary>
/// HangFire reconciliation job — runs daily at 6:00 AM UTC.
/// Looks back <see cref="LookbackMonths"/> completed months and triggers Car Bonus
/// calculation for any month that was missed or skipped.
/// The current in-progress month is intentionally excluded — it will be finalized
/// by <see cref="CarBonusJob"/> on the 1st of the next month.
/// Idempotent: <see cref="CalculateCarBonusHandler"/> returns ALREADY_CALCULATED
/// when a month was already processed, so re-triggering is always safe.
/// </summary>
public class CarBonusSweepJob
{
    private const int LookbackMonths = 3;

    private readonly IMediator               _mediator;
    private readonly IDateTimeProvider       _dateTime;
    private readonly ILogger<CarBonusSweepJob> _logger;

    public CarBonusSweepJob(
        IMediator mediator,
        IDateTimeProvider dateTime,
        ILogger<CarBonusSweepJob> logger)
    {
        _mediator = mediator;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now          = _dateTime.Now;
        var currentMonth = new DateTime(now.Year, now.Month, 1);

        for (int i = 1; i <= LookbackMonths; i++)
        {
            var targetMonth = currentMonth.AddMonths(-i);
            var result      = await _mediator.Send(new CalculateCarBonusCommand(targetMonth), ct);

            if (result.IsSuccess)
            {
                if (result.Value!.RecordsCreated > 0)
                    _logger.LogInformation(
                        "CarBonusSweep: backfilled {Period} — {Count} record(s), ${Total:F2}.",
                        targetMonth.ToString("yyyy-MM"),
                        result.Value.RecordsCreated,
                        result.Value.TotalAmountCalculated);
            }
            else if (result.ErrorCode is not ("ALREADY_CALCULATED" or "NO_CAR_BONUS_TYPE"))
            {
                _logger.LogWarning(
                    "CarBonusSweep: {Period} failed — [{Code}] {Error}",
                    targetMonth.ToString("yyyy-MM"),
                    result.ErrorCode,
                    result.Error);
            }
        }
    }
}
