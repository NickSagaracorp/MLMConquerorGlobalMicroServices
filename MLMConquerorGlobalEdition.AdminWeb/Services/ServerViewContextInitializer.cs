using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

public class ServerViewContextInitializer
{
    private readonly IHttpContextAccessor _http;
    private readonly ViewContextService   _viewContext;

    public ServerViewContextInitializer(IHttpContextAccessor http, ViewContextService viewContext)
    {
        _http        = http;
        _viewContext = viewContext;
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
            isAdminContext:  true,   // AdminWeb always sets admin context
            viewerRoles:     roles);
    }
}
