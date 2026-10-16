using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Middleware;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Middleware;

/// <summary>
/// Toda respuesta de error de SignupAPI lleva el traceId de la petición, para cruzarla con el log y con
/// ErrorLogs: las que arma un controlador con ApiResponse.Fail(código, texto) sin pasarlo, y los 500.
/// </summary>
public class TraceIdEnLasRespuestasTests
{
    private static async Task<ResultExecutingContext> EjecutarAsync(object valor, string traceId)
    {
        var http = new DefaultHttpContext { TraceIdentifier = traceId };
        var accion = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var contexto = new ResultExecutingContext(accion, new List<IFilterMetadata>(), new BadRequestObjectResult(valor), new object());
        await new ApiResponseTraceIdFilter().OnResultExecutionAsync(contexto,
            () => Task.FromResult(new ResultExecutedContext(accion, new List<IFilterMetadata>(), contexto.Result, new object())));
        return contexto;
    }

    [Fact]
    public async Task Un_error_armado_en_un_controlador_sin_traceId_lo_recibe()
    {
        var r = ApiResponse<string>.Fail("ENTRAX_NOT_AVAILABLE", "x");

        await EjecutarAsync(r, "0HN-traza-1");

        r.TraceId.Should().Be("0HN-traza-1");
    }

    [Fact]
    public async Task Un_traceId_ya_puesto_no_se_pisa()
    {
        var r = ApiResponse<string>.Fail("X", "x", "otro");

        await EjecutarAsync(r, "0HN-traza-1");

        r.TraceId.Should().Be("otro");
    }

    [Fact]
    public async Task Un_500_lleva_el_traceId_de_la_peticion()
    {
        var errores = new Mock<IErrorTrackingService>();
        var http = new DefaultHttpContext { TraceIdentifier = "0HN-traza-500" };
        http.Response.Body = new MemoryStream();
        var middleware = new DomainExceptionMiddleware(_ => throw new InvalidOperationException("detalle interno"),
            NullLogger<DomainExceptionMiddleware>.Instance, Mock.Of<IHostEnvironment>());

        await middleware.InvokeAsync(http, errores.Object);

        http.Response.StatusCode.Should().Be(500);
        http.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(http.Response.Body);
        doc.RootElement.GetProperty("traceId").GetString().Should().Be("0HN-traza-500");
    }
}
