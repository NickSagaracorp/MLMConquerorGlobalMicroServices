using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.CancelMembership;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

public class CancelMembershipHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static async Task<string> SeedMemberWithActiveSubscription(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string memberId,
        string? lastOrderId = null)
    {
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = memberId,
            FirstName = "Test",
            LastName = "User",
            MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Active,
            EnrollDate = FixedNow.AddDays(-10),
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        var sub = new MembershipSubscription
        {
            MemberId = memberId,
            MembershipLevelId = 2,
            SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New,
            StartDate = FixedNow.AddDays(-10),
            LastOrderId = lastOrderId,
            IsAutoRenew = true,
            CreatedBy = memberId,
            LastUpdateDate = FixedNow
        };
        await db.MembershipSubscriptions.AddAsync(sub);
        await db.SaveChangesAsync();
        return sub.Id;
    }

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var sponsorBonus = new Mock<ISponsorBonusService>();
        var handler = new CancelMembershipHandler(db, DateTimeProvider().Object, sponsorBonus.Object);

        var result = await handler.Handle(
            new CancelMembershipCommand("NON-EXISTENT", null), CancellationToken.None);

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
            EnrollDate = FixedNow.AddDays(-10),
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var sponsorBonus = new Mock<ISponsorBonusService>();
        var handler = new CancelMembershipHandler(db, DateTimeProvider().Object, sponsorBonus.Object);

        var result = await handler.Handle(
            new CancelMembershipCommand("AMB-000001", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_ACTIVE_SUBSCRIPTION");
    }

    [Fact]
    public async Task Handle_WhenScheduledCancellationDateIsFuture_SetsAutoRenewFalseAndCancellationDateButKeepsActive()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithActiveSubscription(db, "AMB-000001");

        var futureDate = FixedNow.AddDays(15);
        var sponsorBonus = new Mock<ISponsorBonusService>();
        var handler = new CancelMembershipHandler(db, DateTimeProvider().Object, sponsorBonus.Object);

        var result = await handler.Handle(
            new CancelMembershipCommand("AMB-000001", "No longer interested", futureDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sub = db.MembershipSubscriptions.First(s => s.MemberId == "AMB-000001");
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Active); // still active
        sub.IsAutoRenew.Should().BeFalse();
        sub.CancellationDate.Should().Be(futureDate);

        // Member should still be active
        var member = db.MemberProfiles.First(m => m.MemberId == "AMB-000001");
        member.Status.Should().Be(MemberAccountStatus.Active);

        // SponsorBonus reversal should NOT be called for scheduled cancellations
        sponsorBonus.Verify(
            s => s.TryReverseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                                   It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenImmediateCancellation_SetsStatusCancelledAndMemberInactive()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedMemberWithActiveSubscription(db, "AMB-000001");

        var sponsorBonus = new Mock<ISponsorBonusService>();
        sponsorBonus.Setup(s => s.TryReverseAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CancelMembershipHandler(db, DateTimeProvider().Object, sponsorBonus.Object);

        var result = await handler.Handle(
            new CancelMembershipCommand("AMB-000001", "Leaving", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sub = db.MembershipSubscriptions.First(s => s.MemberId == "AMB-000001");
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
        sub.EndDate.Should().Be(FixedNow);
        sub.IsAutoRenew.Should().BeFalse();

        var member = db.MemberProfiles.First(m => m.MemberId == "AMB-000001");
        member.Status.Should().Be(MemberAccountStatus.Inactive);

        var statusHistory = db.MemberStatusHistories.FirstOrDefault(h => h.MemberId == "AMB-000001");
        statusHistory.Should().NotBeNull();
        statusHistory!.NewStatus.Should().Be(MemberAccountStatus.Inactive);
    }

    [Fact]
    public async Task Handle_WhenSignupOrderWithin14Days_CallsTryReverseAsync()
    {
        await using var db = InMemoryDbHelper.Create();
        var orderId = "order-001";

        // Order created 5 days ago — within 14-day chargeback window
        await db.Orders.AddAsync(new Orders
        {
            Id = orderId,
            MemberId = "AMB-000001",
            OrderDate = FixedNow.AddDays(-5),
            CreatedBy = "AMB-000001",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        await SeedMemberWithActiveSubscription(db, "AMB-000001", lastOrderId: orderId);

        var sponsorBonus = new Mock<ISponsorBonusService>();
        sponsorBonus.Setup(s => s.TryReverseAsync(
            "AMB-000001", orderId, It.IsAny<string?>(),
            FixedNow, "AMB-000001", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CancelMembershipHandler(db, DateTimeProvider().Object, sponsorBonus.Object);

        await handler.Handle(new CancelMembershipCommand("AMB-000001", "Refund request", null), CancellationToken.None);

        sponsorBonus.Verify(
            s => s.TryReverseAsync("AMB-000001", orderId, It.IsAny<string?>(),
                                   FixedNow, "AMB-000001", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSignupOrderOlderThan14Days_DoesNotCallTryReverseAsync()
    {
        await using var db = InMemoryDbHelper.Create();
        var orderId = "order-002";

        // Order created 15 days ago — outside 14-day chargeback window
        await db.Orders.AddAsync(new Orders
        {
            Id = orderId,
            MemberId = "AMB-000002",
            OrderDate = FixedNow.AddDays(-15),
            CreatedBy = "AMB-000002",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        await SeedMemberWithActiveSubscription(db, "AMB-000002", lastOrderId: orderId);

        var sponsorBonus = new Mock<ISponsorBonusService>();
        var handler = new CancelMembershipHandler(db, DateTimeProvider().Object, sponsorBonus.Object);

        await handler.Handle(new CancelMembershipCommand("AMB-000002", null, null), CancellationToken.None);

        sponsorBonus.Verify(
            s => s.TryReverseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                                   It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenImmediateCancellationWithUplineStats_ReversesUplinePoints()
    {
        await using var db = InMemoryDbHelper.Create();

        // Sponsor member with stats
        await db.MemberStatistics.AddAsync(new MemberStatisticEntity
        {
            MemberId = "AMB-SPONSOR",
            PersonalPoints = 6,
            EnrollmentPoints = 12,
            EnrollmentTeamSize = 2,
            QualifiedSponsoredMembers = 2,
            CreatedBy = "seed"
        });

        // New member being cancelled
        await db.MemberStatistics.AddAsync(new MemberStatisticEntity
        {
            MemberId = "AMB-000001",
            PersonalPoints = 6,
            EnrollmentPoints = 0,
            EnrollmentTeamSize = 0,
            QualifiedSponsoredMembers = 0,
            CreatedBy = "seed"
        });

        // Genealogy node showing sponsor relationship
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId = "AMB-000001",
            ParentMemberId = "AMB-SPONSOR",
            HierarchyPath = "/AMB-SPONSOR/AMB-000001",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });

        await SeedMemberWithActiveSubscription(db, "AMB-000001");

        var sponsorBonus = new Mock<ISponsorBonusService>();
        sponsorBonus.Setup(s => s.TryReverseAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CancelMembershipHandler(db, DateTimeProvider().Object, sponsorBonus.Object);
        await handler.Handle(new CancelMembershipCommand("AMB-000001", null, null), CancellationToken.None);

        // Cancelled member's points zeroed
        var memberStat = db.MemberStatistics.First(s => s.MemberId == "AMB-000001");
        memberStat.PersonalPoints.Should().Be(0);

        // Sponsor stats decremented
        var sponsorStat = db.MemberStatistics.First(s => s.MemberId == "AMB-SPONSOR");
        sponsorStat.EnrollmentPoints.Should().Be(6);      // 12 - 6
        sponsorStat.EnrollmentTeamSize.Should().Be(1);    // 2 - 1
        sponsorStat.QualifiedSponsoredMembers.Should().Be(1); // 2 - 1 (direct sponsor)
    }
}
