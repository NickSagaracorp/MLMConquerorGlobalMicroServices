using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.CreateCorporatePromo;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.DeleteCorporatePromo;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromoById;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromos;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoMembers;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoStats;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.UpdateCorporatePromo;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CorporatePromos;

public class CorporatePromosHandlerTests
{
    private static readonly DateTime PromoStart = DateTime.Now.AddYears(-1);
    private static readonly DateTime PromoEnd   = DateTime.Now.AddYears(1);
    private static readonly DateTime FixedNow   = DateTime.Now;

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

    private static PagedRequest Page(int page = 1, int size = 10) => new() { Page = page, PageSize = size };

    private static CorporatePromo BuildPromo(string id = "PROMO-001") => new()
    {
        Id = id,
        Title = "Q1 Promo",
        Description = "First quarter promotion",
        StartDate = PromoStart,
        EndDate = PromoEnd,
        IsActive = true,
        IsDeleted = false,
        CreationDate = FixedNow,
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Create_WhenCalled_CreatesPromoAndReturnsId()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateCorporatePromoHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(new CreateCorporatePromoCommand(new CreateCorporatePromoRequest
        {
            Title = "Summer Sale",
            StartDate = PromoStart,
            EndDate = PromoEnd
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        db.CorporatePromos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_WhenCreated_IsActiveTrueByDefault()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateCorporatePromoHandler(db, CurrentUser().Object, Clock().Object);

        await handler.Handle(new CreateCorporatePromoCommand(new CreateCorporatePromoRequest
        {
            Title = "Promo", StartDate = PromoStart, EndDate = PromoEnd
        }), CancellationToken.None);

        db.CorporatePromos.Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WhenPromoNotFound_ReturnsPromoNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new DeleteCorporatePromoHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new DeleteCorporatePromoCommand("PROMO-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_WhenPromoExists_SoftDeletesAndDeactivates()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporatePromos.AddAsync(BuildPromo());
        await db.SaveChangesAsync();

        var handler = new DeleteCorporatePromoHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new DeleteCorporatePromoCommand("PROMO-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var promo = db.CorporatePromos.Single();
        promo.IsDeleted.Should().BeTrue();
        promo.IsActive.Should().BeFalse();
        promo.DeletedBy.Should().Be("admin-001");
        promo.DeletedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Delete_WhenPromoAlreadyDeleted_ReturnsPromoNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var promo = BuildPromo();
        promo.IsDeleted = true;
        await db.CorporatePromos.AddAsync(promo);
        await db.SaveChangesAsync();

        var handler = new DeleteCorporatePromoHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new DeleteCorporatePromoCommand("PROMO-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WhenPromoNotFound_ReturnsPromoNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetCorporatePromoByIdHandler(db);

        var result = await handler.Handle(
            new GetCorporatePromoByIdQuery("PROMO-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WhenPromoExists_ReturnsDtoWithMemberCount()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporatePromos.AddAsync(BuildPromo());
        await db.MembershipSubscriptions.AddAsync(new MembershipSubscription
        {
            MemberId = "AMB-001",
            MembershipLevelId = 1,
            SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new GetCorporatePromoByIdHandler(db);

        var result = await handler.Handle(
            new GetCorporatePromoByIdQuery("PROMO-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Q1 Promo");
        result.Value.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task GetById_WhenDeletedPromo_ReturnsPromoNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var promo = BuildPromo();
        promo.IsDeleted = true;
        await db.CorporatePromos.AddAsync(promo);
        await db.SaveChangesAsync();

        var handler = new GetCorporatePromoByIdHandler(db);

        var result = await handler.Handle(
            new GetCorporatePromoByIdQuery("PROMO-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task GetPromos_WhenNoPromos_ReturnsEmptyPagedResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetCorporatePromosHandler(db);

        var result = await handler.Handle(
            new GetCorporatePromosQuery(Page()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPromos_ExcludesDeletedPromos()
    {
        await using var db = InMemoryDbHelper.Create();
        var active  = BuildPromo("PROMO-001");
        var deleted = BuildPromo("PROMO-002");
        deleted.IsDeleted = true;
        await db.CorporatePromos.AddRangeAsync(active, deleted);
        await db.SaveChangesAsync();

        var handler = new GetCorporatePromosHandler(db);

        var result = await handler.Handle(
            new GetCorporatePromosQuery(Page()), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().Id.Should().Be("PROMO-001");
    }

    [Fact]
    public async Task GetPromos_ReturnsPaginatedSubset()
    {
        await using var db = InMemoryDbHelper.Create();
        for (int i = 0; i < 4; i++)
        {
            var p = BuildPromo($"PROMO-{i:D3}");
            p.StartDate = PromoStart.AddMonths(i);
            await db.CorporatePromos.AddAsync(p);
        }
        await db.SaveChangesAsync();

        var handler = new GetCorporatePromosHandler(db);

        var result = await handler.Handle(
            new GetCorporatePromosQuery(Page(page: 1, size: 2)), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(4);
        result.Value.Items.Count().Should().Be(2);
    }

    [Fact]
    public async Task GetPromoMembers_WhenPromoNotFound_ReturnsPromoNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetPromoMembersHandler(db);

        var result = await handler.Handle(
            new GetPromoMembersQuery("PROMO-GHOST", Page()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task GetPromoMembers_WhenPromoExists_ReturnsMembersEnrolledDuringWindow()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporatePromos.AddAsync(BuildPromo());
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-001", FirstName = "John", LastName = "Doe",
            Email = "john@test.com", MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Active, IsDeleted = false,
            EnrollDate = DateTime.UtcNow, CreationDate = DateTime.UtcNow,
            CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow
        });
        await db.MembershipSubscriptions.AddAsync(new MembershipSubscription
        {
            MemberId = "AMB-001", MembershipLevelId = 1,
            SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new GetPromoMembersHandler(db);

        var result = await handler.Handle(
            new GetPromoMembersQuery("PROMO-001", Page()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().MemberId.Should().Be("AMB-001");
        result.Value.Items.Single().FullName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetPromoMembers_ExcludesMembersEnrolledOutsideWindow()
    {
        await using var db = InMemoryDbHelper.Create();

        var pastPromo = new CorporatePromo
        {
            Id = "PROMO-PAST",
            Title = "Past Promo",
            StartDate = DateTime.Now.AddYears(-5),
            EndDate   = DateTime.Now.AddYears(-4),
            IsActive  = true,
            IsDeleted = false,
            CreatedBy = "seed"
        };
        await db.CorporatePromos.AddAsync(pastPromo);
        await db.MembershipSubscriptions.AddAsync(new MembershipSubscription
        {
            MemberId = "AMB-OUTSIDE", MembershipLevelId = 1,
            SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new GetPromoMembersHandler(db);

        var result = await handler.Handle(
            new GetPromoMembersQuery("PROMO-PAST", Page()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPromoStats_WhenPromoNotFound_ReturnsPromoNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetPromoStatsHandler(db);

        var result = await handler.Handle(
            new GetPromoStatsQuery("PROMO-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task GetPromoStats_WhenNoSignups_ReturnsZeroStats()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporatePromos.AddAsync(BuildPromo());
        await db.SaveChangesAsync();

        var handler = new GetPromoStatsHandler(db);

        var result = await handler.Handle(
            new GetPromoStatsQuery("PROMO-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalSignups.Should().Be(0);
        result.Value.RetentionRate.Should().Be(0m);
    }

    [Fact]
    public async Task GetPromoStats_WhenSignupsExist_CalculatesRetentionCorrectly()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(new MembershipLevel
        {
            Id = 1, Name = "Ambassador", SortOrder = 1, Price = 0m, IsActive = true,
            CreationDate = FixedNow, CreatedBy = "seed"
        });
        await db.CorporatePromos.AddAsync(BuildPromo());
        await db.MembershipSubscriptions.AddRangeAsync(
            new MembershipSubscription { MemberId = "AMB-001", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active, ChangeReason = SubscriptionChangeReason.New, CreatedBy = "seed" },
            new MembershipSubscription { MemberId = "AMB-002", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active, ChangeReason = SubscriptionChangeReason.New, CreatedBy = "seed" },
            new MembershipSubscription { MemberId = "AMB-003", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Cancelled, ChangeReason = SubscriptionChangeReason.New, CreatedBy = "seed" }
        );
        await db.SaveChangesAsync();

        var handler = new GetPromoStatsHandler(db);

        var result = await handler.Handle(
            new GetPromoStatsQuery("PROMO-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalSignups.Should().Be(3);
        result.Value.ActiveSubscriptions.Should().Be(2);
        result.Value.CancelledSubscriptions.Should().Be(1);
        result.Value.RetentionRate.Should().BeApproximately(66.67m, 0.01m);
    }

    [Fact]
    public async Task Update_WhenPromoNotFound_ReturnsPromoNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateCorporatePromoHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UpdateCorporatePromoCommand("PROMO-GHOST", new UpdateCorporatePromoRequest
            {
                Title = "Updated", StartDate = PromoStart, EndDate = PromoEnd, IsActive = true
            }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task Update_WhenPromoExists_UpdatesFields()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporatePromos.AddAsync(BuildPromo());
        await db.SaveChangesAsync();

        var handler = new UpdateCorporatePromoHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UpdateCorporatePromoCommand("PROMO-001", new UpdateCorporatePromoRequest
            {
                Title = "Updated Title",
                StartDate = PromoStart.AddMonths(1),
                EndDate = PromoEnd.AddMonths(1),
                IsActive = false
            }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = db.CorporatePromos.Single();
        updated.Title.Should().Be("Updated Title");
        updated.IsActive.Should().BeFalse();
        updated.LastUpdateBy.Should().Be("admin-001");
    }
}

