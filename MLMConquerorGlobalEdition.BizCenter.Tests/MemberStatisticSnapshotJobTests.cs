using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class MemberStatisticSnapshotJobTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 5, 21, 1, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _db;
    private readonly Mock<IDateTimeProvider> _dateTime;

    public MemberStatisticSnapshotJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _dateTime = new Mock<IDateTimeProvider>();
        _dateTime.Setup(x => x.Now).Returns(Now);
    }

    public void Dispose() => _db.Dispose();

    private MemberStatisticSnapshotJob CreateJob() =>
        new(_db, _dateTime.Object, NullLogger<MemberStatisticSnapshotJob>.Instance,
            new EnrollmentTeamPointsService(_db));

    // ── helpers ─────────────────────────────────────────────────────────────

    private static GenealogyEntity Geno(string memberId, string? parentId, string path) => new()
    {
        MemberId = memberId, ParentMemberId = parentId, HierarchyPath = path,
        Level = path.Trim('/').Split('/').Length,
        CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static MemberStatisticEntity Stat(string memberId, int enrollmentPoints,
        int qualifiedSponsored = 0) => new()
    {
        MemberId = memberId,
        EnrollmentPoints = enrollmentPoints,
        QualifiedSponsoredMembers = qualifiedSponsored,
        CreatedBy = "seed",
        CreationDate = Now
    };

    private static MemberProfile Profile(string memberId, string? sponsorId = null) => new()
    {
        MemberId = memberId, SponsorMemberId = sponsorId,
        Status = MemberAccountStatus.Active,
        FirstName = "Test", LastName = "User", Email = $"{memberId}@test.com",
        MemberType = MemberType.Ambassador, Country = "US",
        EnrollDate = Now,
        CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static Product Prod(string id, int points) => new()
    {
        Id = id, Name = id, Description = "d", ImageUrl = "u",
        MonthlyFee = 0, SetupFee = 0, QualificationPoins = points,
        CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static Orders Order(string id, string memberId) => new()
    {
        Id = id, MemberId = memberId, Status = OrderStatus.Completed,
        OrderDate = Now, TotalAmount = 0,
        CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static OrderDetail Detail(string orderId, string productId) => new()
    {
        OrderId = orderId, ProductId = productId, Quantity = 1, UnitPrice = 0,
        CreatedBy = "seed", CreationDate = Now
    };

    // ── tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seed a member with a stale EnrollmentPoints value (999). After
    /// ExecuteAsync the field must be corrected to the true sum from completed
    /// orders: M's own 40 + downline A's 50 = 90.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RegroundsEnrollmentPoints_WhenValueHasDrifted()
    {
        // Genealogy
        await _db.GenealogyTree.AddRangeAsync(
            Geno("M", null, "/M/"),
            Geno("A", "M",  "/M/A/"));

        // Products & orders
        await _db.Products.AddRangeAsync(Prod("PM", 40), Prod("PA", 50));
        await _db.Orders.AddRangeAsync(Order("OM", "M"), Order("OA", "A"));
        await _db.OrderDetails.AddRangeAsync(Detail("OM", "PM"), Detail("OA", "PA"));

        // Stale stat row — EnrollmentPoints is wrong (999 instead of 90)
        await _db.MemberStatistics.AddAsync(Stat("M", enrollmentPoints: 999));
        await _db.SaveChangesAsync();

        await CreateJob().ExecuteAsync();

        var stat = await _db.MemberStatistics.SingleAsync(s => s.MemberId == "M");
        // M's own 40 + A's 50 = 90
        stat.EnrollmentPoints.Should().Be(90);
    }

    /// <summary>
    /// Verify that Phase 1 still correctly refreshes QualifiedSponsoredMembers
    /// from live MemberProfile data independently of the EnrollmentPoints logic.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RefreshesQualifiedSponsoredMembers()
    {
        // Sponsor S has two active direct members
        await _db.MemberProfiles.AddRangeAsync(
            Profile("CHILD1", sponsorId: "S"),
            Profile("CHILD2", sponsorId: "S"));

        // Stat for S shows 0 qualified sponsored (stale)
        await _db.MemberStatistics.AddAsync(Stat("S", enrollmentPoints: 0, qualifiedSponsored: 0));
        await _db.SaveChangesAsync();

        await CreateJob().ExecuteAsync();

        var stat = await _db.MemberStatistics.SingleAsync(s => s.MemberId == "S");
        stat.QualifiedSponsoredMembers.Should().Be(2);
    }
}
