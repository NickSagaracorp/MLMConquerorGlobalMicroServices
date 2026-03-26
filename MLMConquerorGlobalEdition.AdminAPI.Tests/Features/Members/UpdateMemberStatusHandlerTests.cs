using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

public class UpdateMemberStatusHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static MemberProfile BuildMember(string memberId) => new()
    {
        MemberId = memberId,
        FirstName = "Bob",
        LastName = "Jones",
        Country = "US",
        Status = MemberAccountStatus.Active,
        MemberType = MemberType.Ambassador,
        EnrollDate = FixedNow.AddDays(-20),
        CreationDate = FixedNow.AddDays(-20),
        LastUpdateDate = FixedNow.AddDays(-1),
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsMemberNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateMemberStatusHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new UpdateMemberStatusCommand("AMB-999", new UpdateMemberStatusRequest
            {
                Status = MemberAccountStatus.Inactive
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenMemberFound_UpdatesStatusAndCreatesHistory()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.SaveChangesAsync();

        var handler = new UpdateMemberStatusHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new UpdateMemberStatusCommand("AMB-001", new UpdateMemberStatusRequest
            {
                Status = MemberAccountStatus.Suspended,
                Reason = "Policy violation"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Suspended");

        var member = db.MemberProfiles.Single();
        member.Status.Should().Be(MemberAccountStatus.Suspended);

        var history = db.MemberStatusHistories.Single();
        history.MemberId.Should().Be("AMB-001");
        history.OldStatus.Should().Be(MemberAccountStatus.Active);
        history.NewStatus.Should().Be(MemberAccountStatus.Suspended);
        history.Reason.Should().Be("Policy violation");
        history.ChangedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Handle_WhenStatusUpdated_ReturnsDtoWithNewStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.SaveChangesAsync();

        var handler = new UpdateMemberStatusHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new UpdateMemberStatusCommand("AMB-001", new UpdateMemberStatusRequest
            {
                Status = MemberAccountStatus.Inactive
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().Be("AMB-001");
        result.Value.FirstName.Should().Be("Bob");
        result.Value.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Handle_WhenStatusUpdated_SetsLastUpdateByToCurrentUser()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.SaveChangesAsync();

        var handler = new UpdateMemberStatusHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        await handler.Handle(
            new UpdateMemberStatusCommand("AMB-001", new UpdateMemberStatusRequest
            {
                Status = MemberAccountStatus.Inactive
            }),
            CancellationToken.None);

        var member = db.MemberProfiles.Single();
        member.LastUpdateBy.Should().Be("admin-001");
    }
}
