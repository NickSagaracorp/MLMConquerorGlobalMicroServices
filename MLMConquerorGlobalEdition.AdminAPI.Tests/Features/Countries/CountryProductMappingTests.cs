using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Countries;

public class CountryProductMappingTests
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
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, "test-admin") }, "TestAuth"))
            }
        };
        return controller;
    }

    private static Country BuildCountry(string iso2, string iso3 = "TST") => new()
    {
        Iso2 = iso2, Iso3 = iso3, NameEn = iso2, NameNative = iso2,
        DefaultLanguageCode = "en", FlagEmoji = "🏳", IsActive = true,
        CreatedBy = "seed", CreationDate = DateTime.UtcNow
    };

    private static Product BuildProduct(string id, string name = "Product") => new()
    {
        Id = id, Name = name, Description = "Desc", ImageUrl = "",
        MonthlyFee = 40m, SetupFee = 0m, IsActive = true, IsDeleted = false,
        CreatedBy = "seed", CreationDate = DateTime.UtcNow, LastUpdateDate = DateTime.UtcNow
    };

    // ── GetProducts ──────────────────────────────���────────────────────────

    [Fact]
    public async Task GetProducts_WhenCountryNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db, CacheMock());

        var result = await controller.GetProducts(999, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetProducts_WhenNoMappings_ReturnsEmptyList()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("US", "USA");
        await db.Countries.AddAsync(country);
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.GetProducts(country.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value
            .Should().BeOfType<ApiResponse<IEnumerable<CountriesController.CountryProductDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProducts_WhenMappingsExist_ReturnsAll()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("VE", "VEN");
        var product = BuildProduct("prod-001", "VIP");
        await db.Countries.AddAsync(country);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        await db.CountryProducts.AddAsync(new CountryProduct
        {
            CountryId = country.Id, ProductId = product.Id, IsActive = true,
            CreatedBy = "admin", CreationDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.GetProducts(country.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value
            .Should().BeOfType<ApiResponse<IEnumerable<CountriesController.CountryProductDto>>>().Subject;
        response.Data.Should().HaveCount(1);
        response.Data!.First().ProductId.Should().Be(product.Id);
    }

    // ── SetProducts (bulk replace) ──────────────────────────────────��─────

    [Fact]
    public async Task SetProducts_WhenCountryNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db, CacheMock());
        var request = new CountriesController.CountryProductsBulkRequest(["prod-001"]);

        var result = await controller.SetProducts(999, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetProducts_WhenInvalidProductId_ReturnsBadRequest()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("CO", "COL");
        await db.Countries.AddAsync(country);
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var request = new CountriesController.CountryProductsBulkRequest(["nonexistent-id"]);

        var result = await controller.SetProducts(country.Id, request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetProducts_WhenValid_ReplacesExistingMappings()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("BR", "BRA");
        var prodVip   = BuildProduct("prod-vip",   "VIP");
        var prodElite = BuildProduct("prod-elite", "Elite");
        await db.Countries.AddAsync(country);
        await db.Products.AddRangeAsync(prodVip, prodElite);
        await db.SaveChangesAsync();

        // Seed an existing mapping
        await db.CountryProducts.AddAsync(new CountryProduct
        {
            CountryId = country.Id, ProductId = prodVip.Id, IsActive = true,
            CreatedBy = "admin", CreationDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var cache = CacheMock();
        var controller = CreateController(db, cache);
        // Replace with just Elite
        var request = new CountriesController.CountryProductsBulkRequest([prodElite.Id]);

        var result = await controller.SetProducts(country.Id, request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        db.CountryProducts.Where(cp => cp.CountryId == country.Id).Should().HaveCount(1);
        db.CountryProducts.First(cp => cp.CountryId == country.Id).ProductId.Should().Be(prodElite.Id);
        cache.Verify(c => c.RemoveAsync(It.Is<string>(k => k.Contains("br")), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AddProduct ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddProduct_WhenProductNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("AR", "ARG");
        await db.Countries.AddAsync(country);
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.AddProduct(country.Id, "nonexistent", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddProduct_WhenAlreadyMapped_ReturnsConflict()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("CL", "CHL");
        var product = BuildProduct("prod-vip");
        await db.Countries.AddAsync(country);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        await db.CountryProducts.AddAsync(new CountryProduct
        {
            CountryId = country.Id, ProductId = product.Id, IsActive = true,
            CreatedBy = "admin", CreationDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.AddProduct(country.Id, product.Id, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AddProduct_WhenValid_CreatesMappingAndInvalidatesCache()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("PE", "PER");
        var product = BuildProduct("prod-vip", "VIP");
        await db.Countries.AddAsync(country);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var cache = CacheMock();
        var controller = CreateController(db, cache);

        var result = await controller.AddProduct(country.Id, product.Id, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        db.CountryProducts.Should().ContainSingle(cp => cp.CountryId == country.Id && cp.ProductId == product.Id);
        cache.Verify(c => c.RemoveAsync(It.Is<string>(k => k.Contains("pe")), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── RemoveProduct ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveProduct_WhenMappingNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("EC", "ECU");
        await db.Countries.AddAsync(country);
        await db.SaveChangesAsync();

        var controller = CreateController(db, CacheMock());
        var result = await controller.RemoveProduct(country.Id, "prod-xyz", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RemoveProduct_WhenValid_DeletesMappingAndInvalidatesCache()
    {
        await using var db = InMemoryDbHelper.Create();
        var country = BuildCountry("BO", "BOL");
        var product = BuildProduct("prod-vip");
        await db.Countries.AddAsync(country);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        await db.CountryProducts.AddAsync(new CountryProduct
        {
            CountryId = country.Id, ProductId = product.Id, IsActive = true,
            CreatedBy = "admin", CreationDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var cache = CacheMock();
        var controller = CreateController(db, cache);

        var result = await controller.RemoveProduct(country.Id, product.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        db.CountryProducts.Should().BeEmpty();
        cache.Verify(c => c.RemoveAsync(It.Is<string>(k => k.Contains("bo")), It.IsAny<CancellationToken>()), Times.Once);
    }
}
