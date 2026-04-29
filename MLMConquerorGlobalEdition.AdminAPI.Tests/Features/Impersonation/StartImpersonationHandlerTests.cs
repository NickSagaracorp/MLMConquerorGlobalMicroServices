using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StartImpersonation;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Impersonation;

public class StartImpersonationHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IJwtService> CreateJwtService()
    {
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns("impersonation-token");
        return jwt;
    }

    private static MemberProfile BuildMember(string memberId) => new()
    {
        MemberId = memberId,
        FirstName = "Alice",
        LastName = "Doe",
        Country = "US",
        Status = MemberAccountStatus.Active,
        MemberType = MemberType.Ambassador,
        EnrollDate = FixedNow.AddDays(-30),
        CreationDate = FixedNow.AddDays(-30),
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsMemberNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser>().AsAsyncQueryable());

        var handler = new StartImpersonationHandler(
            db, userManager.Object, CreateJwtService().Object,
            DateTimeProvider().Object, new Mock<ILogger<StartImpersonationHandler>>().Object);

        var result = await handler.Handle(
            new StartImpersonationCommand("admin-001", new[] { "Admin" }, "AMB-999"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoLinkedUser_ReturnsMemberHasNoUserAccountFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-001");
        await db.MemberProfiles.AddAsync(member);
        await db.SaveChangesAsync();

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser>().AsAsyncQueryable());

        var handler = new StartImpersonationHandler(
            db, userManager.Object, CreateJwtService().Object,
            DateTimeProvider().Object, new Mock<ILogger<StartImpersonationHandler>>().Object);

        var result = await handler.Handle(
            new StartImpersonationCommand("admin-001", new[] { "Admin" }, "AMB-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_HAS_NO_USER_ACCOUNT");
    }

    [Fact]
    public async Task Handle_WhenSupportManagerWithoutAdmin_IsReadOnly()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-001");
        await db.MemberProfiles.AddAsync(member);
        await db.SaveChangesAsync();

        var targetUser = new ApplicationUser
        {
            Id = "user-001",
            Email = "amb@test.com",
            MemberProfileId = member.MemberId,
            IsActive = true
        };
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { targetUser }.AsAsyncQueryable());
        userManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(new List<string> { "Ambassador" });

        var handler = new StartImpersonationHandler(
            db, userManager.Object, CreateJwtService().Object,
            DateTimeProvider().Object, new Mock<ILogger<StartImpersonationHandler>>().Object);

        var result = await handler.Handle(
            new StartImpersonationCommand("admin-001", new[] { "SupportManager" }, "AMB-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAdminRole_IsNotReadOnly()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-001");
        await db.MemberProfiles.AddAsync(member);
        await db.SaveChangesAsync();

        var targetUser = new ApplicationUser
        {
            Id = "user-001",
            Email = "amb@test.com",
            MemberProfileId = member.MemberId,
            IsActive = true
        };
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { targetUser }.AsAsyncQueryable());
        userManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(new List<string> { "Ambassador" });

        var handler = new StartImpersonationHandler(
            db, userManager.Object, CreateJwtService().Object,
            DateTimeProvider().Object, new Mock<ILogger<StartImpersonationHandler>>().Object);

        var result = await handler.Handle(
            new StartImpersonationCommand("admin-001", new[] { "Admin" }, "AMB-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenSupportManagerAndSuperAdmin_IsNotReadOnly()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-001");
        await db.MemberProfiles.AddAsync(member);
        await db.SaveChangesAsync();

        var targetUser = new ApplicationUser
        {
            Id = "user-001",
            Email = "amb@test.com",
            MemberProfileId = member.MemberId,
            IsActive = true
        };
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { targetUser }.AsAsyncQueryable());
        userManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(new List<string> { "Ambassador" });

        var handler = new StartImpersonationHandler(
            db, userManager.Object, CreateJwtService().Object,
            DateTimeProvider().Object, new Mock<ILogger<StartImpersonationHandler>>().Object);

        var result = await handler.Handle(
            new StartImpersonationCommand("admin-001", new[] { "SupportManager", "SuperAdmin" }, "AMB-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenSuccess_ReturnsMemberIdAndNameInResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-001");
        await db.MemberProfiles.AddAsync(member);
        await db.SaveChangesAsync();

        var targetUser = new ApplicationUser
        {
            Id = "user-001",
            Email = "amb@test.com",
            MemberProfileId = member.MemberId,
            IsActive = true
        };
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { targetUser }.AsAsyncQueryable());
        userManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(new List<string> { "Ambassador" });

        var handler = new StartImpersonationHandler(
            db, userManager.Object, CreateJwtService().Object,
            DateTimeProvider().Object, new Mock<ILogger<StartImpersonationHandler>>().Object);

        var result = await handler.Handle(
            new StartImpersonationCommand("admin-001", new[] { "Admin" }, "AMB-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().Be("AMB-001");
        result.Value.MemberName.Should().Be("Alice Doe");
        result.Value.AccessToken.Should().Be("impersonation-token");
    }
}
