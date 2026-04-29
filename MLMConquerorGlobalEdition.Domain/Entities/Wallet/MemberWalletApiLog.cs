using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Wallet;

/// <summary>
/// Records every interaction (request + response) with an external payment-gateway
/// API on behalf of a member. Used for support escalations, fraud disputes, and
/// reconciliation when a gateway claims a transfer was rejected or never received.
/// High-volume table — uses long PK and minimal columns.
/// </summary>
public class MemberWalletApiLog : AuditChangesLongKey
{
    public string     MemberId   { get; set; } = string.Empty;
    public WalletType WalletType { get; set; }

    /// <summary>Operation name — e.g. "RegisterAccount", "VerifyAccount", "Withdraw".</summary>
    public string  Operation  { get; set; } = string.Empty;

    public string? Endpoint   { get; set; }
    public string? HttpMethod { get; set; }

    /// <summary>Request payload sent to the gateway (JSON or form data, with secrets redacted).</summary>
    public string? RequestBody { get; set; }

    public int     HttpStatusCode { get; set; }

    /// <summary>Raw response body from the gateway.</summary>
    public string? ResponseBody { get; set; }

    public bool    Success      { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Round-trip duration in milliseconds.</summary>
    public int DurationMs { get; set; }
}
