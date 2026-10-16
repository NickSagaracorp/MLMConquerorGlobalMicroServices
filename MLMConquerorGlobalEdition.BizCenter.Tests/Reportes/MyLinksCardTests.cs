using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Profile;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class MyLinksCardTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(f(r));
    }

    private const string ConSlug = """{"success":true,"data":{"slugMissing":false,"personal":[{"key":"mwrlife","label":"MWR Life","url":"https://mwrlife.com/ana"}],"corporate":[{"key":"academy","label":"MWR Academy","url":"https://mwracademy.com"}]}}""";
    private const string SinSlug = """{"success":true,"data":{"slugMissing":true,"personal":[],"corporate":[{"key":"academy","label":"MWR Academy","url":"https://mwracademy.com"}]}}""";

    private readonly List<string> _rutas = new();

    private IRenderedComponent<MyLinksCard> Montar(string json, HttpStatusCode estado = HttpStatusCode.OK,
        string api = "api/v1/bizcenter", TimeSpan? aviso = null)
    {
        Services.AddSingleton(new HttpClient(new Manejador(r =>
        {
            _rutas.Add(r.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<MyLinksCard>(p => p
            .Add(c => c.ApiBaseUrl, api)
            .Add(c => c.CopiedFeedback, aviso ?? TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Pinta_los_enlaces_personales_y_los_corporativos()
    {
        var tarjeta = Montar(ConSlug);

        tarjeta.WaitForAssertion(() => tarjeta.FindAll(".my-link").Should().HaveCount(2));
        tarjeta.Markup.Should().Contain("https://mwrlife.com/ana").And.Contain("https://mwracademy.com");
        tarjeta.FindAll(".my-links-noslug").Should().BeEmpty();
    }

    [Fact]
    public void Sin_slug_avisa_con_enlace_al_perfil_y_no_pinta_personales()
    {
        var tarjeta = Montar(SinSlug);

        tarjeta.WaitForAssertion(() => tarjeta.Find(".my-links-noslug"));
        tarjeta.Find(".my-links-noslug a").GetAttribute("href").Should().Be("/profile");
        tarjeta.FindAll(".my-link").Should().ContainSingle();
    }

    [Fact]
    public void Copiar_manda_la_url_al_portapapeles_y_lo_dice()
    {
        var tarjeta = Montar(ConSlug);
        tarjeta.WaitForAssertion(() => tarjeta.FindAll(".my-link-copy").Should().HaveCount(2));

        tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy").Click();

        JSInterop.VerifyInvoke("navigator.clipboard.writeText").Arguments[0].Should().Be("https://mwrlife.com/ana");
        tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy").TextContent.Trim().Should().Be("MyLinks.Copied");
    }

    [Fact]
    public void Copiado_vuelve_a_Copiar_al_cabo_de_un_rato()
    {
        var tarjeta = Montar(ConSlug, aviso: TimeSpan.FromMilliseconds(50));
        tarjeta.WaitForAssertion(() => tarjeta.FindAll(".my-link-copy").Should().HaveCount(2));

        tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy").Click();

        tarjeta.WaitForAssertion(() => tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy")
            .TextContent.Trim().Should().Be("MyLinks.Copy"));
    }

    [Fact]
    public void El_aviso_dura_dos_segundos_por_defecto()
        => new MyLinksCard().CopiedFeedback.Should().Be(TimeSpan.FromSeconds(2));

    public static IEnumerable<object[]> FallosDeJs() => new[]
    {
        new object[] { new Microsoft.JSInterop.JSException("sin permiso") },
        new object[] { new Microsoft.JSInterop.JSDisconnectedException("circuito caído") },
        new object[] { new TaskCanceledException("cancelado") },
    };

    [Theory]
    [MemberData(nameof(FallosDeJs))]
    public void Si_el_portapapeles_falla_no_rompe_y_no_dice_copiado(Exception fallo)
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(fallo);
        var tarjeta = Montar(ConSlug);
        tarjeta.WaitForAssertion(() => tarjeta.FindAll(".my-link-copy").Should().HaveCount(2));

        var act = () => tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy").Click();

        act.Should().NotThrow();
        tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy").TextContent.Trim().Should().Be("MyLinks.Copy");
    }

    [Fact]
    public void Un_error_de_la_api_se_ve()
    {
        var tarjeta = Montar("""{"success":false}""", HttpStatusCode.InternalServerError);

        tarjeta.WaitForAssertion(() => tarjeta.Find(".my-links-error").TextContent.Should().Contain("MyLinks.LoadFailed"));
    }

    [Fact]
    public void Usa_la_ruta_base_que_recibe()
    {
        var tarjeta = Montar(ConSlug, api: "api/v1/admin/members/AMB-1");

        tarjeta.WaitForAssertion(() => _rutas.Should().Contain("/api/v1/admin/members/AMB-1/my-links"));
    }
}
