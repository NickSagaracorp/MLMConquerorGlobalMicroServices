using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class EntraxSignupsControllerTests
{
    private sealed record Montaje(
        EntraxSignupsController Controlador, Mock<IEntraxCallbackProcessor> Callbacks,
        Mock<IEntraxSignupStatusService> Estado, Mock<IEntraxSignupSettlement> Liquidacion);

    private static Montaje Crear(byte[]? cuerpo = null, string? firma = null)
    {
        var callbacks   = new Mock<IEntraxCallbackProcessor>();
        var estado      = new Mock<IEntraxSignupStatusService>();
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(cuerpo ?? Array.Empty<byte>());
        if (firma is not null) ctx.Request.Headers["X-Signature"] = firma;

        var c = new EntraxSignupsController(callbacks.Object, estado.Object, liquidacion.Object,
            NullLogger<EntraxSignupsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
        return new Montaje(c, callbacks, estado, liquidacion);
    }

    private static int? Codigo(IActionResult r) => r.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;

    // ── Callback ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EntraxCallbackOutcome.Accepted, 200)]
    [InlineData(EntraxCallbackOutcome.Unauthorized, 401)]
    [InlineData(EntraxCallbackOutcome.Invalid, 400)]
    [InlineData(EntraxCallbackOutcome.Stale, 400)]
    [InlineData(EntraxCallbackOutcome.NotFound, 404)]
    [InlineData(EntraxCallbackOutcome.RetryLater, 503)]
    public async Task El_callback_contesta_segun_lo_que_paso(EntraxCallbackOutcome resultado, int http)
    {
        var m = Crear(Encoding.UTF8.GetBytes("{}"), "firma");
        m.Callbacks.Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(resultado);

        Codigo(await m.Controlador.EntraxCallback(default)).Should().Be(http);
    }

    [Fact]
    public async Task Al_procesador_le_llega_el_cuerpo_crudo_y_la_firma()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":\"x\",  \"status\":\"SUCCESS\"}");
        var m = Crear(cuerpo, "abc=");
        byte[]? recibido = null;
        string? firma = null;
        m.Callbacks.Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .Callback<byte[], string?, CancellationToken>((b, f, _) => { recibido = b; firma = f; })
                   .ReturnsAsync(EntraxCallbackOutcome.Accepted);

        await m.Controlador.EntraxCallback(default);

        recibido.Should().Equal(cuerpo, "la firma es sobre los bytes tal cual llegaron, sin re-serializar.");
        firma.Should().Be("abc=");
    }

    [Fact]
    public async Task Un_cuerpo_demasiado_grande_no_se_procesa()
    {
        var m = Crear(new byte[EntraxSignupsController.MaxCallbackBytes + 1], "f");

        Codigo(await m.Controlador.EntraxCallback(default)).Should().Be(StatusCodes.Status413PayloadTooLarge);
        m.Callbacks.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_error_inesperado_es_500_sin_detalles()
    {
        var m = Crear(Encoding.UTF8.GetBytes("{}"), "f");
        m.Callbacks.Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("detalle interno"));

        var r = await m.Controlador.EntraxCallback(default);

        r.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(500,
            "nunca ex.ToString() en una respuesta; el detalle va al log.");
    }

    // ── Estado y métodos ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Un_estado_que_no_casa_es_404_generico()
    {
        var m = Crear();
        m.Estado.Setup(s => s.GetAsync("ORD", "ref", It.IsAny<CancellationToken>())).ReturnsAsync((EntraxSignupStatusDto?)null);

        var r = await m.Controlador.GetEntraxStatus("ORD", "ref", default);

        r.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().BeOfType<ApiResponse<EntraxSignupStatusDto>>()
         .Which.ErrorCode.Should().Be(EntraxSignupErrors.PaymentNotFound);
    }

    [Fact]
    public async Task Un_estado_conocido_se_devuelve()
    {
        var m = Crear();
        m.Estado.Setup(s => s.GetAsync("ORD", "ref", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EntraxSignupStatusDto { Status = EntraxSignupStatuses.Paid });

        var r = await m.Controlador.GetEntraxStatus("ORD", "ref", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<EntraxSignupStatusDto>>()
         .Which.Data!.Status.Should().Be(EntraxSignupStatuses.Paid);
    }

    [Fact]
    public void Los_metodos_disponibles_salen_del_servicio()
    {
        var m = Crear();
        m.Estado.Setup(s => s.GetPaymentOptions()).Returns(new SignupPaymentOptionsDto { EntraxCountries = ["RU"] });

        m.Controlador.GetPaymentOptions().Should().BeOfType<OkObjectResult>()
         .Which.Value.Should().BeOfType<ApiResponse<SignupPaymentOptionsDto>>()
         .Which.Data!.EntraxCountries.Should().Equal("RU");
    }

    // ── Re-check ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Re_check_liquida_por_el_camino_unico_con_origen_Admin()
    {
        var m = Crear();
        m.Liquidacion.Setup(s => s.SettleAsync("pago-1", EntraxSettlementSource.Admin, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(EntraxSettlementOutcome.Activated);

        var r = await m.Controlador.Recheck("pago-1", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value
         .Should().BeOfType<ApiResponse<EntraxSignupsController.EntraxRecheckResponse>>()
         .Which.Data!.Outcome.Should().Be("Activated");
    }

    [Fact]
    public async Task Re_check_de_un_pago_que_no_existe_es_404()
    {
        var m = Crear();
        m.Liquidacion.Setup(s => s.SettleAsync("x", EntraxSettlementSource.Admin, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(EntraxSettlementOutcome.NotFound);

        (await m.Controlador.Recheck("x", default)).Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Quién puede llamar ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(EntraxSignupsController.EntraxCallback))]
    [InlineData(nameof(EntraxSignupsController.GetEntraxStatus))]
    [InlineData(nameof(EntraxSignupsController.GetPaymentOptions))]
    public void Callback_estado_y_metodos_son_anonimos(string metodo)
        => typeof(EntraxSignupsController).GetMethod(metodo)!.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();

    [Fact]
    public void Re_check_exige_los_mismos_roles_que_confirmar_cripto()
    {
        var m = typeof(EntraxSignupsController).GetMethod(nameof(EntraxSignupsController.Recheck))!;

        m.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers);
        m.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }
}
