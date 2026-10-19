using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Plan Q (DQ13): al guardar, los cambios de suscripciones hacia o desde Active encolan los deltas de calificados
/// (QualifiedTeamDeltas). Va en AppDbContext.OnConfiguring, como StatusHistoryInterceptor, para que ningun anfitrion lo
/// olvide. Hora de servidor (DateTime.Now).
/// </summary>
public sealed class QualifiedTeamCountsInterceptor : SaveChangesInterceptor
{
    private readonly Func<DateTime> _now;

    public QualifiedTeamCountsInterceptor(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) QualifiedTeamDeltas.Record(context, _now());
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) QualifiedTeamDeltas.Record(context, _now());
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
