using MediatR;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Jobs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class BoostBonusSweepJobTests
{
    // Fixed Monday — used as "now" so week boundaries are deterministic.
    private static readonly DateTime FixedNow = new(2026, 3, 23, 8, 0, 0, DateTimeKind.Utc);

    // Monday of the current week = 2026-03-23 (same as FixedNow since it IS a Monday)
    private static DateTime CurrentWeekStart => new(2026, 3, 23);

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<ILogger<BoostBonusSweepJob>> Logger() => new();

    private static Mock<IMediator> MediatorAlreadyCalculated()
    {
        var m = new Mock<IMediator>();
        m.Setup(med => med.Send(It.IsAny<CalculateBoostBonusCommand>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<CalculationResultResponse>.Failure("ALREADY_CALCULATED", "already done"));
        return m;
    }

    // ── Core behavior ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SendsExactlyFourCommands()
    {
        var mediator = MediatorAlreadyCalculated();
        var job = new BoostBonusSweepJob(mediator.Object, Clock().Object, Logger().Object);

        await job.ExecuteAsync(CancellationToken.None);

        mediator.Verify(
            m => m.Send(It.IsAny<CalculateBoostBonusCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task ExecuteAsync_NeverSendsForCurrentWeek()
    {
        var capturedWeeks = new List<DateTime?>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<CalculateBoostBonusCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<CalculationResultResponse>>, CancellationToken>(
                (req, _) => capturedWeeks.Add(((CalculateBoostBonusCommand)req).PeriodDate))
            .ReturnsAsync(Result<CalculationResultResponse>.Failure("ALREADY_CALCULATED", "done"));

        var job = new BoostBonusSweepJob(mediator.Object, Clock().Object, Logger().Object);
        await job.ExecuteAsync(CancellationToken.None);

        capturedWeeks.Should().NotContain(CurrentWeekStart);
        capturedWeeks.Should().AllSatisfy(w => w.Should().BeBefore(CurrentWeekStart));
    }

    [Fact]
    public async Task ExecuteAsync_SendsCorrectFourPastWeekStarts()
    {
        // FixedNow = Monday 2026-03-23.  Past 4 completed weeks:
        //   i=1 → 2026-03-16   i=2 → 2026-03-09   i=3 → 2026-03-02   i=4 → 2026-02-23
        var expectedWeeks = new List<DateTime?>
        {
            new DateTime(2026, 3, 16),
            new DateTime(2026, 3, 9),
            new DateTime(2026, 3, 2),
            new DateTime(2026, 2, 23)
        };

        var capturedWeeks = new List<DateTime?>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<CalculateBoostBonusCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<CalculationResultResponse>>, CancellationToken>(
                (req, _) => capturedWeeks.Add(((CalculateBoostBonusCommand)req).PeriodDate))
            .ReturnsAsync(Result<CalculationResultResponse>.Failure("ALREADY_CALCULATED", "done"));

        var job = new BoostBonusSweepJob(mediator.Object, Clock().Object, Logger().Object);
        await job.ExecuteAsync(CancellationToken.None);

        capturedWeeks.Should().BeEquivalentTo(expectedWeeks);
    }

    // ── Resilience ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenOneWeekFails_ContinuesRemainingWeeks()
    {
        int callCount = 0;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<CalculateBoostBonusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 2
                    ? Result<CalculationResultResponse>.Failure("SOME_ERROR", "transient")
                    : Result<CalculationResultResponse>.Failure("ALREADY_CALCULATED", "done");
            });

        var job = new BoostBonusSweepJob(mediator.Object, Clock().Object, Logger().Object);
        await job.ExecuteAsync(CancellationToken.None);

        // All 4 weeks sent despite one failure
        mediator.Verify(
            m => m.Send(It.IsAny<CalculateBoostBonusCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task ExecuteAsync_WhenBackfillSucceeds_DoesNotThrow()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<CalculateBoostBonusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType        = "BoostBonus",
                RecordsCreated        = 2,
                TotalAmountCalculated = 1200m,
                PeriodDate            = FixedNow.Date
            }));

        var job = new BoostBonusSweepJob(mediator.Object, Clock().Object, Logger().Object);

        var act = () => job.ExecuteAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllAlreadyCalculated_DoesNotThrow()
    {
        var mediator = MediatorAlreadyCalculated();
        var job = new BoostBonusSweepJob(mediator.Object, Clock().Object, Logger().Object);

        var act = () => job.ExecuteAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
