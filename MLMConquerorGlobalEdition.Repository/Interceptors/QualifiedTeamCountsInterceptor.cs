using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Plan Q (DQ13): al guardar, los cambios de suscripciones hacia o desde Active encolan los deltas de calificados
/// (QualifiedTeamDeltas) y, por DQ16, los puntos del producto (MembershipPointsDeltas). Va en AppDbContext.OnConfiguring, como StatusHistoryInterceptor, para que ningun anfitrion lo
/// olvide. Hora de servidor (DateTime.Now).
/// Revision 2 del bloque 2: las lecturas de MembershipPointsDeltas son asincronas; el SaveChanges sincrono (que casi nadie
/// usa) las espera en el acto. Tercera revision: es aceptable porque toda la cadena (MembershipPointsDeltas,
/// MemberPointsLocks y EF) espera con ConfigureAwait(false), asi que nada vuelve al contexto de sincronizacion bloqueado.
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
        if (eventData.Context is { } context) await RecordAsync(context, cancellationToken).ConfigureAwait(false);
        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    // Tercera revision del bloque 2 (I-a): los candados de sesion por miembro se sueltan tras la confirmacion de EF, o si
    // el guardado falla o se cancela (MemberPointsLocks).
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context)
        {
            MembershipPointsDeltas.Saved(context);
            MemberPointsLocks.Release(context);
        }
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            MembershipPointsDeltas.Saved(context);
            await MemberPointsLocks.ReleaseAsync(context).ConfigureAwait(false);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context) MemberPointsLocks.Release(context);
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) await MemberPointsLocks.ReleaseAsync(context).ConfigureAwait(false);
        await base.SaveChangesFailedAsync(eventData, cancellationToken).ConfigureAwait(false);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        if (eventData.Context is { } context) MemberPointsLocks.Release(context);
        base.SaveChangesCanceled(eventData);
    }

    public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) await MemberPointsLocks.ReleaseAsync(context).ConfigureAwait(false);
        await base.SaveChangesCanceledAsync(eventData, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Plan Q (DQ16): primero los puntos (cambian PersonalPoints y encolan), despues los calificados (DQ13).</summary>
    private async Task RecordAsync(DbContext context, CancellationToken ct)
    {
        var ahora = _now();
        try
        {
            await MembershipPointsDeltas.RecordAsync(context, ahora, ct).ConfigureAwait(false);
            QualifiedTeamDeltas.Record(context, ahora);
        }
        catch
        {
            // El guardado no llega a empezar: ni SavedChanges ni SaveChangesFailed sueltan los candados.
            await MemberPointsLocks.ReleaseAsync(context).ConfigureAwait(false);
            throw;
        }
    }
}
