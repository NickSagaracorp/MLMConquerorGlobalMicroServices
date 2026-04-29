namespace MLMConquerorGlobalEdition.SharedComponents.Services;

/// <summary>
/// Provides context about WHO is viewing and WHOSE data is being displayed.
/// Injected into all shared components to control visibility of admin-only actions.
/// </summary>
public interface IViewContextService
{
    /// <summary>The MemberProfileId whose data is being rendered.</summary>
    string ViewingMemberId { get; }

    /// <summary>The ApplicationUser.Id of the person currently logged in.</summary>
    string ViewerUserId { get; }

    /// <summary>True when an admin/support user is viewing another member's data.</summary>
    bool IsImpersonating { get; }

    /// <summary>True when the component is rendered inside the Admin application.</summary>
    bool IsAdminContext { get; }

    /// <summary>Roles of the logged-in user.</summary>
    IEnumerable<string> ViewerRoles { get; }

    /// <summary>Full display name of the viewing member (first + last, falling back to email local part).</summary>
    string MemberFullName { get; }

    /// <summary>Email of the viewing member, when available.</summary>
    string MemberEmail { get; }

    /// <summary>Membership level / rank label for the viewing member (e.g., "Jade Ambassador"). May be empty.</summary>
    string MemberRankLabel { get; }

    /// <summary>Returns true if the viewer has the specified permission.</summary>
    bool HasPermission(string permission);

    /// <summary>Returns true if the viewer has ANY of the specified roles.</summary>
    bool IsInAnyRole(params string[] roles);
}
