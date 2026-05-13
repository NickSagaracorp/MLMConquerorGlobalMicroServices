using Microsoft.AspNetCore.Components;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Profile;

/// <summary>
/// Centralised base-path resolution for the profile / billing / wallet endpoints
/// the shared <c>UserProfilePage</c> uses. The same Razor tree backs both the
/// member-facing BizCenter view and the AdminWeb view; the only difference is the
/// HTTP route prefix:
///   • Member view  → <c>api/v1/bizcenter/...</c> (memberId comes from JWT)
///   • Admin  view  → <c>api/v1/admin/members/{memberId}/...</c>
/// Each child card asks this helper for its prefix so the URL flip is one decision
/// per call site instead of branching scattered through the markup.
/// </summary>
public static class ProfileApiPaths
{
    /// <summary>
    /// Base path for the profile + billing surface (no trailing slash).
    /// Decision is driven by the **current browser URL** rather than the scoped
    /// <see cref="IViewContextService"/> flag, because <c>NavigationManager.Uri</c>
    /// is reliable in every Blazor render mode (including <c>prerender: false</c>
    /// pages where <c>HttpContext.Request.Path</c> is not the admin URL during
    /// interactive render).
    /// </summary>
    public static string ProfileBase(NavigationManager nav, string? memberProfileId)
    {
        if (IsAdminUrl(nav.Uri) && !string.IsNullOrWhiteSpace(memberProfileId))
            return $"api/v1/admin/members/{memberProfileId}";
        return "api/v1/bizcenter";
    }

    /// <summary>
    /// Legacy overload that consults <see cref="IViewContextService.IsAdminContext"/>.
    /// Kept so callers that don't have a NavigationManager handy keep working;
    /// prefer the NavigationManager overload in new code.
    /// </summary>
    public static string ProfileBase(IViewContextService ctx, string? memberProfileId)
    {
        if (ctx.IsAdminContext && !string.IsNullOrWhiteSpace(memberProfileId))
            return $"api/v1/admin/members/{memberProfileId}";
        return "api/v1/bizcenter";
    }

    private static bool IsAdminUrl(string uri)
        => !string.IsNullOrEmpty(uri)
        && uri.Contains("/admin/", System.StringComparison.OrdinalIgnoreCase);
}
