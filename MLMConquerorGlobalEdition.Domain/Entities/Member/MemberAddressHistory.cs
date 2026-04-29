using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Append-only log of every address change a member makes from the BizCenter.
/// Stores both the previous and the new values so the timeline shows a clean
/// "from → to" diff for fraud/support investigations.
/// </summary>
public class MemberAddressHistory : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;

    public string? PreviousAddress  { get; set; }
    public string? PreviousCity     { get; set; }
    public string? PreviousState    { get; set; }
    public string? PreviousZipCode  { get; set; }
    public string? PreviousCountry  { get; set; }

    public string? NewAddress       { get; set; }
    public string? NewCity          { get; set; }
    public string? NewState         { get; set; }
    public string? NewZipCode       { get; set; }
    public string? NewCountry       { get; set; }

    /// <summary>Optional reason captured from the user, free-text.</summary>
    public string? Reason { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
