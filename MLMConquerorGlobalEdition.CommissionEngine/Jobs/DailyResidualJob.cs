using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateMatchingBonus;

namespace MLMConquerorGlobalEdition.CommissionEngine.Jobs;

/// <summary>
/// HangFire recurring job — runs nightly at 2:00 AM UTC.
/// Calculates binary residual for all active ambassadors,
/// then immediately runs the matching bonus on the same period.
/// </summary>
public class DailyResidualJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<DailyResidualJob> _logger;

    public DailyResidualJob(IMediator mediator, ILogger<DailyResidualJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("DailyResidualJob started at {Time}", DateTime.Now);

        var residualResult = await _mediator.Send(new CalculateDailyResidualCommand(), ct);
        if (residualResult.IsSuccess)
            _logger.LogInformation("DailyResidual: {Count} records, ${Total:F2}",
                residualResult.Value!.RecordsCreated, residualResult.Value.TotalAmountCalculated);
        else
            _logger.LogError("DailyResidual failed: {Error}", residualResult.Error);

        // Matching bonus immediately after residual on the same period
        var matchingResult = await _mediator.Send(new CalculateMatchingBonusCommand(), ct);
        if (matchingResult.IsSuccess)
            _logger.LogInformation("MatchingBonus: {Count} records, ${Total:F2}",
                matchingResult.Value!.RecordsCreated, matchingResult.Value.TotalAmountCalculated);
        else
            _logger.LogError("MatchingBonus failed: {Error}", matchingResult.Error);

        _logger.LogInformation("DailyResidualJob completed at {Time}", DateTime.Now);
    }
}
