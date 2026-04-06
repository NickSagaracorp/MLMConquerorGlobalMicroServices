using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetAllTeamMembers;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTree;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentTeam;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetTeamMembers;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class TeamHandlersTests : IDisposable
{
    private const string MemberId = "member-team-001";

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;

    public TeamHandlersTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);
    }

    public void Dispose() => _db.Dispose();

    private MemberProfile BuildProfile(string memberId, string? sponsorId = null) => new()
    {
        MemberId        = memberId,
        FirstName       = "Test",
        LastName        = memberId,
        MemberType      = MemberType.Ambassador,
        Status          = MemberAccountStatus.Active,
        EnrollDate      = DateTime.UtcNow.AddDays(-10),
        SponsorMemberId = sponsorId,
        CreationDate    = DateTime.UtcNow,
        LastUpdateDate  = DateTime.UtcNow,
        CreatedBy       = "seed"
    };

    // ── GetEnrollmentTeamHandler ───────────────────────────────────────────────

    [Fact]
    public async Task GetEnrollmentTeam_WhenMemberHasDirectChildren_ReturnsDirectChildrenOnly()
    {
        _db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = "child-1", ParentMemberId = MemberId, HierarchyPath = $"/{MemberId}/child-1/" },
            new GenealogyEntity { MemberId = "child-2", ParentMemberId = MemberId, HierarchyPath = $"/{MemberId}/child-2/" },
            new GenealogyEntity { MemberId = "grandchild", ParentMemberId = "child-1", HierarchyPath = $"/{MemberId}/child-1/grandchild/" }
        );
        _db.MemberProfiles.AddRange(
            BuildProfile("child-1", MemberId),
            BuildProfile("child-2", MemberId),
            BuildProfile("grandchild", "child-1")
        );
        await _db.SaveChangesAsync();

        var handler = new GetEnrollmentTeamHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetEnrollmentTeamQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2); // only direct children
        result.Value.Select(m => m.MemberId).Should().Contain("child-1").And.Contain("child-2");
    }

    [Fact]
    public async Task GetEnrollmentTeam_WhenNoDirectChildren_ReturnsEmpty()
    {
        var handler = new GetEnrollmentTeamHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetEnrollmentTeamQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEnrollmentTeam_WhenOtherMemberHasChildren_DoesNotIncludeThem()
    {
        _db.GenealogyTree.Add(
            new GenealogyEntity { MemberId = "other-child", ParentMemberId = "other-member", HierarchyPath = "/other-member/other-child/" }
        );
        _db.MemberProfiles.Add(BuildProfile("other-child", "other-member"));
        await _db.SaveChangesAsync();

        var handler = new GetEnrollmentTeamHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetEnrollmentTeamQuery(), default);

        result.Value!.Should().BeEmpty();
    }

    // ── GetTeamMembersHandler ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamMembers_WhenMemberHasSponsoredMembers_ReturnsThem()
    {
        _db.MemberProfiles.AddRange(
            BuildProfile("sponsored-1", MemberId),
            BuildProfile("sponsored-2", MemberId),
            BuildProfile("not-mine", "other-sponsor")
        );
        await _db.SaveChangesAsync();

        var handler = new GetTeamMembersHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTeamMembersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Select(m => m.MemberId)
              .Should().Contain("sponsored-1").And.Contain("sponsored-2");
    }

    [Fact]
    public async Task GetTeamMembers_WhenNoSponsoredMembers_ReturnsEmptyPage()
    {
        var handler = new GetTeamMembersHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTeamMembersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTeamMembers_WhenItemsExceedPageSize_PaginatesCorrectly()
    {
        for (int i = 0; i < 5; i++)
            _db.MemberProfiles.Add(BuildProfile($"sponsored-{i}", MemberId));
        await _db.SaveChangesAsync();

        var handler = new GetTeamMembersHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTeamMembersQuery(1, 3), default);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(3);
    }

    // ── GetAllTeamMembersHandler ───────────────────────────────────────────────

    [Fact]
    public async Task GetAllTeamMembers_WhenMemberHasFullSubtree_ReturnsAllDescendants()
    {
        // Subtree: member → child-1 → grandchild; member → child-2
        var pattern = $"/{MemberId}/";
        _db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = "child-1",   ParentMemberId = MemberId,   HierarchyPath = $"/{MemberId}/child-1/" },
            new GenealogyEntity { MemberId = "child-2",   ParentMemberId = MemberId,   HierarchyPath = $"/{MemberId}/child-2/" },
            new GenealogyEntity { MemberId = "grandchild", ParentMemberId = "child-1", HierarchyPath = $"/{MemberId}/child-1/grandchild/" },
            new GenealogyEntity { MemberId = "outsider",  ParentMemberId = "z",        HierarchyPath = "/z/outsider/" }
        );
        _db.MemberProfiles.AddRange(
            BuildProfile("child-1"),
            BuildProfile("child-2"),
            BuildProfile("grandchild"),
            BuildProfile("outsider")
        );
        await _db.SaveChangesAsync();

        var handler = new GetAllTeamMembersHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetAllTeamMembersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(3); // child-1, child-2, grandchild
        result.Value.Items.Select(m => m.MemberId)
              .Should().NotContain("outsider");
    }

    [Fact]
    public async Task GetAllTeamMembers_WhenNoDescendants_ReturnsEmptyPage()
    {
        var handler = new GetAllTeamMembersHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetAllTeamMembersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllTeamMembers_PaginatesDescendantsCorrectly()
    {
        _db.GenealogyTree.AddRange(Enumerable.Range(1, 5).Select(i =>
            new GenealogyEntity
            {
                MemberId      = $"m-{i}",
                ParentMemberId = MemberId,
                HierarchyPath  = $"/{MemberId}/m-{i}/"
            }));
        _db.MemberProfiles.AddRange(Enumerable.Range(1, 5).Select(i => BuildProfile($"m-{i}")));
        await _db.SaveChangesAsync();

        var handler = new GetAllTeamMembersHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetAllTeamMembersQuery(1, 3), default);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(3);
    }

    // ── GetDualTreeHandler ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetDualTree_WhenMemberHasLeftAndRightChildren_ReturnsBoth()
    {
        _db.DualTeamTree.AddRange(
            new DualTeamEntity { MemberId = "left-child",  ParentMemberId = MemberId, Side = TreeSide.Left,  HierarchyPath = $"/{MemberId}/left-child/" },
            new DualTeamEntity { MemberId = "right-child", ParentMemberId = MemberId, Side = TreeSide.Right, HierarchyPath = $"/{MemberId}/right-child/" }
        );
        _db.MemberProfiles.AddRange(
            BuildProfile("left-child"),
            BuildProfile("right-child")
        );
        await _db.SaveChangesAsync();

        var handler = new GetDualTreeHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetDualTreeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Should().Contain(m => m.Side == "Left");
        result.Value.Should().Contain(m => m.Side == "Right");
    }

    [Fact]
    public async Task GetDualTree_WhenNoChildren_ReturnsEmpty()
    {
        var handler = new GetDualTreeHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetDualTreeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDualTree_WhenChildHasNoProfile_IsExcluded()
    {
        // Child in tree but no MemberProfile
        _db.DualTeamTree.Add(
            new DualTeamEntity { MemberId = "orphan", ParentMemberId = MemberId, Side = TreeSide.Left, HierarchyPath = $"/{MemberId}/orphan/" }
        );
        await _db.SaveChangesAsync();

        var handler = new GetDualTreeHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetDualTreeQuery(), default);

        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDualTree_WhenOtherMemberHasChildren_DoesNotReturnThem()
    {
        _db.DualTeamTree.Add(
            new DualTeamEntity { MemberId = "other-child", ParentMemberId = "other-parent", Side = TreeSide.Left, HierarchyPath = "/other-parent/other-child/" }
        );
        _db.MemberProfiles.Add(BuildProfile("other-child"));
        await _db.SaveChangesAsync();

        var handler = new GetDualTreeHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetDualTreeQuery(), default);

        result.Value!.Should().BeEmpty();
    }
}
