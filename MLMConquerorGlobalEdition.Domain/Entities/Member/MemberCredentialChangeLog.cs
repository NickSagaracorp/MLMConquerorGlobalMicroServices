using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Security audit trail for every login-credential change (email, password, 2FA).
/// PreviousValue/NewValue are populated only for email changes — password and 2FA
/// changes log just the metadata so we never store secrets.
/// </summary>
public class MemberCredentialChangeLog : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;

    public CredentialChangeKind Kind { get; set; }

    /// <summary>
    /// Old value — only populated for <see cref="CredentialChangeKind.Email"/>.
    /// Null for password/2FA changes (we never store the password).
    /// </summary>
    public string? PreviousValue { get; set; }

    /// <summary>New value — only populated for email changes.</summary>
    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
