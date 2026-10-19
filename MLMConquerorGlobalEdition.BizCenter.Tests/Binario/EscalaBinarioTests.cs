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

    private Task<TimeSpan> Mediana(string que, Func<int, Task> paso) => EscalaDb.MedianaAsync(_salida, que, paso);

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
        var lob = await db.Database.SqlQuery<long>($"SELECT CAST(SUM(CAST(DATALENGTH(HierarchyPath) AS bigint)) / 1048576 AS bigint) AS [Value] FROM DualTeamTree").SingleAsync();
        var clave = await db.Database.SqlQuery<int>($"SELECT MAX(DATALENGTH(BinaryPath)) AS [Value] FROM DualTeamTree").SingleAsync();
        var prof = await db.DualTeamTree.MaxAsync(d => d.Depth);
        var gen = await db.GenealogyTree.AsNoTracking().CurrentOfAsync(Medido);
        var equipoGen = await db.GenealogyTree.AsNoTracking().CurrentUnder(gen!.Node).CountAsync() - 1;
        var nivelGen = await db.GenealogyTree.MaxAsync(g => g.Level);

        _salida.WriteLine($"[ESCALA] ESC-M: binario {m.LeftLegCount + m.RightLegCount} (izq {m.LeftLegCount}, der {m.RightLegCount}), " +
                          $"inscripcion {equipoGen}; ESC-P: {p.LeftLegCount + p.RightLegCount + 1} con el; profundidad maxima {prof}, " +
                          $"clave maxima {clave} B, nivel maximo de inscripcion {nivelGen}");
        foreach (var t in tam) _salida.WriteLine($"[ESCALA] DualTeamTree {t.Indice}: {t.Mb} MB, {t.Filas} filas");
        _salida.WriteLine($"[ESCALA] HierarchyPath del binario: {lob} MB");

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
            (await DualSubtree.Under(db, m).CountAsync()).Should().Be(esperado));
        t.Should().BeLessThan(Objetivo);
    }

    [EscalaFact]
    public async Task Mi_equipo_binario_primera_pagina_y_pagina_1000()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));
        var t1 = await Mediana("Mi equipo binario de ESC-M, primera pagina (servicio)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20 })).Items.Should().HaveCount(20));
        var t2 = await Mediana("Mi equipo binario de ESC-M, pagina 1.000 (servicio)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { Page = 1000, PageSize = 20 })).Items.Should().HaveCount(20));

        // La del plan: orden por la clave y paginacion por clave (BinaryPath > @ultima): la pagina 1.000 empieza
        // detras de la ultima clave de la 999.
        var m = await NodoAsync(db, Medido);
        var ultima = await DualSubtree.Under(db, m).AsNoTracking().OrderBy(d => d.BinaryPath).Skip(999 * 20 - 1)
            .Select(d => d.BinaryPath).FirstAsync();
        var (_, hi) = DualBinaryPath.SubtreeBounds(m.BinaryPath!);
        var t3 = await Mediana("Mi equipo binario de ESC-M, pagina 1.000 por clave", async _ =>
            (await db.DualTeamTree.FromSqlInterpolated($"SELECT TOP (20) * FROM DualTeamTree WHERE BinaryPath > {ultima} AND BinaryPath < {hi} AND IsDeleted = 0 ORDER BY BinaryPath")
                .AsNoTracking().ToListAsync()).Should().HaveCount(20));

        new[] { t1, t2, t3 }.Should().OnlyContain(t => t < Objetivo);
    }

    [EscalaFact]
    public async Task Mi_equipo_binario_con_busqueda_por_nombre()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));
        var t = await Mediana("Mi equipo binario de ESC-M, busqueda 'Lucia Diaz' (1 de cada 100)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20, Search = "Lucia Diaz" })).TotalCount.Should().BePositive());
        t.Should().BeLessThan(Objetivo, "si no se cumple, se propone un indice de texto (Plan P, Bloque D)");
    }

    [EscalaFact]
    public async Task Puntos_de_pierna_lectura()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        await NodoAsync(db, Medido);
        var t = await Mediana("Puntos y contadores de pierna de ESC-M (lectura)", async _ =>
            (await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId == Medido)
                .Select(d => new { d.LeftLegPoints, d.RightLegPoints, d.LeftLegCount, d.RightLegCount }).SingleAsync())
                .LeftLegCount.Should().BePositive());
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
        var t = await Mediana($"Colocar una hoja bajo {hondo.MemberId} (profundidad {hondo.Depth + 1}, sube por {hondo.Depth} ascendientes)", async i =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var id = $"EZH{i}{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            await BinarioDb.SembrarPerfilAsync(db, id, hondo.MemberId);
            await BinarioDb.PuntosAsync(db, id, 6);
            var r = await motor.PlaceAsync(id, hondo.MemberId);
            r.IsSuccess.Should().BeTrue(r.Error);
            await tx.RollbackAsync();
        });
        t.Should().BeLessThan(Objetivo);
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
                    var c = Stopwatch.StartNew();
                    var r = await new PlacementService(db, new Reloj()).PlaceAsync(Provisional, Cabeza);
                    c.Stop();
                    return (r, c.Elapsed);
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
                var (rm, tm) = await mover;
                var otras = await Task.WhenAll(colocar);
                movs.Add(tm.TotalMilliseconds);
                esperas.Add(otras.Max(o => o.Elapsed.TotalMilliseconds));
                _salida.WriteLine($"[ESCALA] Mover ESC-P (50.000), repeticion {rep + 1}: {tm.TotalMilliseconds:0} ms, profundidad {rm.Value?.Depth}; " +
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
            (await db.GenealogyTree.AsNoTracking().CurrentUnder(gen!.Node).CountAsync()).Should().BeGreaterThan(400_000));
        var t1 = await Mediana("Mi equipo de inscripcion de ESC-M, primera pagina (servicio)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { PageSize = 20 })).Items.Should().HaveCount(20));
        var t2 = await Mediana("Mi equipo de inscripcion de ESC-M, pagina 1.000 (servicio)", async _ =>
            (await servicio.GetMyTeamGridAsync(Medido, new GridDataRequest { Page = 1000, PageSize = 20 })).Items.Should().HaveCount(20));
        new[] { t0, t1, t2 }.Should().OnlyContain(t => t < Objetivo);
    }

    [EscalaFact]
    public async Task Reporte_de_actividad_de_ESC_M_con_tres_meses()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var reloj = new RelojFijo(DateTime.Now); // extremo de un rango de medicion, no logica de negocio
        var servicio = new EnrollmentActivityService(db, reloj);
        var t = await Mediana("Actividad de ESC-M, 3 meses", async _ =>
            (await servicio.GetGridAsync(Medido, ReportDateRange.ActivityMinFrom(reloj.Now), reloj.Now.Date,
                new GridDataRequest { PageSize = 20 })).IsSuccess.Should().BeTrue());
        t.Should().BeLessThan(Objetivo);
    }

    [EscalaFact]
    public async Task Reporte_de_clientes_de_todo_el_equipo_de_ESC_M()
    {
        await using var db = EscalaDb.Db(TimeoutProduccion);
        var servicio = ClientesDelEquipoTests.Servicio(db);
        var t = await Mediana("Clientes de todo el equipo de ESC-M", async _ =>
            await servicio.GetCustomersGridAsync(Medido, new GridDataRequest { PageSize = 20 }, CustomerScope.Team));
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
        });
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
    }
}
