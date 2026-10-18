using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;


namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Placement;

/// <summary>
/// Revisiones C1 e I1 del Plan P, camino admin: un miembro retirado por el admin, con su equipo esperando, vuelve a
/// colocarse y su equipo vuelve con el (clave, contadores y puntos coherentes; puede colocar a otro debajo).
/// </summary>
public class SqlServerFlotantesTests
{
    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [SqlServerFact]
    public async Task El_admin_recoloca_al_retirado_con_su_equipo()
    {
        var x = ("PFA" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
        try
        {
            await FlotantesComun.PrepararAsync(x);
            await using (var db = SqlServerTestDb.Db())
            {
                var usuario = new Mock<ICurrentUserService>();
                usuario.Setup(u => u.UserId).Returns("plan-p-prueba");
                var r = await new AdminPlaceMemberHandler(db, usuario.Object, new Reloj())
                    .Handle(new AdminPlaceMemberCommand($"{x}X", $"{x}P", "Left"), CancellationToken.None);
                r.IsSuccess.Should().BeTrue(r.Error);
            }
            await FlotantesComun.ComprobarAsync(x);
        }
        finally
        {
            await FlotantesComun.LimpiarAsync(x);
        }
    }
}

/// <summary>El escenario comun: P → X → C → D con puntos; el admin retira a X y C y D quedan flotando.</summary>
internal static class FlotantesComun
{
    public static async Task PrepararAsync(string x)
    {
        await LimpiarAsync(x);
        await using var db = SqlServerTestDb.Db();
        foreach (var (id, sponsor) in new[] { ("P", (string?)null), ("X", "P"), ("C", "X"), ("D", "C"), ("Y", "X") })
            db.MemberProfiles.Add(new MemberProfile
            {
                UserId = Guid.NewGuid(), MemberId = x + id, Email = $"{(x + id).ToLowerInvariant()}@prueba.test",
                SponsorMemberId = sponsor is null ? null : x + sponsor, FirstName = "Plan", LastName = "P", Country = "US",
                MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = DateTime.Now,
                CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba"
            });
        foreach (var (id, pts) in new[] { ("X", 1), ("C", 10), ("D", 5) })
            db.MemberStatistics.Add(new MemberStatisticEntity { MemberId = x + id, PersonalPoints = pts, CreatedBy = "plan-p-prueba", CreationDate = DateTime.Now });
        var p = await DualTeamNodes.RootAsync(db, $"{x}P", DateTime.Now, "plan-p-prueba");
        var nx = await DualTeamNodes.ChildAsync(db, p, $"{x}X", TreeSide.Left, DateTime.Now, "plan-p-prueba");
        var nc = await DualTeamNodes.ChildAsync(db, nx, $"{x}C", TreeSide.Left, DateTime.Now, "plan-p-prueba");
        var nd = await DualTeamNodes.ChildAsync(db, nc, $"{x}D", TreeSide.Left, DateTime.Now, "plan-p-prueba");
        db.DualTeamTree.AddRange(p, nx, nc, nd);
        await db.SaveChangesAsync();
        await LegPointMaintenance.ApplyIncrementalAsync(db, new[] { $"{x}X", $"{x}C", $"{x}D" });

        await using var tx = await db.Database.BeginTransactionAsync();
        await DualTeamGate.EnterTreeExclusiveAsync(db);
        (await DualTeamNodes.RemoveAsync(db, $"{x}X", DualTeamChildrenOnRemoval.Floating, DateTime.Now, "plan-p-prueba")).Should().BeTrue();
        await tx.CommitAsync();
    }

    public static async Task ComprobarAsync(string x)
    {
        await using var db = SqlServerTestDb.Db();
        var nodos = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).ToDictionaryAsync(d => d.MemberId[x.Length..]);
        nodos["X"].ParentMemberId.Should().Be($"{x}P");
        (await DualSubtree.Under(db, nodos["X"]).Select(d => d.MemberId).ToListAsync())
            .Should().BeEquivalentTo(new[] { $"{x}C", $"{x}D" });
        (nodos["X"].LeftLegPoints, nodos["X"].LeftLegCount).Should().Be((15m, 2));
        (nodos["P"].LeftLegPoints, nodos["P"].LeftLegCount).Should().Be((16m, 3));
        nodos.Values.Should().OnlyContain(n => n.LeftLegPoints >= 0 && n.RightLegPoints >= 0 && n.LeftLegCount >= 0 && n.RightLegCount >= 0);
        var y = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}Y", $"{x}X");
        y.IsSuccess.Should().BeTrue(y.Error);
        (y.Value!.ParentMemberId, y.Value.Side).Should().Be(($"{x}X", TreeSide.Right));
    }

    public static async Task LimpiarAsync(string x)
    {
        await using var db = SqlServerTestDb.Db();
        var patron = x + "%";
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamLegFrontiers WHERE SponsorMemberId LIKE {patron} OR DeepestMemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM RankEvaluationQueue WHERE TriggerMemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamTree WHERE MemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberStatistics WHERE MemberId LIKE {patron} AND CreatedBy = 'plan-p-prueba'");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberProfiles WHERE MemberId LIKE {patron} AND CreatedBy = 'plan-p-prueba'");
    }

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
