using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Commissions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class DailyResidualHistoryTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(f(r));
    }

    private const string Mayo = """
        {"success":true,"data":{"items":[
          {"id":2,"earnedDate":"2025-05-04T00:00:00","amount":5,"status":"Pending","paymentDate":null,"eligiblePoints":null,"paymentComment":null},
          {"id":1,"earnedDate":"2025-05-03T00:00:00","amount":10,"status":"Paid","paymentDate":"2025-05-12T00:00:00","eligiblePoints":120,"paymentComment":"Consolidated"}],
          "totalCount":2,"page":1,"pageSize":10,"year":2025,"month":null,
          "periodTotal":15,"periodPaid":10,"periodPending":5,"availableYears":[2025,2024]}}
        """;

    private readonly List<Uri> _peticiones = new();

    private IRenderedComponent<DailyResidualHistory> Montar(string json, HttpStatusCode estado = HttpStatusCode.OK)
    {
        Services.AddSingleton(new HttpClient(new Manejador(r =>
        {
            _peticiones.Add(r.RequestUri!);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        return Render<DailyResidualHistory>(p => p.Add(c => c.ApiBaseUrl, "api/v1/bizcenter"));
    }

    [Fact]
    public void Pinta_filas_y_totales_del_periodo()
    {
        var c = Montar(Mayo);

        c.WaitForAssertion(() => c.FindAll(".dr-row").Should().HaveCount(2));
        c.Find(".dr-total").TextContent.Should().Be("$15.00");
        c.Find(".dr-paid").TextContent.Should().Be("$10.00");
        c.Find(".dr-pending").TextContent.Should().Be("$5.00");
        c.Markup.Should().Contain("Consolidated").And.Contain("120");
    }

    [Fact]
    public void Pide_el_endpoint_diario_y_no_el_de_comisiones()
    {
        var c = Montar(Mayo);

        c.WaitForAssertion(() => _peticiones.Should().NotBeEmpty());
        _peticiones[0].AbsolutePath.Should().Be("/api/v1/bizcenter/commissions/dual-residual/daily");
    }

    [Fact]
    public void Los_anios_son_los_que_da_el_servidor_y_cambiar_de_mes_vuelve_a_pedir()
    {
        var c = Montar(Mayo);
        c.WaitForAssertion(() => c.FindAll("#dr-year option").Select(o => o.TextContent).Should().Equal("2025", "2024"));

        c.Find("#dr-month").Change("5");

        c.WaitForAssertion(() => _peticiones.Last().Query.Should().Contain("year=2025").And.Contain("month=5"));
    }

    [Fact]
    public void Un_error_de_la_api_se_ve()
    {
        var c = Montar("""{"success":false}""", HttpStatusCode.InternalServerError);

        c.WaitForAssertion(() => c.Find(".daily-residuals-error").TextContent.Should().Contain("Team.LoadFailedResiduals"));
    }
}
