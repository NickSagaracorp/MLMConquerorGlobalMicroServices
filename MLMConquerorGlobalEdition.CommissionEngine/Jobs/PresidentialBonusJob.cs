using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculatePresidentialBonus;

namespace MLMConquerorGlobalEdition.CommissionEngine.Jobs;

/// <summary>
/// HangFire recurring job — runs monthly on the 1st at 4:00 AM UTC.
/// </summary>
public class PresidentialBonusJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<PresidentialBonusJob> _logger;

    public PresidentialBonusJob(IMediator mediator, ILogger<PresidentialBonusJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("PresidentialBonusJob started at {Time}", DateTime.Now);

        var result = await _mediator.Send(new CalculatePresidentialBonusCommand(), ct);
        if (result.IsSuccess)
            _logger.LogInformation("PresidentialBonus: {Count} records, ${Total:F2}",
                result.Value!.RecordsCreated, result.Value.TotalAmountCalculated);
        else
            _logger.LogError("PresidentialBonus failed: {Error}", result.Error);

        _logger.LogInformation("PresidentialBonusJob completed at {Time}", DateTime.Now);
    }
}
