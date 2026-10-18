using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

/// <summary>
/// Plan P: hace coincidir dos escritores del binario justo antes de guardar un nodo con padre. Sin candado,
/// los dos pasan la comprobacion del hueco e insertan; con candado, el segundo espera al primero, que sigue
/// al agotar <see cref="Espera"/>. (Misma idea que BizCenter.Tests/Binario/Encuentro y AdminAPI.Tests.)
/// </summary>
public sealed class Encuentro(int escritores = 2) : SaveChangesInterceptor
{
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(3);
    private readonly CountdownEvent _llegadas = new(escritores);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var insertaNodo = e.Context!.ChangeTracker.Entries<DualTeamEntity>()
            .Any(x => x.State == EntityState.Added && x.Entity.ParentMemberId != null);
        if (insertaNodo)
        {
            try { _llegadas.Signal(); } catch (InvalidOperationException) { /* ya estaban todos */ }
            await Task.Run(() => _llegadas.Wait(Espera), ct);
        }
        return result;
    }
}
