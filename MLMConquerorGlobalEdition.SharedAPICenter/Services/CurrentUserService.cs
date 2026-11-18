using MLMConquerorGlobalEdition.SharedKernel.Security;
using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Services;

/// <summary>
/// Reads the current authenticated user's identity from the JWT claims
/// carried on the active HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string UserId   => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    // JwtService emite "memberId"; aquí se leía "member_id" y salía siempre vacío.
    public string MemberId => CallerIdentity.MemberIdOf(User) ?? string.Empty;
    public string Email    => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public bool   IsAdmin  => User?.IsInRole("Admin") ?? false;

    public IEnumerable<string> Roles =>
        User?.Claims
             .Where(c => c.Type == ClaimTypes.Role)
             .Select(c => c.Value)
        ?? Enumerable.Empty<string>();
}
