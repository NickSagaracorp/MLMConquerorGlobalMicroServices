using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedComponents.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.SharedComponents.Services;

/// <summary>
/// In-memory implementation of IViewContextService.
/// Auto-initialises from <see cref="IViewContextSeed"/> on first access if not yet set.
/// BizCenter sets ViewingMemberId = current user's own MemberId.
/// AdminApp sets ViewingMemberId = the selected/impersonated member's MemberId.
/// </summary>
/// <remarks>
/// La semilla entra por una interfaz y no como <c>IHttpContextAccessor</c> porque esta clase la
/// usan los cuatro anfitriones: los dos portales web y las dos MAUI. Ver <see cref="IViewContextSeed"/>
/// para por qué el acoplamiento anterior no solo estorbaba a móvil, sino que lo rompía.
/// </remarks>
public class ViewContextService : IViewContextService
{
    private readonly IViewContextSeed _seed;

    private string _viewingMemberId = string.Empty;
    private string _viewerUserId    = string.Empty;
    private bool   _isImpersonating;
    private bool   _isAdminContext;
    private List<string> _viewerRoles = new();
    private string _memberFullName  = string.Empty;
    private string _memberEmail     = string.Empty;
    private string _memberRankLabel = string.Empty;
    private bool _initialized;

    public ViewContextService(IViewContextSeed seed)
    {
        _seed = seed;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // Derive admin/member routing context from the request path of the host that
        // resolved this scoped service. AdminWeb pages live under /admin/...; everything
        // else (BizCenterWeb, the join page) is treated as member context. This avoids
        // relying on an explicit initializer call that historically wasn't wired up.
        var path = _seed.GetPath();
        if (!string.IsNullOrEmpty(path) &&
            path.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase))
        {
            _isAdminContext = true;
        }

        var user = _seed.GetUser();
        if (user?.Identity?.IsAuthenticated != true) return;

        _viewerUserId    = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        _viewingMemberId = user.FindFirst("memberId")?.Value ?? user.FindFirst("member_id")?.Value ?? string.Empty;

        _memberEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        var given  = user.FindFirst(ClaimTypes.GivenName)?.Value ?? user.FindFirst("given_name")?.Value ?? string.Empty;
        var family = user.FindFirst(ClaimTypes.Surname)?.Value   ?? user.FindFirst("family_name")?.Value ?? string.Empty;
        var full   = user.FindFirst(ClaimTypes.Name)?.Value      ?? user.FindFirst("name")?.Value        ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(family))
            _memberFullName = $"{given} {family}".Trim();
        else if (!string.IsNullOrWhiteSpace(full) && !full.Contains('@'))
            _memberFullName = full;
        else if (!string.IsNullOrWhiteSpace(_memberEmail))
            _memberFullName = _memberEmail.Split('@')[0];
        else
            _memberFullName = string.Empty;

        _memberRankLabel = user.FindFirst("membership_level")?.Value
                        ?? user.FindFirst("membershipLevel")?.Value
                        ?? user.FindFirst("rank")?.Value
                        ?? string.Empty;

        // JWT role claim can arrive as the full URI or as the short name
        _viewerRoles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role ||
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                        c.Type == "role")
            .Select(c => c.Value)
            .ToList();
    }

    public string ViewingMemberId  { get { EnsureInitialized(); return _viewingMemberId; } }
    public string ViewerUserId     { get { EnsureInitialized(); return _viewerUserId; } }
    public bool   IsImpersonating  { get { EnsureInitialized(); return _isImpersonating; } }
    public bool   IsAdminContext   { get { EnsureInitialized(); return _isAdminContext; } }
    public IEnumerable<string> ViewerRoles { get { EnsureInitialized(); return _viewerRoles; } }
    public string MemberFullName   { get { EnsureInitialized(); return _memberFullName; } }
    public string MemberEmail      { get { EnsureInitialized(); return _memberEmail; } }
    public string MemberRankLabel  { get { EnsureInitialized(); return _memberRankLabel; } }

    /// <summary>Allows the host app to override the resolved member display name (e.g., after a profile load).</summary>
    public void SetMemberDisplay(string fullName, string email, string rankLabel)
    {
        if (!string.IsNullOrWhiteSpace(fullName))  _memberFullName  = fullName;
        if (!string.IsNullOrWhiteSpace(email))     _memberEmail     = email;
        if (!string.IsNullOrWhiteSpace(rankLabel)) _memberRankLabel = rankLabel;
    }

    public void SetContext(
        string viewingMemberId,
        string viewerUserId,
        bool isImpersonating,
        bool isAdminContext,
        IEnumerable<string> viewerRoles)
    {
        _viewingMemberId = viewingMemberId;
        _viewerUserId    = viewerUserId;
        _isImpersonating = isImpersonating;
        _isAdminContext  = isAdminContext;
        _viewerRoles     = viewerRoles.ToList();
        _initialized     = true;
    }

    /// <summary>
    /// Support tier seniority (Nick's spec): L1 ⊇ L2 ⊇ L3 — higher tiers see/do everything
    /// lower tiers do. Used so a permission scoped to a junior tier is also granted to all
    /// senior tiers within the support chain.
    /// </summary>
    private static readonly Dictionary<string, int> SupportTierRank = new()
    {
        [AppRoles.SupportLevel3] = 1,
        [AppRoles.SupportLevel2] = 2,
        [AppRoles.SupportLevel1] = 3,
    };

    private bool HasSupportTierAtLeast(string minTierRole)
    {
        if (!SupportTierRank.TryGetValue(minTierRole, out var minRank)) return false;
        return _viewerRoles.Any(r =>
            SupportTierRank.TryGetValue(r, out var rank) && rank >= minRank);
    }

    public bool HasPermission(string permission)
    {
        EnsureInitialized();

        // SuperAdmin-only permissions are evaluated before any broad shortcut.
        if (permission == Permissions.SystemUsers.Manage)
            return _viewerRoles.Contains(AppRoles.SuperAdmin);

        // Top-tier shortcut: SuperAdmin, Admin, and IT see everything (Nick: SuperAdmin = IT,
        // Admin inherits all manager and support tiers).
        if (_viewerRoles.Contains(AppRoles.SuperAdmin)
            || _viewerRoles.Contains(AppRoles.Admin)
            || _viewerRoles.Contains(AppRoles.IT))
            return true;

        // CommissionManager and SupportManager inherit all support-tier capabilities
        // (Nick: CM ve todo lo de support level 1,2,3).
        var hasManagerInheritance =
            _viewerRoles.Contains(AppRoles.CommissionManager)
         || _viewerRoles.Contains(AppRoles.SupportManager);

        return permission switch
        {
            Permissions.Commission.Delete       => _viewerRoles.Contains(AppRoles.CommissionManager),
            Permissions.Commission.ForcePay     => _viewerRoles.Contains(AppRoles.CommissionManager),
            Permissions.Commission.View         => _viewerRoles.Contains(AppRoles.CommissionManager),
            Permissions.Member.ChangeStatus     => false,
            Permissions.Member.Impersonate      => _viewerRoles.Any(r => AppRoles.CanImpersonate.Contains(r)),
            Permissions.Member.ImpersonateReadOnly => _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.GhostPoints.Add         => false,
            Permissions.Tokens.AdminGrant       => false,
            Permissions.Rank.Override           => false,
            Permissions.Loyalty.ManualUnlock    => false,
            Permissions.Wallet.ViewFullHistory  => _viewerRoles.Contains(AppRoles.BillingManager),

            // Support escalation chain — each permission requires the originating tier OR any
            // more-senior support tier (per Nick: L1 ⊇ L2 ⊇ L3) plus manager inheritance.
            Permissions.Ticket.EscalateToL2     => HasSupportTierAtLeast(AppRoles.SupportLevel1) || hasManagerInheritance,
            Permissions.Ticket.EscalateToL3     => HasSupportTierAtLeast(AppRoles.SupportLevel2) || hasManagerInheritance,
            Permissions.Ticket.EscalateToIT     => HasSupportTierAtLeast(AppRoles.SupportLevel3) || hasManagerInheritance,
            Permissions.Ticket.Assign           => _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.Ticket.Resolve          => HasSupportTierAtLeast(AppRoles.SupportLevel3) || hasManagerInheritance,
            Permissions.Ticket.Merge            => _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.Ticket.ViewAll          => HasSupportTierAtLeast(AppRoles.SupportLevel3) || hasManagerInheritance,

            Permissions.SystemUsers.Manage      => _viewerRoles.Contains(AppRoles.SuperAdmin),
            _ => false
        };
    }

    public bool IsInAnyRole(params string[] roles)
    {
        EnsureInitialized();
        return _viewerRoles.Any(r => roles.Contains(r));
    }
}
