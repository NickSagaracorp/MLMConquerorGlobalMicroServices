using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Placement;

/// <summary>
/// Plan P, Tarea 7: dos colocaciones forzadas del admin al mismo hueco a la vez, cada una con su contexto,
/// contra SQL Server (con RCSI). La comprobacion del hueco estaba fuera de la transaccion: las dos pasaban.
/// </summary>
public class SqlServerAdminPlaceMemberConcurrenteTests
{
    private static AdminPlaceMemberHandler Handler(AppDbContext db)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("plan-p-prueba");
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(DateTime.Now);
        return new AdminPlaceMemberHandler(db, usuario.Object, reloj.Object);
    }

    [SqlServerFact]
    public async Task Dos_colocaciones_forzadas_al_mismo_hueco_dejan_un_hijo()
    {
        var x = ("PPA" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
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
                foreach (var m in new[] { $"{x}M1", $"{x}M2" })
                    db.MemberProfiles.Add(new MemberProfile
                    {
                        UserId = Guid.NewGuid(), MemberId = m, Email = $"{m.ToLowerInvariant()}@prueba.test",
                        FirstName = "Plan", LastName = "P", Country = "US", MemberType = MemberType.Ambassador,
                        Status = MemberAccountStatus.Active, EnrollDate = DateTime.Now, CreationDate = DateTime.Now,
                        LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba"
                    });
                await db.SaveChangesAsync();
            }

            var resultados = await Task.WhenAll(new[] { $"{x}M1", $"{x}M2" }.Select(async m =>
            {
                await using var db = SqlServerTestDb.Db(encuentro);
                return await Handler(db).Handle(new AdminPlaceMemberCommand(m, $"{x}P", "Left"), CancellationToken.None);
            }));

            resultados.Count(r => r.IsSuccess).Should().Be(1);
            resultados.Single(r => !r.IsSuccess).ErrorCode.Should().Be("SLOT_OCCUPIED");
            await using var lectura = SqlServerTestDb.Db();
            (await lectura.DualTeamTree.CountAsync(d => d.ParentMemberId == $"{x}P" && d.Side == TreeSide.Left)).Should().Be(1);
        }
        finally
        {
            await using var db = SqlServerTestDb.Db();
            var patron = x + "%";
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId LIKE {patron}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamTree WHERE MemberId LIKE {patron}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberProfiles WHERE MemberId LIKE {patron} AND CreatedBy = 'plan-p-prueba'");
        }
    }
}
