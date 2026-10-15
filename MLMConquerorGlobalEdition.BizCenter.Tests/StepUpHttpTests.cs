using System.Net;
using System.Text;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class StepUpHttpTests
{
    private static HttpResponseMessage Resp(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Un_403_STEP_UP_REQUIRED_pide_codigo()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "{\"success\":false,\"errorCode\":\"STEP_UP_REQUIRED\"}");
        (await StepUpHttp.IsStepUpRequiredAsync(resp)).Should().BeTrue();
    }

    [Fact]
    public async Task Un_403_de_otra_cosa_no_pide_codigo()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "{\"success\":false,\"errorCode\":\"FORBIDDEN\"}");
        (await StepUpHttp.IsStepUpRequiredAsync(resp)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_cuerpo_que_no_es_json_no_revienta()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "<html>");
        (await StepUpHttp.IsStepUpRequiredAsync(resp)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_STEP_UP_REQUIRED_con_otro_estado_no_pide_codigo()
    {
        var resp = Resp(HttpStatusCode.BadRequest, "{\"errorCode\":\"STEP_UP_REQUIRED\"}");
        (await StepUpHttp.IsStepUpRequiredAsync(resp)).Should().BeFalse();
    }

    [Fact]
    public void El_reintento_lleva_el_permiso_en_la_cabecera()
    {
        var req = StepUpHttp.WithGrant(new HttpRequestMessage(HttpMethod.Put, "x"), "permiso");
        req.Headers.GetValues("X-Step-Up-Token").Should().ContainSingle().Which.Should().Be("permiso");
    }

    [Fact]
    public async Task Un_403_STEP_UP_INVALID_se_reconoce_como_permiso_rechazado()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "{\"errorCode\":\"STEP_UP_INVALID\"}");
        (await StepUpHttp.IsGrantRejectedAsync(resp)).Should().BeTrue();
    }

    [Fact]
    public async Task Leer_el_codigo_deja_el_cuerpo_disponible_para_quien_llama()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "{\"errorCode\":\"FORBIDDEN\"}");
        await StepUpHttp.IsStepUpRequiredAsync(resp);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("FORBIDDEN");
    }

    [Fact]
    public async Task Con_step_up_pide_el_permiso_y_reintenta_una_vez_con_la_cabecera()
    {
        var llamadas = new List<string?>();
        using var http = new HttpClient(new Manejador(req =>
        {
            llamadas.Add(req.Headers.TryGetValues("X-Step-Up-Token", out var v) ? v.Single() : null);
            return llamadas.Count == 1
                ? Resp(HttpStatusCode.Forbidden, "{\"errorCode\":\"STEP_UP_REQUIRED\"}")
                : new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("https://x/") };

        var resp = await StepUpHttp.SendAsync(http, () => new HttpRequestMessage(HttpMethod.Put, "w"),
            _ => Task.FromResult<string?>("permiso"));

        resp!.StatusCode.Should().Be(HttpStatusCode.OK);
        llamadas.Should().Equal(null, "permiso");
    }

    [Fact]
    public async Task Si_la_persona_cancela_no_se_reintenta_y_devuelve_null()
    {
        var n = 0;
        using var http = new HttpClient(new Manejador(_ =>
        { n++; return Resp(HttpStatusCode.Forbidden, "{\"errorCode\":\"STEP_UP_REQUIRED\"}"); }))
        { BaseAddress = new Uri("https://x/") };

        var resp = await StepUpHttp.SendAsync(http, () => new HttpRequestMessage(HttpMethod.Put, "w"),
            _ => Task.FromResult<string?>(null));

        resp.Should().BeNull();
        n.Should().Be(1);
    }

    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            Task.FromResult(f(r));
    }
}
