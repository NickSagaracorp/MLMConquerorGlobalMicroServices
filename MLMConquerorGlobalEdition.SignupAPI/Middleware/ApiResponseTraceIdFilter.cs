using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Middleware;

/// <summary>
/// Pone el traceId de la petición (HttpContext.TraceIdentifier, el mismo que DomainExceptionMiddleware
/// escribe en ErrorLogs) en toda ApiResponse que un controlador devuelva sin él: los errores se arman con
/// ApiResponse.Fail(código, texto) y casi ninguno lo pasaba, así que llegaban con traceId vacío.
/// No pisa uno ya puesto.
/// </summary>
public sealed class ApiResponseTraceIdFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: IApiResponseEnvelope envelope }
            && string.IsNullOrEmpty(envelope.TraceId))
            envelope.TraceId = context.HttpContext.TraceIdentifier;

        return next();
    }
}
