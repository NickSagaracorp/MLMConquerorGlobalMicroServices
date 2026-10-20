using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan Q, Bloque 3: el camino bajo la raiz (buscador y mas profundo del visualizador) sale de la clave.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerCaminoPorClaveTests
{
    [SqlServerFact]
    public async Task El_camino_bajo_la_raiz_no_depende_de_la_ruta_de_texto()
    {
        var x = BinarioDb.Prefijo("PQK");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
            ($"{x}LR", $"{x}L", TreeSide.Right), ($"{x}LRL", $"{x}LR", TreeSide.Left));
        await BinarioDb.SembrarPerfilesAsync(db, $"{x}P", $"{x}L", $"{x}LR", $"{x}LRL");
        var svc = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var hondo = await svc.GetDeepestNodeAsync($"{x}P", TreeSide.Left);
        hondo!.MemberId.Should().Be($"{x}LRL");
        hondo.Depth.Should().Be(3);
        hondo.Path.Select(p => p.MemberId).Should().Equal($"{x}L", $"{x}LR", $"{x}LRL");

        var hallado = (await svc.SearchBinarySubtreeAsync($"{x}P", $"{x}LRL")).Single();
        hallado.Leg.Should().Be("Left");
        hallado.Depth.Should().Be(3);
        hallado.Path.Select(p => p.MemberId).Should().Equal($"{x}L", $"{x}LR", $"{x}LRL");
        await tx.RollbackAsync();
    }
}
