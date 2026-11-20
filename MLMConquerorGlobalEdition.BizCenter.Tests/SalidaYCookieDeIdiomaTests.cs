using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>La salida por sesión caducada no sale del portal y la cookie del idioma va protegida.</summary>
public class SalidaYCookieDeIdiomaTests
{
    [Theory]
    [InlineData("/login", true)]
    [InlineData("/", true)]
    [InlineData("/account/login?x=1", true)]
    [InlineData("//evil.example/login", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("https://evil.example/login", false)]
    [InlineData("login", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Solo_es_local_una_ruta_del_mismo_sitio(string? ruta, bool esperado) =>
        SessionExpiry.EsRutaLocal(ruta).Should().Be(esperado);

    [Fact]
    public async Task Con_una_pantalla_de_login_externa_manda_a_la_raiz()
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = ServiciosConAutenticacion();

        await SessionExpiry.SignOutAndRedirectAsync(ctx, "https://evil.example/login");

        ctx.Response.Headers.Location.ToString().Should().StartWith("/?error=");
    }

    [Fact]
    public void La_cookie_del_idioma_es_segura_y_solo_del_servidor()
    {
        var o = CookieDeIdioma.Opciones();
        o.Secure.Should().BeTrue();
        o.HttpOnly.Should().BeTrue();
        o.SameSite.Should().Be(SameSiteMode.Lax);
        o.Path.Should().Be("/");
    }

    private static IServiceProvider ServiciosConAutenticacion()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(servicios);
        Microsoft.Extensions.DependencyInjection.CookieExtensions.AddCookie(
            Microsoft.Extensions.DependencyInjection.AuthenticationServiceCollectionExtensions
                .AddAuthentication(servicios, "Cookies"),
            "Cookies");
        return Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
            .BuildServiceProvider(servicios);
    }
}
