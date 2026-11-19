using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using Xunit;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Los servicios corren detrás del ALB. La IP y el esquema del cliente se toman de
/// X-Forwarded-For / X-Forwarded-Proto solo si la conexión viene de la red de la VPC, y solo del
/// último salto (el que añade el balanceador): lo que escribe el cliente en la cabecera no cuenta.
/// </summary>
public class DetrasDelBalanceadorTests
{
    private const string Vpc = "10.20.0.0/16";

    [Fact]
    public async Task DesdeElBalanceador_TomaLaIpQueAnadioElAlbYElEsquemaHttps()
    {
        var ctx = await Pasar(Vpc, desde: "10.20.5.6", xff: "6.6.6.6, 203.0.113.9", proto: "https");

        ctx.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.9"));
        ctx.Request.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task DesdeFueraDeLaVpc_LaCabeceraNoCambiaNada()
    {
        var ctx = await Pasar(Vpc, desde: "198.51.100.7", xff: "1.2.3.4", proto: "https");

        ctx.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.7"));
        ctx.Request.Scheme.Should().Be("http");
    }

    [Fact]
    public async Task SinRedConfigurada_SoloSeConfiaEnElPropioEquipo()
    {
        var local = await Pasar(null, desde: "127.0.0.1", xff: "203.0.113.9", proto: "https");
        var red   = await Pasar(null, desde: "10.20.5.6", xff: "203.0.113.9", proto: "https");

        local.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.9"));
        red.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("10.20.5.6"));
    }

    [Fact]
    public void ConCarpetaDeLlaves_OtroProcesoLeeLoQueProtegioElPrimero()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "llaves-" + Guid.NewGuid().ToString("N"));
        try
        {
            var protegido = Protector(carpeta).Protect("sesion");

            // Un segundo proveedor, como otra réplica o el pod tras reiniciar.
            Protector(carpeta).Unprotect(protegido).Should().Be("sesion");
            Directory.GetFiles(carpeta, "key-*.xml").Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void SinCarpetaDeLlaves_NoRegistraNada()
    {
        var servicios = new ServiceCollection();
        servicios.AddPersistentDataProtectionKeys(Config(new()), "MLMConqueror.Prueba");

        servicios.Should().BeEmpty();
    }

    private static IDataProtector Protector(string carpeta)
    {
        var servicios = new ServiceCollection();
        servicios.AddPersistentDataProtectionKeys(
            Config(new() { [ProxyHosting.KeysPathKey] = carpeta }), "MLMConqueror.Prueba");
        return servicios.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("prueba");
    }

    private static async Task<HttpContext> Pasar(string? red, string desde, string xff, string proto)
    {
        var valores = new Dictionary<string, string?>();
        if (red is not null) valores[ProxyHosting.KnownNetworkKey] = red;

        var servicios = new ServiceCollection();
        servicios.AddProxyForwardedHeaders(Config(valores));
        var opciones = servicios.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>();

        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(desde);
        ctx.Request.Scheme = "http";
        ctx.Request.Headers["X-Forwarded-For"]   = xff;
        ctx.Request.Headers["X-Forwarded-Proto"] = proto;

        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, opciones);
        await middleware.Invoke(ctx);
        return ctx;
    }

    private static IConfiguration Config(Dictionary<string, string?> valores)
        => new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
}
