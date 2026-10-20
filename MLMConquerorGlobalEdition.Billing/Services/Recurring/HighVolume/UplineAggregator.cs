using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

/// <summary>
/// Stage 3 of the high-volume pipeline (BILLING-RULES §10.5).
/// See <see cref="IUplineAggregator"/> for the full contract.
/// </summary>
public class UplineAggregator : IUplineAggregator
{
    private readonly AppDbContext                 _db;
    private readonly IDateTimeProvider            _dateTime;
    private readonly ILogger<UplineAggregator>    _logger;

    private const string Actor = "upline-aggregator";

    public UplineAggregator(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<UplineAggregator> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task<Result<UplineAggregatorResult>> AggregateAsync(
        string batchId,
        CancellationToken ct = default)
    {
        // ── Load all Queued events for this batch ──────────────────────────────
        var queuedEvents = await _db.PointDeltaEvents
            .Where(e => e.BatchId == batchId && e.Status == PointDeltaEventStatus.Queued)
            .ToListAsync(ct);

        if (queuedEvents.Count == 0)
        {
            _logger.LogInformation(
                "UplineAggregator: no Queued PointDeltaEvents for batch {BatchId}.", batchId);
            return Result<UplineAggregatorResult>.Success(new UplineAggregatorResult
            {
                EventsApplied       = 0,
                UplineMembersUpdated = 0
            });
        }

        _logger.LogInformation(
            "UplineAggregator: {Count} Queued events for batch {BatchId}.", queuedEvents.Count, batchId);

        // Plan Q (DQ16, reglas de Nick): una renovacion no cambia los puntos de nadie, y los que si cambian (alta, cancelacion,
        // retencion, reactivacion, cambio de nivel) los encola MembershipPointsDeltas desde el interceptor de suscripciones:
        // al binario por la cola de piernas y a la linea de patrocinio por MemberStatisticDeltas. Este agregador sumaba
        // DualTeamPoints por la linea de INSCRIPCION (el arbol equivocado; el vaciado de piernas lo pisaba) y
        // EnrollmentPoints/PersonalPoints en cada renovacion: ya no escribe estadisticas, solo marca los eventos.
        var now = _dateTime.Now;
        const int updatedCount = 0;

        // ── Mark events Applied (in same save) ────────────────────────────────
        foreach (var ev in queuedEvents)
        {
            ev.Status   = PointDeltaEventStatus.Applied;
            ev.AppliedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "UplineAggregator: batch {BatchId} complete — {Events} events applied, {Members} upline rows updated.",
            batchId, queuedEvents.Count, updatedCount);

        return Result<UplineAggregatorResult>.Success(new UplineAggregatorResult
        {
            EventsApplied        = queuedEvents.Count,
            UplineMembersUpdated = updatedCount
        });
    }
}
