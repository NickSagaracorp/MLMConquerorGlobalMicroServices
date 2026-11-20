using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Signups.Middleware;
using Xunit;

namespace MLMConquerorGlobalEdition.Signups.Tests.Middleware;

/// <summary>
/// El navegador del alta pide /appsettings.json y de ahí saca la dirección de SignupAPI. La sirve
/// el servidor desde su configuración para que la misma imagen valga en cualquier entorno.
/// </summary>
public class ConfiguracionDelClienteTests
{
    [Fact]
    public async Task ConDireccionPublica_ElNavegadorRecibeLaPublicaYNoLaInterna()
    {
        var (ctx, siguio) = await Pedir("/appsettings.json", new()
        {
            ["SignupApiBaseUrl"]   = "http://signupapi.mlmconqueror.svc.cluster.local",
            ["SignupApiPublicUrl"] = "https://auth-api.mlmconqueror.com",
        });

        siguio.Should().BeFalse();
        ctx.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
        Leer(ctx).Should().Be("https://auth-api.mlmconqueror.com");
    }

    [Fact]
    public async Task SinDireccionPublica_UsaLaDelServidor_ComoEnDesarrollo()
    {
        var (ctx, _) = await Pedir("/appsettings.json", new() { ["SignupApiBaseUrl"] = "https://localhost:7005" });

        Leer(ctx).Should().Be("https://localhost:7005");
    }

    [Fact]
    public async Task OtrasRutas_PasanDeLargo()
    {
        var (_, siguio) = await Pedir("/ambassador-join", new() { ["SignupApiPublicUrl"] = "https://x" });

        siguio.Should().BeTrue();
    }

    private static async Task<(HttpContext, bool)> Pedir(string ruta, Dictionary<string, string?> valores)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var siguio = false;
        var middleware = new ClientConfigurationMiddleware(_ => { siguio = true; return Task.CompletedTask; }, config);

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = ruta;
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        return (ctx, siguio);
    }

    private static string? Leer(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        using var doc = JsonDocument.Parse(ctx.Response.Body);
        return doc.RootElement.GetProperty("SignupApiBaseUrl").GetString();
    }
}
