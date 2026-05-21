using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class EnrollmentTeamPointsServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    private static GenealogyEntity Geno(string memberId, string? parentId, string path) => new()
    {
        MemberId = memberId, ParentMemberId = parentId, HierarchyPath = path,
        Level = path.Trim('/').Split('/').Length,
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

    [Fact]
    public async Task RecomputeEnrollmentPoints_SumsCompletedOrderPointsAcrossSubtree()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(
            Geno("M", null, "/M/"),
            Geno("A", "M", "/M/A/"));

        var p1 = new Product { Id = "P1", Name = "P1", Description = "d", ImageUrl = "u", MonthlyFee = 0, SetupFee = 0, QualificationPoins = 30, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        var p2 = new Product { Id = "P2", Name = "P2", Description = "d", ImageUrl = "u", MonthlyFee = 0, SetupFee = 0, QualificationPoins = 20, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        await db.Products.AddRangeAsync(p1, p2);

        var order = new Orders { Id = "O1", MemberId = "A", Status = OrderStatus.Completed, OrderDate = Now, TotalAmount = 0, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddRangeAsync(
            new OrderDetail { OrderId = "O1", ProductId = "P1", Quantity = 1, UnitPrice = 0, CreatedBy = "seed", CreationDate = Now },
            new OrderDetail { OrderId = "O1", ProductId = "P2", Quantity = 1, UnitPrice = 0, CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        var svc = new EnrollmentTeamPointsService(db);
        var result = await svc.RecomputeEnrollmentPointsAsync("M");

        result.Should().Be(50);
    }

    [Fact]
    public async Task RecomputeEnrollmentPoints_ExcludesNonCompletedOrders()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(
            Geno("M", null, "/M/"),
            Geno("A", "M", "/M/A/"));

        var p1 = new Product { Id = "P1", Name = "P1", Description = "d", ImageUrl = "u", MonthlyFee = 0, SetupFee = 0, QualificationPoins = 30, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        await db.Products.AddAsync(p1);

        // Pending order — should be excluded from sum.
        var order = new Orders { Id = "O1", MemberId = "A", Status = OrderStatus.Pending, OrderDate = Now, TotalAmount = 0, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(
            new OrderDetail { OrderId = "O1", ProductId = "P1", Quantity = 1, UnitPrice = 0, CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        var svc = new EnrollmentTeamPointsService(db);
        var result = await svc.RecomputeEnrollmentPointsAsync("M");

        result.Should().Be(0);
    }

    [Fact]
    public async Task RecomputeEnrollmentPoints_WhenMemberHasNoGenealogyNode_ReturnsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        // Completely empty DB — no genealogy node for "GHOST".
        var svc = new EnrollmentTeamPointsService(db);
        var result = await svc.RecomputeEnrollmentPointsAsync("GHOST");

        result.Should().Be(0);
    }

    [Fact]
    public async Task RecomputeEnrollmentPoints_IncludesMembersOwnCompletedOrders()
    {
        // M has a Completed order worth 40 points (own); child A has a Completed order
        // worth 50 points (downline). RecomputeEnrollmentPointsAsync("M") must return 90
        // because EnrollmentPoints = own + downline (the member is included in the subtree).
        await using var db = InMemoryDbHelper.Create();
        await db.GenealogyTree.AddRangeAsync(
            Geno("M", null, "/M/"),
            Geno("A", "M",  "/M/A/"));

        var pM = new Product { Id = "PM", Name = "PM", Description = "d", ImageUrl = "u", MonthlyFee = 0, SetupFee = 0, QualificationPoins = 40, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        var pA = new Product { Id = "PA", Name = "PA", Description = "d", ImageUrl = "u", MonthlyFee = 0, SetupFee = 0, QualificationPoins = 50, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        await db.Products.AddRangeAsync(pM, pA);

        var orderM = new Orders { Id = "OM", MemberId = "M", Status = OrderStatus.Completed, OrderDate = Now, TotalAmount = 0, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        var orderA = new Orders { Id = "OA", MemberId = "A", Status = OrderStatus.Completed, OrderDate = Now, TotalAmount = 0, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now };
        await db.Orders.AddRangeAsync(orderM, orderA);
        await db.OrderDetails.AddRangeAsync(
            new OrderDetail { OrderId = "OM", ProductId = "PM", Quantity = 1, UnitPrice = 0, CreatedBy = "seed", CreationDate = Now },
            new OrderDetail { OrderId = "OA", ProductId = "PA", Quantity = 1, UnitPrice = 0, CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        var svc = new EnrollmentTeamPointsService(db);
        var result = await svc.RecomputeEnrollmentPointsAsync("M");

        // M's own 40 + downline A's 50 = 90
        result.Should().Be(90);
    }
}
