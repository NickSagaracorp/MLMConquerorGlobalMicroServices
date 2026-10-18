using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.UnplaceMember;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Placement;

/// <summary>
/// Plan P, Tarea 7: Unplace borra el nodo, asi que va en una transaccion con el arbol en exclusivo y espera
/// a las colocaciones en curso (una colocacion bajo el nodo dejaria un hijo colgando de un nodo borrado).
/// </summary>
public class SqlServerUnplaceMemberEsperaTests
{
    [SqlServerFact]
    public async Task Unplace_espera_a_una_colocacion_en_curso()
    {
        var x = ("PPU" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
        try
        {
            await using (var db = SqlServerTestDb.Db())
            {
                db.DualTeamTree.AddRange(
                    new DualTeamEntity { MemberId = $"{x}P", Side = TreeSide.Left, HierarchyPath = $"/{x}P/",
                        CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba" },
                    new DualTeamEntity { MemberId = $"{x}M", ParentMemberId = $"{x}P", Side = TreeSide.Left, HierarchyPath = $"/{x}P/{x}M/",
                        CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba" });
                await db.SaveChangesAsync();
            }

            await using var colocacion = SqlServerTestDb.Db();
            await using var txColocacion = await colocacion.Database.BeginTransactionAsync();
            await DualTeamGate.EnterSlotAsync(colocacion, $"{x}M", TreeSide.Left);

            var retirada = Task.Run(async () =>
            {
                await using var db = SqlServerTestDb.Db();
                var reloj = new Mock<IDateTimeProvider>();
                reloj.Setup(r => r.Now).Returns(DateTime.Now);
                return await new UnplaceMemberHandler(db, reloj.Object)
                    .Handle(new UnplaceMemberCommand($"{x}M", $"{x}M"), CancellationToken.None);
            });

            await Task.Delay(1500);
            retirada.IsCompleted.Should().BeFalse("Unplace espera al candado compartido de la colocacion");

            await txColocacion.RollbackAsync();
            var r = await retirada;
            r.IsSuccess.Should().BeTrue(r.Error);
        }
        finally
        {
            await using var db = SqlServerTestDb.Db();
            var patron = x + "%";
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId LIKE {patron}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamTree WHERE MemberId LIKE {patron}");
        }
    }
}
