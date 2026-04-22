using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateCarBonus;

namespace MLMConquerorGlobalEdition.CommissionEngine.Jobs;

/// <summary>
/// HangFire recurring job — runs monthly on the 1st at 5:00 AM UTC.
/// Calculates Car Bonus for all qualifying ambassadors in the prior calendar month.
/// </summary>
public class CarBonusJob
{
    private readonly IMediator           _mediator;
    private readonly ILogger<CarBonusJob> _logger;

    public CarBonusJob(IMediator mediator, ILogger<CarBonusJob> logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("CarBonusJob started at {Time}", DateTime.UtcNow);

        // Calculate for the month that just ended
        var priorMonth = DateTime.UtcNow.AddMonths(-1);
        var result = await _mediator.Send(new CalculateCarBonusCommand(priorMonth), ct);

        if (result.IsSuccess)
            _logger.LogInformation(
                "CarBonus {Period}: {Count} record(s), ${Total:F2}",
                priorMonth.ToString("yyyy-MM"),
                result.Value!.RecordsCreated,
                result.Value.TotalAmountCalculated);
        else
            _logger.LogError(
                "CarBonus {Period} failed: [{Code}] {Error}",
                priorMonth.ToString("yyyy-MM"),
                result.ErrorCode,
                result.Error);

        _logger.LogInformation("CarBonusJob completed at {Time}", DateTime.UtcNow);
    }
}
