using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
/// = <see cref="MembershipSubscription.EnrollmentContribution"/>; 0 fuera de Active):
///   <list type="bullet">
///   <item>al entrar en Active: los puntos de calificacion de los productos de su ultimo pedido (como los sumaba el alta)
///   o, sin pedido, los del producto de su nivel (el mayor si hay varios);</item>
///   <item>al cambiar de nivel sin pedido nuevo (revision 2 del bloque 2, I3 e I4): lo que aportaba MENOS los puntos de su
///   nivel viejo MAS los del nuevo (conserva los productos extra del pedido en la misma base). En una subida o bajada la
///   vieja caduca y nace otra activa sin pedido en el mismo guardado: la nueva parte de lo que aportaba la vieja. Con
///   pedido nuevo, los puntos de ese pedido.</item>
///   </list></item>
/// <item>ABSOLUTO por miembro tocado (revision 2 del bloque 2, C1): para cada miembro con una suscripcion que cambia de
/// estado, de nivel o de miembro (o que nace o se borra), sus PersonalPoints pasan a ser la suma de lo que aportan sus
/// suscripciones activas tras el guardado, y la diferencia con lo que tenia es el cambio neto. Un miembro de antes de
/// DQ16 con puntos y sin aporte guardado (retenido, pendiente con los puntos que sembraba el alta antiguo) no cuenta dos
/// veces al reactivarse: queda corregido en su primer cambio. Una renovacion sin cambio de nivel no toca a nadie.</item>
/// <item>El cambio neto va a su <see cref="MemberStatisticEntity.PersonalPoints"/> y a su
/// <see cref="MemberStatisticEntity.EnrollmentPoints"/>, en el mismo guardado. PersonalPoints ya incluye los propios de
/// puntos pendientes en la cola (se suman aqui al encolarlos), asi que el cambio neto es objetivo - PersonalPoints: las
/// piernas acaban en PersonalPoints al vaciar (lo aplicado, PersonalPoints - pendientes, mas lo pendiente), igual que la
/// hoja de <see cref="LegPointMaintenance.EnqueueLeafAsync"/>.</item>
/// <item>Y sube como delta: un propio con puntos en DualLegDeltas (O(1); llega a las piernas de sus ascendientes del
/// binario al vaciar, y espera si no esta colocado) y una fila de MemberStatisticDeltas por ascendiente de su linea de
/// patrocinio (hasta el Bloque 4). Un PersonalPoints escrito a mano que no este en las piernas descuadra las piernas en
/// el primer cambio del miembro: 05-realinear-puntos-por-membresia.sql deja la base entera en la regla.</item>
/// </list>
/// Idempotente: un guardado que falla y se reintenta no aplica dos veces (lo ya ajustado en este contexto se recuerda
/// hasta que el guardado se confirma). Todas sus lecturas son asincronas (revision 2 del bloque 2).
/// </summary>
public static class MembershipPointsDeltas
{
    public const string Actor = "member-points";

    private static readonly ConditionalWeakTable<DbContext, Dictionary<MemberStatisticEntity, int>> Ajustado = new();

    /// <summary>Los puntos que aporta una suscripcion activa de <paramref name="membershipLevelId"/> sin pedido.</summary>
    public static async Task<int> LevelPointsAsync(DbContext context, int membershipLevelId, CancellationToken ct = default) =>
        await context.Set<Product>().AsNoTracking()
            .Where(p => p.MembershipLevelId == membershipLevelId)
            .Select(p => (int?)p.QualificationPoins).MaxAsync(ct) ?? 0;

    /// <summary>Los puntos de calificacion de los productos de un pedido (como los sumaba el alta).</summary>
    public static async Task<int> OrderPointsAsync(DbContext context, string orderId, CancellationToken ct = default)
    {
        var seguidos = context.ChangeTracker.Entries<OrderDetail>()
            .Where(e => e.State != EntityState.Deleted && e.Entity.OrderId == orderId)
            .Select(e => e.Entity.ProductId).ToList();
        var productos = seguidos.Count > 0
            ? seguidos
            : await context.Set<OrderDetail>().AsNoTracking().Where(d => d.OrderId == orderId).Select(d => d.ProductId).ToListAsync(ct);
        if (productos.Count == 0) return 0;
        var distintos = productos.Distinct().ToList();
        var puntos = (await context.Set<Product>().AsNoTracking().Where(p => distintos.Contains(p.Id))
                .Select(p => new { p.Id, p.QualificationPoins }).ToListAsync(ct))
            .ToDictionary(p => p.Id, p => p.QualificationPoins);
        return productos.Sum(id => puntos.GetValueOrDefault(id));
    }

    /// <summary>Tras un guardado confirmado: lo ajustado deja de estar pendiente.</summary>
    public static void Saved(DbContext context) => Ajustado.Remove(context);

    public static async Task RecordAsync(DbContext context, DateTime now, CancellationToken ct = default)
    {
        var nivelPuntos = new Dictionary<int, int>();
        var pedidoPuntos = new Dictionary<string, int>(StringComparer.Ordinal);
        async Task<int> Nivel(int nivel) =>
            nivelPuntos.TryGetValue(nivel, out var p) ? p : nivelPuntos[nivel] = await LevelPointsAsync(context, nivel, ct);
        async Task<int> Pedido(string pedido) =>
            pedidoPuntos.TryGetValue(pedido, out var q) ? q : pedidoPuntos[pedido] = await OrderPointsAsync(context, pedido, ct);
        // Al entrar en Active: el pedido si lo hay (y no es de otro nivel que el actual) o el producto del nivel.
        async Task<int> Actual(MembershipSubscription s, bool nivelCambiado, bool pedidoNuevo) =>
            !string.IsNullOrEmpty(s.LastOrderId) && (pedidoNuevo || !nivelCambiado)
                ? await Pedido(s.LastOrderId) : await Nivel(s.MembershipLevelId);

        // 1. Lo que aporta cada suscripcion tras el guardado, y los miembros tocados.
        var entradas = context.ChangeTracker.Entries<MembershipSubscription>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (entradas.Count == 0 && !Ajustado.TryGetValue(context, out _)) return;

        var tocados = new HashSet<string>(StringComparer.Ordinal);
        // Las que salen de Active en este guardado: base de una subida o bajada sin pedido (la nueva nace en el mismo guardado).
        var salen = entradas
            .Where(e => e.State == EntityState.Modified
                        && e.Property(x => x.SubscriptionStatus).OriginalValue == MembershipStatus.Active
                        && e.Entity.SubscriptionStatus != MembershipStatus.Active)
            .Select(e => (Miembro: e.Property(x => x.MemberId).OriginalValue, Nivel: e.Property(x => x.MembershipLevelId).OriginalValue,
                          Aporte: e.Property(x => x.DualTeamContribution).OriginalValue))
            .ToList();
        var usadas = new HashSet<int>();

        foreach (var e in entradas)
        {
            var s = e.Entity;
            switch (e.State)
            {
                case EntityState.Added:
                {
                    tocados.Add(s.MemberId);
                    var c = 0;
                    if (s.SubscriptionStatus == MembershipStatus.Active)
                    {
                        var i = string.IsNullOrEmpty(s.LastOrderId) ? Base(salen, usadas, s) : -1;
                        if (i >= 0)
                        {
                            usadas.Add(i);
                            c = Math.Max(0, salen[i].Aporte - await Nivel(salen[i].Nivel) + await Nivel(s.MembershipLevelId));
                        }
                        else c = await Actual(s, nivelCambiado: false, pedidoNuevo: true);
                    }
                    Fijar(s, c);
                    break;
                }
                case EntityState.Deleted:
                    tocados.Add(e.Property(x => x.MemberId).OriginalValue);
                    break;
                case EntityState.Modified:
                {
                    var estadoAntes = e.Property(x => x.SubscriptionStatus).OriginalValue;
                    var antes = estadoAntes == MembershipStatus.Active;
                    var ahora = s.SubscriptionStatus == MembershipStatus.Active;
                    var nivelAntes = e.Property(x => x.MembershipLevelId).OriginalValue;
                    var miembroAntes = e.Property(x => x.MemberId).OriginalValue;
                    var aporte = e.Property(x => x.DualTeamContribution).OriginalValue;
                    var nivelCambiado = nivelAntes != s.MembershipLevelId;
                    var pedidoNuevo = !string.Equals(e.Property(x => x.LastOrderId).OriginalValue, s.LastOrderId, StringComparison.Ordinal);
                    if (estadoAntes != s.SubscriptionStatus || nivelCambiado || miembroAntes != s.MemberId)
                    {
                        tocados.Add(miembroAntes);
                        tocados.Add(s.MemberId);
                    }

                    if (!ahora) Fijar(s, 0);
                    else if (!antes) Fijar(s, await Actual(s, nivelCambiado, pedidoNuevo));
                    else if (nivelCambiado)
                        Fijar(s, pedidoNuevo && !string.IsNullOrEmpty(s.LastOrderId)
                            ? await Pedido(s.LastOrderId)
                            : Math.Max(0, aporte - await Nivel(nivelAntes) + await Nivel(s.MembershipLevelId)));
                    // Activa que sigue activa sin cambiar de nivel (renovacion): se queda con lo que aportaba.
                    break;
                }
            }
        }

        // 2. El objetivo de cada miembro tocado: la suma de lo que aportan sus suscripciones activas tras el guardado (las
        //    seguidas con sus valores actuales y las demas de la base). Cuenta las activas borradas logicamente, como los
        //    calificados (Q-R21) y 05-realinear-puntos-por-membresia.sql.
        var objetivo = tocados.ToDictionary(m => m, _ => 0, StringComparer.Ordinal);
        if (tocados.Count > 0)
        {
            var seguidas = context.ChangeTracker.Entries<MembershipSubscription>().ToList();
            var idsSeguidas = seguidas.Select(x => x.Entity.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var x in seguidas)
                if (x.State is not (EntityState.Deleted or EntityState.Detached)
                    && x.Entity.SubscriptionStatus == MembershipStatus.Active && objetivo.ContainsKey(x.Entity.MemberId))
                    objetivo[x.Entity.MemberId] += x.Entity.DualTeamContribution;
            var lista = tocados.ToList();
            var enBase = await context.Set<MembershipSubscription>().AsNoTracking()
                .Where(x => lista.Contains(x.MemberId) && x.SubscriptionStatus == MembershipStatus.Active)
                .Select(x => new { x.Id, x.MemberId, x.DualTeamContribution }).ToListAsync(ct);
            foreach (var x in enBase.Where(x => !idsSeguidas.Contains(x.Id)))
                objetivo[x.MemberId] += x.DualTeamContribution;
        }

        // 3. El cambio neto (objetivo - PersonalPoints de la base), a PersonalPoints y a EnrollmentPoints del propio.
        //    Idempotente: lo ya ajustado en este contexto (un guardado anterior que fallo) se descuenta.
        var ajustado = Ajustado.GetOrCreateValue(context);
        var neto = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (memberId, meta) in objetivo)
        {
            var fila = await FilaDeAsync(context, memberId, ct);
            var ya = fila is null ? 0 : ajustado.GetValueOrDefault(fila);
            var delta = meta - ((fila?.PersonalPoints ?? 0) - ya);
            if (delta == 0 && ya == 0) continue;
            fila ??= context.Set<MemberStatisticEntity>().Add(new MemberStatisticEntity
            {
                MemberId = memberId, CreatedBy = Actor, CreationDate = now
            }).Entity;
            fila.PersonalPoints += delta - ya;
            fila.EnrollmentPoints += delta - ya;
            if (delta == 0) ajustado.Remove(fila); else ajustado[fila] = delta;
            if (delta != 0) neto[memberId] = delta;
        }
        foreach (var (fila, ya) in ajustado.ToList())
            if (!objetivo.ContainsKey(fila.MemberId) && ya != 0)
            {
                // Un reintento en el que este miembro ya no se toca: se deshace lo ajustado.
                fila.PersonalPoints -= ya;
                fila.EnrollmentPoints -= ya;
                ajustado.Remove(fila);
            }

        // 4. El neto sube como delta: al binario (un propio con puntos) y a los ascendientes de la linea de patrocinio.
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
            var meta = neto.GetValueOrDefault(memberId);

            var faltaBinario = meta - yaBinario.GetValueOrDefault(memberId);
            if (faltaBinario != 0)
                context.Set<DualLegDelta>().Add(new DualLegDelta
                {
                    StartMemberId = memberId, Side = TreeSide.Left, Points = faltaBinario, NodeCount = 0, QualifiedCount = 0,
                    OwnChange = true, CreationDate = now, CreatedBy = Actor
                });

            var faltaInscripcion = meta - yaInscripcion.GetValueOrDefault(memberId);
            if (faltaInscripcion != 0)
                foreach (var id in await AscendientesAsync(context, memberId, ct))
                    context.Set<MemberStatisticDelta>().Add(new MemberStatisticDelta
                    {
                        MemberId = id, EnrollmentPointsDelta = faltaInscripcion, SourceMemberId = memberId,
                        IsApplied = false, CreatedBy = Actor, CreationDate = now
                    });
        }
    }

    private static void Fijar(MembershipSubscription s, int aporte)
    {
        if (s.DualTeamContribution != aporte) s.DualTeamContribution = aporte;
        if (s.EnrollmentContribution != aporte) s.EnrollmentContribution = aporte;
    }

    /// <summary>
    /// La suscripcion que sale de Active en este guardado de la que parte una nueva activa sin pedido (subida o bajada): la
    /// del mismo miembro con el nivel anterior de la nueva si lo tiene, si no la primera del mismo miembro. -1 si no hay.
    /// </summary>
    private static int Base(List<(string Miembro, int Nivel, int Aporte)> salen, HashSet<int> usadas, MembershipSubscription nueva)
    {
        var candidata = -1;
        for (var i = 0; i < salen.Count; i++)
        {
            if (usadas.Contains(i) || !string.Equals(salen[i].Miembro, nueva.MemberId, StringComparison.Ordinal)) continue;
            if (nueva.PreviousMembershipLevelId == salen[i].Nivel) return i;
            if (candidata < 0) candidata = i;
        }
        return candidata;
    }

    /// <summary>La fila de estadisticas del miembro (la primera por Id), seguida o leida y seguida.</summary>
    private static async Task<MemberStatisticEntity?> FilaDeAsync(DbContext context, string memberId, CancellationToken ct)
    {
        var seguida = context.ChangeTracker.Entries<MemberStatisticEntity>()
            .Where(e => e.State != EntityState.Deleted && e.Entity.MemberId == memberId)
            .OrderBy(e => e.Entity.Id == 0 ? long.MaxValue : e.Entity.Id)
            .Select(e => e.Entity).FirstOrDefault();
        return seguida ?? await context.Set<MemberStatisticEntity>().Where(s => s.MemberId == memberId).OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Los ascendientes de inscripcion (sin el propio), por la ruta de su nodo vigente (el de mas reciente creacion),
    /// seguido o en la base; sin nodo propio, la linea de su patrocinador. Sin nada de eso, ninguno.
    /// </summary>
    private static async Task<List<string>> AscendientesAsync(DbContext context, string memberId, CancellationToken ct)
    {
        var ruta = await RutaAsync(context, memberId, ct);
        if (ruta is null)
        {
            // Sin nodo propio todavia (un alta que se activa antes de tenerlo): la linea de su patrocinador, el incluido,
            // como la recorria la activacion.
            var patrocinador = context.ChangeTracker.Entries<MemberProfile>()
                                   .Where(e => e.Entity.MemberId == memberId).Select(e => e.Entity.SponsorMemberId).FirstOrDefault()
                               ?? await context.Set<MemberProfile>().AsNoTracking().Where(m => m.MemberId == memberId)
                                   .Select(m => m.SponsorMemberId).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(patrocinador)) ruta = await RutaAsync(context, patrocinador, ct);
        }
        return (ruta ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => !string.Equals(id, memberId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).ToList();
    }

    private static async Task<string?> RutaAsync(DbContext context, string memberId, CancellationToken ct) =>
        context.ChangeTracker.Entries<GenealogyEntity>()
            .Where(e => e.State != EntityState.Deleted && e.Entity.MemberId == memberId)
            .Select(e => e.Entity.HierarchyPath).FirstOrDefault()
        ?? await context.Set<GenealogyEntity>().AsNoTracking().Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id)
            .Select(g => g.HierarchyPath).FirstOrDefaultAsync(ct);
}
