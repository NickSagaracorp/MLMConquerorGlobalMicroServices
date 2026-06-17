using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Grid;
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
        var handler = new GetMembersHandler(db, new NoOpCacheService());

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

        var handler = new GetMembersHandler(db, new NoOpCacheService());
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

        var handler = new GetMembersHandler(db, new NoOpCacheService());
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

        var handler = new GetMembersHandler(db, new NoOpCacheService());
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

        var handler = new GetMembersHandler(db, new NoOpCacheService());
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

        var handler = new GetMembersHandler(db, new NoOpCacheService());
        var result = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 2, PageSize = 2 }, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Count().Should().Be(2);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenSearchMatchesMember_ReturnsItRegardlessOfPage()
    {
        // The whole point of server-side search: a name that would land on a later
        // page must still be found from page 1 with a small page size.
        await using var db = InMemoryDbHelper.Create();
        for (var i = 1; i <= 5; i++)
            await db.MemberProfiles.AddAsync(BuildMember($"AMB-00{i}")); // all "John Doe"
        var target = BuildMember("AMB-TARGET");
        target.FirstName = "Zelda";
        target.LastName  = "Targaryen";
        await db.MemberProfiles.AddAsync(target);
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db, new NoOpCacheService());
        var result  = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 2 }, null, SearchTerm: "targaryen"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().MemberId.Should().Be("AMB-TARGET");
    }

    [Fact]
    public async Task Handle_WhenColumnFilterByCountry_ReturnsOnlyMatchingAcrossDataset()
    {
        await using var db = InMemoryDbHelper.Create();
        var us = BuildMember("AMB-US"); us.Country = "US";
        var ca = BuildMember("AMB-CA"); ca.Country = "CA";
        await db.MemberProfiles.AddRangeAsync(us, ca);
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db, new NoOpCacheService());
        var filters = new List<GridFilter> { new() { Field = "Country", Operator = "equal", Value = "CA" } };
        var result  = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 10 }, null, Filters: filters),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().MemberId.Should().Be("AMB-CA");
    }

    [Fact]
    public async Task Handle_WhenSortByFirstNameDescending_OrdersResults()
    {
        await using var db = InMemoryDbHelper.Create();
        var a = BuildMember("AMB-A"); a.FirstName = "Aaron";
        var z = BuildMember("AMB-Z"); z.FirstName = "Zoe";
        await db.MemberProfiles.AddRangeAsync(a, z);
        await db.SaveChangesAsync();

        var handler = new GetMembersHandler(db, new NoOpCacheService());
        var sorts   = new List<GridSort> { new() { Field = "FirstName", Direction = "desc" } };
        var result  = await handler.Handle(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 10 }, null, Sorts: sorts),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.First().FirstName.Should().Be("Zoe");
    }
}
