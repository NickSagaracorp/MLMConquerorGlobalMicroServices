using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetCeoDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetGrowthDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetHealthDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMemberStats;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Server.Services;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Dashboards;

/// <summary>
/// Los paneles del admin se recalculan en segundo plano para que nadie espere las agregaciones sobre un millón de
/// miembros al abrir la pantalla.
/// </summary>
public class PrecalculoDePanelesTests
{
    private static readonly DateTime Ahora = new(2026, 11, 24, 15, 40, 0);

    private static ICacheService CacheCompartida() =>
        new CacheService(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    private static PrecalculoDePaneles Precalculo(ICacheService cache, Mock<IMediator> mediator)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);

        var servicios = new ServiceCollection()
            .AddSingleton(cache)
            .AddSingleton(mediator.Object)
            .AddSingleton(reloj.Object)
            .BuildServiceProvider();

        return new PrecalculoDePaneles(
            servicios.GetRequiredService<IServiceScopeFactory>(), NullLogger<PrecalculoDePaneles>.Instance);
    }

    private static List<object> Pedidos(Mock<IMediator> mediator) =>
        mediator.Invocations.Where(i => i.Method.Name == nameof(IMediator.Send)).Select(i => i.Arguments[0]).ToList();

    [Fact]
    public async Task Una_vuelta_recalcula_cada_panel_saltando_la_cache()
    {
        var mediator = new Mock<IMediator>();

        (await Precalculo(CacheCompartida(), mediator).RecalcularAsync(CancellationToken.None)).Should().BeTrue();

        var pedidos = Pedidos(mediator);
        pedidos.Should().ContainSingle(p => p is GetCeoDashboardQuery && ((GetCeoDashboardQuery)p).BypassCache);
        pedidos.Should().ContainSingle(p => p is GetMemberStatsQuery && ((GetMemberStatsQuery)p).BypassCache);
        pedidos.Should().ContainSingle(p => p is GetGrowthDashboardQuery && ((GetGrowthDashboardQuery)p).BypassCache);
        pedidos.Should().ContainSingle(p => p is GetHealthDashboardQuery && ((GetHealthDashboardQuery)p).BypassCache);

        // El financiero con el rango con que lo pide la pantalla al abrir: del día 1 del mes a hoy.
        pedidos.OfType<GetFinancialDashboardQuery>().Should().ContainSingle()
            .Which.Should().Be(new GetFinancialDashboardQuery(new DateTime(2026, 11, 1), Ahora.Date, BypassCache: true));

        // Y la primera página del listado, para que EF la compile antes de que la abra un admin.
        pedidos.OfType<GetMembersQuery>().Should().ContainSingle()
            .Which.Should().Match<GetMembersQuery>(q => q.Page.Page == 1 && q.Page.PageSize == 50 && q.BypassCache
                                                     && q.StatusFilter == null && q.SponsorId == null);
    }

    [Fact]
    public async Task Con_varias_replicas_solo_una_recalcula_cada_vuelta()
    {
        var cache = CacheCompartida();
        var primera = new Mock<IMediator>();
        var segunda = new Mock<IMediator>();

        (await Precalculo(cache, primera).RecalcularAsync(CancellationToken.None)).Should().BeTrue();
        (await Precalculo(cache, segunda).RecalcularAsync(CancellationToken.None)).Should().BeFalse();

        Pedidos(segunda).Should().BeEmpty();
    }

    [Fact]
    public async Task Un_panel_que_falla_no_frena_a_los_demas()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetCeoDashboardQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("agregación demasiado lenta"));

        (await Precalculo(CacheCompartida(), mediator).RecalcularAsync(CancellationToken.None)).Should().BeTrue();

        Pedidos(mediator).Should().HaveCount(6);
    }

    [Fact]
    public async Task Si_no_puede_pedir_el_turno_no_recalcula()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("Redis no responde"));
        var mediator = new Mock<IMediator>();

        (await Precalculo(cache.Object, mediator).RecalcularAsync(CancellationToken.None)).Should().BeFalse();

        Pedidos(mediator).Should().BeEmpty();
    }

    [Fact]
    public async Task El_panel_CEO_dice_cuando_se_calculo()
    {
        await using var db = InMemoryDbHelper.Create();
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);

        var resultado = await new GetCeoDashboardHandler(db, reloj.Object, new NoOpCacheService())
            .Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        resultado.Value!.GeneratedAt.Should().Be(Ahora);
    }
}
