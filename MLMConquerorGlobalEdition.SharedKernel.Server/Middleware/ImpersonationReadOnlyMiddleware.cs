using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Middleware;

/// <summary>
/// DONDE SE APLICA DE VERDAD EL "SOLO LECTURA" DE UNA SUPLANTACIÓN: en el servidor, antes de que
/// la ruta llegue a ejecutarse.
/// </summary>
/// <remarks>
/// POR QUÉ MIDDLEWARE Y NO UN ATRIBUTO EN CADA CONTROLADOR. Un atributo protege lo que alguien se
/// acordó de decorar; esto tiene que proteger TODO, incluidas las rutas que aún no existen y los
/// endpoints mínimos que no pasan por un controlador. La restricción no es de un controlador: es
/// del token, y por tanto de la petición entera.
///
/// POR QUÉ NO UNA POLÍTICA DE AUTORIZACIÓN. Una política se evalúa contra los endpoints que la
/// piden —vuelve a depender de acordarse— y no ve el método HTTP sin gimnasia. Aquí el método es
/// justo el dato que decide.
///
/// DÓNDE VA EN LA TUBERÍA: después de <c>UseAuthentication</c>, para que el principal ya esté
/// construido y el claim sea legible, y después de <c>UseAuthorization</c>, para que un 401 o un
/// 403 por rol se contesten antes que este —quien no podía entrar siquiera no necesita enterarse
/// de que además era de solo lectura—. El endpoint ya está resuelto en ese punto porque
/// <c>WebApplication</c> inserta el enrutado al principio de la tubería, que es de lo que también
/// dependen los dos middlewares anteriores.
///
/// COSTE PARA EL RESTO DEL MUNDO: una lectura de claim por petición y solo cuando el método no es
/// seguro. Un token normal —que es el 100% del tráfico— sale por la primera comprobación.
///
/// SE REGISTRA CON <see cref="ImpersonationReadOnlyExtensions.UseImpersonationReadOnly"/> en los
/// siete servicios que aceptan estos tokens. Que falte en uno solo significa que ese servicio
/// vuelve a aceptar escrituras de una sesión declarada de solo lectura, así que hay una prueba que
/// lo comprueba por reflexión sobre cada <c>Program.cs</c>.
/// </remarks>
public sealed class ImpersonationReadOnlyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ImpersonationReadOnlyMiddleware> _logger;

    public ImpersonationReadOnlyMiddleware(
        RequestDelegate next,
        ILogger<ImpersonationReadOnlyMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!EstaProhibido(context))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Suplantación de solo lectura: se rechaza {Method} {Path}. Suplantador {ImpersonatedBy}, miembro {MemberId}.",
            context.Request.Method,
            context.Request.Path,
            context.User.FindFirst("impersonatedBy")?.Value ?? "(desconocido)",
            CallerIdentity.MemberIdOf(context.User) ?? "(sin memberId)");

        context.Response.StatusCode  = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        var cuerpo = ApiResponse<bool>.Fail(
            "IMPERSONATION_READ_ONLY",
            "Esta sesión de suplantación es de solo lectura y no puede modificar datos.",
            context.TraceIdentifier);

        await context.Response.WriteAsync(JsonSerializer.Serialize(cuerpo, JsonOpciones));
    }

    /// <summary>
    /// El orden de las tres comprobaciones no es casual: primero la barata y más frecuente —el
    /// método—, luego el claim, y solo al final se mira el endpoint, que es lo único que puede ser
    /// nulo y lo único que concede excepciones.
    /// </summary>
    private static bool EstaProhibido(HttpContext context)
    {
        if (ImpersonationScope.IsSafeMethod(context.Request.Method)) return false;
        if (!ImpersonationScope.IsReadOnly(context.User))            return false;

        var endpoint = context.GetEndpoint();
        return endpoint?.Metadata.GetMetadata<ReadOnlySafeAttribute>() is null;
    }

    private static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web);
}

/// <summary>El registro, para que los siete anfitriones escriban la misma línea.</summary>
public static class ImpersonationReadOnlyExtensions
{
    /// <summary>
    /// Niega toda escritura a un token de suplantación marcado de solo lectura. Va después de
    /// <c>UseAuthorization()</c> y antes de <c>MapControllers()</c>.
    /// </summary>
    public static IApplicationBuilder UseImpersonationReadOnly(this IApplicationBuilder app) =>
        app.UseMiddleware<ImpersonationReadOnlyMiddleware>();
}
