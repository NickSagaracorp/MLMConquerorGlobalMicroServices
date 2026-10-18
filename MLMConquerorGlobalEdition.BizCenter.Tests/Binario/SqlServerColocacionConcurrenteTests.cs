using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Tarea 6: colocaciones simultaneas, cada una con su contexto y su conexion, contra SQL Server.
/// Sin transaccion envolvente (cada colocacion confirma la suya); las filas del prefijo se borran al final.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColocacionConcurrenteTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// Con RCSI las lecturas READ COMMITTED leen la ultima version confirmada y no esperan a una insercion
    /// sin confirmar. La exclusion tiene que salir del applock del hueco (y del indice unico), nunca de que una
    /// lectura espere. P ya tiene hijo izquierdo, asi que las dos van a (P, Right) por la regla b, que no
    /// escribe la fila de cola (su indice unico las serializaria por casualidad y esconderia el fallo). Se
    /// encuentran justo antes de guardar (Encuentro): sin candado insertarian las dos. Con candado, la segunda
    /// espera al applock, entra cuando la primera ya confirmo, vuelve a leer el hueco (lo ve ocupado sin que
    /// ninguna lectura haya esperado) y derrama a la cola de la pierna izquierda, L.
    /// </summary>
    [SqlServerFact]
    public async Task Con_RCSI_la_exclusion_sale_del_candado_y_no_de_una_lectura_que_espera()
    {
        await using (var meta = SqlServerReportes.Db())
        {
            var rcsi = await meta.Database.SqlQuery<bool>(
                $"SELECT is_read_committed_snapshot_on AS [Value] FROM sys.databases WHERE name = DB_NAME()").ToListAsync();
            rcsi.Should().Equal(new[] { true }, "esta prueba demuestra la colocacion con RCSI activado en la base de pruebas");
        }

        var x = BinarioDb.Prefijo("PPR");
        var encuentro = new Encuentro();
        try
        {
            await using (var db = SqlServerReportes.Db())
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));

            var resultados = await Task.WhenAll(new[] { $"{x}A", $"{x}B" }.Select(async m =>
            {
                await using var db = new Repository.Context.AppDbContext(
                    new DbContextOptionsBuilder<Repository.Context.AppDbContext>()
                        .UseSqlServer(SqlServerReportes.ConnectionString).AddInterceptors(encuentro).Options);
                return await new PlacementService(db, new Reloj()).PlaceAsync(m, $"{x}P");
            }));

            resultados.Should().OnlyContain(r => r.IsSuccess && !r.Value!.AlreadyPlaced);
            await using var lectura = SqlServerReportes.Db();
            (await BinarioDb.HijosAsync(lectura, $"{x}P", TreeSide.Right)).Should().Be(1);
            (await BinarioDb.HijosAsync(lectura, $"{x}L", TreeSide.Left)).Should().Be(1);
            (await BinarioDb.HuecosRepetidosAsync(lectura, x)).Should().Be(0);
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }

    [SqlServerFact]
    public async Task Colocaciones_concurrentes_no_repiten_hueco()
    {
        var x = BinarioDb.Prefijo("PPC");
        try
        {
            await using (var db = SqlServerReportes.Db())
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));

            // 20 patrocinados de P (raiz) y 20 de L, su hijo izquierdo: los derrames de P caen en la pierna de L,
            // asi que los dos grupos compiten por los mismos huecos.
            var pares = Enumerable.Range(1, 20).Select(i => ($"{x}A{i:00}", $"{x}P"))
                .Concat(Enumerable.Range(1, 20).Select(i => ($"{x}B{i:00}", $"{x}L")));
            var resultados = await Task.WhenAll(pares.Select(async par =>
            {
                await using var db = SqlServerReportes.Db();
                return await new PlacementService(db, new Reloj()).PlaceAsync(par.Item1, par.Item2);
            }));

            resultados.Should().OnlyContain(r => r.IsSuccess && !r.Value!.AlreadyPlaced,
                string.Join("; ", resultados.Where(r => !r.IsSuccess).Select(r => r.Error)));

            await using var lectura = SqlServerReportes.Db();
            (await BinarioDb.HuecosRepetidosAsync(lectura, x)).Should().Be(0);
            var nodos = await lectura.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).ToListAsync();
            nodos.Should().HaveCount(42, "la raiz, L y los 40 colocados");
            var rutas = nodos.ToDictionary(n => n.MemberId, n => n.HierarchyPath);
            nodos.Where(n => n.ParentMemberId != null).Should().OnlyContain(n =>
                rutas.ContainsKey(n.ParentMemberId!) && n.HierarchyPath == rutas[n.ParentMemberId!] + n.MemberId + "/");
            (await lectura.PlacementLogs.CountAsync(l => l.MemberId.StartsWith(x))).Should().Be(40);
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }
}
