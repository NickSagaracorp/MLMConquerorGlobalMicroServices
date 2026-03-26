using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

public class GetMembersHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static MemberProfile BuildMember(string memberId, MemberAccountStatus status = MemberAccountStatus.Active) => new()
    {
        MemberId = memberId,
        FirstName = "John",
        LastName = "Doe",
        Country = "US",
        Status = status,
        MemberType = MemberType.Ambassador,
        EnrollDate = FixedNow.AddDays(-10),
        CreationDate = FixedNow.AddDays(-10),
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenNoMembers_ReturnsEmptyPagedResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetMembersHandler(db);

        var result = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 10 }, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNoStatusFilter_ReturnsAllMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", MemberAccountStatus.Active),
            BuildMember("AMB-002", MemberAccountStatus.Inactive));
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db);
        var result = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 10 }, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsOnlyMatchingMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", MemberAccountStatus.Active),
            BuildMember("AMB-002", MemberAccountStatus.Inactive),
            BuildMember("AMB-003", MemberAccountStatus.Active));
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db);
        var result = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 10 }, "Active"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.All(m => m.Status == "Active").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithCaseInsensitiveStatusFilter_ReturnsMatchingMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001", MemberAccountStatus.Suspended));
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db);
        var result = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 10 }, "suspended"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithInvalidStatusFilter_ReturnsAllMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db);
        var result = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 10 }, "NotARealStatus"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        await using var db = InMemoryDbHelper.Create();
        for (var i = 1; i <= 5; i++)
            await db.MemberProfiles.AddAsync(BuildMember($"AMB-00{i}"));
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db);
        var result = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 2, PageSize = 2 }, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Count().Should().Be(2);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }
}
