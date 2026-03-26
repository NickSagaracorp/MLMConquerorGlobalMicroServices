namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Persists unhandled exceptions to ErrorLog and resolves localized user-facing messages
/// from the ErrorMessages catalog.
/// Implementations must use an isolated DB scope so a failed transaction never
/// prevents error persistence.
/// </summary>
public interface IErrorTrackingService
{
    /// <summary>
    /// Logs the exception to ErrorLog. Fire-and-forget safe — never throws.
    /// </summary>
    Task TrackAsync(
        string apiName,
        string endpoint,
        string codeSection,
        string errorCode,
        Exception exception,
        string? memberId = null,
        string? requestData = null,
        string? traceId = null,
        string language = "en",
        int httpStatusCode = 500,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the user-friendly message for the given errorCode and language.
    /// Falls back to "en", then to a hardcoded default if no entry exists.
    /// </summary>
    Task<string> GetUserMessageAsync(
        string errorCode,
        string language,
        CancellationToken ct = default);
}
