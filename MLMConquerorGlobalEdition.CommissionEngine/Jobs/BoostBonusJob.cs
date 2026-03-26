using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;

namespace MLMConquerorGlobalEdition.CommissionEngine.Jobs;

/// <summary>
/// HangFire recurring job — runs weekly on Sunday at 3:00 AM UTC.
/// </summary>
public class BoostBonusJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<BoostBonusJob> _logger;

    public BoostBonusJob(IMediator mediator, ILogger<BoostBonusJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("BoostBonusJob started at {Time}", DateTime.Now);

        var result = await _mediator.Send(new CalculateBoostBonusCommand(), ct);
        if (result.IsSuccess)
            _logger.LogInformation("BoostBonus: {Count} records, ${Total:F2}",
                result.Value!.RecordsCreated, result.Value.TotalAmountCalculated);
        else
            _logger.LogError("BoostBonus failed: {Error}", result.Error);

        _logger.LogInformation("BoostBonusJob completed at {Time}", DateTime.Now);
    }
}
