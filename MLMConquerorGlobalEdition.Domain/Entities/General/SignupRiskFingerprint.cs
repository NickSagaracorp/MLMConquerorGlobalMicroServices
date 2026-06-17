using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// One row per fingerprint event captured on the public join pages.
/// VisitorId comes from FingerprintJS (OSS or Pro) and remains stable across IP rotation
/// and VPN changes — so duplicate signup attempts that masquerade as different IPs still
/// appear under the same VisitorId. Combined with IP + UA + sponsor + flow we can flag
/// fraud rings post-hoc and block real-time submission when a threshold is crossed.
/// </summary>
public class SignupRiskFingerprint : AuditChangesLongKey
{
    /// <summary>FingerprintJS visitorId (deterministic per browser).</summary>
    public string VisitorId { get; set; } = string.Empty;

    /// <summary>Pro-only request identifier; for OSS we generate a per-event GUID.</summary>
    public string? RequestId { get; set; }

    /// <summary>Where the event was generated in the signup funnel.</summary>
    public SignupRiskFlow Flow { get; set; } = SignupRiskFlow.AmbassadorSignup;

    /// <summary>Set once an Order is created from this signup attempt.</summary>
    public string? OrderId { get; set; }

    /// <summary>Set once a MemberProfile is created (signup completed).</summary>
    public string? MemberId { get; set; }

    /// <summary>Sponsor slug or MemberId resolved from the join URL.</summary>
    public string? SponsorReplicateSite { get; set; }

    /// <summary>Public IP captured server-side from HttpContext (X-Forwarded-For aware).</summary>
    public string? IpAddress { get; set; }

    /// <summary>Raw User-Agent from the request.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Country guess derived from IP (when available — best-effort).</summary>
    public string? CountryIso2 { get; set; }

    /// <summary>True when this event tripped the duplicate-threshold guard.</summary>
    public bool IsFlagged { get; set; }

    /// <summary>Reason for flagging, e.g. "DUP_VISITOR_3_IN_24H".</summary>
    public string? FlagReason { get; set; }

    /// <summary>True when admin has manually cleared this row so it no longer counts toward the duplicate threshold.</summary>
    public bool Cleared { get; set; }

    /// <summary>Timestamp when admin cleared this row.</summary>
    public DateTime? ClearedAt { get; set; }

    /// <summary>Admin UserId that cleared this row (sourced from ICurrentUserService).</summary>
    public string? ClearedBy { get; set; }

    /// <summary>Free-text reason given by the admin (e.g. "Confirmed legitimate user — duplicate due to network retries").</summary>
    public string? ClearReason { get; set; }
}
