using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.DowngradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

public class DowngradeMembershipHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

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
            EnrollDate = FixedNow.AddDays(-60),
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
            CreatedBy = "seed"
        });
        await db.MembershipSubscriptions.AddAsync(new MembershipSubscription
        {
            MemberId = memberId,
            MembershipLevelId = currentLevelId,
            SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New,
            StartDate = FixedNow.AddDays(-60),
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
        var handler = new DowngradeMembershipHandler(db, DateTimeProvider().Object);

        var result = await handler.Handle(
            new DowngradeMembershipCommand("NON-EXISTENT", 1, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenDowngradingToSameLevel_ThrowsMembershipChangeNotAllowedException()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithSubscription(db, "AMB-000001", 3, 30);
        // Same sort order as current
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = 99, Name = "Same Sort", SortOrder = 30, IsActive = true, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new DowngradeMembershipHandler(db, DateTimeProvider().Object);

        Func<Task> act = () => handler.Handle(
            new DowngradeMembershipCommand("AMB-000001", 99, null), CancellationToken.None);

        await act.Should().ThrowAsync<MembershipChangeNotAllowedException>();
    }

    [Fact]
    public async Task Handle_WhenDowngradingToHigherSortOrder_ThrowsMembershipChangeNotAllowedException()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithSubscription(db, "AMB-000001", 2, 20);
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = 99, Name = "Higher Level", SortOrder = 30, IsActive = true, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new DowngradeMembershipHandler(db, DateTimeProvider().Object);

        Func<Task> act = () => handler.Handle(
            new DowngradeMembershipCommand("AMB-000001", 99, null), CancellationToken.None);

        await act.Should().ThrowAsync<MembershipChangeNotAllowedException>();
    }

    [Fact]
    public async Task Handle_WhenValidDowngrade_ClosesCurrentSubAndCreatesNewOne()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithSubscription(db, "AMB-000001", 3, 30);
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = 2, Name = "VIP", SortOrder = 20, IsActive = true, IsFree = false, IsAutoRenew = true, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new DowngradeMembershipHandler(db, DateTimeProvider().Object);

        var result = await handler.Handle(
            new DowngradeMembershipCommand("AMB-000001", 2, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var subs = db.MembershipSubscriptions.Where(s => s.MemberId == "AMB-000001").ToList();
        subs.Should().HaveCount(2);

        var old = subs.First(s => s.MembershipLevelId == 3);
        old.SubscriptionStatus.Should().Be(MembershipStatus.Expired);

        var newSub = subs.First(s => s.MembershipLevelId == 2);
        newSub.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        newSub.ChangeReason.Should().Be(SubscriptionChangeReason.Downgrade);
        newSub.PreviousMembershipLevelId.Should().Be(3);
    }
}
