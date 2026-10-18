using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MLMConquerorGlobalEdition.AdminAPI.Filters;

/// <summary>
/// Plan P: fuera de Development el controlador no responde (404), sin depender de que cada accion recuerde
/// comprobarlo. Es un filtro de recurso, asi que corre despues de la autorizacion: fuera de Development una
/// peticion anonima a una accion con [Authorize] recibe antes el 401 del middleware de autorizacion. En los dos
/// casos no se ejecuta nada.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DevelopmentOnlyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment()) context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
