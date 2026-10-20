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
/// <item>Bajo un candado por miembro (<see cref="MemberPointsLocks"/>, tercera revision del bloque 2, I-a) y con lecturas
/// de la base (no de lo seguido, que puede ser viejo), PersonalPoints pasa a ser el objetivo en el mismo guardado; los
/// EnrollmentPoints del propio NO se escriben aqui (I-b): le llegan como una fila mas de MemberStatisticDeltas, igual que a
/// sus ascendientes, porque un valor absoluto pisaba el MERGE relativo de ApplyMemberStatisticDeltasJob. PersonalPoints ya incluye los propios de
/// puntos pendientes en la cola (se suman aqui al encolarlos), asi que el cambio neto es objetivo - PersonalPoints: las
/// piernas acaban en PersonalPoints al vaciar (lo aplicado, PersonalPoints - pendientes, mas lo pendiente), igual que la
/// hoja de <see cref="LegPointMaintenance.EnqueueLeafAsync"/>.</item>
/// <item>Y sube como delta: un propio con puntos en DualLegDeltas (O(1); llega a las piernas de sus ascendientes del
/// binario al vaciar, y espera si no esta colocado) y una fila de MemberStatisticDeltas para el propio y otra por
/// ascendiente de su linea de patrocinio (hasta el Bloque 4). Un PersonalPoints escrito a mano que no este en las piernas descuadra las piernas en
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

        // Tercera revision del bloque 2 (I-a): candado por miembro (los de antes y despues de cada suscripcion que cambia y
        // los ya ajustados en un guardado que fallo) y, ya con el, los valores de la base de esas suscripciones.
        if (context.Database.IsRelational())
        {
            var candidatos = entradas.SelectMany(e => new[] { e.Property(x => x.MemberId).OriginalValue, e.Entity.MemberId });
            if (Ajustado.TryGetValue(context, out var previos)) candidatos = candidatos.Concat(previos.Keys.Select(f => f.MemberId));
            await MemberPointsLocks.AcquireAsync(context, candidatos, ct).ConfigureAwait(false);
            await RefrescarAsync(context, entradas, ct).ConfigureAwait(false);
        }

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

        // 2. El objetivo de cada miembro tocado: la suma de lo que aportan sus suscripciones activas tras el guardado. Las que
        //    se anaden, cambian o borran en este guardado con sus valores (refrescados con la base, I-a); las demas, como
        //    estan en la base (una seguida sin cambios puede ser vieja). Cuenta las activas borradas logicamente, como los
        //    calificados (Q-R21) y 05-realinear-puntos-por-membresia.sql.
        var objetivo = tocados.ToDictionary(m => m, _ => 0, StringComparer.Ordinal);
        if (tocados.Count > 0)
        {
            var seguidas = context.ChangeTracker.Entries<MembershipSubscription>()
                .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
            var idsSeguidas = seguidas.Select(x => x.Entity.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var x in seguidas)
                if (x.State != EntityState.Deleted
                    && x.Entity.SubscriptionStatus == MembershipStatus.Active && objetivo.ContainsKey(x.Entity.MemberId))
                    objetivo[x.Entity.MemberId] += x.Entity.DualTeamContribution;
            var lista = tocados.ToList();
            var enBase = await context.Set<MembershipSubscription>().AsNoTracking().IgnoreQueryFilters()
                .Where(x => lista.Contains(x.MemberId) && x.SubscriptionStatus == MembershipStatus.Active)
                .Select(x => new { x.Id, x.MemberId, x.DualTeamContribution }).ToListAsync(ct).ConfigureAwait(false);
            foreach (var x in enBase.Where(x => !idsSeguidas.Contains(x.Id)))
                objetivo[x.MemberId] += x.DualTeamContribution;
        }

        // 3. PersonalPoints pasa a ser el objetivo y el cambio neto es objetivo - PersonalPoints LEIDO DE LA BASE bajo el
        //    candado (I-a: una fila seguida puede ser vieja). Los EnrollmentPoints del propio ya no se escriben aqui (I-b):
        //    un valor absoluto desde una lectura vieja pisaba el MERGE relativo de ApplyMemberStatisticDeltasJob; el propio
        //    recibe su cambio como una fila mas de MemberStatisticDeltas (paso 4). Idempotente: un reintento vuelve a partir
        //    de la base; lo ajustado en una fila nueva (sin guardar) se recuerda para descontarlo.
        var ajustado = Ajustado.GetOrCreateValue(context);
        var neto = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (memberId, meta) in objetivo)
        {
            var (fila, base_) = await FilaDeAsync(context, memberId, ajustado, ct).ConfigureAwait(false);
            var delta = meta - base_;
            if (fila is null)
            {
                if (delta == 0) continue;
                fila = context.Set<MemberStatisticEntity>().Add(new MemberStatisticEntity
                {
                    MemberId = memberId, CreatedBy = Actor, CreationDate = now
                }).Entity;
            }
            FijarPuntos(context, fila, base_, meta);
            if (delta == 0) ajustado.Remove(fila); else ajustado[fila] = delta;
            if (delta != 0) neto[memberId] = delta;
        }
        foreach (var (fila, _) in ajustado.ToList())
            if (!objetivo.ContainsKey(fila.MemberId))
            {
                // Un reintento en el que este miembro ya no se toca: se deshace lo ajustado.
                var (_, base_) = await FilaDeAsync(context, fila.MemberId, ajustado, ct).ConfigureAwait(false);
                FijarPuntos(context, fila, base_, base_);
                ajustado.Remove(fila);
            }

        // 4. El neto sube como delta: al binario (un propio con puntos) y a la linea de patrocinio, el propio incluido.
        var yaBinario = context.ChangeTracker.Entries<DualLegDelta>()
            .Where(e => e.State == EntityState.Added && e.Entity.OwnChange && e.Entity.CreatedBy == Actor)
            .GroupBy(e => e.Entity.StartMemberId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (int)g.Sum(e => e.Entity.Points), StringComparer.Ordinal);
        var yaInscripcion = context.ChangeTracker.Entries<MemberStatisticDelta>()
            .Where(e => e.State == EntityState.Added && e.Entity.CreatedBy == Actor)
            .GroupBy(e => e.Entity.SourceMemberId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                // Cada vez se encola lo mismo a cada uno (el propio y sus ascendientes): lo ya encolado es lo del propio.
                g => g.Where(e => string.Equals(e.Entity.MemberId, g.Key, StringComparison.Ordinal)).Sum(e => e.Entity.EnrollmentPointsDelta),
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
                foreach (var id in (await AscendientesAsync(context, memberId, ct).ConfigureAwait(false)).Prepend(memberId))
                    context.Set<MemberStatisticDelta>().Add(new MemberStatisticDelta
                    {
                        MemberId = id, EnrollmentPointsDelta = faltaInscripcion, SourceMemberId = memberId,
                        IsApplied = false, CreatedBy = Actor, CreationDate = now
                    });
        }
    }

    /// <summary>
    /// I-a: PersonalPoints de la fila pasa a <paramref name="valor"/> partiendo de <paramref name="base_"/> (lo de la base):
    /// el original se iguala a la base para que EF escriba si y solo si cambia respecto de lo confirmado.
    /// </summary>
    private static void FijarPuntos(DbContext context, MemberStatisticEntity fila, int base_, int valor)
    {
        var entrada = context.Entry(fila);
        var pp = entrada.Property(x => x.PersonalPoints);
        if (entrada.State == EntityState.Added) { pp.CurrentValue = valor; return; }
        pp.OriginalValue = base_;
        pp.CurrentValue = valor;
        pp.IsModified = base_ != valor;
    }

    /// <summary>
    /// I-a: las suscripciones que cambian o se borran en este guardado toman de la base (leida bajo el candado) los valores
    /// de los que depende el calculo: lo que el guardado cambia conserva su valor nuevo y su original pasa a ser el de la
    /// base; lo que no cambia se iguala a la base (una seguida desde hace rato puede ser vieja).
    /// </summary>
    private static async Task RefrescarAsync(DbContext context, List<EntityEntry<MembershipSubscription>> entradas, CancellationToken ct)
    {
        var cambiadas = entradas.Where(e => e.State is EntityState.Modified or EntityState.Deleted).ToList();
        if (cambiadas.Count == 0) return;
        var ids = cambiadas.Select(e => e.Entity.Id).ToList();
        var enBase = await context.Set<MembershipSubscription>().AsNoTracking().IgnoreQueryFilters()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.MemberId, s.MembershipLevelId, s.SubscriptionStatus, s.DualTeamContribution, s.EnrollmentContribution, s.LastOrderId })
            .ToListAsync(ct).ConfigureAwait(false);
        var porId = enBase.ToDictionary(s => s.Id, StringComparer.Ordinal);
        foreach (var e in cambiadas)
        {
            if (!porId.TryGetValue(e.Entity.Id, out var b)) continue;
            Igualar(e.Property(x => x.MemberId), b.MemberId);
            Igualar(e.Property(x => x.MembershipLevelId), b.MembershipLevelId);
            Igualar(e.Property(x => x.SubscriptionStatus), b.SubscriptionStatus);
            Igualar(e.Property(x => x.DualTeamContribution), b.DualTeamContribution);
            Igualar(e.Property(x => x.EnrollmentContribution), b.EnrollmentContribution);
            Igualar(e.Property(x => x.LastOrderId), b.LastOrderId);
        }
    }

    private static void Igualar<T>(PropertyEntry<MembershipSubscription, T> p, T enBase)
    {
        if (p.IsModified) { p.OriginalValue = enBase; return; }
        if (EqualityComparer<T>.Default.Equals(p.OriginalValue, enBase) && EqualityComparer<T>.Default.Equals(p.CurrentValue, enBase)) return;
        p.OriginalValue = enBase;
        p.CurrentValue = enBase;
        p.IsModified = false;
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

    /// <summary>
    /// La fila de estadisticas del miembro (la primera por Id en la base, seguida o leida y seguida) y sus PersonalPoints EN LA
    /// BASE (I-a). Sin fila en la base, la nueva seguida si la hay (su valor menos lo que este calculo le ajusto) o ninguna.
    /// </summary>
    private static async Task<(MemberStatisticEntity? Fila, int Base)> FilaDeAsync(
        DbContext context, string memberId, Dictionary<MemberStatisticEntity, int> ajustado, CancellationToken ct)
    {
        var enBase = await context.Set<MemberStatisticEntity>().AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.MemberId == memberId).OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.PersonalPoints }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (enBase is not null)
        {
            var fila = context.ChangeTracker.Entries<MemberStatisticEntity>().Where(e => e.Entity.Id == enBase.Id).Select(e => e.Entity).FirstOrDefault()
                       ?? await context.Set<MemberStatisticEntity>().IgnoreQueryFilters().FirstAsync(s => s.Id == enBase.Id, ct).ConfigureAwait(false);
            return (fila, enBase.PersonalPoints);
        }
        var nueva = context.ChangeTracker.Entries<MemberStatisticEntity>()
            .Where(e => e.State == EntityState.Added && e.Entity.MemberId == memberId)
            .Select(e => e.Entity).FirstOrDefault();
        return nueva is null ? (null, 0) : (nueva, nueva.PersonalPoints - ajustado.GetValueOrDefault(nueva));
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
