using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Controllers;

/// <summary>
/// Plan P, Bloque D: el visualizador del admin con 6 niveles sobre ESC-M (400.000 o 1.000.000 en su equipo binario)
/// en la base de escala. Mediana de 3 ejecuciones; objetivo menos de 2 s. Solo con MLM_ESCALA_SQLSERVER.
/// </summary>
[Trait("Categoria", "Medicion")]
public class EscalaVisualizadorBinarioTests
{
    private readonly ITestOutputHelper _salida;
    public EscalaVisualizadorBinarioTests(ITestOutputHelper salida) => _salida = salida;

    [EscalaFact]
    public async Task El_visualizador_de_ESC_M_con_6_niveles_responde_en_tiempo()
    {
        await using var db = EscalaDb.Db(30);
        var controlador = new NetworkController(db);
        var t = await EscalaDb.MedianaAsync(_salida, "Visualizador del admin, ESC-M, 6 niveles", async _ =>
            (await controlador.GetBinaryTree("ESC-M", 6)).Should().BeOfType<OkObjectResult>(), db);
        t.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
}
