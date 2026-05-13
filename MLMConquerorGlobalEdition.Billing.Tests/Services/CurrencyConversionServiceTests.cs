using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class CurrencyConversionServiceTests
{
    private const decimal EurRate = 0.92m;

    private static CurrencyConversionService CreateService(
        AppDbContext db,
        IDistributedCache? cache = null,
        IHttpClientFactory? httpFactory = null,
        IDateTimeProvider? dateTime = null)
    {
        cache ??= new Mock<IDistributedCache>().Object;
        dateTime ??= Mock.Of<IDateTimeProvider>(d => d.Now == DateTime.UtcNow);
        httpFactory ??= new Mock<IHttpClientFactory>().Object;
        var logger = new Mock<ILogger<CurrencyConversionService>>().Object;
        return new CurrencyConversionService(db, cache, httpFactory, dateTime, logger);
    }

    // ── USD passthrough ────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertAsync_WhenTargetIsUsd_ReturnsOriginalAmount()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var result = await svc.ConvertAsync(100m, "USD", 2m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(100m);
    }

    // ── Cache hit: return cached rate without calling API ─────────────────

    [Fact]
    public async Task GetRateAsync_WhenCacheHit_ReturnsCachedRate()
    {
        using var db = TestDbContextFactory.Create();
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(c => c.GetAsync("exchange:USD:EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(EurRate.ToString("G")));

        var httpMock = new Mock<IHttpClientFactory>();
        var svc = CreateService(db, cacheMock.Object, httpMock.Object);

        var result = await svc.GetRateAsync("EUR");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(EurRate);
        // HTTP factory should never be called
        httpMock.Verify(h => h.CreateClient(It.IsAny<string>()), Times.Never);
    }

    // ── DB snapshot fallback when API fails ───────────────────────────────

    [Fact]
    public async Task GetRateAsync_WhenApiFailsAndSnapshotExists_ReturnsSnapshotRate()
    {
        using var db = TestDbContextFactory.Create();

        // Seeded DB snapshot
        db.ExchangeRateSnapshots.Add(new ExchangeRateSnapshot
        {
            BaseCurrency  = "USD",
            QuoteCurrency = "EUR",
            Rate          = EurRate,
            FetchedAtUtc  = DateTime.UtcNow.AddHours(-1),
            ExpiresAtUtc  = DateTime.UtcNow.AddHours(1),
            CreationDate  = DateTime.UtcNow,
            CreatedBy     = "seed"
        });
        await db.SaveChangesAsync();

        // Cache miss
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // HTTP factory returns a failing client
        var httpMock = new Mock<IHttpClientFactory>();
        var badClient = new HttpClient(new FailingHttpMessageHandler());
        httpMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(badClient);

        var svc = CreateService(db, cacheMock.Object, httpMock.Object);

        var result = await svc.GetRateAsync("EUR");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(EurRate);
    }

    // ── No snapshot + API fails → failure ────────────────────────────────

    [Fact]
    public async Task GetRateAsync_WhenNeitherCacheNorApiNorSnapshot_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();

        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var httpMock = new Mock<IHttpClientFactory>();
        var badClient = new HttpClient(new FailingHttpMessageHandler());
        httpMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(badClient);

        var svc = CreateService(db, cacheMock.Object, httpMock.Object);

        var result = await svc.GetRateAsync("EUR");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EXCHANGE_RATE_UNAVAILABLE");
    }

    // ── Markup is applied ─────────────────────────────────────────────────

    [Fact]
    public async Task ConvertAsync_WithMarkup_AppliesMarkupCorrectly()
    {
        using var db = TestDbContextFactory.Create();
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(c => c.GetAsync("exchange:USD:EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("0.92"));

        var svc = CreateService(db, cacheMock.Object);

        // 100 USD × 0.92 rate × 1.02 markup = 93.84
        var result = await svc.ConvertAsync(100m, "EUR", 2m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(93.84m);
    }

    // ── Zero markup returns exact converted amount ─────────────────────────

    [Fact]
    public async Task ConvertAsync_WithZeroMarkup_ReturnsExactConvertedAmount()
    {
        using var db = TestDbContextFactory.Create();
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(c => c.GetAsync("exchange:USD:EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("0.92"));

        var svc = CreateService(db, cacheMock.Object);

        var result = await svc.ConvertAsync(100m, "EUR", 0m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(92.00m);
    }

    // ── Helper: always-failing HTTP handler ───────────────────────────────
    private class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated API failure");
    }
}
