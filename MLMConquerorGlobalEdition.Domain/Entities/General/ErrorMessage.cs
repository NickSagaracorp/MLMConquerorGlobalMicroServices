namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// Localization catalog that maps an ErrorCode + Language to a safe,
/// user-facing message. Managed by admin; never exposes stack traces
/// or technical internals.
/// </summary>
public class ErrorMessage : AuditChangesIntKey
{
    /// <summary>
    /// Matches ErrorLog.ErrorCode, e.g. "INTERNAL_ERROR", "ORDER_NOT_FOUND".
    /// Use "DEFAULT" as a catch-all for any unmapped error codes.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>ISO 639-1 language code, e.g. "en", "es", "fr", "pt".</summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Generic, friendly message shown to the end user.
    /// Must NOT contain method names, SQL, endpoint paths, or stack traces.
    /// Example: "An error occurred while processing your request. Please try again."
    /// </summary>
    public string UserFriendlyMessage { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
