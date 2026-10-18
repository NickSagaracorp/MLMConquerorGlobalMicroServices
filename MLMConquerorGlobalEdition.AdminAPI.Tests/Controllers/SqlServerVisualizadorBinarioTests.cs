using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Controllers;

/// <summary>
/// Plan P (revision I5): el visualizador del admin con el miembro de mayor equipo y la profundidad maxima (6) solo
/// lee esos niveles (como mucho 127 nodos): responde en menos de 2 s aunque el equipo tenga 119.000 personas.
/// </summary>
public class SqlServerVisualizadorBinarioTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerVisualizadorBinarioTests(ITestOutputHelper salida) => _salida = salida;

    [SqlServerFact]
    public async Task El_visualizador_del_mayor_equipo_responde_en_tiempo()
    {
        await using var db = SqlServerTestDb.Db();
        var raiz = await db.DualTeamTree.AsNoTracking().Where(d => d.ParentMemberId == null)
            .OrderByDescending(d => d.LeftLegCount + d.RightLegCount).ThenBy(d => d.MemberId).FirstAsync();
        var controlador = new NetworkController(db);
        await controlador.GetBinaryTree(raiz.MemberId, 6);   // calienta

        var c = Stopwatch.StartNew();
        var r = await controlador.GetBinaryTree(raiz.MemberId, 6);
        c.Stop();

        _salida.WriteLine($"Visualizador de {raiz.MemberId} ({raiz.LeftLegCount + raiz.RightLegCount} en su equipo), 6 niveles: {c.ElapsedMilliseconds} ms");
        r.Should().BeOfType<OkObjectResult>();
        c.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
}
