using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Signups.Features.Membership.Commands.UpgradeMembership;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Membership;

public class UpgradeMembershipHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTime() =>
        new Mock<IDateTimeProvider>().Also(m => m.Setup(d => d.Now).Returns(FixedNow));

    private static async Task SeedMemberWithSubscription(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string memberId,
        int currentLevelId,
        int currentSortOrder)
    {
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = memberId,
            FirstName = "Test",
            LastName = "User",
            MemberType = MemberType.Ambassador,
            EnrollDate = FixedNow.AddDays(-30),
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = currentLevelId,
            Name = $"Level {currentLevelId}",
            SortOrder = currentSortOrder,
            IsActive = true,
            IsFree = false,
            IsAutoRenew = true,
            CreatedBy = "seed"
        });
        await db.MembershipSubscriptions.AddAsync(new MembershipSubscription
        {
            MemberId = memberId,
            MembershipLevelId = currentLevelId,
            SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New,
            StartDate = FixedNow.AddDays(-30),
            IsAutoRenew = true,
            CreatedBy = memberId,
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpgradeMembershipHandler(db, DateTime().Object);

        var result = await handler.Handle(
            new UpgradeMembershipCommand("NON-EXISTENT", 2, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoActiveSubscription_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-000001",
            FirstName = "Test",
            LastName = "User",
            MemberType = MemberType.Ambassador,
            EnrollDate = FixedNow.AddDays(-30),
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new UpgradeMembershipHandler(db, DateTime().Object);

        var result = await handler.Handle(
            new UpgradeMembershipCommand("AMB-000001", 2, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_ACTIVE_SUBSCRIPTION");
    }

    [Fact]
    public async Task Handle_WhenTargetLevelNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithSubscription(db, "AMB-000001", 1, 10);

        var handler = new UpgradeMembershipHandler(db, DateTime().Object);

        var result = await handler.Handle(
            new UpgradeMembershipCommand("AMB-000001", 99, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBERSHIP_LEVEL_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenUpgradingToSameLevel_ThrowsMembershipChangeNotAllowedException()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithSubscription(db, "AMB-000001", 2, 20);
        // Same level (sortOrder=20) as new target
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = 99, Name = "Same Sort Order", SortOrder = 20, IsActive = true, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new UpgradeMembershipHandler(db, DateTime().Object);

        Func<Task> act = () => handler.Handle(
            new UpgradeMembershipCommand("AMB-000001", 99, null), CancellationToken.None);

        await act.Should().ThrowAsync<MembershipChangeNotAllowedException>();
    }

    [Fact]
    public async Task Handle_WhenUpgradingToLowerSortOrder_ThrowsMembershipChangeNotAllowedException()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithSubscription(db, "AMB-000001", 2, 30);
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = 99, Name = "Lower Level", SortOrder = 10, IsActive = true, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new UpgradeMembershipHandler(db, DateTime().Object);

        Func<Task> act = () => handler.Handle(
            new UpgradeMembershipCommand("AMB-000001", 99, null), CancellationToken.None);

        await act.Should().ThrowAsync<MembershipChangeNotAllowedException>();
    }

    [Fact]
    public async Task Handle_WhenValidUpgrade_ClosesCurrentSubAndCreatesNewOne()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithSubscription(db, "AMB-000001", 1, 10);
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = 3, Name = "Elite", SortOrder = 30, IsActive = true, IsFree = false, IsAutoRenew = true, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new UpgradeMembershipHandler(db, DateTime().Object);

        var result = await handler.Handle(
            new UpgradeMembershipCommand("AMB-000001", 3, "Upgrading to Elite"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var subs = db.MembershipSubscriptions.Where(s => s.MemberId == "AMB-000001").ToList();
        subs.Should().HaveCount(2);

        var old = subs.First(s => s.MembershipLevelId == 1);
        old.SubscriptionStatus.Should().Be(MembershipStatus.Expired);
        old.EndDate.Should().Be(FixedNow);

        var newSub = subs.First(s => s.MembershipLevelId == 3);
        newSub.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        newSub.ChangeReason.Should().Be(SubscriptionChangeReason.Upgrade);
        newSub.PreviousMembershipLevelId.Should().Be(1);
    }
}

// Extension helper to make mock setup more fluent
file static class MockExtensions
{
    public static Mock<T> Also<T>(this Mock<T> mock, Action<Mock<T>> configure) where T : class
    {
        configure(mock);
        return mock;
    }
}
