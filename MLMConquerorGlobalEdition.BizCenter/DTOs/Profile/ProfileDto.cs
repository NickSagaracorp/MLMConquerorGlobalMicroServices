namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;

/// <summary>
/// Full profile payload returned to the BizCenter Profile page. Includes the
/// member's identity (locked / read-only — see <see cref="UpdateProfileRequest"/>
/// for what is actually editable), membership status block, and display flags.
/// </summary>
public class ProfileDto
{
    // ─── Identity (locked from edit per security rules) ────────────────────
    public string  MemberId        { get; set; } = string.Empty;
    public string  FirstName       { get; set; } = string.Empty;
    public string  LastName        { get; set; } = string.Empty;
    public DateTime DateOfBirth    { get; set; }
    public string? BusinessName    { get; set; }
    public string? SsnLast4        { get; set; }                    // SSN displayed as ***-**-1234

    // ─── Identity (allowed to change, security-sensitive — separate endpoints)
    public string  Email           { get; set; } = string.Empty;
    public string? ReplicateSiteSlug { get; set; }
    public string? PhotoUrl        { get; set; }

    // ─── Contact (editable via PUT /profile) ────────────────────────────────
    public string? Phone           { get; set; }
    public string? WhatsApp        { get; set; }

    // ─── Address (editable; every change logged to AddressHistory) ─────────
    public string? Country         { get; set; }
    public string? State           { get; set; }
    public string? City            { get; set; }
    public string? Address         { get; set; }
    public string? ZipCode         { get; set; }

    // ─── Preferences ───────────────────────────────────────────────────────
    public string  DefaultLanguage  { get; set; } = "en";
    public string  PayoutFrequency  { get; set; } = "Weekly";        // "Daily" | "Weekly"

    // ─── Public-page visibility flags ──────────────────────────────────────
    public bool ShowBusinessName    { get; set; }
    public bool IsEmailPublic       { get; set; }
    public bool IsPhonePublic       { get; set; }

    // ─── MLM ────────────────────────────────────────────────────────────────
    public string  MemberType       { get; set; } = string.Empty;
    public string  Status           { get; set; } = string.Empty;
    public DateTime EnrollDate      { get; set; }
    public string? SponsorMemberId  { get; set; }

    // ─── Active membership snapshot (level + status + expiry) ──────────────
    public MembershipSnapshotDto? Membership { get; set; }
}

public class MembershipSnapshotDto
{
    public int      LevelId         { get; set; }
    public string   LevelName       { get; set; } = string.Empty;
    public string   Status          { get; set; } = string.Empty;   // Active / Hold / Cancelled / Expired
    public DateTime StartDate       { get; set; }
    public DateTime? ExpireDate     { get; set; }
    public bool     IsAutoRenew     { get; set; }
}
