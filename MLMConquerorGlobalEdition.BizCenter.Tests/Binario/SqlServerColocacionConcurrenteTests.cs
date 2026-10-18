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
