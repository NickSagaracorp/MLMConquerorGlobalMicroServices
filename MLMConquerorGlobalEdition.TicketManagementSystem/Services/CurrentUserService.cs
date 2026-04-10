using System.Security.Claims;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    public string MemberId => User?.FindFirstValue("memberId") ?? string.Empty;
    public string Email => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public bool IsAdmin => User?.IsInRole("SuperAdmin") == true || User?.IsInRole("Admin") == true;
    public IEnumerable<string> Roles => User?.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value) ?? Enumerable.Empty<string>();
}
