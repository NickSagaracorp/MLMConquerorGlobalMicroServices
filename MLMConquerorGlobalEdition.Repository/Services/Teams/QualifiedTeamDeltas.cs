using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Plan Q (DQ13): el UNICO sitio que traduce un cambio de MembershipSubscription.SubscriptionStatus hacia o desde Active
/// en deltas de los contadores de calificados del equipo. Lo llama QualifiedTeamCountsInterceptor en cada guardado, asi
/// que lo recorren todos los escritores por EF (alta, activacion, cancelacion, facturacion, subida y bajada de nivel,
/// admin y semillas) sin llamarlo a mano. ExecuteUpdate y el SQL crudo no pasan por aqui: lo vigilan
/// EstadosSinInterceptorTests y EscritoresDeSuscripcionesTests.
/// Cuenta SUSCRIPCIONES activas (no miembros) y no mira IsDeleted, como el recuento al vuelo al que sustituye (Q-R21).
/// </summary>
public static class QualifiedTeamDeltas
{
    public const string Actor = "qualified-count";

    /// <summary>El cambio neto de suscripciones activas por miembro en lo que el contexto va a guardar (sin ceros).</summary>
    public static Dictionary<string, int> NetChanges(DbContext context)
    {
        var neto = new Dictionary<string, int>(StringComparer.Ordinal);
        void Sumar(string memberId, int d) => neto[memberId] = neto.GetValueOrDefault(memberId) + d;

        foreach (var e in context.ChangeTracker.Entries<MembershipSubscription>())
        {
            var estado  = e.Property(s => s.SubscriptionStatus);
            var miembro = e.Property(s => s.MemberId);
            switch (e.State)
            {
                case EntityState.Added:
                    if (e.Entity.SubscriptionStatus == MembershipStatus.Active) Sumar(e.Entity.MemberId, +1);
                    break;
                case EntityState.Deleted:
                    if (estado.OriginalValue == MembershipStatus.Active) Sumar(miembro.OriginalValue, -1);
                    break;
                case EntityState.Modified:
                {
                    var antes = estado.OriginalValue == MembershipStatus.Active;
                    var ahora = e.Entity.SubscriptionStatus == MembershipStatus.Active;
                    if (antes) Sumar(miembro.OriginalValue, -1);
                    if (ahora) Sumar(e.Entity.MemberId, +1);
                    break;
                }
            }
        }
        return neto.Where(kv => kv.Value != 0).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Anade al contexto, en el mismo guardado, lo que falte para que los deltas propios pendientes de este guardado
    /// sumen el cambio neto de cada miembro. Idempotente: un guardado que falla y se reintenta no duplica.
    /// </summary>
    public static void Record(DbContext context, DateTime now)
    {
        var cambios = NetChanges(context);
        var yaEncolados = context.ChangeTracker.Entries<DualLegDelta>()
            .Where(e => e.State == EntityState.Added && e.Entity.OwnChange && e.Entity.CreatedBy == Actor)
            .GroupBy(e => e.Entity.StartMemberId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Entity.QualifiedCount), StringComparer.Ordinal);

        foreach (var memberId in cambios.Keys.Union(yaEncolados.Keys, StringComparer.Ordinal).ToList())
        {
            var falta = cambios.GetValueOrDefault(memberId) - yaEncolados.GetValueOrDefault(memberId);
            if (falta == 0) continue;
            context.Set<DualLegDelta>().Add(new DualLegDelta
            {
                StartMemberId = memberId, Side = TreeSide.Left, Points = 0, NodeCount = 0,
                QualifiedCount = falta, OwnChange = true, CreationDate = now, CreatedBy = Actor
            });
        }
    }
}
