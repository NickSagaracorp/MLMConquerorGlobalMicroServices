using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Placement;

/// <summary>
/// Plan P, Tarea 7: dos peticiones simultaneas con el mismo padre y lado pedidos, cada una con su contexto,
/// contra SQL Server (con RCSI). El manejador comprobaba y luego insertaba sin candado ni transaccion: las
/// dos elegian el mismo hueco. Ahora las dos quedan colocadas, cada una en un hueco distinto.
/// </summary>
public class SqlServerPlaceMemberConcurrenteTests
{
    private static PlaceMemberHandler Handler(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(DateTime.Now);
        var avisos = new Mock<IPushNotificationService>();
        avisos.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        return new PlaceMemberHandler(db, reloj.Object, avisos.Object);
    }

    [SqlServerFact]
    public async Task Dos_peticiones_al_mismo_padre_y_lado_quedan_en_huecos_distintos()
    {
        var x = ("PPS" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
        var encuentro = new Encuentro();
        try
        {
            await using (var db = SqlServerTestDb.Db())
            {
                db.DualTeamTree.Add(new DualTeamEntity
                {
                    MemberId = $"{x}P", Side = TreeSide.Left, HierarchyPath = $"/{x}P/", BinaryPath = ClaveDePrueba.Raiz(db), Depth = 0,
                    CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba"
                });
                foreach (var m in new[] { $"{x}P", $"{x}M1", $"{x}M2" })
                    db.MemberProfiles.Add(new MemberProfile
                    {
                        UserId = Guid.NewGuid(), MemberId = m, Email = $"{m.ToLowerInvariant()}@prueba.test",
                        FirstName = "Plan", LastName = "P", Country = "US", MemberType = MemberType.Ambassador,
                        Status = MemberAccountStatus.Active, EnrollDate = DateTime.Now, CreationDate = DateTime.Now,
                        SponsorMemberId = m == $"{x}P" ? null : $"{x}P",   // revision I8: se cuelgan bajo su patrocinador
                        LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba"
                    });
                await db.SaveChangesAsync();
            }

            var resultados = await Task.WhenAll(new[] { $"{x}M1", $"{x}M2" }.Select(async m =>
            {
                await using var db = SqlServerTestDb.Db(encuentro);
                return await Handler(db).Handle(new PlaceMemberCommand(m, $"{x}P", "Left"), CancellationToken.None);
            }));

            resultados.Should().OnlyContain(r => r.IsSuccess, string.Join("; ", resultados.Select(r => r.Error)));
            await using var lectura = SqlServerTestDb.Db();
            var colocados = await lectura.DualTeamTree.AsNoTracking()
                .Where(d => d.MemberId.StartsWith(x) && d.ParentMemberId != null).ToListAsync();
            colocados.Should().HaveCount(2);
            colocados.GroupBy(d => (d.ParentMemberId, d.Side)).Should().OnlyContain(g => g.Count() == 1);
        }
        finally
        {
            await using var db = SqlServerTestDb.Db();
            var patron = x + "%";
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM RankEvaluationQueue WHERE TriggerMemberId LIKE {patron}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId LIKE {patron}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualLegDeltas WHERE StartMemberId LIKE {patron}");   // Plan Q
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamTree WHERE MemberId LIKE {patron}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberProfiles WHERE MemberId LIKE {patron} AND CreatedBy = 'plan-p-prueba'");
        }
    }
}
