using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MLMConquerorGlobalEdition.SharedKernel.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// <c>TryClaimOnceAsync</c> es la marca de "permiso de step-up ya usado". Lo que importa probar no
/// es solo que marque, sino que NO degrade como el resto de <see cref="CacheService"/>: si el
/// almacén falla tiene que lanzar, porque tragarse el error equivale a decir "no estaba marcado".
/// </summary>
public class CacheServiceClaimOnceTests
{
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(3);

    private static CacheService InMemory() =>
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    [Fact]
    public async Task La_primera_vez_marca_y_la_segunda_no()
    {
        var cache = InMemory();

        (await cache.TryClaimOnceAsync("stepup:jti-1", Expiry)).Should().BeTrue();
        (await cache.TryClaimOnceAsync("stepup:jti-1", Expiry)).Should().BeFalse();
    }

    [Fact]
    public async Task Claves_distintas_no_se_pisan()
    {
        var cache = InMemory();

        (await cache.TryClaimOnceAsync("stepup:jti-1", Expiry)).Should().BeTrue();
        (await cache.TryClaimOnceAsync("stepup:jti-2", Expiry)).Should().BeTrue();
    }

    [Fact]
    public async Task Concurrentes_sobre_la_misma_clave_solo_gana_una()
    {
        var cache = InMemory();

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => cache.TryClaimOnceAsync("stepup:jti-race", Expiry))));

        results.Count(r => r).Should().Be(1);
    }

    [Fact]
    public async Task Si_el_almacen_falla_al_leer_lanza_en_vez_de_degradar()
    {
        var broken = new Mock<IDistributedCache>();
        broken.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("almacén caído"));

        var act = () => new CacheService(broken.Object).TryClaimOnceAsync("stepup:jti-1", Expiry);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Si_el_almacen_falla_al_escribir_lanza_en_vez_de_degradar()
    {
        var broken = new Mock<IDistributedCache>();
        broken.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((byte[]?)null);
        broken.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
                                     It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("almacén caído"));

        var act = () => new CacheService(broken.Object).TryClaimOnceAsync("stepup:jti-1", Expiry);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
