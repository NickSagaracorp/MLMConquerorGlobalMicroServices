using MLMConquerorGlobalEdition.SharedKernel.Security;
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

        // Los nombres de los claims salen de CallerIdentity, que es quien sabe cómo los escribe el
        // emisor: el miembro va en "memberId" y aquí se buscaba "member_id", que llegaba siempre vacío.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var userId    = CallerIdentity.UserIdOf(principal) ?? string.Empty;
        var memberId  = CallerIdentity.MemberIdOf(principal) ?? string.Empty;
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
