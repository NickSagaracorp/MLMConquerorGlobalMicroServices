using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Revision del bloque 5 (C1): la busqueda por prefijo no paga por todos los perfiles que casan en la base. Antes, «a»,
/// «amb» o «amb-» casaban con los 119.000 ids locales y la union buscaba el nodo de cada uno (973 ms y 3,9 s de CPU), tambien
/// para quien tiene un equipo de tres. Se mide en lecturas logicas de la sesion (sys.dm_exec_sessions), que no dependen de
/// la carga de la maquina.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerBusquedaAcotadaTests
{
    private const string RaizMayor = "AMB-700829";
    private readonly ITestOutputHelper _salida;

    public SqlServerBusquedaAcotadaTests(ITestOutputHelper salida) => _salida = salida;

    /// <summary>Lecturas logicas de la sesion durante <paramref name="paso"/> (la conexion queda abierta entre medias).</summary>
    internal static async Task<long> LecturasAsync(AppDbContext db, Func<Task> paso) => (await CosteAsync(db, paso)).Lecturas;

    /// <summary>Lecturas logicas y CPU (ms) de la sesion durante <paramref name="paso"/>.</summary>
    internal static async Task<(long Lecturas, long Cpu)> CosteAsync(AppDbContext db, Func<Task> paso)
    {
        // Dentro de una transaccion la conexion ya esta abierta: no se toca (cerrarla la devolveria al pool con la
        // transaccion pendiente).
        var abierta = db.Database.GetDbConnection().State == System.Data.ConnectionState.Open;
        if (!abierta) await db.Database.OpenConnectionAsync();
        try
        {
            async Task<Contador> Leer() => (await db.Database
                .SqlQueryRaw<Contador>("SELECT CAST(logical_reads AS bigint) AS Lecturas, CAST(cpu_time AS bigint) AS Cpu " +
                                       "FROM sys.dm_exec_sessions WHERE session_id = @@SPID")
                .ToListAsync())[0];
            var antes = await Leer();
            await paso();
            var despues = await Leer();
            return (despues.Lecturas - antes.Lecturas, despues.Cpu - antes.Cpu);
        }
        finally
        {
            if (!abierta) await db.Database.CloseConnectionAsync();
        }
    }

    private sealed record Contador(long Lecturas, long Cpu);

    private static DualTeamService Dual(AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

    [SqlServerFact]
    public async Task El_visualizador_de_un_equipo_de_tres_busca_desde_su_rango()
    {
        var x = BinarioDb.Prefijo("PQV");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}1", $"{x}P", TreeSide.Left),
            ($"{x}2", $"{x}P", TreeSide.Right), ($"{x}3", $"{x}1", TreeSide.Left));
        ReportesDb.Miembro(db, $"{x}P");
        foreach (var (id, nombre, apellido) in new[] { ("1", "Ana", "Diaz"), ("2", "Alba", "Ruiz"), ("3", "Bruno", "Sanz") })
        {
            var perfil = ReportesDb.Miembro(db, $"{x}{id}", $"{x}P");
            perfil.FirstName = nombre;
            perfil.LastName = apellido;
        }
        await db.SaveChangesAsync();

        List<string>? hallados = null;
        var (lecturas, cpu) = await CosteAsync(db, async () =>
            hallados = (await Dual(db).SearchBinarySubtreeAsync($"{x}P", "a", 25)).Select(h => h.MemberId).Order().ToList());
        _salida.WriteLine($"«a» en un equipo de 3: {lecturas} lecturas, {cpu} ms de CPU");

        hallados.Should().Equal($"{x}1", $"{x}2");
        (await Dual(db).SearchBinarySubtreeAsync($"{x}P", "ruiz", 25)).Select(h => h.MemberId).Should().Equal($"{x}2");
        lecturas.Should().BeLessThan(5_000, "un equipo de tres no recorre los perfiles de la base que empiezan por «a»");
        await tx.RollbackAsync();
    }

    [SqlServerTheory]
    [InlineData("a")]
    [InlineData("am")]
    [InlineData("amb")]
    [InlineData("amb-")]
    [InlineData("amb-1")]
    public async Task Mi_equipo_binario_limitado_lee_como_mucho_el_tope_de_candidatos(string termino)
    {
        await using var db = SqlServerReportes.Db();
        var servicio = Dual(db);
        var (lecturas, cpu) = await CosteAsync(db, () =>
            servicio.GetMyTeamGridAsync(RaizMayor, new GridDataRequest { PageSize = 20, Search = termino }));
        _salida.WriteLine($"«{termino}» en Mi equipo binario de {RaizMayor}: {lecturas} lecturas, {cpu} ms de CPU");

        // Antes: una busqueda por cada perfil que casaba en la base (119.000 con «a» o «amb-»), para contar y para paginar
        // (228.000 lecturas con «amb-1»). Ahora lo acota el tope por rama, con cualquier numero de miembros.
        lecturas.Should().BeLessThan(100_000);
    }

    /// <summary>El visualizador del mayor equipo: el tope por rama es el suyo (devuelve 25).</summary>
    [SqlServerTheory]
    [InlineData("a")]
    [InlineData("amb-")]
    [InlineData("amb-1")]
    public async Task El_visualizador_del_mayor_equipo_lee_como_mucho_su_tope(string termino)
    {
        await using var db = SqlServerReportes.Db();
        var servicio = Dual(db);
        List<string>? hallados = null;
        var (lecturas, cpu) = await CosteAsync(db, async () =>
            hallados = (await servicio.SearchBinarySubtreeAsync(RaizMayor, termino, 25)).Select(h => h.MemberId).ToList());
        _salida.WriteLine($"«{termino}» en el visualizador de {RaizMayor}: {hallados!.Count} hallados, {lecturas} lecturas, {cpu} ms de CPU");
        // Los caminos de 25 hallados a miles de niveles (DualAncestors) son parte de la cuenta.
        lecturas.Should().BeLessThan(100_000);
    }

    [SqlServerTheory]
    [InlineData("a")]
    [InlineData("amb-")]
    [InlineData("amb-1")]
    public async Task Mi_equipo_de_inscripcion_limitado_lee_como_mucho_el_tope_de_candidatos(string termino)
    {
        await using var db = SqlServerReportes.Db();
        var servicio = new EnrollmentTeamService(db, CalificadoEnLosEquiposTests.Rangos(db),
            new Repository.Services.Ranks.EnrollmentTeamPointsService(db), CalificadoEnLosEquiposTests.Calificacion(db));
        var (lecturas, cpu) = await CosteAsync(db, () =>
            servicio.GetMyTeamGridAsync(RaizMayor, new GridDataRequest { PageSize = 20, Search = termino }));
        _salida.WriteLine($"«{termino}» en Mi equipo de inscripcion de {RaizMayor}: {lecturas} lecturas, {cpu} ms de CPU");
        lecturas.Should().BeLessThan(100_000);
    }
}
