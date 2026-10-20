using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class EnrollmentTeamPointsServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    // Plan M: el equipo se suma por Node; el de cada nodo sigue su ruta (M = /1/, A = /1/1/, B = /1/2/).
    private static readonly Dictionary<string, string> Nodos = new() { ["M"] = "/1/", ["A"] = "/1/1/", ["B"] = "/1/2/" };

    private static GenealogyEntity Geno(string memberId, string? parentId, string path) => new()
    {
        MemberId = memberId, ParentMemberId = parentId, HierarchyPath = path,
        Level = path.Trim('/').Split('/').Length,
        Node = HierarchyId.Parse(Nodos[memberId]),
        CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static MemberStatisticEntity Stat(string memberId, int enrollmentPoints) => new()
    {
        MemberId = memberId, EnrollmentPoints = enrollmentPoints, CreatedBy = "seed", CreationDate = Now
    };

    [Fact]
    public async Task GetRawEnrollmentTeamPoints_SumsAllDirectBranches()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(
            Geno("M", null, "/M/"),
            Geno("A", "M", "/M/A/"),
            Geno("B", "M", "/M/B/"));
        await db.MemberStatistics.AddRangeAsync(Stat("A", 400), Stat("B", 600));
        await db.SaveChangesAsync();

        var svc = new EnrollmentTeamPointsService(db);
        var raw = await svc.GetRawEnrollmentTeamPointsAsync("M");

        raw.Should().Be(1000);
    }

    [Fact]
    public async Task GetEligibleEnrollmentTeamPoints_AppliesPerBranchCapThenThresholdCap()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(
            Geno("M", null, "/M/"),
            Geno("A", "M", "/M/A/"),
            Geno("B", "M", "/M/B/"));
        // One dominant branch: A=10000, B=10. Threshold 1000, per-branch cap 0.5 => 500/branch.
        await db.MemberStatistics.AddRangeAsync(Stat("A", 10000), Stat("B", 10));
        await db.SaveChangesAsync();

        var req = new RankRequirement { EnrollmentTeam = 1000, MaxEnrollmentTeamPointsPerBranch = 0.5 };
        var svc = new EnrollmentTeamPointsService(db);

        var eligible = await svc.GetEligibleEnrollmentTeamPointsAsync("M", req);

        // A capped at 500, B = 10 => 510 (below threshold 1000).
        eligible.Should().Be(510);
    }

    [Fact]
    public async Task GetEligibleEnrollmentTeamPoints_WhenNoEtDimension_ReturnsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        var svc = new EnrollmentTeamPointsService(db);
        var eligible = await svc.GetEligibleEnrollmentTeamPointsAsync(
            "M", new RankRequirement { EnrollmentTeam = 0 });
        eligible.Should().Be(0);
    }

    // ── GetEnrollmentBranchPointsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetEnrollmentBranchPoints_ReturnsOneEntryPerDirectChild_ZeroWhenNoStat()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(
            Geno("M", null, "/M/"),
            Geno("A", "M", "/M/A/"),
            Geno("B", "M", "/M/B/"));
        // Only child A has a stat row; child B has none.
        await db.MemberStatistics.AddAsync(Stat("A", 250));
        await db.SaveChangesAsync();

        var svc = new EnrollmentTeamPointsService(db);
        var branches = await svc.GetEnrollmentBranchPointsAsync("M");

        branches.Should().HaveCount(2);
        branches.Should().ContainSingle(b => b.ChildMemberId == "A" && b.BranchPoints == 250);
        branches.Should().ContainSingle(b => b.ChildMemberId == "B" && b.BranchPoints == 0);
    }

    // ── RecomputeEnrollmentPointsAsync ─────────────────────────────────────
    // Plan Q (revision 2 del bloque 2, C2): EnrollmentPoints = la suma, sobre el subarbol vigente (el propio incluido), de
    // lo que aportan las suscripciones ACTIVAS (MembershipSubscription.EnrollmentContribution), como DQ16 y
    // 05-realinear-puntos-por-membresia.sql. Ya no son los pedidos completados (contaban membresias no activas).

    private static Product ProductoDeNivel(string id, int nivel, int puntos) => new()
    {
        Id = id, Name = id, Description = "d", ImageUrl = "u", MonthlyFee = 0, SetupFee = 0, QualificationPoins = puntos,
        MembershipLevelId = nivel, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static MembershipSubscription Suscripcion(string memberId, int nivel, MembershipStatus estado) => new()
    {
        MemberId = memberId, MembershipLevelId = nivel, SubscriptionStatus = estado, StartDate = Now,
        CreatedBy = "seed", CreationDate = Now
    };

    private static Orders PedidoCompletado(string id, string memberId, string productId, Microsoft.EntityFrameworkCore.DbContext db)
    {
        db.Set<OrderDetail>().Add(new OrderDetail { OrderId = id, ProductId = productId, Quantity = 1, UnitPrice = 0, CreatedBy = "seed", CreationDate = Now });
        return new Orders { Id = id, MemberId = memberId, Status = OrderStatus.Completed, OrderDate = Now, TotalAmount = 0, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
    }

    [Fact]
    public async Task RecomputeEnrollmentPoints_SumaLoQueAportanLasActivasDelSubarbolConElPropio()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(Geno("M", null, "/M/"), Geno("A", "M", "/M/A/"), Geno("B", "M", "/M/B/"));
        await db.Products.AddRangeAsync(ProductoDeNivel("P1", 1, 40), ProductoDeNivel("P2", 2, 50));
        await db.SaveChangesAsync(); // los productos antes: el interceptor lee de la base los puntos de cada nivel
        await db.MembershipSubscriptions.AddRangeAsync(
            Suscripcion("M", 1, MembershipStatus.Active),
            Suscripcion("A", 2, MembershipStatus.Active),
            Suscripcion("B", 2, MembershipStatus.HoldByBilling));
        await db.SaveChangesAsync();

        (await new EnrollmentTeamPointsService(db).RecomputeEnrollmentPointsAsync("M")).Should().Be(90, "40 propios + 50 de A; B retenida no cuenta");
    }

    [Fact]
    public async Task RecomputeEnrollmentPoints_NoCuentaPedidosCompletadosDeMembresiasNoActivas()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(Geno("M", null, "/M/"), Geno("A", "M", "/M/A/"));
        await db.Products.AddAsync(ProductoDeNivel("P2", 2, 30));
        await db.SaveChangesAsync();
        await db.Orders.AddAsync(PedidoCompletado("O1", "A", "P2", db));
        await db.MembershipSubscriptions.AddAsync(Suscripcion("A", 2, MembershipStatus.Cancelled));
        await db.SaveChangesAsync();

        (await new EnrollmentTeamPointsService(db).RecomputeEnrollmentPointsAsync("M")).Should().Be(0);
    }

    [Fact]
    public async Task RecomputeEnrollmentPoints_WhenMemberHasNoGenealogyNode_ReturnsNoData()
    {
        await using var db = InMemoryDbHelper.Create();
        // Completely empty DB — no genealogy node for "GHOST".
        var svc = new EnrollmentTeamPointsService(db);
        var result = await svc.RecomputeEnrollmentPointsAsync("GHOST");

        // Plan M (revisión C1): sin nodo no hay dato; un 0 se persistiría como si el equipo no tuviera puntos.
        result.Should().BeNull();
    }
}
