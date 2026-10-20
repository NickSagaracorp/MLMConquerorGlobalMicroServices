using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Plan Q (DQ13): al guardar, los cambios de suscripciones hacia o desde Active encolan los deltas de calificados
/// (QualifiedTeamDeltas) y, por DQ16, los puntos del producto (MembershipPointsDeltas). Va en AppDbContext.OnConfiguring, como StatusHistoryInterceptor, para que ningun anfitrion lo
/// olvide. Hora de servidor (DateTime.Now).
/// Revision 2 del bloque 2: las lecturas de MembershipPointsDeltas son asincronas; el SaveChanges sincrono (que casi nadie
/// usa) las espera en el acto.
/// </summary>
public sealed class QualifiedTeamCountsInterceptor : SaveChangesInterceptor
{
    private readonly Func<DateTime> _now;

    public QualifiedTeamCountsInterceptor(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) RecordAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) await RecordAsync(context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
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
    private async Task RecordAsync(DbContext context, CancellationToken ct)
    {
        var ahora = _now();
        await MembershipPointsDeltas.RecordAsync(context, ahora, ct);
        QualifiedTeamDeltas.Record(context, ahora);
    }
}
