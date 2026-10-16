using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>
/// Hace fallar guardados concretos en InMemory: los que cumplen una condición, un número de veces. Es
/// la forma de probar que una compra no se queda en Pending cuando la base falla justo después de
/// cobrar, o que un choque con un índice único (que InMemory no tiene) se trata bien.
/// </summary>
public sealed class SaveFaults : SaveChangesInterceptor
{
    private Func<DbContext, bool>? _when;
    private Func<DbContext, Exception>? _make;
    private int _remaining;

    /// <summary>Cuántas veces ha hecho fallar un guardado.</summary>
    public int Thrown { get; private set; }

    /// <summary>Hace fallar los próximos <paramref name="times"/> guardados que cumplan <paramref name="when"/>.</summary>
    public void FailWhen(Func<DbContext, bool> when, int times = int.MaxValue, Func<DbContext, Exception>? make = null)
    {
        _when      = when;
        _remaining = times;
        _make      = make ?? (_ => new DbUpdateException("Fallo simulado al guardar."));
    }

    public void Stop() => _remaining = 0;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Check(DbContext? context)
    {
        if (context is null || _remaining <= 0 || _when is null || !_when(context)) return;

        _remaining--;
        Thrown++;
        throw _make!(context);
    }
}
