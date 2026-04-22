using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupMember;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

public class SignupMemberHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IPushNotificationService> BuildPush()
    {
        var m = new Mock<IPushNotificationService>();
        m.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IEncryptionService> BuildEncryption()
    {
        var m = new Mock<IEncryptionService>();
        m.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => "ENC:" + s);
        m.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s.Replace("ENC:", ""));
        return m;
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManager(bool emailTaken = false)
    {
        var mgr = UserManagerHelper.Create();
        mgr.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(emailTaken ? new ApplicationUser { Email = "taken@example.com" } : null);
        mgr.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        mgr.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        return mgr;
    }

    private static SignupMemberHandler BuildHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        Mock<UserManager<ApplicationUser>>? userMgr = null,
        Mock<IPushNotificationService>? push = null,
        Mock<IEncryptionService>? encryption = null) =>
        new(db,
            BuildClock().Object,
            (userMgr ?? BuildUserManager()).Object,
            (push ?? BuildPush()).Object,
            (encryption ?? BuildEncryption()).Object);

    private static MemberSignupRequest BuildRequest(
        string email          = "new.member@example.com",
        string? sponsorSlug   = null,
        int membershipLevelId = 1) => new()
    {
        SponsorReplicateSite = sponsorSlug,
        FirstName            = "Ana",
        LastName             = "Lopez",
        Email                = email,
        Password             = "SecurePass!1",
        Phone                = "+1-555-0200",
        Country              = "US",
        State                = "TX",
        City                 = "Houston",
        Address              = "456 Main Street",
        MembershipLevelId    = membershipLevelId
    };

    private static MembershipLevel BuildLevel(int id = 1, bool isActive = true) => new()
    {
        Id           = id,
        Name         = "External Member Basic",
        Description  = "Entry level external member tier",
        Price        = 30,
        RenewalPrice = 30,
        IsActive     = isActive,
        IsFree       = false,
        IsAutoRenew  = true,
        SortOrder    = 1,
        CreatedBy    = "seed",
        CreationDate = FixedNow
    };

    private static MemberProfile BuildSponsor(string memberId, string slug = "pedro-site") => new()
    {
        MemberId          = memberId,
        FirstName         = "Pedro",
        LastName          = "Martinez",
        Email             = "pedro@example.com",
        MemberType        = MemberType.Ambassador,
        Status            = MemberAccountStatus.Active,
        ReplicateSiteSlug = slug,
        EnrollDate        = FixedNow.AddMonths(-3),
        Country           = "US",
        CreatedBy         = "seed",
        LastUpdateDate    = FixedNow
    };

    // ─── Validation: email ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenEmailAlreadyTaken_ReturnsEmailTaken()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var mgr     = BuildUserManager(emailTaken: true);
        var handler = BuildHandler(db, userMgr: mgr);

        var result = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_TAKEN");
    }

    // ─── Validation: sponsor ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSponsorSlugNotFound_ReturnsSponsorNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);

        var result = await handler.Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: "nonexistent-slug")), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPONSOR_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenSponsorSlugIsNull_SkipsSponsorValidation()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);

        var result = await handler.Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: null)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ─── Validation: membership level ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMembershipLevelNotFound_ReturnsMembershipLevelNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        // No level seeded

        var handler = BuildHandler(db);

        var result = await handler.Handle(
            new SignupMemberCommand(BuildRequest(membershipLevelId: 99)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBERSHIP_LEVEL_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenMembershipLevelIsInactive_ReturnsMembershipLevelNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel(isActive: false));
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);

        var result = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBERSHIP_LEVEL_NOT_FOUND");
    }

    // ─── Happy path: DB records ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenValid_CreatesMemberProfileWithExternalMemberType()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var member = await db.MemberProfiles.FirstOrDefaultAsync(m => m.MemberId == result.Value!.MemberId);
        member.Should().NotBeNull();
        member!.MemberType.Should().Be(MemberType.ExternalMember);
        member.Status.Should().Be(MemberAccountStatus.Pending);
        member.Email.Should().Be("new.member@example.com");
    }

    [Fact]
    public async Task Handle_WhenValid_MemberIdStartsWithMbrPrefix()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().StartWith("MBR-");
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesPendingOrder()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == result.Value!.SignupId);
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(0);
        order.MemberId.Should().Be(result.Value!.MemberId);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesPendingMembershipSubscription()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sub = await db.MembershipSubscriptions
            .FirstOrDefaultAsync(s => s.MemberId == result.Value!.MemberId);
        sub.Should().NotBeNull();
        sub!.SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        sub.MembershipLevelId.Should().Be(1);
        sub.ChangeReason.Should().Be(SubscriptionChangeReason.New);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesGenealogyNodeAtLevelOne()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var node = await db.GenealogyTree
            .FirstOrDefaultAsync(g => g.MemberId == result.Value!.MemberId);
        node.Should().NotBeNull();
        node!.ParentMemberId.Should().BeNull();
        node.Level.Should().Be(1);
        node.HierarchyPath.Should().Contain(result.Value!.MemberId);
    }

    [Fact]
    public async Task Handle_WhenValidWithSponsor_CreatesGenealogyNodeUnderSponsor()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        var sponsor = BuildSponsor("AMB-000001", "pedro-site");
        await db.MemberProfiles.AddAsync(sponsor);
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId       = "AMB-000001",
            HierarchyPath  = "/AMB-000001/",
            Level          = 1,
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: "pedro-site")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var node = await db.GenealogyTree
            .FirstOrDefaultAsync(g => g.MemberId == result.Value!.MemberId);
        node.Should().NotBeNull();
        node!.ParentMemberId.Should().Be("AMB-000001");
        node.HierarchyPath.Should().StartWith("/AMB-000001/");
        node.Level.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenValidWithSponsor_QueuesRankEvaluationForUpline()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        var sponsor = BuildSponsor("AMB-000001", "pedro-site");
        await db.MemberProfiles.AddAsync(sponsor);
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId       = "AMB-000001",
            HierarchyPath  = "/AMB-000001/",
            Level          = 1,
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        await handler.Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: "pedro-site")),
            CancellationToken.None);

        var queue = await db.RankEvaluationQueue.ToListAsync();
        queue.Should().NotBeEmpty();
        queue.All(q => q.EvaluateMemberId == "AMB-000001").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidWithNoSponsor_DoesNotQueueRankEvaluation()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        await handler.Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: null)),
            CancellationToken.None);

        var queue = await db.RankEvaluationQueue.ToListAsync();
        queue.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsExternalMemberTypeInResponse()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberType.Should().Be("ExternalMember");
        result.Value.EnrollDate.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Handle_WhenValid_OrderLinksToSubscriptionId()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var order = await db.Orders.FirstAsync(o => o.Id == result.Value!.SignupId);
        var sub   = await db.MembershipSubscriptions
            .FirstAsync(s => s.MemberId == result.Value!.MemberId);

        order.MembershipSubscriptionId.Should().Be(sub.Id);
    }

    // ─── Identity user ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenIdentityCreateFails_ReturnsIdentityCreateFailed()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var mgr = UserManagerHelper.Create();
        mgr.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        mgr.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code        = "DuplicateEmail",
                Description = "Email already in use."
            }));

        var handler = BuildHandler(db, userMgr: mgr);

        var result = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("IDENTITY_CREATE_FAILED");
    }

    [Fact]
    public async Task Handle_WhenValid_AssignsMemberRole()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var mgr     = BuildUserManager();
        var handler = BuildHandler(db, userMgr: mgr);

        await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        mgr.Verify(u => u.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), "Member"), Times.Once);
    }

    // ─── Notifications ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenValidWithSponsor_SendsPushNotificationToUpline()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        var sponsor = BuildSponsor("AMB-000001", "pedro-site");
        await db.MemberProfiles.AddAsync(sponsor);
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId       = "AMB-000001",
            HierarchyPath  = "/AMB-000001/",
            Level          = 1,
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var push    = BuildPush();
        var handler = BuildHandler(db, push: push);

        await handler.Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: "pedro-site")),
            CancellationToken.None);

        push.Verify(p => p.SendAsync(
            "AMB-000001",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValidWithNoSponsor_DoesNotSendPushNotification()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var push    = BuildPush();
        var handler = BuildHandler(db, push: push);

        await handler.Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: null)),
            CancellationToken.None);

        push.Verify(p => p.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Subscription-level config inherited from membership level ───────────────

    [Fact]
    public async Task Handle_WhenLevelIsFree_SubscriptionIsFreeIsTrue()
    {
        await using var db = InMemoryDbHelper.Create();
        var level = BuildLevel();
        level.IsFree = true;
        await db.MembershipLevels.AddAsync(level);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sub = await db.MembershipSubscriptions
            .FirstAsync(s => s.MemberId == result.Value!.MemberId);
        sub.IsFree.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenLevelHasAutoRenew_SubscriptionAutoRenewIsTrue()
    {
        await using var db = InMemoryDbHelper.Create();
        var level = BuildLevel();
        level.IsAutoRenew = true;
        await db.MembershipLevels.AddAsync(level);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new SignupMemberCommand(BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sub = await db.MembershipSubscriptions
            .FirstAsync(s => s.MemberId == result.Value!.MemberId);
        sub.IsAutoRenew.Should().BeTrue();
    }

    // ─── SSN encryption ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCountryIsUS_StoresSsnEncrypted()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var enc     = BuildEncryption();
        var handler = BuildHandler(db, encryption: enc);
        var req     = BuildRequest();
        req.Country = "US";
        req.Ssn     = "987-65-4321";

        var result = await handler.Handle(new SignupMemberCommand(req), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var member = await db.MemberProfiles.FirstAsync(m => m.MemberId == result.Value!.MemberId);
        member.SsnEncrypted.Should().StartWith("ENC:");
        enc.Verify(e => e.Encrypt("987-65-4321"), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCountryIsNotUS_SsnEncryptedIsNull()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var enc     = BuildEncryption();
        var handler = BuildHandler(db, encryption: enc);
        var req     = BuildRequest();
        req.Country = "CA";
        req.Ssn     = null;

        var result = await handler.Handle(new SignupMemberCommand(req), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var member = await db.MemberProfiles.FirstAsync(m => m.MemberId == result.Value!.MemberId);
        member.SsnEncrypted.Should().BeNull();
        enc.Verify(e => e.Encrypt(It.IsAny<string>()), Times.Never);
    }
}
