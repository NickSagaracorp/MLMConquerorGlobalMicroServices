using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Placement;

/// <summary>Plan P, Tarea 7: la retirada del admin reescribe rutas y espera a las colocaciones en curso.</summary>
public class SqlServerAdminRemovePlacementEsperaTests
{
    [SqlServerFact]
    public async Task La_retirada_del_admin_espera_a_una_colocacion_en_curso()
    {
        var x = ("PPY" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
        try
        {
            await using (var db = SqlServerTestDb.Db())
            {
                var claveP = ClaveDePrueba.Raiz(db);
                db.DualTeamTree.AddRange(
                    new DualTeamEntity { MemberId = $"{x}P", Side = TreeSide.Left, BinaryPath = claveP, Depth = 0,
                        CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba" },
                    new DualTeamEntity { MemberId = $"{x}M", ParentMemberId = $"{x}P", Side = TreeSide.Left,
                        BinaryPath = ClaveDePrueba.Hijo(claveP, TreeSide.Left), Depth = 1, CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba" });
                await db.SaveChangesAsync();
            }

            await using var colocacion = SqlServerTestDb.Db();
            await using var txColocacion = await colocacion.Database.BeginTransactionAsync();
            await DualTeamGate.EnterSlotAsync(colocacion, $"{x}M", TreeSide.Left);

            var retirada = Task.Run(async () =>
            {
                await using var db = SqlServerTestDb.Db();
                var usuario = new Mock<ICurrentUserService>();
                usuario.Setup(u => u.UserId).Returns("plan-p-prueba");
                var reloj = new Mock<IDateTimeProvider>();
                reloj.Setup(r => r.Now).Returns(DateTime.Now);
                return await new AdminRemovePlacementHandler(db, usuario.Object, reloj.Object)
                    .Handle(new AdminRemovePlacementCommand($"{x}M"), CancellationToken.None);
            });

            await Task.Delay(1500);
            retirada.IsCompleted.Should().BeFalse("la retirada espera al candado compartido de la colocacion");

            await txColocacion.RollbackAsync();
            var r = await retirada;
            r.IsSuccess.Should().BeTrue(r.Error);
        }
        finally
        {
            await using var db = SqlServerTestDb.Db();
            var patron = x + "%";
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId LIKE {patron}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualLegDeltas WHERE StartMemberId LIKE {patron}");   // Plan Q
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamTree WHERE MemberId LIKE {patron}");
        }
    }
}
