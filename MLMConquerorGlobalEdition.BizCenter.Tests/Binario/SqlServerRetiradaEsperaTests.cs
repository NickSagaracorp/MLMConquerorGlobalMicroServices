using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Tarea 7: una retirada reescribe el arbol (quita el nodo y suelta a sus hijos), asi que toma el
/// arbol en exclusivo y espera a que terminen las colocaciones en curso; si no, una colocacion bajo el nodo
/// que se retira dejaria un hijo colgando de un nodo que ya no existe.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerRetiradaEsperaTests
{
    [SqlServerFact]
    public async Task La_retirada_del_BizCenter_espera_a_una_colocacion_en_curso()
    {
        var x = BinarioDb.Prefijo("PPX");
        await BinarioDb.LimpiarAsync(x);   // restos de una ejecucion que no llego a su finally
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}M", $"{x}P", TreeSide.Left));
                await BinarioDb.SembrarPerfilesAsync(db, $"{x}M");
            }

            await using var colocacion = SqlServerReportes.Db();
            await using var txColocacion = await colocacion.Database.BeginTransactionAsync();
            await DualTeamGate.EnterSlotAsync(colocacion, $"{x}M", TreeSide.Left);   // una colocacion bajo M, sin confirmar

            var retirada = Task.Run(async () =>
            {
                await using var db = SqlServerReportes.Db();
                var usuario = new Mock<BizCenter.Services.ICurrentUserService>();
                usuario.Setup(u => u.IsAdmin).Returns(true);
                usuario.Setup(u => u.UserId).Returns("plan-p-prueba");
                var reloj = new Mock<BizCenter.Services.IDateTimeProvider>();
                reloj.Setup(r => r.Now).Returns(DateTime.Now);
                reloj.Setup(r => r.UtcNow).Returns(DateTime.UtcNow);
                return await new RemovePlacementHandler(db, usuario.Object, reloj.Object)
                    .Handle(new RemovePlacementCommand($"{x}M"), CancellationToken.None);
            });

            await Task.Delay(1500);
            retirada.IsCompleted.Should().BeFalse("la retirada espera al candado compartido de la colocacion");

            await txColocacion.RollbackAsync();
            var r = await retirada;
            r.IsSuccess.Should().BeTrue(r.Error);
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }
}
