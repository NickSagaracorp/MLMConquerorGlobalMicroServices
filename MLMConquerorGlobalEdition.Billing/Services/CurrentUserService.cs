using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    // Se lee con CallerIdentity porque JwtService emite "memberId" y aquí se leía "member_id":
    // MemberId salía siempre vacío. El nombre vive en una sola constante para que no vuelva a divergir.
    public string UserId => CallerIdentity.UserIdOf(User) ?? string.Empty;
    public string MemberId => CallerIdentity.MemberIdOf(User) ?? string.Empty;
    public string Email => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public bool IsAdmin => User?.IsInRole("Admin") ?? false;
    public IEnumerable<string> Roles => User?.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value) ?? Enumerable.Empty<string>();
}
