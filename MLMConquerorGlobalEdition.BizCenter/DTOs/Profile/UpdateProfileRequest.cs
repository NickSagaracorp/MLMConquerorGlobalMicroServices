namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;

/// <summary>
/// Editable subset of the profile. Identity-bearing fields (FirstName,
/// LastName, DateOfBirth, BusinessName, SSN, EIN) are intentionally NOT here —
/// changing those requires a support ticket so an attacker who hijacks an
/// account cannot rewrite the rightful owner's identity.
/// Address changes are logged to <c>MemberAddressHistories</c> on every save.
/// </summary>
public class UpdateProfileRequest
{
    // Contact
    public string? Phone    { get; set; }
    public string? WhatsApp { get; set; }

    // Address (logged to history on change)
    public string? Country  { get; set; }
    public string? State    { get; set; }
    public string? City     { get; set; }
    public string? Address  { get; set; }
    public string? ZipCode  { get; set; }
    public string? AddressChangeReason { get; set; }

    // Preferences
    public string? DefaultLanguage  { get; set; }                   // "en" / "es" / "pt"…
    public string? PayoutFrequency  { get; set; }                   // "Daily" | "Weekly"

    // Public-page visibility
    public bool ShowBusinessName  { get; set; }
    public bool IsEmailPublic     { get; set; }
    public bool IsPhonePublic     { get; set; }
}

public class UpdateReplicateSiteRequest
{
    public string Slug { get; set; } = string.Empty;
}

public class AddressHistoryDto
{
    public long     Id              { get; set; }
    public DateTime ChangedAt       { get; set; }
    public string?  ChangedBy       { get; set; }
    public string?  PreviousAddress { get; set; }
    public string?  PreviousCity    { get; set; }
    public string?  PreviousState   { get; set; }
    public string?  PreviousZipCode { get; set; }
    public string?  PreviousCountry { get; set; }
    public string?  NewAddress      { get; set; }
    public string?  NewCity         { get; set; }
    public string?  NewState        { get; set; }
    public string?  NewZipCode      { get; set; }
    public string?  NewCountry      { get; set; }
    public string?  Reason          { get; set; }
}

public class CredentialChangeDto
{
    public long     Id        { get; set; }
    public DateTime ChangedAt { get; set; }
    public string?  ChangedBy { get; set; }
    public string   Kind      { get; set; } = string.Empty;          // "Email" / "Password" / "TwoFactor"
    public string?  IpAddress { get; set; }
    public string?  UserAgent { get; set; }
}
