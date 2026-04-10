using MLMConquerorGlobalEdition.AdminAPI.Features.LoyaltyAdmin.UnlockLoyaltyPoints;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Loyalty;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.LoyaltyAdmin;

public class UnlockLoyaltyPointsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static async Task<MLMConquerorGlobalEdition.Repository.Context.AppDbContext> DbWithMember(string memberId = "AMB-001")
    {
        var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = memberId,
            FirstName = "Test",
            LastName = "Member",
            Email = "test@test.com",
            MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Active,
            IsDeleted = false,
            EnrollDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
            CreatedBy = "seed",
            LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsMemberNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UnlockLoyaltyPointsHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UnlockLoyaltyPointsCommand("GHOST-999"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenLoyaltyPointsIdProvided_AndRecordNotFound_ReturnsRecordNotFoundFailure()
    {
        await using var db = await DbWithMember("AMB-001");
        var handler = new UnlockLoyaltyPointsHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UnlockLoyaltyPointsCommand("AMB-001", "LP-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RECORD_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenLoyaltyPointsIdProvided_AndAlreadyUnlocked_ReturnsAlreadyUnlockedFailure()
    {
        await using var db = await DbWithMember("AMB-001");
        await db.LoyaltyPoints.AddAsync(new LoyaltyPoints
        {
            Id = "LP-001", MemberId = "AMB-001", ProductId = "PROD-1", OrderId = "ORD-1",
            PointsEarned = 50m, IsLocked = false,
            CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new UnlockLoyaltyPointsHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UnlockLoyaltyPointsCommand("AMB-001", "LP-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_UNLOCKED");
    }

    [Fact]
    public async Task Handle_WhenLoyaltyPointsIdProvided_AndIsLocked_UnlocksSingleRecord()
    {
        await using var db = await DbWithMember("AMB-001");
        await db.LoyaltyPoints.AddAsync(new LoyaltyPoints
        {
            Id = "LP-001", MemberId = "AMB-001", ProductId = "PROD-1", OrderId = "ORD-1",
            PointsEarned = 50m, IsLocked = true,
            CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new UnlockLoyaltyPointsHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UnlockLoyaltyPointsCommand("AMB-001", "LP-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);

        var updated = db.LoyaltyPoints.Single();
        updated.IsLocked.Should().BeFalse();
        updated.UnlockedAt.Should().Be(FixedNow);
        updated.LastUpdateBy.Should().Be("admin-001");
    }

    [Fact]
    public async Task Handle_WhenNoLoyaltyPointsId_AndNoLockedRecords_ReturnsNothingToUnlockFailure()
    {
        await using var db = await DbWithMember("AMB-001");
        await db.LoyaltyPoints.AddAsync(new LoyaltyPoints
        {
            Id = "LP-001", MemberId = "AMB-001", ProductId = "PROD-1", OrderId = "ORD-1",
            PointsEarned = 50m, IsLocked = false,
            CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new UnlockLoyaltyPointsHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UnlockLoyaltyPointsCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NOTHING_TO_UNLOCK");
    }

    [Fact]
    public async Task Handle_WhenNoLoyaltyPointsId_UnlocksAllLockedRecords()
    {
        await using var db = await DbWithMember("AMB-001");
        await db.LoyaltyPoints.AddRangeAsync(
            new LoyaltyPoints { Id = "LP-001", MemberId = "AMB-001", ProductId = "P1", OrderId = "O1", PointsEarned = 10m, IsLocked = true, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow },
            new LoyaltyPoints { Id = "LP-002", MemberId = "AMB-001", ProductId = "P2", OrderId = "O2", PointsEarned = 20m, IsLocked = true, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow },
            new LoyaltyPoints { Id = "LP-003", MemberId = "AMB-001", ProductId = "P3", OrderId = "O3", PointsEarned = 30m, IsLocked = false, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var handler = new UnlockLoyaltyPointsHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UnlockLoyaltyPointsCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        db.LoyaltyPoints.Count(lp => lp.IsLocked).Should().Be(0);
    }

    [Fact]
    public async Task Handle_BulkUnlock_DoesNotTouchOtherMembersRecords()
    {
        await using var db = await DbWithMember("AMB-001");
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-002", FirstName = "Other", LastName = "Member",
            Email = "other@test.com", MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Active, IsDeleted = false,
            EnrollDate = DateTime.UtcNow, CreationDate = DateTime.UtcNow, CreatedBy = "seed",
            LastUpdateDate = DateTime.UtcNow
        });
        await db.LoyaltyPoints.AddRangeAsync(
            new LoyaltyPoints { Id = "LP-AMB1", MemberId = "AMB-001", ProductId = "P1", OrderId = "O1", PointsEarned = 10m, IsLocked = true, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow },
            new LoyaltyPoints { Id = "LP-AMB2", MemberId = "AMB-002", ProductId = "P2", OrderId = "O2", PointsEarned = 20m, IsLocked = true, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var handler = new UnlockLoyaltyPointsHandler(db, CurrentUser().Object, Clock().Object);

        await handler.Handle(new UnlockLoyaltyPointsCommand("AMB-001"), CancellationToken.None);

        db.LoyaltyPoints.Single(lp => lp.MemberId == "AMB-001").IsLocked.Should().BeFalse();
        db.LoyaltyPoints.Single(lp => lp.MemberId == "AMB-002").IsLocked.Should().BeTrue();
    }
}
