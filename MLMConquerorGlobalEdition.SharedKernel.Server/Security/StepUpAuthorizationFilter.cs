using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Security;

/// <summary>Lo que la UI necesita para saber qué código pedir.</summary>
public sealed record StepUpRequiredData(string OperationKey);

/// <summary>
/// Deja pasar la acción solo con un permiso de step-up válido para esta operación y este usuario,
/// y lo quema en el acto: un permiso, un uso.
/// </summary>
/// <remarks>
/// Corre como filtro de autorización, antes del model binding y de la acción: sin permiso, la
/// acción no llega a ejecutarse ni a leer el cuerpo.
///
/// El permiso se consume aunque la acción falle después (por ejemplo, un identificador mal
/// escrito). Es a propósito: consumir después dejaría una ventana en la que dos peticiones
/// simultáneas con el mismo permiso pasan las dos. Repetir el código es el precio.
/// </remarks>
public sealed class StepUpAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string               _operationKey;
    private readonly StepUpGrantValidator _validator;
    private readonly ICacheService        _cache;

    public StepUpAuthorizationFilter(string operationKey, StepUpGrantValidator validator, ICacheService cache)
    {
        _operationKey = operationKey;
        _validator    = validator;
        _cache        = cache;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http   = context.HttpContext;
        var header = http.Request.Headers[StepUpGrant.HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            context.Result = Forbidden(StepUpGrant.RequiredErrorCode,
                "Esta operación requiere confirmar un código de verificación.");
            return;
        }

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? http.User.FindFirstValue("sub")
                  ?? string.Empty;

        var validated = _validator.Validate(header, userId, _operationKey);
        if (!validated.IsSuccess)
        {
            context.Result = Forbidden(StepUpGrant.InvalidErrorCode, validated.Error!);
            return;
        }

        // Incremento atómico: el primero que lo lleva a 1 se queda el permiso. La marca dura más
        // que el permiso para que no pueda reaparecer "sin usar" antes de vencer.
        var uses = await _cache.IncrementAsync(
            CacheKeys.StepUpGrantConsumed(validated.Value!),
            StepUpGrant.Lifetime + TimeSpan.FromMinutes(1),
            http.RequestAborted);

        if (uses > 1)
            context.Result = Forbidden(StepUpGrant.InvalidErrorCode, "El permiso de step-up ya se usó.");
    }

    private ObjectResult Forbidden(string code, string message)
    {
        var body = ApiResponse<StepUpRequiredData>.Fail(code, message);
        body.Data = new StepUpRequiredData(_operationKey);
        return new ObjectResult(body) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
