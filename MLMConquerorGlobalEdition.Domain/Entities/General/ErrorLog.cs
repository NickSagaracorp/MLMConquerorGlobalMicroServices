namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// Persists every unhandled exception for developer diagnostics.
/// High-volume — uses long PK. Sensitive technical data; never expose to end users.
/// </summary>
public class ErrorLog : AuditChangesLongKey
{
    /// <summary>e.g. "MLMConquerorGlobalEdition.Signups"</summary>
    public string ApiName { get; set; } = string.Empty;

    /// <summary>HTTP verb + path, e.g. "POST /api/v1/signups/ambassador"</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>MediatR request type or class.method, e.g. "SignupAmbassadorCommand"</summary>
    public string CodeSection { get; set; } = string.Empty;

    /// <summary>Short error classification key, e.g. "INTERNAL_ERROR", "DB_TIMEOUT"</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Exception.Message — technical, for developers only.</summary>
    public string TechnicalMessage { get; set; } = string.Empty;

    /// <summary>Full exception.ToString() — type, message, and stack trace.</summary>
    public string? FullException { get; set; }

    /// <summary>Inner exception message when present.</summary>
    public string? InnerException { get; set; }

    /// <summary>Affected member (when determinable from context).</summary>
    public string? MemberId { get; set; }

    /// <summary>Sanitised request payload (PII and credentials must be stripped).</summary>
    public string? RequestData { get; set; }

    /// <summary>HTTP TraceIdentifier for correlating with API gateway / APM logs.</summary>
    public string? TraceId { get; set; }

    /// <summary>User's preferred language at time of error (for message lookup).</summary>
    public string Language { get; set; } = "en";

    /// <summary>HTTP status code that was (or would be) returned to the client.</summary>
    public int HttpStatusCode { get; set; } = 500;

    public DateTime OccurredAt { get; set; }

    // ── Resolution tracking ────────────────────────────────────────────────
    public bool IsResolved { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}
