using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Bloque D (Tarea 19): mediciones contra la base de escala (MLMConquerorGlobalEditionDb_Escala), con las
/// altas completas de Scripts/sql/plan-p/escala/01-generar-altas.sql: ESC-M con N nodos en su equipo binario (columna
/// de 8.500 niveles y subarboles) y N + 50.000 en su equipo de inscripcion, y ESC-P, raiz provisional con 50.000.
/// Cada medicion se repite 3 veces y se compara la MEDIANA con el objetivo (la prueba falla si no se cumple).
/// Las escrituras van en una transaccion que se deshace, salvo las concurrentes y el movimiento de ESC-P, que
/// confirman y despues dejan la base como estaba. Las operaciones medidas usan el tiempo de espera de comando de
/// produccion (30 s, el de EF por defecto: ningun servicio lo cambia); la preparacion y la limpieza, uno largo.
/// <code>
/// $env:MLM_ESCALA_SQLSERVER = "Server=.;Database=MLMConquerorGlobalEditionDb_Escala;Integrated Security=true;TrustServerCertificate=True;Encrypt=True"
/// dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --settings MLMConquerorGlobalEdition.BizCenter.Tests/Medicion.runsettings --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
/// </code>
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
[Trait(SqlServerMedicionPlanMTests.Categoria, SqlServerMedicionPlanMTests.Medicion)]
public class EscalaBinarioTests
{
    private static readonly TimeSpan Objetivo = TimeSpan.FromSeconds(2);
    private const int TimeoutProduccion = 30;
    private const string Medido = "ESC-M";
    private const string Provisional = "ESC-P";
    /// <summary>Primer eslabon de la columna: tiene los dos hijos (ESC-M solo tiene el izquierdo) y su pierna izquierda es la cadena de 8.500 niveles.</summary>
    private const string Cabeza = "ESC-2";

    private readonly ITestOutputHelper _salida;
    public EscalaBinarioTests(ITestOutputHelper salida) => _salida = salida;

    private sealed class Reloj : SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Con <paramref name="db"/>, tambien las lecturas logicas y la CPU de la sesion (Plan Q, Tarea 33).</summary>
    private Task<TimeSpan> Mediana(string que, Func<int, Task> paso, AppDbContext? db = null) => EscalaDb.MedianaAsync(_salida, que, paso, db);

    private static async Task<Domain.Entities.Tree.DualTeamEntity> NodoAsync(AppDbContext db, string id) =>
        await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == id);

    // ─────────────────────────────── Datos generados ───────────────────────────────

    [EscalaFact]
    public async Task Informe_de_la_base_de_escala()
    {
        await using var db = EscalaDb.Db();
        var m = await NodoAsync(db, Medido);
        var p = await NodoAsync(db, Provisional);
        var tam = await db.Database.SqlQuery<Tamano>($"""
            SELECT i.name AS Indice, SUM(ps.used_page_count) * 8 / 1024 AS Mb, SUM(ps.row_count) AS Filas
            FROM sys.dm_db_partition_stats ps JOIN sys.indexes i ON i.object_id = ps.object_id AND i.index_id = ps.index_id
            WHERE ps.object_id = OBJECT_ID('DualTeamTree') GROUP BY i.name
            """).ToListAsync();
        var clave = await db.Database.SqlQuery<int>($"SELECT MAX(DATALENGTH(BinaryPath)) AS [Value] FROM DualTeamTree").SingleAsync();
        var prof = await db.DualTeamTree.MaxAsync(d => d.Depth);
        var gen = await db.GenealogyTree.AsNoTracking().CurrentOfAsync(Medido);
        var equipoGen = await db.GenealogyTree.AsNoTracking().CurrentUnder(gen!.Node).CountAsync() - 1;
        var nivelGen = await db.GenealogyTree.MaxAsync(g => g.Level);

        _salida.WriteLine($"[ESCALA] ESC-M: binario {m.LeftLegCount + m.RightLegCount} (izq {m.LeftLegCount}, der {m.RightLegCount}), " +
                          $"inscripcion {equipoGen}; ESC-P: {p.LeftLegCount + p.RightLegCount + 1} con el; profundidad maxima {prof}, " +
                          $"clave maxima {clave} B, nivel maximo de inscripcion {nivelGen}");
        foreach (var t in tam) _salida.WriteLine($"[ESCALA] DualTeamTree {t.Indice}: {t.Mb} MB, {t.Filas} filas");
        // Plan Q (Tarea 33): DualTeamTree ya sin la ruta de texto, y las tablas que el Plan Q acoto o anadio.
        _salida.WriteLine($"[ESCALA] DualTeamTree en total (sin ruta de texto): {tam.Sum(t => t.Mb)} MB");
        var filas = await db.Database.SqlQuery<Tamano>($"""
            SELECT OBJECT_NAME(ps.object_id) AS Indice, SUM(ps.used_page_count) * 8 / 1024 AS Mb,
                   SUM(CASE WHEN ps.index_id IN (0, 1) THEN ps.row_count ELSE 0 END) AS Filas
            FROM sys.dm_db_partition_stats ps
            WHERE ps.object_id IN (OBJECT_ID('RankEvaluationQueue'), OBJECT_ID('MemberStatisticDeltas'), OBJECT_ID('MemberNotifications'),
                                   OBJECT_ID('MemberRankHistories'), OBJECT_ID('DualLegDeltas'), OBJECT_ID('GenealogyTree'), OBJECT_ID('MemberProfiles'))
            GROUP BY ps.object_id
            """).ToListAsync();
        foreach (var t in filas.OrderBy(t => t.Indice)) _salida.WriteLine($"[ESCALA] {t.Indice}: {t.Mb} MB, {t.Filas} filas");
        var archivos = await db.Database.SqlQuery<Tamano>($"SELECT name AS Indice, CAST(size AS bigint) / 128 AS Mb, CAST(0 AS bigint) AS Filas FROM sys.database_files").ToListAsync();
        foreach (var t in archivos) _salida.WriteLine($"[ESCALA] fichero {t.Indice}: {t.Mb} MB");

        clave.Should().BeLessThan(DualBinaryPath.MaxBytes);
        (p.LeftLegCount + p.RightLegCount + 1).Should().Be(50_000);
    }

    public sealed class Tamano
    {
        public string Indice { get; set; } = "";
        public long Mb { get; set; }
        public long Filas { get; set; }
    }

    // ─────────────────────────────── Mi equipo (binario) ───────────────────────────────

    [EscalaFact]
    public async Task Mi_equipo_binario_conteo()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var m = await NodoAsync(db, Medido);
        var esperado = m.LeftLegCount + m.RightLegCount;
        var t = await Mediana($"Mi equipo binario de ESC-M, conteo ({esperado})", async _ =>
            (await DualSubtree.Under(db, m).CountAsync()).Should().Be(esperado), db);
        t.Should().BeLessThan(Objetivo);
    }

    [EscalaFact]
    public async Task Mi_equipo_binario_primera_pagina_y_pagina_1000()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));
        // Plan Q (DQ7, DQ14): sin orden pedido, por la clave; el total sale de los contadores del nodo.
        var total = 0;
        var t1 = await Mediana("Mi equipo binario de ESC-M, primera pagina (servicio, orden por la clave, con el total)", async _ =>
        {
            var r = await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20 });
            r.Items.Should().HaveCount(20);
            total = r.TotalCount;
        }, db);
        _salida.WriteLine($"[ESCALA] Mi equipo binario de ESC-M: total de la rejilla {total}");
        var t2 = await Mediana("Mi equipo binario de ESC-M, pagina 1.000 (servicio)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { Page = 1000, PageSize = 20 })).Items.Should().HaveCount(20), db);

        // La del plan: orden por la clave y paginacion por clave (BinaryPath > @ultima): la pagina 1.000 empieza
        // detras de la ultima clave de la 999.
        var m = await NodoAsync(db, Medido);
        var ultima = await DualSubtree.Under(db, m).AsNoTracking().OrderBy(d => d.BinaryPath).Skip(999 * 20 - 1)
            .Select(d => d.BinaryPath).FirstAsync();
        var (_, hi) = DualBinaryPath.SubtreeBounds(m.BinaryPath!);
        var t3 = await Mediana("Mi equipo binario de ESC-M, pagina 1.000 por clave", async _ =>
            (await db.DualTeamTree.FromSqlInterpolated($"SELECT TOP (20) * FROM DualTeamTree WHERE BinaryPath > {ultima} AND BinaryPath < {hi} AND IsDeleted = 0 ORDER BY BinaryPath")
                .AsNoTracking().ToListAsync()).Should().HaveCount(20), db);

        new[] { t1, t2, t3 }.Should().OnlyContain(t => t < Objetivo);
    }

    [EscalaFact]
    public async Task Mi_equipo_binario_con_busqueda_por_nombre()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));
        // Plan Q (DQ8): por el comienzo del nombre completo («lucia diaz»), con el limite de equipo grande.
        var t = await Mediana("Mi equipo binario de ESC-M, busqueda 'Lucia Diaz' por prefijo (1 de cada 100)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20, Search = "Lucia Diaz" })).TotalCount.Should().BePositive(), db);
        t.Should().BeLessThan(Objetivo, "si no se cumple, se propone un indice de texto (Plan P, Bloque D)");
    }

    /// <summary>
    /// Plan Q (Bloques 4 y 5): las busquedas por prefijo de las dos rejillas de Mi equipo y del buscador del visualizador sobre
    /// ESC-M. «amb» y «amb-1» son los terminos de la revision del bloque 5 (en la base de escala no casan con nada: los ids son
    /// ESC-…); «esc» y «esc-1» son sus equivalentes aqui (la rama del id solo corre desde «esc-1»). «a» y «mari» casan con
    /// decenas de miles de nombres. Lo acota el tope por rama, no el numero de miembros.
    /// </summary>
    [EscalaTheory]
    [InlineData("a")]
    [InlineData("amb")]
    [InlineData("amb-1")]
    [InlineData("esc")]
    [InlineData("esc-1")]
    [InlineData("mari")]
    public async Task Busquedas_por_prefijo_en_Mi_equipo_y_el_visualizador(string termino)
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var binario = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));
        var inscripcion = ClientesDelEquipoTests.Servicio(db);
        int hb = 0, hi = 0, hv = 0;
        var tb = await Mediana($"Mi equipo binario de ESC-M, busqueda '{termino}'", async _ =>
            hb = (await binario.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20, Search = termino })).TotalCount, db);
        var ti = await Mediana($"Mi equipo de inscripcion de ESC-M, busqueda '{termino}'", async _ =>
            hi = (await inscripcion.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20, Search = termino })).TotalCount, db);
        var tv = await Mediana($"Visualizador, buscador de ESC-M '{termino}' (25 con camino)", async _ =>
            hv = (await binario.SearchBinarySubtreeAsync(Medido, termino, 25)).Count, db);
        _salida.WriteLine($"[ESCALA] '{termino}': hallados binario {hb}, inscripcion {hi}, visualizador {hv}");
        new[] { tb, ti, tv }.Should().OnlyContain(t => t < Objetivo);
    }

    [EscalaFact]
    public async Task Puntos_de_pierna_lectura()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        await NodoAsync(db, Medido);
        var t = await Mediana("Puntos y contadores de pierna de ESC-M (lectura)", async _ =>
            (await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId == Medido)
                .Select(d => new { d.LeftLegPoints, d.RightLegPoints, d.LeftLegCount, d.RightLegCount }).SingleAsync())
                .LeftLegCount.Should().BePositive(), db);
        t.Should().BeLessThan(TimeSpan.FromMilliseconds(50));
    }

    // ─────────────────────────────── Escrituras ───────────────────────────────

    private static Task<Domain.Entities.Tree.DualTeamEntity> NodoAbiertoMasHondoAsync(AppDbContext db) =>
        db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId != null && db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId) < 2)
            .OrderByDescending(d => d.Depth).ThenBy(d => d.Id).FirstAsync();

    [EscalaFact]
    public async Task Colocar_una_hoja_a_unos_8500_niveles()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var hondo = await NodoAbiertoMasHondoAsync(db);
        hondo.Depth.Should().BeGreaterThan(8400);
        var motor = new PlacementService(db, new Reloj());
        // Plan Q (Bloque 2): la colocacion ya no sube por los ascendientes; encola un delta. Se mide solo PlaceAsync (sembrar el
        // perfil y los puntos, y deshacer, quedan fuera). Objetivo 300 ms.
        var colocar = new List<TimeSpan>();
        await Mediana($"Colocar una hoja bajo {hondo.MemberId} (profundidad {hondo.Depth + 1}), con la siembra y el deshacer", async i =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var id = $"EZH{i}{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            await BinarioDb.SembrarPerfilAsync(db, id, hondo.MemberId);
            await BinarioDb.PuntosAsync(db, id, 6);
            var c = Stopwatch.StartNew();
            var r = await motor.PlaceAsync(id, hondo.MemberId);
            c.Stop();
            colocar.Add(c.Elapsed);
            r.IsSuccess.Should().BeTrue(r.Error);
            await tx.RollbackAsync();
        }, db);
        var t = colocar.OrderBy(x => x).ElementAt(1);
        _salida.WriteLine($"[ESCALA] Colocar una hoja a {hondo.Depth + 1} niveles (solo PlaceAsync): {string.Join(" / ", colocar.Select(x => $"{x.TotalMilliseconds:0} ms"))} -> mediana {t.TotalMilliseconds:0} ms");
        t.Should().BeLessThan(TimeSpan.FromMilliseconds(300));
    }

    /// <summary>
    /// Plan Q (Tarea 33): 1.000 deltas de hoja (1 nodo y 6 puntos, como encola una colocacion) sobre nodos abiertos a mas de
    /// 8.000 niveles, en una transaccion que se deshace; se mide LegPointMaintenance.DrainAsync. Objetivo: menos de 60 s, y
    /// las piernas de ESC-M (ascendiente de todos) suben exactamente 1.000 nodos y 6.000 puntos.
    /// </summary>
    [EscalaFact]
    public async Task Vaciar_los_deltas_de_1000_colocaciones_en_la_linea_profunda()
    {
        await using var db = EscalaDb.Db();
        (await db.DualLegDeltas.CountAsync()).Should().Be(0, "la base de escala sale con la cola vacia");
        var abiertos = await db.DualTeamTree.AsNoTracking()
            .Where(d => d.Depth > 8000 && d.IsDeleted == false && db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId && !k.IsDeleted) < 2)
            .OrderByDescending(d => d.Depth).ThenBy(d => d.Id).Select(d => new { d.MemberId, d.Depth }).Take(1000).ToListAsync();
        abiertos.Should().HaveCount(1000);
        var m0 = await NodoAsync(db, Medido);
        var tiempos = new List<TimeSpan>();
        var t = await Mediana($"Vaciar 1.000 deltas de hoja a {abiertos[^1].Depth}-{abiertos[0].Depth} niveles", async i =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var ahora = DateTime.Now;
            foreach (var a in abiertos)
                db.DualLegDeltas.Add(new Domain.Entities.Tree.DualLegDelta
                {
                    StartMemberId = a.MemberId, Side = (a.Depth + i) % 2 == 0 ? TreeSide.Left : TreeSide.Right,
                    Points = 6, NodeCount = 1, CreationDate = ahora, CreatedBy = "plan-q-medicion"
                });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var c = Stopwatch.StartNew();
            var aplicados = await LegPointMaintenance.DrainAsync(db);
            c.Stop();
            tiempos.Add(c.Elapsed);
            var m1 = await NodoAsync(db, Medido);
            aplicados.Should().Be(1000);
            (m1.LeftLegCount - m0.LeftLegCount).Should().Be(1000);
            (m1.LeftLegPoints - m0.LeftLegPoints).Should().Be(6000);
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
        }, db);
        var vaciado = tiempos.OrderBy(x => x).ElementAt(1);
        _salida.WriteLine($"[ESCALA] Vaciar 1.000 deltas (solo DrainAsync): {string.Join(" / ", tiempos.Select(x => $"{x.TotalMilliseconds:0} ms"))} -> mediana {vaciado.TotalMilliseconds:0} ms");
        vaciado.Should().BeLessThan(TimeSpan.FromSeconds(60));
    }

    [EscalaFact]
    public async Task Cola_sin_cache()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var motor = new PlacementService(db, new Reloj());
        var t = await Mediana("Derrame de ESC-2 (cabeza de la columna) con su cola (DualTeamLegFrontiers) vacia", async i =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM DualTeamLegFrontiers WHERE SponsorMemberId = {0}", Cabeza);
            var id = $"EZC{i}{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            await BinarioDb.SembrarPerfilAsync(db, id, Cabeza);
            var r = await motor.PlaceAsync(id, Cabeza);
            r.IsSuccess.Should().BeTrue(r.Error);
            r.Value!.Depth.Should().BeGreaterThan(8400, "ESC-2 tiene los dos hijos: derrama a la hoja mas honda de su pierna izquierda");
            await tx.RollbackAsync();
        });
        t.Should().BeLessThan(Objetivo, "si no, la cache es obligatoria y 04 la reconstruye");
    }

    /// <summary>Riesgo P-R9: 10 colocaciones a la vez bajo 10 nodos distintos de la linea mas honda.</summary>
    [EscalaFact]
    public async Task Diez_colocaciones_simultaneas_en_la_misma_linea_profunda()
    {
        var p50 = new List<double>();
        var p95 = new List<double>();
        for (var rep = 0; rep < 3; rep++)
        {
            List<string> patrocinadores;
            await using (var db = EscalaDb.Db())
                patrocinadores = await db.DualTeamTree.AsNoTracking()
                    .Where(d => d.Depth > 8000 && db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId) < 2)
                    .OrderByDescending(d => d.Depth).ThenBy(d => d.Id).Select(d => d.MemberId).Take(10).ToListAsync();
            patrocinadores.Should().HaveCount(10);
            var x = BinarioDb.Prefijo("EZ10");
            var tiempos = new ConcurrentBag<double>();
            try
            {
                var resultados = await Task.WhenAll(patrocinadores.Select((sp, i) => Task.Run(async () =>
                {
                    await using var db = EscalaDb.Db(TimeoutProduccion);
                    var c = Stopwatch.StartNew();
                    var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}{i:00}", sp);
                    c.Stop();
                    tiempos.Add(c.Elapsed.TotalMilliseconds);
                    return r;
                })));
                var orden = tiempos.OrderBy(t => t).ToList();
                double P(double q) => orden[(int)Math.Ceiling(q * orden.Count) - 1];
                p50.Add(P(0.5)); p95.Add(P(0.95));
                _salida.WriteLine($"[ESCALA] 10 simultaneas, repeticion {rep + 1}: p50 {P(0.5):0} ms, p95 {P(0.95):0} ms, max {orden[^1]:0} ms");
                resultados.Should().OnlyContain(r => r.IsSuccess, string.Join("; ", resultados.Where(r => !r.IsSuccess).Select(r => r.Error)));
            }
            finally
            {
                await RetirarPrefijoAsync(x);
            }
        }
        var m50 = p50.OrderBy(v => v).ElementAt(1);
        var m95 = p95.OrderBy(v => v).ElementAt(1);
        _salida.WriteLine($"[ESCALA] 10 simultaneas en la linea profunda: mediana de p50 {m50:0} ms, mediana de p95 {m95:0} ms");
        TimeSpan.FromMilliseconds(m95).Should().BeLessThan(Objetivo, "objetivo de una colocacion; ver P-R9");
    }

    /// <summary>
    /// Riesgo P-R6: colocar la raiz provisional ESC-P (50.000 en su equipo) mueve su subarbol entero bajo la linea de
    /// ESC-M (derrame a unos 8.500 niveles), con el arbol en exclusivo. Mientras, 5 colocaciones normales esperan: se
    /// mide cuanto. Despues se devuelve ESC-P a raiz provisional (sin medir) para repetir. Se coloca bajo ESC-2 (cabeza de la
    /// columna, con los dos hijos), para que derrame a unos 8.500 niveles; bajo ESC-M iria a su derecha libre.
    /// </summary>
    [EscalaFact]
    public async Task Mover_un_subarbol_de_50000_nodos_y_cuanto_bloquea()
    {
        var movs = new List<double>();
        var esperas = new List<double>();
        var fallos = new List<string>();
        for (var rep = 0; rep < 3; rep++)
        {
            List<string> patrocinadores;
            await using (var db = EscalaDb.Db())
            {
                (await NodoAsync(db, Provisional)).ParentMemberId.Should().BeNull();
                patrocinadores = await db.DualTeamTree.AsNoTracking()
                    .Where(d => d.Depth > 100 && d.Depth < 8000 && db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId) == 0)
                    .OrderBy(d => d.Depth).ThenBy(d => d.Id).Select(d => d.MemberId).Take(5).ToListAsync();
            }
            var x = BinarioDb.Prefijo("EZMV");
            try
            {
                var mover = Task.Run(async () =>
                {
                    // Tiempo de espera largo para conocer la duracion real; la asercion la compara con los 30 s de produccion.
                    await using var db = EscalaDb.Db();
                    await db.Database.OpenConnectionAsync();
                    var antes = await EscalaDb.SesionAsync(db);
                    var c = Stopwatch.StartNew();
                    var r = await new PlacementService(db, new Reloj()).PlaceAsync(Provisional, Cabeza);
                    c.Stop();
                    var despues = await EscalaDb.SesionAsync(db);
                    return (r, c.Elapsed, despues.Lecturas - antes.Lecturas, despues.Cpu - antes.Cpu);
                });
                await Task.Delay(300);
                var colocar = patrocinadores.Select((sp, i) => Task.Run(async () =>
                {
                    await using var db = EscalaDb.Db(TimeoutProduccion);
                    var c = Stopwatch.StartNew();
                    var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}{i:00}", sp);
                    c.Stop();
                    return (r, c.Elapsed);
                })).ToList();
                var (rm, tm, lecturasMov, cpuMov) = await mover;
                var otras = await Task.WhenAll(colocar);
                movs.Add(tm.TotalMilliseconds);
                esperas.Add(otras.Max(o => o.Elapsed.TotalMilliseconds));
                _salida.WriteLine($"[ESCALA] Mover ESC-P (50.000), repeticion {rep + 1}: {tm.TotalMilliseconds:0} ms ({lecturasMov} lecturas, {cpuMov} ms de CPU), profundidad {rm.Value?.Depth}; " +
                                  $"5 colocaciones durante el movimiento: {string.Join(" / ", otras.Select(o => $"{o.Elapsed.TotalMilliseconds:0} ms{(o.r.IsSuccess ? "" : " " + o.r.Error)}"))}");
                // Las aserciones van al final, para tener las tres repeticiones aunque alguna falle.
                if (!rm.IsSuccess) fallos.Add($"repeticion {rep + 1}: el movimiento fallo ({rm.Error})");
                fallos.AddRange(otras.Where(o => !o.r.IsSuccess).Select(o => $"repeticion {rep + 1}: una colocacion fallo tras {o.Elapsed.TotalSeconds:0.0} s ({o.r.Error})"));
            }
            finally
            {
                await RetirarPrefijoAsync(x);
                await DevolverEscPAsync();
            }
        }
        var mm = movs.OrderBy(v => v).ElementAt(1);
        var me = esperas.OrderBy(v => v).ElementAt(1);
        _salida.WriteLine($"[ESCALA] Mover ESC-P: mediana {mm:0} ms; espera maxima de las colocaciones, mediana {me:0} ms");
        fallos.Should().BeEmpty("ninguna colocacion debe fallar por el candado mientras se mueve un subarbol");
        TimeSpan.FromMilliseconds(mm).Should().BeLessThan(TimeSpan.FromSeconds(TimeoutProduccion), "un comando de mas de 30 s fallaria en produccion");
    }

    /// <summary>Riesgo P-R3: PlaceBulkAsync (lo que hace AutoPlacementJob) con 1.000 pendientes de patrocinadores repartidos.</summary>
    [EscalaFact]
    public async Task PlaceBulk_con_1000_pendientes()
    {
        // Tiempo de espera largo: deshacer las 1.000 colocaciones de una vez tarda mas de 30 s (en produccion cada una confirma).
        await using var db = EscalaDb.Db();
        // Patrocinadores repartidos por el equipo de inscripcion de ESC-M (cada 397.o, determinista).
        var gen = await db.GenealogyTree.AsNoTracking().CurrentOfAsync(Medido);
        var patrocinadores = await db.GenealogyTree.AsNoTracking().CurrentUnder(gen!.Node)
            .OrderBy(g => g.MemberId).Select(g => g.MemberId).ToListAsync();
        // MLM_ESCALA_PENDIENTES solo para comprobar la prueba con pocos; la medicion es con 1.000.
        var cuantos = int.TryParse(Environment.GetEnvironmentVariable("MLM_ESCALA_PENDIENTES"), out var k) ? k : 1000;
        patrocinadores = patrocinadores.Where((_, i) => i % 397 == 0).Take(cuantos).ToList();
        while (patrocinadores.Count < cuantos) patrocinadores.Add(Medido);
        var porColocacion = new List<double>();

        var t = await Mediana("PlaceBulkAsync con 1.000 pendientes", async rep =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var x = BinarioDb.Prefijo($"EZB{rep}")[..10];
            var pares = patrocinadores.Select((sp, i) => ($"{x}{i:000}", sp)).ToList();
            foreach (var (id, sp) in pares)
            {
                db.MemberProfiles.Add(new Domain.Entities.Member.MemberProfile
                {
                    UserId = Guid.NewGuid(), MemberId = id, Email = $"{id.ToLowerInvariant()}@example.com", SponsorMemberId = sp,
                    FirstName = "Escala", LastName = "Pendiente", Country = "US", MemberType = MemberType.Ambassador,
                    Status = Domain.Entities.Member.MemberAccountStatus.Active, EnrollDate = DateTime.Now, CreationDate = DateTime.Now,
                    LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba"
                });
                db.MemberStatistics.Add(new Domain.Entities.Member.MemberStatisticEntity
                    { MemberId = id, PersonalPoints = 6, CreatedBy = "plan-p-prueba", CreationDate = DateTime.Now });
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var c = Stopwatch.StartNew();
            var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(pares);
            c.Stop();
            porColocacion.Add(c.Elapsed.TotalMilliseconds / pares.Count);
            _salida.WriteLine($"[ESCALA] PlaceBulkAsync, repeticion {rep + 1}: {c.Elapsed.TotalSeconds:0.0} s, {c.Elapsed.TotalMilliseconds / pares.Count:0} ms por colocacion, " +
                              $"colocados {r.Value?.Placed}, fallidos {r.Value?.Failed}");
            r.IsSuccess.Should().BeTrue();
            r.Value!.Failed.Should().Be(0);
            r.Value.Placed.Should().Be(cuantos);
            // Deshacer 1.000 colocaciones tarda mas de 30 s y SqlTransaction.Rollback no admite otro tiempo de espera: el
            // ROLLBACK va como sentencia, con el tiempo de espera largo del contexto (SqlClient da la transaccion por cerrada).
            await db.Database.ExecuteSqlRawAsync("IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION");
            db.ChangeTracker.Clear();
        });
        var mediaPorColocacion = porColocacion.OrderBy(v => v).ElementAt(1);
        _salida.WriteLine($"[ESCALA] PlaceBulkAsync: mediana {t.TotalSeconds:0.0} s, {mediaPorColocacion:0} ms por colocacion");
        TimeSpan.FromMilliseconds(mediaPorColocacion).Should().BeLessThan(Objetivo);
    }

    // ─────────────────────────────── Inscripcion y reportes ───────────────────────────────

    [EscalaFact]
    public async Task Mi_equipo_de_inscripcion_conteo_primera_pagina_y_pagina_1000()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var gen = await db.GenealogyTree.AsNoTracking().CurrentOfAsync(Medido);
        var servicio = ClientesDelEquipoTests.Servicio(db);
        var t0 = await Mediana("Mi equipo de inscripcion de ESC-M, conteo (CurrentUnder)", async _ =>
            (await db.GenealogyTree.AsNoTracking().CurrentUnder(gen!.Node).CountAsync()).Should().BeGreaterThan(400_000), db);
        var t1 = await Mediana("Mi equipo de inscripcion de ESC-M, primera pagina (servicio)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20 })).Items.Should().HaveCount(20), db);
        var t2 = await Mediana("Mi equipo de inscripcion de ESC-M, pagina 1.000 (servicio)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { Page = 1000, PageSize = 20 })).Items.Should().HaveCount(20), db);
        // Plan Q (revision del bloque 5, I1): el total de la rejilla son los nodos vivos del equipo, por IX_GenealogyTree_Node_Vivo.
        var total = 0;
        var t3 = await Mediana("Mi equipo de inscripcion de ESC-M, total de la rejilla (CountUnderAsync)", async _ =>
            total = await db.CountUnderAsync(gen!.Node), db);
        _salida.WriteLine($"[ESCALA] Mi equipo de inscripcion de ESC-M: total {total}");
        new[] { t0, t1, t2, t3 }.Should().OnlyContain(t => t < Objetivo);
    }

    /// <summary>
    /// Plan Q (Tarea 20 a profundidad real): 1.000 altas bajo el miembro mas profundo de la inscripcion (la rama larga, unos
    /// 440 niveles): una fila SponsorLine por alta, como deja la activacion, y ApplyMemberStatisticDeltasJob las expande por
    /// toda la linea. En una transaccion que se deshace. Objetivo: menos de 10 s, y ESC-M suma exactamente 1.000 a su equipo.
    /// </summary>
    [EscalaFact]
    public async Task Mil_altas_bajo_el_miembro_mas_profundo_de_la_inscripcion()
    {
        await using var db = EscalaDb.Db();
        (await db.MemberStatisticDeltas.CountAsync(d => !d.IsApplied)).Should().Be(0, "la base de escala sale con la cola aplicada");
        var hondo = await db.GenealogyTree.AsNoTracking().Where(g => g.Node != null && !g.IsDeleted)
            .OrderByDescending(g => g.Level).ThenBy(g => g.Id).FirstAsync();
        int Equipo(Domain.Entities.Member.MemberStatisticEntity s) => s.EnrollmentTeamSize;
        var tiempos = new List<TimeSpan>();
        await Mediana($"1.000 altas bajo {hondo.MemberId} (nivel {hondo.Level}), con la siembra y el deshacer", async i =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var antes = Equipo(await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == Medido).OrderBy(s => s.Id).FirstAsync());
            for (var k = 0; k < 1000; k++)
                db.MemberStatisticDeltas.Add(new Domain.Entities.Member.MemberStatisticDelta
                {
                    MemberId = hondo.MemberId, Scope = Domain.Entities.Member.MemberStatisticDeltaScope.SponsorLine,
                    EnrollmentPointsDelta = 0, EnrollmentTeamSizeDelta = 1, QualifiedSponsoredMembersDelta = 1,
                    SourceMemberId = $"EZA{i}{k:0000}", CreatedBy = "medicion", CreationDate = DateTime.Now
                });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var c = Stopwatch.StartNew();
            await new Repository.Jobs.ApplyMemberStatisticDeltasJob(db, new Reloj(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Repository.Jobs.ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();
            c.Stop();
            tiempos.Add(c.Elapsed);
            var despues = Equipo(await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == Medido).OrderBy(s => s.Id).FirstAsync());
            (despues - antes).Should().Be(1000);
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
        }, db);
        var t = tiempos.OrderBy(x => x).ElementAt(1);
        _salida.WriteLine($"[ESCALA] Aplicar 1.000 altas a {hondo.Level} niveles (solo el trabajo): {string.Join(" / ", tiempos.Select(x => $"{x.TotalMilliseconds:0} ms"))} -> mediana {t.TotalMilliseconds:0} ms");
        t.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [EscalaFact]
    public async Task Reporte_de_actividad_de_ESC_M_con_tres_meses()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var reloj = new RelojFijo(DateTime.Now); // extremo de un rango de medicion, no logica de negocio
        var servicio = new EnrollmentActivityService(db, reloj);
        var t = await Mediana("Actividad de ESC-M, 3 meses", async _ =>
            (await servicio.GetGridAsync(Medido, ReportDateRange.ActivityMinFrom(reloj.Now), reloj.Now.Date,
                new GridDataRequest { PageSize = 20 })).IsSuccess.Should().BeTrue(), db);
        t.Should().BeLessThan(Objetivo);
    }

    [EscalaFact]
    public async Task Reporte_de_clientes_de_todo_el_equipo_de_ESC_M()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var servicio = ClientesDelEquipoTests.Servicio(db);
        var t = await Mediana("Clientes de todo el equipo de ESC-M", async _ =>
            await servicio.GetCustomersGridAsync(Medido, new GridDataRequest { PageSize = 20 }, CustomerScope.Team), db);
        t.Should().BeLessThan(Objetivo);
    }

    [EscalaFact]
    public async Task Reporte_de_eventos_con_todo_el_equipo_de_ESC_M()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        await using var tx = await db.Database.BeginTransactionAsync();
        var gen = await db.GenealogyTree.AsNoTracking().CurrentOfAsync(Medido);
        var algunos = await db.GenealogyTree.AsNoTracking().CurrentUnder(gen!.Node).OrderBy(g => g.Id).Select(g => g.MemberId).Take(200).ToListAsync();
        var evento = "EZ-EV-" + Guid.NewGuid().ToString("N")[..12];
        db.CorporateEvents.Add(new CorporateEvent { Id = evento, Title = "Medicion escala", EventDate = DateTime.Now.AddMonths(2), IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        foreach (var (m, i) in algunos.Select((m, i) => (m, i)))
            db.CorporateEventRegistrations.Add(TeamEventRegistrationsServiceTests.Inscripcion(evento, m, 1, DateTime.Now.AddDays(-3), $"{evento}-{i}"));
        await db.SaveChangesAsync();
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);
        var t = await Mediana("Eventos de ESC-M (todo el equipo, 200 inscritos)", async _ =>
        {
            (await servicio.GetGridAsync(Medido, evento, registeredOnly: false, new GridDataRequest { PageSize = 20 })).IsSuccess.Should().BeTrue();
            (await servicio.GetTotalsAsync(Medido, evento)).IsSuccess.Should().BeTrue();
        }, db);
        await tx.RollbackAsync();
        t.Should().BeLessThan(Objetivo);
    }

    // ─────────────────────────────── Limpieza ───────────────────────────────

    /// <summary>Retira (restando sus piernas) los nodos del prefijo y borra sus perfiles, logs y colas. Solo en la base de escala.</summary>
    private static async Task RetirarPrefijoAsync(string prefijo)
    {
        await using var db = EscalaDb.Db();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await DualTeamGate.EnterTreeExclusiveAsync(db);
            var ids = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(prefijo))
                .OrderByDescending(d => d.Depth).Select(d => d.MemberId).ToListAsync();
            foreach (var id in ids)
                await DualTeamNodes.RemoveAsync(db, id, DualTeamChildrenOnRemoval.Reject, DateTime.Now, "medicion");
            await tx.CommitAsync();
        }
        var patron = prefijo + "%";
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamLegFrontiers WHERE SponsorMemberId LIKE {patron} OR DeepestMemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberProfiles WHERE MemberId LIKE {patron} AND CreatedBy = 'plan-p-prueba'");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberStatistics WHERE MemberId LIKE {patron} AND CreatedBy = 'plan-p-prueba'");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM RankEvaluationQueue WHERE TriggerMemberId LIKE {patron}");
        // Plan Q (Bloque 2): colocar y retirar encolan deltas de pierna; se aplican aqui para dejar la base como estaba (cola vacia).
        await LegPointMaintenance.DrainAsync(db);
    }

    /// <summary>Devuelve ESC-P a raiz provisional (resta su volumen de la linea de ESC-M) y borra lo que dejo su colocacion.</summary>
    private static async Task DevolverEscPAsync()
    {
        await using var db = EscalaDb.Db();
        var p = await NodoAsync(db, Provisional);
        if (p.ParentMemberId is null) return;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await DualTeamGate.EnterTreeExclusiveAsync(db);
            await DualTeamNodes.MoveSubtreeAsync(db, Provisional, new DualTeamDestination.NewRoot(), adjustLegs: true, DateTime.Now, "medicion");
            await tx.CommitAsync();
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId = {Provisional} AND CreatedBy = 'placement-service' AND Reason LIKE '%provisional root%'");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM RankEvaluationQueue WHERE TriggerMemberId = {Provisional} AND TriggerEvent = 2");
        // Plan Q (Bloque 2): el movimiento y la vuelta encolan el subarbol en las dos lineas; se aplican aqui (cola vacia).
        await LegPointMaintenance.DrainAsync(db);
    }
}
