using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Server.Services;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

/// <summary>
/// Sin filtros, el total del listado de miembros sale del panel precalculado en vez de contar un millón de filas en
/// cada carga; en cuanto hay algo que acota el listado, o el admin pide "Refresh", se cuenta de verdad.
/// </summary>
public class TotalDelListadoDeMiembrosTests
{
    private const int TotalDelPanel = 1_000_003;

    private static MemberProfile Miembro(string id, MemberAccountStatus estado) => new()
    {
        MemberId = id, FirstName = "Ana", LastName = "Garcia", Country = "US", Status = estado,
        MemberType = MemberType.Ambassador, EnrollDate = new DateTime(2026, 11, 1),
        CreationDate = new DateTime(2026, 11, 1), CreatedBy = "seed"
    };

    private static async Task<(GetMembersHandler Handler, IAsyncDisposable Db)> ConPanelPrecalculado()
    {
        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.AddRange(
            Miembro("AMB-001", MemberAccountStatus.Active),
            Miembro("AMB-002", MemberAccountStatus.Inactive));
        await db.SaveChangesAsync();

        var cache = new CacheService(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        await cache.SetAsync(CacheKeys.AdminCeoDashboard, new CeoDashboardDto { TotalMembers = TotalDelPanel },
            CacheKeys.AdminCeoDashboardTtl);

        return (new GetMembersHandler(db, cache), db);
    }

    private static GetMembersQuery Pagina(string? estado = null, string? busqueda = null, bool refresh = false) =>
        new(new PagedRequest { Page = 1, PageSize = 50 }, estado, SearchTerm: busqueda, BypassCache: refresh);

    [Fact]
    public async Task Sin_filtros_el_total_sale_del_panel()
    {
        var (handler, db) = await ConPanelPrecalculado();
        await using var _ = db;

        var resultado = await handler.Handle(Pagina(), CancellationToken.None);

        resultado.Value!.TotalCount.Should().Be(TotalDelPanel);
        resultado.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Con_estado_se_cuenta()
    {
        var (handler, db) = await ConPanelPrecalculado();
        await using var _ = db;

        (await handler.Handle(Pagina(estado: "Active"), CancellationToken.None)).Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Con_busqueda_se_cuenta()
    {
        var (handler, db) = await ConPanelPrecalculado();
        await using var _ = db;

        (await handler.Handle(Pagina(busqueda: "AMB-002"), CancellationToken.None)).Value!.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData("AMB-00", 2)]   // el ID empieza por lo escrito
    [InlineData("gar", 2)]      // el apellido, sin mayúsculas
    [InlineData("MB-001", 0)]   // por en medio no: la búsqueda es por prefijo, para que use el índice
    public async Task La_busqueda_libre_es_por_prefijo(string termino, int esperados)
    {
        var (handler, db) = await ConPanelPrecalculado();
        await using var _ = db;

        (await handler.Handle(Pagina(busqueda: termino), CancellationToken.None)).Value!.TotalCount.Should().Be(esperados);
    }

    [Fact]
    public async Task Refresh_cuenta_de_verdad()
    {
        var (handler, db) = await ConPanelPrecalculado();
        await using var _ = db;

        (await handler.Handle(Pagina(refresh: true), CancellationToken.None)).Value!.TotalCount.Should().Be(2);
    }
}
