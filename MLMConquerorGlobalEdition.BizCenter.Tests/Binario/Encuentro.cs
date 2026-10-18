using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P: hace coincidir dos escritores justo antes de guardar un nodo del binario. Cada uno avisa al
/// llegar y espera al otro hasta <see cref="Espera"/>. Sin candado, los dos pasan la comprobacion del hueco,
/// se encuentran aqui e insertan los dos. Con candado, el segundo esta esperando al primero y nunca llega:
/// el primero sigue al agotar la espera y el segundo, al entrar, ve el hueco ocupado.
/// </summary>
public sealed class Encuentro(int escritores = 2) : SaveChangesInterceptor
{
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(3);
    private readonly CountdownEvent _llegadas = new(escritores);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var insertaNodo = e.Context!.ChangeTracker.Entries<DualTeamEntity>()
            .Any(x => x.State == Microsoft.EntityFrameworkCore.EntityState.Added && x.Entity.ParentMemberId != null);
        if (insertaNodo)
        {
            try { _llegadas.Signal(); } catch (InvalidOperationException) { /* ya estaban todos */ }
            await Task.Run(() => _llegadas.Wait(Espera), ct);
        }
        return result;
    }
}
