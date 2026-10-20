using System.Net;
using System.Text;
using MLMConquerorGlobalEdition.SharedComponents.Components.Grid;
using MLMConquerorGlobalEdition.SharedComponents.Components.Team;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Un 400 del servidor en una rejilla (p. ej. el rango de Actividad) llega a la pantalla con su
/// código, en vez de dejar la rejilla vacía sin explicación.
/// </summary>
public class ErroresDeRejillaTests
{
    private sealed class Fila { public string MemberId { get; set; } = string.Empty; }

    private sealed class Manejador(HttpStatusCode estado, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private static ServerGridAdaptor<Fila> Adaptador(HttpStatusCode estado, string json, Func<string?, Task>? alFallar) => new()
    {
        Http          = new HttpClient(new Manejador(estado, json)) { BaseAddress = new Uri("https://api.pruebas/") },
        Endpoint      = "api/v1/bizcenter/team/enrollment/activity/grid",
        OnServerError = alFallar
    };

    private const string Rechazo = """{"success":false,"errorCode":"REPORT_RANGE_TOO_OLD","errors":["Invalid date range."]}""";

    [Fact]
    public async Task Con_OnServerError_un_400_entrega_el_codigo_y_devuelve_vacio()
    {
        string? recibido = null;
        var adaptador = Adaptador(HttpStatusCode.BadRequest, Rechazo, c => { recibido = c; return Task.CompletedTask; });

        var r = await adaptador.ReadAsync(new DataManagerRequest { RequiresCounts = true });

        recibido.Should().Be("REPORT_RANGE_TOO_OLD");
        ((DataResult)r).Count.Should().Be(0);
    }

    [Fact]
    public async Task Sin_OnServerError_sigue_fallando_como_antes()
    {
        var adaptador = Adaptador(HttpStatusCode.BadRequest, Rechazo, null);

        var act = () => adaptador.ReadAsync(new DataManagerRequest { RequiresCounts = true });

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>Plan Q (DQ7): el adaptador avisa al anfitrion si el servidor limito orden y busqueda por el tamano.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnLimited_recibe_si_el_servidor_limito_la_rejilla(bool limitado)
    {
        var json = """{"success":true,"data":{"items":[],"totalCount":0,"page":1,"pageSize":20,"limited":LIMITADO}}"""
            .Replace("LIMITADO", limitado ? "true" : "false");
        bool? recibido = null;
        var adaptador = new ServerGridAdaptor<Fila>
        {
            Http      = new HttpClient(new Manejador(HttpStatusCode.OK, json)) { BaseAddress = new Uri("https://api.pruebas/") },
            Endpoint  = "api/v1/bizcenter/team/enrollment/grid",
            OnLimited = l => { recibido = l; return Task.CompletedTask; }
        };

        await adaptador.ReadAsync(new DataManagerRequest { RequiresCounts = true });

        recibido.Should().Be(limitado);
    }

    [Theory]
    [InlineData("REPORT_RANGE_INVALID",   "Reports.RangeInvalid")]
    [InlineData("REPORT_RANGE_TOO_OLD",   "Reports.RangeTooOld")]
    [InlineData("REPORT_RANGE_IN_FUTURE", "Reports.RangeInFuture")]
    [InlineData("REPORT_RANGE_TOO_LONG",  "Reports.RangeTooLong")]
    [InlineData("OTRA_COSA",              "Common.Error")]
    [InlineData(null,                     "Common.Error")]
    public void Cada_codigo_de_rango_tiene_su_texto(string? codigo, string clave)
        => ReportRangeCheck.FromServerCode(codigo, new DateTime(2026, 7, 16), 366).Key.Should().Be(clave);
}
