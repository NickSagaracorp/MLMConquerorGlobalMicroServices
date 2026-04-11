using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Countries;

public class CountriesControllerTests
{
    private static Mock<ICacheService> CacheMock()
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static CountriesController CreateController(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        Mock<ICacheService> cache)
    {
        var controller = new CountriesController(db, cache.Object);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "test-admin") },
            "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    private static Country BuildCountry(string iso2, string iso3, string nameEn, bool isActive = false) => new()
    {
        Iso2 = iso2,
        Iso3 = iso3,
        NameEn = nameEn,
        NameNative = nameEn,
        DefaultLanguageCode = "en",
        FlagEmoji = "🏳",
        PhoneCode = "+1",
        IsActive = isActive,
        SortOrder = 0,
        CreatedBy = "seed",
        CreationDate = DateTime.UtcNow
    };

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithNoFilter_ReturnsPaged()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Countries.AddRangeAsync(
            BuildCountry("US", "USA", "United States"),
            BuildCountry("CA", "CAN", "Canada"),
            BuildCountry("MX", "MEX", "Mexico"));
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.GetAll(null, new PagedRequest { Page = 1, PageSize = 20 }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<PagedResult<CountriesController.CountryDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.TotalCount.Should().Be(3);
        response.Data.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAll_FilteredByIsActive_ReturnsOnlyActive()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Countries.AddRangeAsync(
            BuildCountry("US", "USA", "United States", isActive: true),
            BuildCountry("CA", "CAN", "Canada", isActive: true),
            BuildCountry("MX", "MEX", "Mexico", isActive: false));
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.GetAll(isActive: true, new PagedRequest { Page = 1, PageSize = 20 }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<PagedResult<CountriesController.CountryDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.TotalCount.Should().Be(2);
        response.Data.Items.Should().OnlyContain(c => c.IsActive);
    }

    // ── GetById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db, CacheMock());

        var result = await controller.GetById(999, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsCountryDto()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("US", "USA", "United States", isActive: true);
        await db.Countries.AddAsync(country);
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.GetById(country.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<CountriesController.CountryDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Iso2.Should().Be("US");
        response.Data.Iso3.Should().Be("USA");
        response.Data.NameEn.Should().Be("United States");
    }

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WhenDuplicateIso2_ReturnsConflict()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Countries.AddAsync(BuildCountry("US", "USA", "United States"));
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var dto = new CountriesController.CountryFormDto("US", "US2", "Other", "Other", "en", "🏳", "+1", false, 0);

        var result = await controller.Create(dto, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Create_WhenValid_CreatesCountry()
    {
        await using var db = InMemoryDbHelper.Create();
        var cache = CacheMock();
        var controller = CreateController(db, cache);
        var dto = new CountriesController.CountryFormDto("GB", "GBR", "United Kingdom", "United Kingdom", "en", "🇬🇧", "+44", true, 1);

        var result = await controller.Create(dto, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        db.Countries.Should().ContainSingle(c => c.Iso2 == "GB");
        cache.Verify(c => c.RemoveAsync("countries:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db, CacheMock());
        var dto = new CountriesController.CountryFormDto("ZZ", "ZZZ", "Nowhere", "Nowhere", "en", "🏳", "+0", false, 0);

        var result = await controller.Update(999, dto, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_WhenDuplicateIso2OnOtherCountry_ReturnsConflict()
    {
        await using var db = InMemoryDbHelper.Create();
        var country1 = BuildCountry("US", "USA", "United States");
        var country2 = BuildCountry("CA", "CAN", "Canada");
        await db.Countries.AddRangeAsync(country1, country2);
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        // Try to update country2 using country1's ISO2
        var dto = new CountriesController.CountryFormDto("US", "CAN", "Canada", "Canada", "en", "🏳", "+1", false, 0);

        var result = await controller.Update(country2.Id, dto, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Update_WhenValid_UpdatesAllFields()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("CA", "CAN", "Canada");
        await db.Countries.AddAsync(country);
        await db.SaveChangesAsync();

        var cache = CacheMock();
        var controller = CreateController(db, cache);
        var dto = new CountriesController.CountryFormDto("CA", "CAN", "Canada Updated", "Canada Natif", "fr", "🇨🇦", "+1", true, 5);

        var result = await controller.Update(country.Id, dto, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<CountriesController.CountryDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.NameEn.Should().Be("Canada Updated");
        response.Data.NameNative.Should().Be("Canada Natif");
        response.Data.DefaultLanguageCode.Should().Be("fr");
        response.Data.IsActive.Should().BeTrue();
        response.Data.SortOrder.Should().Be(5);
        cache.Verify(c => c.RemoveAsync("countries:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ToggleActive ──────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleActive_WhenFound_TogglesIsActive()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("JP", "JPN", "Japan", isActive: false);
        await db.Countries.AddAsync(country);
        await db.SaveChangesAsync();

        var cache = CacheMock();
        var controller = CreateController(db, cache);

        var result = await controller.ToggleActive(country.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        result.Should().BeOfType<OkObjectResult>();
        db.Countries.First(c => c.Id == country.Id).IsActive.Should().BeTrue();
        cache.Verify(c => c.RemoveAsync("countries:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db, CacheMock());

        var result = await controller.ToggleActive(999, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
