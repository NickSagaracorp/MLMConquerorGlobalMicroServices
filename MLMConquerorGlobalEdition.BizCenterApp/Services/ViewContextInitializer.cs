using System.IdentityModel.Tokens.Jwt;
using MLMConquerorGlobalEdition.ClientCore.AccessTokens;
using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.BizCenterApp.Services;

/// <summary>
/// Reads the JWT claims and configures the shared ViewContextService for BizCenter context.
/// Called once per session from the root App component after authentication.
/// In BizCenter: viewer is always viewing their own member data (IsAdminContext = false).
/// </summary>
public class ViewContextInitializer
{
    private readonly JwtAuthStateProvider _authProvider;
    private readonly ViewContextService   _viewContext;
    private readonly ClientAccessTokenValidator _validator;

    public ViewContextInitializer(
        JwtAuthStateProvider authProvider, ViewContextService viewContext, ClientAccessTokenValidator validator)
    {
        _authProvider = authProvider;
        _viewContext  = viewContext;
        _validator    = validator;
    }

    public async Task InitializeAsync()
    {
        var token = await _authProvider.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) return;

        // Solo un token con nuestra firma decide qué miembro y qué roles ve la interfaz.
        var check = await _validator.CheckAsync(token);
        if (!check.IsValid) return;

        var claims = check.Token!.Claims.ToList();

        var userId   = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
        var memberId = claims.FirstOrDefault(c => c.Type == "member_id")?.Value ?? string.Empty;
        var roles    = claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value);

        // BizCenter: member always views their own data
        _viewContext.SetContext(
            viewingMemberId: memberId,
            viewerUserId:    userId,
            isImpersonating: false,
            isAdminContext:  false,
            viewerRoles:     roles);
    }
}
