using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Seguridad;

/// <summary>El 401 de los dos esquemas, con el mismo sobre <c>ApiResponse</c> que el resto de la API.</summary>
internal static class RespuestaJson
{
    public static Task EscribirAsync(HttpContext context, int estado, string codigo, string mensaje)
    {
        context.Response.StatusCode = estado;
        return context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(codigo, mensaje, context.TraceIdentifier));
    }
}
