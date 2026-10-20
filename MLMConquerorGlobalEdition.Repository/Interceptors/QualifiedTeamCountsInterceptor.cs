using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Plan Q (DQ13): al guardar, los cambios de suscripciones hacia o desde Active encolan los deltas de calificados
/// (QualifiedTeamDeltas) y, por DQ16, los puntos del producto (MembershipPointsDeltas). Va en AppDbContext.OnConfiguring, como StatusHistoryInterceptor, para que ningun anfitrion lo
/// olvide. Hora de servidor (DateTime.Now).
/// </summary>
public sealed class QualifiedTeamCountsInterceptor : SaveChangesInterceptor
{
    private readonly Func<DateTime> _now;

    public QualifiedTeamCountsInterceptor(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) Record(context);
        return base.SavingChanges(eventData, result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context) MembershipPointsDeltas.Saved(context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) MembershipPointsDeltas.Saved(context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Plan Q (DQ16): primero los puntos (cambian PersonalPoints y encolan), despues los calificados (DQ13).</summary>
    private void Record(DbContext context)
    {
        var ahora = _now();
        MembershipPointsDeltas.Record(context, ahora);
        QualifiedTeamDeltas.Record(context, ahora);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Record(context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
