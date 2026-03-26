using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.AdminApp.Services;

/// <summary>
/// Reads JWT claims and configures ViewContextService for admin context.
///
/// Normal mode:  isAdminContext = true, isImpersonating = false, viewingMemberId = admin's own memberId (may be empty)
/// Impersonating: isAdminContext = true, isImpersonating = true, viewingMemberId = selected member's memberId
/// </summary>
public class AdminViewContextInitializer
{
    private readonly AdminJwtAuthStateProvider _authProvider;
    private readonly ViewContextService        _viewContext;

    public AdminViewContextInitializer(AdminJwtAuthStateProvider authProvider, ViewContextService viewContext)
    {
        _authProvider = authProvider;
        _viewContext  = viewContext;
    }

    /// <summary>
    /// Called after login and after impersonation state changes.
    /// Reads from the effective token (impersonation token takes priority).
    /// </summary>
    public async Task InitializeAsync()
    {
        var token = await _authProvider.GetEffectiveTokenAsync();
        if (string.IsNullOrEmpty(token)) return;

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token)) return;

        var jwt    = handler.ReadJwtToken(token);
        var claims = jwt.Claims.ToList();

        var userId          = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
        var memberId        = claims.FirstOrDefault(c => c.Type == "member_id")?.Value ?? string.Empty;
        var roles           = claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();
        var isImpersonating = claims.Any(c => c.Type == "impersonating" && c.Value == "true");
        var impersonatedBy  = claims.FirstOrDefault(c => c.Type == "impersonated_by")?.Value;

        if (isImpersonating)
        {
            // When impersonating, viewerUserId should be the original admin, not the impersonated user
            var adminToken = await _authProvider.GetAdminTokenAsync();
            var adminUserId = userId; // fallback
            if (!string.IsNullOrEmpty(adminToken) && handler.CanReadToken(adminToken))
            {
                var adminJwt = handler.ReadJwtToken(adminToken);
                adminUserId  = adminJwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ?? userId;
                // For permissions, use the admin's roles
                var adminRoles = adminJwt.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();

                _viewContext.SetContext(
                    viewingMemberId: memberId,
                    viewerUserId:    adminUserId,
                    isImpersonating: true,
                    isAdminContext:  true,
                    viewerRoles:     adminRoles);
                return;
            }
        }

        // Normal admin context
        _viewContext.SetContext(
            viewingMemberId: memberId,
            viewerUserId:    userId,
            isImpersonating: false,
            isAdminContext:  true,
            viewerRoles:     roles);
    }

    /// <summary>Called when admin selects a member to impersonate.</summary>
    public async Task SetImpersonationAsync(string impersonationToken, string targetMemberId)
    {
        await _authProvider.StartImpersonationAsync(impersonationToken);
        await InitializeAsync();
    }

    /// <summary>Called when admin exits impersonation mode.</summary>
    public async Task ExitImpersonationAsync()
    {
        await _authProvider.StopImpersonationAsync();
        await InitializeAsync();
    }
}
