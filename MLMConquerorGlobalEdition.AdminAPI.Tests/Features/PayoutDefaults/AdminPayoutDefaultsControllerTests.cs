using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.PayoutDefaults;

/// <summary>
/// AdminPayoutDefaultsController owns the per-country default payout gateway
/// CRUD. The retroactive update is the high-risk path — these tests pin its
/// behavior so a stray change can't accidentally start rewriting unrelated
/// members' wallets.
/// </summary>
public class AdminPayoutDefaultsControllerTests
{
    private static AdminPayoutDefaultsController CreateController(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
    {
        var controller = new AdminPayoutDefaultsController(db);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "test-admin") },
            "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    private static Country BuildCountry(string iso2) => new()
    {
        Iso2 = iso2, Iso3 = $"{iso2}X", NameEn = iso2, NameNative = iso2,
        DefaultLanguageCode = "en", FlagEmoji = "🏳", IsActive = true,
        CreatedBy = "seed", CreationDate = DateTime.UtcNow
    };

    private static CountryPayoutDefault BuildDefault(string iso2, WalletType type, int id = 1) => new()
    {
        Id = id, CountryIso2 = iso2, WalletType = type, IsActive = true,
        CreatedBy = "seed", CreationDate = DateTime.UtcNow, LastUpdateDate = DateTime.UtcNow
    };

    private static MemberProfile BuildMember(string memberId, string country) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = "Member",
        Email          = $"{memberId}@x.com",
        Country        = country,
        MemberType     = MemberType.Ambassador,
        Status         = MemberAccountStatus.Active,
        EnrollDate     = DateTime.UtcNow,
        CreatedBy      = "seed",
        CreationDate   = DateTime.UtcNow,
        LastUpdateDate = DateTime.UtcNow
    };

    private static MemberProfilesWallet BuildWallet(
        string id, string memberId, WalletType type,
        WalletStatus status = WalletStatus.Approved) => new()
    {
        Id             = id,
        MemberId       = memberId,
        WalletType     = type,
        Status         = status,
        IsPreferred    = true,
        CreatedBy      = "seed",
        CreationDate   = DateTime.UtcNow,
        LastUpdateDate = DateTime.UtcNow
    };

    // ── PUT — basic update (no retroactive) ──────────────────────────────────

    [Fact]
    public async Task Update_WhenIdNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db);

        var result = await controller.Update(99, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType = "Paypal"
        }, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_WithInvalidWalletType_ReturnsBadRequest()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Update(1, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType = "NotAGateway"
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_WhenApplyRetroactivelyFalse_DoesNotTouchWallets()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-1", "US"));
        await db.Wallets.AddAsync(BuildWallet("W-1", "AMB-1", WalletType.Dwolla));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        await controller.Update(1, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType         = "Paypal",
            IsActive           = true,
            ApplyRetroactively = false
        }, CancellationToken.None);

        var wallet = await db.Wallets.AsNoTracking().FirstAsync(w => w.Id == "W-1");
        wallet.WalletType.Should().Be(WalletType.Dwolla); // untouched
        (await db.WalletHistories.CountAsync()).Should().Be(0);
    }

    // ── PUT — retroactive happy path ─────────────────────────────────────────

    [Fact]
    public async Task Update_WithRetroactive_MigratesMatchingWalletsAndWritesHistory()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-1", "US"),
            BuildMember("AMB-2", "US"));
        await db.Wallets.AddRangeAsync(
            BuildWallet("W-1", "AMB-1", WalletType.Dwolla),
            BuildWallet("W-2", "AMB-2", WalletType.Dwolla, WalletStatus.Pending));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Update(1, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType         = "Paypal",
            IsActive           = true,
            ApplyRetroactively = true
        }, CancellationToken.None);

        // Both wallets migrated to Paypal.
        var wallets = await db.Wallets.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
        wallets.Should().AllSatisfy(w => w.WalletType.Should().Be(WalletType.Paypal));

        // History rows written, both pointing to the same admin.
        var history = await db.WalletHistories.AsNoTracking().ToListAsync();
        history.Should().HaveCount(2);
        history.Should().AllSatisfy(h =>
        {
            h.Action.Should().Be(WalletHistoryAction.WalletTypeChanged);
            h.WalletType.Should().Be(WalletType.Paypal);
            h.ChangeReason.Should().Contain("Dwolla → Paypal").And.Contain("test-admin");
            h.CreatedBy.Should().Be("test-admin");
        });

        // Response carries the migration count.
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResp = ok.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        apiResp.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithRetroactive_LeavesRejectedWalletsAlone()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-OK",  "US"),
            BuildMember("AMB-REJ", "US"));
        await db.Wallets.AddRangeAsync(
            BuildWallet("W-OK",  "AMB-OK",  WalletType.Dwolla, WalletStatus.Approved),
            BuildWallet("W-REJ", "AMB-REJ", WalletType.Dwolla, WalletStatus.Rejected));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        await controller.Update(1, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType         = "Paypal",
            ApplyRetroactively = true
        }, CancellationToken.None);

        (await db.Wallets.AsNoTracking().FirstAsync(w => w.Id == "W-OK"))
            .WalletType.Should().Be(WalletType.Paypal);
        (await db.Wallets.AsNoTracking().FirstAsync(w => w.Id == "W-REJ"))
            .WalletType.Should().Be(WalletType.Dwolla);

        (await db.WalletHistories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Update_WithRetroactive_LeavesNonMatchingWalletTypeAlone()
    {
        // Member manually picked Crypto — should NOT be migrated even though
        // they live in the country whose default just changed.
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-DEFAULT", "US"),
            BuildMember("AMB-MANUAL",  "US"));
        await db.Wallets.AddRangeAsync(
            BuildWallet("W-DEFAULT", "AMB-DEFAULT", WalletType.Dwolla),
            BuildWallet("W-MANUAL",  "AMB-MANUAL",  WalletType.Crypto));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        await controller.Update(1, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType         = "Paypal",
            ApplyRetroactively = true
        }, CancellationToken.None);

        (await db.Wallets.AsNoTracking().FirstAsync(w => w.Id == "W-DEFAULT"))
            .WalletType.Should().Be(WalletType.Paypal);
        (await db.Wallets.AsNoTracking().FirstAsync(w => w.Id == "W-MANUAL"))
            .WalletType.Should().Be(WalletType.Crypto); // untouched
    }

    [Fact]
    public async Task Update_WithRetroactive_LeavesWalletsInOtherCountriesAlone()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-US", "US"),
            BuildMember("AMB-MX", "MX"));
        await db.Wallets.AddRangeAsync(
            BuildWallet("W-US", "AMB-US", WalletType.Dwolla),
            BuildWallet("W-MX", "AMB-MX", WalletType.Dwolla));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        await controller.Update(1, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType         = "Paypal",
            ApplyRetroactively = true
        }, CancellationToken.None);

        (await db.Wallets.AsNoTracking().FirstAsync(w => w.Id == "W-US"))
            .WalletType.Should().Be(WalletType.Paypal);
        (await db.Wallets.AsNoTracking().FirstAsync(w => w.Id == "W-MX"))
            .WalletType.Should().Be(WalletType.Dwolla); // different country, untouched
    }

    [Fact]
    public async Task Update_WithRetroactive_SameWalletType_IsNoOp()
    {
        // Admin "saves" without actually changing the gateway. Even with
        // ApplyRetroactively=true nothing should happen because there is no
        // delta to propagate.
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-1", "US"));
        await db.Wallets.AddAsync(BuildWallet("W-1", "AMB-1", WalletType.Dwolla));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        await controller.Update(1, new AdminPayoutDefaultsController.UpsertCountryPayoutDefaultRequest
        {
            WalletType         = "Dwolla",
            ApplyRetroactively = true
        }, CancellationToken.None);

        (await db.WalletHistories.CountAsync()).Should().Be(0);
    }

    // ── GET — retroactive preview ────────────────────────────────────────────

    [Fact]
    public async Task RetroactivePreview_ReturnsAffectedCountForCandidateNewType()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-1", "US"),
            BuildMember("AMB-2", "US"),
            BuildMember("AMB-3", "US"));
        await db.Wallets.AddRangeAsync(
            BuildWallet("W-1", "AMB-1", WalletType.Dwolla),
            BuildWallet("W-2", "AMB-2", WalletType.Dwolla, WalletStatus.Rejected), // excluded
            BuildWallet("W-3", "AMB-3", WalletType.Crypto));                       // excluded
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.RetroactivePreview(1, "Paypal", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ApiResponse<AdminPayoutDefaultsController.RetroactivePreviewDto>>()
            .Subject.Data!;
        dto.OldWalletType.Should().Be("Dwolla");
        dto.NewWalletType.Should().Be("Paypal");
        dto.AffectedWalletCount.Should().Be(1);
    }

    [Fact]
    public async Task RetroactivePreview_WhenSameTypeAsCurrent_ReturnsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CountryPayoutDefaults.AddAsync(BuildDefault("US", WalletType.Dwolla));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-1", "US"));
        await db.Wallets.AddAsync(BuildWallet("W-1", "AMB-1", WalletType.Dwolla));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.RetroactivePreview(1, "Dwolla", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ApiResponse<AdminPayoutDefaultsController.RetroactivePreviewDto>>()
            .Subject.Data!;
        dto.AffectedWalletCount.Should().Be(0);
    }

    [Fact]
    public async Task RetroactivePreview_WhenIdNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db);

        var result = await controller.RetroactivePreview(99, "Paypal", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
