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

    /// <summary>
    /// Revision del bloque 3: con la raiz a media altura (@desde cuenta), varios hallazgos en una busqueda y alguno en la
    /// pierna derecha, cada camino empieza debajo de la raiz y la pierna y la profundidad son relativas a ella.
    /// </summary>
    [SqlServerFact]
    public async Task Con_la_raiz_a_media_altura_cada_hallazgo_trae_su_camino_su_pierna_y_su_profundidad()
    {
        var x = BinarioDb.Prefijo("PQM");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
            ($"{x}LL", $"{x}L", TreeSide.Left), ($"{x}LR", $"{x}L", TreeSide.Right),
            ($"{x}LLR", $"{x}LL", TreeSide.Right), ($"{x}LRL", $"{x}LR", TreeSide.Left));
        await BinarioDb.SembrarPerfilesAsync(db, $"{x}P", $"{x}L", $"{x}LL", $"{x}LR", $"{x}LLR", $"{x}LRL");
        var svc = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var hallados = (await svc.SearchBinarySubtreeAsync($"{x}L", x)).ToDictionary(h => h.MemberId);

        hallados.Keys.Should().BeEquivalentTo(new[] { $"{x}LL", $"{x}LR", $"{x}LLR", $"{x}LRL" });
        void Es(string id, string pierna, int profundidad, params string[] camino)
        {
            hallados[id].Leg.Should().Be(pierna, id);
            hallados[id].Depth.Should().Be(profundidad, id);
            hallados[id].Path.Select(p => p.MemberId).Should().Equal(camino.Select(c => x + c), id);
        }
        Es($"{x}LL", "Left", 1, "LL");
        Es($"{x}LR", "Right", 1, "LR");
        Es($"{x}LLR", "Left", 2, "LL", "LLR");
        Es($"{x}LRL", "Right", 2, "LR", "LRL");

        var hondo = await svc.GetDeepestNodeAsync($"{x}L", TreeSide.Right);
        hondo!.MemberId.Should().Be($"{x}LRL");
        hondo.Depth.Should().Be(2);
        hondo.Path.Select(p => p.MemberId).Should().Equal($"{x}LR", $"{x}LRL");
        await tx.RollbackAsync();
    }
}
