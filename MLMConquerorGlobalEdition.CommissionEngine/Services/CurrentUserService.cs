using System.Security.Claims;

namespace MLMConquerorGlobalEdition.CommissionEngine.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
    public string MemberId => User?.FindFirstValue("MemberId") ?? string.Empty;
    public bool IsAdmin => User?.IsInRole("Admin") ?? false;
}
