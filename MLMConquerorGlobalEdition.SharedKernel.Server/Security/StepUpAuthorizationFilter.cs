using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
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
///
/// FALLA CERRADO. El uso único se marca con <see cref="ICacheService.TryClaimOnceAsync"/>, que es
/// lo único de la caché que no degrada: si Redis no responde, lanza, y aquí eso es un 403. Con
/// el resto de la caché un fallo se convierte en "no había nada", que para esta barrera querría
/// decir "no se había usado" y dejaría repetir el permiso justo mientras Redis falla. Para que la
/// marca valga entre instancias el anfitrión tiene que registrar <c>IConnectionMultiplexer</c>.
///
/// Cada rechazo se registra (usuario, operación, ruta y TraceId; en la repetición también el
/// jti), nunca el token: un permiso vivo en el log es un permiso que se puede usar.
/// </remarks>
public sealed class StepUpAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string                             _operationKey;
    private readonly StepUpGrantValidator               _validator;
    private readonly ICacheService                      _cache;
    private readonly ILogger<StepUpAuthorizationFilter> _logger;

    public StepUpAuthorizationFilter(
        string                             operationKey,
        StepUpGrantValidator               validator,
        ICacheService                      cache,
        ILogger<StepUpAuthorizationFilter> logger)
    {
        _operationKey = operationKey;
        _validator    = validator;
        _cache        = cache;
        _logger       = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http    = context.HttpContext;
        var headers = http.Request.Headers[StepUpGrant.HeaderName];

        if (headers.Count == 0 || string.IsNullOrWhiteSpace(headers.ToString()))
        {
            context.Result = Forbidden(http, StepUpGrant.RequiredErrorCode,
                "Esta operación requiere confirmar un código de verificación.");
            return;
        }

        // Sin sesión autenticada no hay de quién comprobar el permiso: el validador lo rechaza
        // con el usuario vacío. Va aunque el endpoint lleve [Authorize], por si alguien lo quita.
        var userId = http.User.Identity?.IsAuthenticated == true
            ? CallerIdentity.UserIdOf(http.User)
            : null;

        // Dos cabeceras no son "una de las dos": no hay forma correcta de elegir.
        var validated = headers.Count == 1
            ? _validator.Validate(headers[0]!, userId ?? string.Empty, _operationKey)
            : null;

        if (validated is null || !validated.IsSuccess)
        {
            _logger.LogWarning(
                "Step-up rechazado: permiso inválido. Usuario {UserId}, operación {OperationKey}, ruta {Path}, TraceId {TraceId}.",
                userId ?? "(sin usuario)", _operationKey, http.Request.Path, http.TraceIdentifier);

            context.Result = Forbidden(http, StepUpGrant.InvalidErrorCode,
                validated?.Error ?? "El permiso de step-up no es válido.");
            return;
        }

        var jti = validated.Value!;

        // SET NX: el primero que marca el jti se queda el permiso. La marca dura más que el
        // permiso para que no pueda reaparecer "sin usar" antes de vencer.
        bool claimed;
        try
        {
            claimed = await _cache.TryClaimOnceAsync(
                CacheKeys.StepUpGrantConsumed(jti),
                StepUpGrant.Lifetime + TimeSpan.FromMinutes(1),
                http.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Step-up rechazado: no se pudo marcar el permiso como usado. Usuario {UserId}, operación {OperationKey}, ruta {Path}, TraceId {TraceId}.",
                userId, _operationKey, http.Request.Path, http.TraceIdentifier);

            context.Result = Forbidden(http, StepUpGrant.InvalidErrorCode, "No se pudo verificar el permiso.");
            return;
        }

        if (!claimed)
        {
            _logger.LogWarning(
                "Step-up rechazado: permiso repetido. Usuario {UserId}, operación {OperationKey}, jti {Jti}, ruta {Path}, TraceId {TraceId}.",
                userId, _operationKey, jti, http.Request.Path, http.TraceIdentifier);

            context.Result = Forbidden(http, StepUpGrant.InvalidErrorCode, "El permiso de step-up ya se usó.");
        }
    }

    private ObjectResult Forbidden(HttpContext http, string code, string message)
    {
        var body = ApiResponse<StepUpRequiredData>.Fail(code, message, http.TraceIdentifier);
        body.Data = new StepUpRequiredData(_operationKey);
        return new ObjectResult(body) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
