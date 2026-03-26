namespace MLMConquerorGlobalEdition.SharedKernel;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public IEnumerable<string>? Errors { get; set; }
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Localized, user-safe message. Never contains stack traces or internal details.
    /// Populated from the ErrorMessages catalog via IErrorTrackingService.
    /// </summary>
    public string? UserFriendlyMessage { get; set; }

    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(
        string errorCode,
        string error,
        string traceId = "",
        string? userFriendlyMessage = null) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            Errors = new[] { error },
            TraceId = traceId,
            UserFriendlyMessage = userFriendlyMessage
        };
}
