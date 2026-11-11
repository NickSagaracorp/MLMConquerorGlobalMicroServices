using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.SharedComponents.Components.Ranks;
using MLMConquerorGlobalEdition.SharedComponents.Constants;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedComponents.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rangos;

/// <summary>
/// Rank Dashboard del BizCenter (/ranks): la pantalla compartida lee exactamente lo que el API devuelve
/// (nombres de rango, puntos elegibles contra el umbral del próximo rango, fecha de logro, rango anterior)
/// y descarga el certificado por el API con el token, que lo genera si todavía no existe.
/// </summary>
public class RankDashboardTests : BunitContext
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 prueba");

    private readonly List<string> _llamadas = new();

    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(f(r));
    }

    private static HttpResponseMessage Json<T>(T datos) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(ApiResponse<T>.Ok(datos), Web), Encoding.UTF8, "application/json")
    };

    private static RankDashboardDto Panel() => new()
    {
        MemberId = "AMB-7",
        CurrentRankName = "Silver", NextRankName = "Gold", LifetimeRankName = "Gold",
        NextRankDualTeamPoints = 1000, NextRankEligibleDualTeamPoints = 400,
        NextRankEnrollmentPoints = 300, NextRankEligibleEnrollmentPoints = 150,
        DualTeamPoints = 450, EnrollmentPoints = 150
    };

    private static PagedResult<RankHistoryDto> Historial() => new()
    {
        Items = new[]
        {
            new RankHistoryDto { Id = "RH-2", RankName = "Silver", PreviousRankName = "Bronze",
                                 AchievedAt = new DateTime(2026, 10, 5), HasCertificate = false },
            new RankHistoryDto { Id = "RH-1", RankName = "Bronze", PreviousRankName = null,
                                 AchievedAt = new DateTime(2026, 8, 20), HasCertificate = true },
        },
        TotalCount = 2, Page = 1, PageSize = 10
    };

    private IRenderedComponent<RankDashboard> Montar(RankDashboardDto panel, string? api = null, bool historial = true)
    {
        Services.AddSharedComponents();
        Services.AddSingleton(new HttpClient(new Manejador(r =>
        {
            var ruta = r.RequestUri!.PathAndQuery;
            _llamadas.Add(ruta);
            if (ruta.EndsWith("/ranks/dashboard")) return Json(panel);
            if (ruta.Contains("/ranks/history")) return Json(Historial());
            if (ruta.EndsWith("/certificate"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Pdf) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        var vista = new Mock<IViewContextService>();
        vista.Setup(v => v.IsInAnyRole(It.IsAny<string[]>())).Returns(true);
        Services.AddSingleton(vista.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<RankDashboard>(p =>
        {
            p.Add(c => c.MemberProfileId, "AMB-7").Add(c => c.ShowHistory, historial);
            if (api is not null) p.Add(c => c.ApiBaseUrl, api);
        });
    }

    [Fact]
    public void Enseña_el_rango_actual_el_proximo_y_el_de_por_vida_que_devuelve_el_API()
    {
        var p = Montar(Panel());

        p.WaitForAssertion(() => p.Markup.Should().Contain(">Silver<"));
        p.Markup.Should().Contain(">Gold<");
        p.FindAll(".card").Select(c => c.TextContent).Should().Contain(t => t.Contains("Ranks.LifetimeRank") && t.Contains("Gold"));
    }

    [Fact]
    public void El_progreso_son_los_puntos_elegibles_contra_el_umbral_del_proximo_rango()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var p = Montar(Panel());

        p.WaitForAssertion(() => p.Markup.Should().Contain("400 / 1,000"));
        p.Markup.Should().Contain("150 / 300");
        p.Markup.Should().Contain("width: 40%").And.Contain("width: 50%");
    }

    [Fact]
    public void En_el_rango_mas_alto_no_hay_barras_y_lo_dice()
    {
        var tope = Panel();
        tope.NextRankName = null;
        tope.NextRankDualTeamPoints = tope.NextRankEnrollmentPoints = 0;

        var p = Montar(tope);

        p.WaitForAssertion(() => p.Markup.Should().Contain("Ranks.TopRankReached"));
        p.FindAll(".progress").Should().BeEmpty();
    }

    [Fact]
    public void El_historial_trae_fecha_rango_anterior_y_siempre_ofrece_descargar()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var p = Montar(Panel());

        p.WaitForAssertion(() => p.FindAll("tbody tr").Should().HaveCount(2));
        var filas = p.FindAll("tbody tr").Select(f => f.TextContent).ToList();
        filas[0].Should().Contain("Oct 05, 2026").And.Contain("Silver").And.Contain("Bronze");
        filas[1].Should().Contain("Aug 20, 2026").And.Contain("Bronze");
        p.Markup.Should().NotContain("0001");
        p.FindAll("tbody button").Should().HaveCount(2, "el API genera el certificado al pedirlo si no existe");
    }

    [Fact]
    public void Descargar_pide_el_certificado_al_API_y_lo_entrega_al_navegador()
    {
        var modulo = JSInterop.SetupModule(RankHistoryTable.DownloadModule);
        modulo.SetupVoid("downloadFile", _ => true);
        var p = Montar(Panel());
        p.WaitForAssertion(() => p.FindAll("tbody button").Should().HaveCount(2));

        p.FindAll("tbody button")[0].Click();

        p.WaitForAssertion(() => _llamadas.Should().Contain("/api/v1/bizcenter/ranks/RH-2/certificate"));
        p.WaitForAssertion(() => modulo.Invocations["downloadFile"].Should().ContainSingle());
        p.FindAll(".alert-danger").Should().BeEmpty();
        var js = modulo.VerifyInvoke("downloadFile");
        js.Arguments[1].Should().Be("application/pdf");
        js.Arguments[2].Should().Be(Convert.ToBase64String(Pdf));
    }

    [Fact]
    public void Con_otra_raiz_lee_el_panel_de_alli_y_sin_historial_no_lo_pide()
    {
        var p = Montar(Panel(), api: "api/v1/admin/members/AMB-7", historial: false);

        p.WaitForAssertion(() => p.Markup.Should().Contain(">Silver<"));
        _llamadas.Should().Contain("/api/v1/admin/members/AMB-7/ranks/dashboard");
        _llamadas.Should().NotContain(l => l.Contains("history"));
    }

    [Fact]
    public void El_admin_devuelve_un_guion_cuando_no_hay_proximo_rango_y_se_trata_como_tope()
    {
        var tope = Panel();
        tope.NextRankName = "—";
        tope.NextRankDualTeamPoints = tope.NextRankEnrollmentPoints = 0;

        var p = Montar(tope);

        p.WaitForAssertion(() => p.Markup.Should().Contain("Ranks.TopRankReached"));
    }

    // -- Textos -----------------------------------------------------------------

    private static readonly string[] Culturas = { "", ".es", ".pt", ".fr", ".it", ".de", ".ko", ".zh", ".ka" };

    private static readonly string[] Claves =
    {
        "Ranks.CurrentRank", "Ranks.NextRank", "Ranks.LifetimeRank", "Ranks.ProgressTitle",
        "Ranks.DualTeamPoints", "Ranks.EnrollmentPoints", "Ranks.TopRankReached", "Ranks.LoadFailed",
        "Ranks.HistoryTitle", "Ranks.ColAchievedDate", "Ranks.ColRank", "Ranks.ColPreviousRank",
        "Ranks.ColCertificate", "Ranks.CertificateDownloadFailed", "Common.Download",
    };

    public static IEnumerable<object[]> Archivos() => Culturas.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Cada_idioma_tiene_los_textos_del_rank_dashboard(string cultura)
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(), "MLMConquerorGlobalEdition.SharedComponents",
            "Resources", $"SharedResources{cultura}.resx");
        using var archivo = File.OpenRead(ruta);
        var datos = XDocument.Load(archivo).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

        Claves.Where(k => !datos.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v))
              .Should().BeEmpty($"SharedResources{cultura}.resx tiene que traerlas");
    }
}
