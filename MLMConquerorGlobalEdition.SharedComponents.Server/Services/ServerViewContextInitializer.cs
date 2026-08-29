using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Siembra el contexto de vista de un portal web a partir del usuario de la petición en curso.
/// </summary>
/// <remarks>
/// Era el mismo archivo en los dos portales salvo un booleano: administración mira siempre en
/// contexto de administrador y el centro de negocios nunca. Ese booleano es lo único que se
/// parametriza; lo pone el <c>Program.cs</c> de cada portal al registrarlo con
/// <c>AddServerViewContextInitializer</c>.
///
/// Pide el tipo CONCRETO <see cref="ViewContextService"/> a propósito, igual que los
/// inicializadores de las dos MAUI: <c>AddSharedComponents</c> registra el concreto y hace que la
/// interfaz lo resuelva, de modo que lo que escribe aquí <c>SetContext</c> es lo mismo que leen los
/// componentes que inyectan <c>IViewContextService</c>. Si alguien vuelve a partir ese registro en
/// dos, esto escribe en un objeto que nadie pinta.
/// </remarks>
public class ServerViewContextInitializer
{
    private readonly IHttpContextAccessor _http;
    private readonly ViewContextService   _viewContext;
    private readonly bool                 _isAdminContext;

    public ServerViewContextInitializer(IHttpContextAccessor http, ViewContextService viewContext, bool isAdminContext)
    {
        _http           = http;
        _viewContext    = viewContext;
        _isAdminContext = isAdminContext;
    }

    public void Initialize()
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        var userId   = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var memberId = user.FindFirstValue("member_id")               ?? string.Empty;
        var roles    = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

        _viewContext.SetContext(
            viewingMemberId: memberId,
            viewerUserId:    userId,
            isImpersonating: false,
            isAdminContext:  _isAdminContext,
            viewerRoles:     roles);
    }
}
