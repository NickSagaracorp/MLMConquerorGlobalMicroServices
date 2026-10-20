using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Plan Q (DQ16, reglas de Nick): los puntos del producto de un miembro cuentan en TODA su linea ascendente (las piernas
/// del binario y los EnrollmentPoints de la linea de patrocinio, el propio incluido) SOLO mientras su membresia esta
/// activa:
/// <list type="bullet">
/// <item>entrar en Active (activacion del alta, reactivacion desde suspendida, retenida o cancelada) suma los puntos del
/// producto actual;</item>
/// <item>salir de Active (cancelacion, cancelacion programada, retencion por cobro, caducidad) los resta;</item>
/// <item>una renovacion no cambia nada, salvo que cambie el producto o el nivel (subida o bajada): entonces se aplica la
/// diferencia (puntos nuevos - puntos viejos).</item>
/// </list>
/// Es el UNICO sitio que lo traduce, desde QualifiedTeamCountsInterceptor en cada guardado (como los calificados, DQ13):
/// <list type="number">
/// <item>Cada suscripcion guarda lo que aporta mientras esta activa (<see cref="MembershipSubscription.DualTeamContribution"/>
/// y <see cref="MembershipSubscription.EnrollmentContribution"/>) al entrar en Active o al cambiar de nivel: los puntos de
/// calificacion de los productos de su ultimo pedido (como los sumaba el alta) o, sin pedido (subida y bajada), los del
/// producto de su nivel (el mayor si hay varios). Salir resta lo guardado, asi que editar despues los puntos de un
/// producto no deja restos.</item>
/// <item>El cambio neto de cada miembro va a su <see cref="MemberStatisticEntity.PersonalPoints"/> y a su
/// <see cref="MemberStatisticEntity.EnrollmentPoints"/>, en el mismo guardado.</item>
/// <item>Y sube como delta: un propio con puntos en DualLegDeltas (O(1); llega a las piernas de sus ascendientes del
/// binario al vaciar, y espera si no esta colocado) y una fila de MemberStatisticDeltas por ascendiente de su linea de
/// patrocinio. Un PersonalPoints escrito a mano (semillas, pruebas) no se propaga.</item>
/// </list>
/// Idempotente: un guardado que falla y se reintenta no aplica dos veces (lo ya ajustado en este contexto se recuerda
/// hasta que el guardado se confirma).
/// </summary>
public static class MembershipPointsDeltas
{
    public const string Actor = "member-points";

    private static readonly ConditionalWeakTable<DbContext, Dictionary<MemberStatisticEntity, int>> Ajustado = new();

    /// <summary>Los puntos que aporta una suscripcion activa de <paramref name="membershipLevelId"/> sin pedido.</summary>
    public static int LevelPoints(DbContext context, int membershipLevelId) =>
        context.Set<Product>().AsNoTracking()
            .Where(p => p.MembershipLevelId == membershipLevelId)
            .Select(p => (int?)p.QualificationPoins).Max() ?? 0;

    /// <summary>Los puntos de calificacion de los productos de un pedido (como los sumaba el alta).</summary>
    public static int OrderPoints(DbContext context, string orderId)
    {
        var seguidos = context.ChangeTracker.Entries<OrderDetail>()
            .Where(e => e.State != EntityState.Deleted && e.Entity.OrderId == orderId)
            .Select(e => e.Entity.ProductId).ToList();
        var productos = seguidos.Count > 0
            ? seguidos
            : context.Set<OrderDetail>().AsNoTracking().Where(d => d.OrderId == orderId).Select(d => d.ProductId).ToList();
        if (productos.Count == 0) return 0;
        var distintos = productos.Distinct().ToList();
        var puntos = context.Set<Product>().AsNoTracking().Where(p => distintos.Contains(p.Id))
            .Select(p => new { p.Id, p.QualificationPoins }).ToList()
            .ToDictionary(p => p.Id, p => p.QualificationPoins);
        return productos.Sum(id => puntos.GetValueOrDefault(id));
    }

    /// <summary>Tras un guardado confirmado: lo ajustado deja de estar pendiente.</summary>
    public static void Saved(DbContext context) => Ajustado.Remove(context);

    public static void Record(DbContext context, DateTime now)
    {
        // 1. Lo que aporta cada suscripcion y el cambio neto por miembro.
        var neto = new Dictionary<string, int>(StringComparer.Ordinal);
        void Sumar(string m, int d) => neto[m] = neto.GetValueOrDefault(m) + d;
        var nivelPuntos = new Dictionary<int, int>();
        var pedidoPuntos = new Dictionary<string, int>(StringComparer.Ordinal);
        int Puntos(MembershipSubscription sub)
        {
            if (!string.IsNullOrEmpty(sub.LastOrderId))
                return pedidoPuntos.TryGetValue(sub.LastOrderId, out var q)
                    ? q : pedidoPuntos[sub.LastOrderId] = OrderPoints(context, sub.LastOrderId);
            return nivelPuntos.TryGetValue(sub.MembershipLevelId, out var p)
                ? p : nivelPuntos[sub.MembershipLevelId] = LevelPoints(context, sub.MembershipLevelId);
        }

        foreach (var e in context.ChangeTracker.Entries<MembershipSubscription>().ToList())
        {
            var s = e.Entity;
            switch (e.State)
            {
                case EntityState.Added:
                {
                    var c = s.SubscriptionStatus == MembershipStatus.Active ? Puntos(s) : 0;
                    s.DualTeamContribution = c;
                    s.EnrollmentContribution = c;
                    Sumar(s.MemberId, c);
                    break;
                }
                case EntityState.Deleted:
                    if (e.Property(x => x.SubscriptionStatus).OriginalValue == MembershipStatus.Active)
                        Sumar(e.Property(x => x.MemberId).OriginalValue, -e.Property(x => x.DualTeamContribution).OriginalValue);
                    break;
                case EntityState.Modified:
                {
                    var antes = e.Property(x => x.SubscriptionStatus).OriginalValue == MembershipStatus.Active;
                    var ahora = s.SubscriptionStatus == MembershipStatus.Active;
                    var aporte = e.Property(x => x.DualTeamContribution).OriginalValue;
                    var nivelAntes = e.Property(x => x.MembershipLevelId).OriginalValue;
                    var miembroAntes = e.Property(x => x.MemberId).OriginalValue;
                    if (antes && !ahora)
                    {
                        Sumar(miembroAntes, -aporte);
                        s.DualTeamContribution = 0;
                        s.EnrollmentContribution = 0;
                    }
                    else if (!antes && ahora)
                    {
                        var c = Puntos(s);
                        Sumar(s.MemberId, c);
                        s.DualTeamContribution = c;
                        s.EnrollmentContribution = c;
                    }
                    else if (antes && ahora && (nivelAntes != s.MembershipLevelId || miembroAntes != s.MemberId))
                    {
                        // Renovacion o cambio con otro nivel: la diferencia. Una renovacion sin cambio no toca nada.
                        var c = Puntos(s);
                        Sumar(miembroAntes, -aporte);
                        Sumar(s.MemberId, c);
                        s.DualTeamContribution = c;
                        s.EnrollmentContribution = c;
                    }
                    break;
                }
            }
        }
        foreach (var m in neto.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList()) neto.Remove(m);

        // 2. El neto, a PersonalPoints y a EnrollmentPoints del propio (idempotente: se aplica lo que falte respecto de lo
        //    ya ajustado en este contexto).
        var ajustado = Ajustado.GetOrCreateValue(context);
        foreach (var (memberId, delta) in neto)
        {
            var fila = FilaDe(context, memberId)
                       ?? context.Set<MemberStatisticEntity>().Add(new MemberStatisticEntity
                       {
                           MemberId = memberId, CreatedBy = Actor, CreationDate = now
                       }).Entity;
            var falta = delta - ajustado.GetValueOrDefault(fila);
            if (falta == 0) continue;
            fila.PersonalPoints += falta;
            fila.EnrollmentPoints += falta;
            ajustado[fila] = delta;
        }
        foreach (var (fila, ya) in ajustado.ToList())
            if (!neto.ContainsKey(fila.MemberId) && ya != 0)
            {
                // Un reintento en el que el neto de este miembro ya no existe: se deshace lo ajustado.
                fila.PersonalPoints -= ya;
                fila.EnrollmentPoints -= ya;
                ajustado.Remove(fila);
            }

        // 3. El neto sube como delta: al binario (un propio con puntos) y a los ascendientes de la linea de patrocinio.
        var yaBinario = context.ChangeTracker.Entries<DualLegDelta>()
            .Where(e => e.State == EntityState.Added && e.Entity.OwnChange && e.Entity.CreatedBy == Actor)
            .GroupBy(e => e.Entity.StartMemberId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (int)g.Sum(e => e.Entity.Points), StringComparer.Ordinal);
        var yaInscripcion = context.ChangeTracker.Entries<MemberStatisticDelta>()
            .Where(e => e.State == EntityState.Added && e.Entity.CreatedBy == Actor)
            .GroupBy(e => e.Entity.SourceMemberId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                // Cada vez se encola lo mismo a cada ascendiente: lo ya encolado es la suma de uno cualquiera.
                g => g.GroupBy(e => e.Entity.MemberId, StringComparer.Ordinal).First().Sum(e => e.Entity.EnrollmentPointsDelta),
                StringComparer.Ordinal);

        foreach (var memberId in neto.Keys.Union(yaBinario.Keys).Union(yaInscripcion.Keys).ToList())
        {
            var objetivo = neto.GetValueOrDefault(memberId);

            var faltaBinario = objetivo - yaBinario.GetValueOrDefault(memberId);
            if (faltaBinario != 0)
                context.Set<DualLegDelta>().Add(new DualLegDelta
                {
                    StartMemberId = memberId, Side = TreeSide.Left, Points = faltaBinario, NodeCount = 0, QualifiedCount = 0,
                    OwnChange = true, CreationDate = now, CreatedBy = Actor
                });

            var faltaInscripcion = objetivo - yaInscripcion.GetValueOrDefault(memberId);
            if (faltaInscripcion != 0)
                foreach (var id in Ascendientes(context, memberId))
                    context.Set<MemberStatisticDelta>().Add(new MemberStatisticDelta
                    {
                        MemberId = id, EnrollmentPointsDelta = faltaInscripcion, SourceMemberId = memberId,
                        IsApplied = false, CreatedBy = Actor, CreationDate = now
                    });
        }
    }

    /// <summary>La fila de estadisticas del miembro (la primera por Id), seguida o leida y seguida.</summary>
    private static MemberStatisticEntity? FilaDe(DbContext context, string memberId)
    {
        var seguida = context.ChangeTracker.Entries<MemberStatisticEntity>()
            .Where(e => e.State != EntityState.Deleted && e.Entity.MemberId == memberId)
            .OrderBy(e => e.Entity.Id == 0 ? long.MaxValue : e.Entity.Id)
            .Select(e => e.Entity).FirstOrDefault();
        return seguida ?? context.Set<MemberStatisticEntity>().Where(s => s.MemberId == memberId).OrderBy(s => s.Id).FirstOrDefault();
    }

    /// <summary>
    /// Los ascendientes de inscripcion (sin el propio), por la ruta de su nodo vigente (el de mas reciente creacion),
    /// seguido o en la base; sin nodo propio, la linea de su patrocinador. Sin nada de eso, ninguno.
    /// </summary>
    private static List<string> Ascendientes(DbContext context, string memberId)
    {
        var ruta = context.ChangeTracker.Entries<GenealogyEntity>()
                       .Where(e => e.State != EntityState.Deleted && e.Entity.MemberId == memberId)
                       .Select(e => e.Entity.HierarchyPath).FirstOrDefault()
                   ?? context.Set<GenealogyEntity>().AsNoTracking().Where(g => g.MemberId == memberId)
                       .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id)
                       .Select(g => g.HierarchyPath).FirstOrDefault();
        if (ruta is null)
        {
            // Sin nodo propio todavia (un alta que se activa antes de tenerlo): la linea de su patrocinador, el incluido,
            // como la recorria la activacion.
            var patrocinador = context.ChangeTracker.Entries<MemberProfile>()
                                   .Where(e => e.Entity.MemberId == memberId).Select(e => e.Entity.SponsorMemberId).FirstOrDefault()
                               ?? context.Set<MemberProfile>().AsNoTracking().Where(m => m.MemberId == memberId)
                                   .Select(m => m.SponsorMemberId).FirstOrDefault();
            if (!string.IsNullOrEmpty(patrocinador))
                ruta = context.ChangeTracker.Entries<GenealogyEntity>()
                           .Where(e => e.State != EntityState.Deleted && e.Entity.MemberId == patrocinador)
                           .Select(e => e.Entity.HierarchyPath).FirstOrDefault()
                       ?? context.Set<GenealogyEntity>().AsNoTracking().Where(g => g.MemberId == patrocinador)
                           .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id)
                           .Select(g => g.HierarchyPath).FirstOrDefault();
        }
        return (ruta ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => !string.Equals(id, memberId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).ToList();
    }
}
