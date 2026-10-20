using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Controllers;

/// <summary>
/// Plan Q (revision 2 del bloque 2, I1): la salud del sistema avisa si DualLegDeltaDeadLetters tiene filas (deltas de
/// pierna apartados de la cola: piernas sin ellos hasta reponerlos, Q-R30). Sin filas, la comprobacion esta sana.
/// </summary>
public class SaludConDeltasApartadosTests
{
    private static SystemHealthController Controlador(MLMConquerorGlobalEdition.Repository.Context.AppDbContext db) =>
        new(new Mock<IHttpClientFactory>().Object, new ConfigurationBuilder().Build(), db);

    private static ServiceHealthResult Admin(IActionResult r) =>
        ((ApiResponse<SystemHealthResponse>)((OkObjectResult)r).Value!).Data!.Services["MLMConquerorGlobalEdition.AdminAPI"];

    [Fact]
    public async Task Sin_deltas_apartados_la_comprobacion_esta_sana()
    {
        await using var db = InMemoryDbHelper.Create();

        var admin = Admin(await Controlador(db).GetSystemHealth());

        admin.Checks!["dualLegDeadLetters"].Should().Be("Healthy");
        admin.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task Con_deltas_apartados_el_servicio_queda_degradado_con_la_cuenta()
    {
        await using var db = InMemoryDbHelper.Create();
        db.DualLegDeltaDeadLetters.AddRange(
            new DualLegDeltaDeadLetter { DeltaId = 1, StartMemberId = "A", Points = 5, Reason = "fallo: 8115", CreatedBy = "prueba" },
            new DualLegDeltaDeadLetter { DeltaId = 2, StartMemberId = "A", Points = 5, Reason = "fallo: 8115", CreatedBy = "prueba" },
            new DualLegDeltaDeadLetter { DeltaId = 3, StartMemberId = "B", Points = 1, Reason = "sin-nodo", CreatedBy = "prueba" });
        await db.SaveChangesAsync();

        var admin = Admin(await Controlador(db).GetSystemHealth());

        admin.Checks!["dualLegDeadLetters"].Should().Be("Degraded: 3");
        admin.Status.Should().Be("Degraded");
    }
}
