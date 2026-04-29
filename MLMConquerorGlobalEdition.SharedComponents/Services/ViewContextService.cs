using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.SharedComponents.Constants;

namespace MLMConquerorGlobalEdition.SharedComponents.Services;

/// <summary>
/// In-memory implementation of IViewContextService.
/// Auto-initialises from IHttpContextAccessor on first access if not yet set.
/// BizCenter sets ViewingMemberId = current user's own MemberId.
/// AdminApp sets ViewingMemberId = the selected/impersonated member's MemberId.
/// </summary>
public class ViewContextService : IViewContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private string _viewingMemberId = string.Empty;
    private string _viewerUserId    = string.Empty;
    private bool   _isImpersonating;
    private bool   _isAdminContext;
    private List<string> _viewerRoles = new();
    private string _memberFullName  = string.Empty;
    private string _memberEmail     = string.Empty;
    private string _memberRankLabel = string.Empty;
    private bool _initialized;

    public ViewContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        _viewerUserId    = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        _viewingMemberId = user.FindFirstValue("memberId") ?? user.FindFirstValue("member_id") ?? string.Empty;

        _memberEmail = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        var given  = user.FindFirstValue(ClaimTypes.GivenName) ?? user.FindFirstValue("given_name") ?? string.Empty;
        var family = user.FindFirstValue(ClaimTypes.Surname)   ?? user.FindFirstValue("family_name") ?? string.Empty;
        var full   = user.FindFirstValue(ClaimTypes.Name)      ?? user.FindFirstValue("name")        ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(family))
            _memberFullName = $"{given} {family}".Trim();
        else if (!string.IsNullOrWhiteSpace(full) && !full.Contains('@'))
            _memberFullName = full;
        else if (!string.IsNullOrWhiteSpace(_memberEmail))
            _memberFullName = _memberEmail.Split('@')[0];
        else
            _memberFullName = string.Empty;

        _memberRankLabel = user.FindFirstValue("membership_level")
                        ?? user.FindFirstValue("membershipLevel")
                        ?? user.FindFirstValue("rank")
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

    public bool HasPermission(string permission)
    {
        EnsureInitialized();
        // SuperAdmin-only permissions are evaluated before the broad Admin shortcut
        if (permission == Permissions.SystemUsers.Manage)
            return _viewerRoles.Contains(AppRoles.SuperAdmin);

        if (_viewerRoles.Contains(AppRoles.SuperAdmin) || _viewerRoles.Contains(AppRoles.Admin))
            return true;

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
            Permissions.Ticket.EscalateToL2     => _viewerRoles.Contains(AppRoles.SupportLevel1) || _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.Ticket.EscalateToL3     => _viewerRoles.Contains(AppRoles.SupportLevel2) || _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.Ticket.EscalateToIT     => _viewerRoles.Contains(AppRoles.SupportLevel3) || _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.Ticket.Assign           => _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.Ticket.Resolve          => _viewerRoles.Any(r => new[] { AppRoles.SupportLevel3, AppRoles.IT, AppRoles.SupportManager }.Contains(r)),
            Permissions.Ticket.Merge            => _viewerRoles.Contains(AppRoles.SupportManager),
            Permissions.Ticket.ViewAll          => _viewerRoles.Any(r => AppRoles.SupportRoles.Contains(r)),
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
