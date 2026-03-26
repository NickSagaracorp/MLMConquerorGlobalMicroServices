using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services;

/// <summary>
/// Persists error diagnostics to ErrorLog and resolves localized messages from ErrorMessages.
///
/// Uses IServiceScopeFactory to create a fresh DbContext for every operation so that
/// a failed/dirty handler context never prevents error persistence.
///
/// Register as Singleton — safe because all DB access is through fresh child scopes.
/// </summary>
public class ErrorTrackingService : IErrorTrackingService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ErrorTrackingService(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task TrackAsync(
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
        CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.Now;
            var log = new ErrorLog
            {
                ApiName        = apiName,
                Endpoint       = endpoint,
                CodeSection    = codeSection,
                ErrorCode      = errorCode,
                TechnicalMessage = exception.Message,
                FullException  = exception.ToString(),
                InnerException = exception.InnerException?.Message,
                MemberId       = memberId,
                RequestData    = requestData,
                TraceId        = traceId,
                Language       = language,
                HttpStatusCode = httpStatusCode,
                OccurredAt     = now,
                CreationDate   = now,
                CreatedBy      = "system"
            };

            await db.ErrorLogs.AddAsync(log, ct);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Swallow — error tracking must never propagate exceptions back to the caller.
        }
    }

    public async Task<string> GetUserMessageAsync(
        string errorCode, string language, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Exact match
        var message = await db.ErrorMessages
            .AsNoTracking()
            .Where(m => m.ErrorCode == errorCode && m.Language == language && m.IsActive)
            .Select(m => m.UserFriendlyMessage)
            .FirstOrDefaultAsync(ct);

        if (message is not null) return message;

        // Fallback to English
        if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            message = await db.ErrorMessages
                .AsNoTracking()
                .Where(m => m.ErrorCode == errorCode && m.Language == "en" && m.IsActive)
                .Select(m => m.UserFriendlyMessage)
                .FirstOrDefaultAsync(ct);
        }

        // Catch-all DEFAULT entry in the catalog
        message ??= await db.ErrorMessages
            .AsNoTracking()
            .Where(m => m.ErrorCode == "DEFAULT" && m.Language == language && m.IsActive)
            .Select(m => m.UserFriendlyMessage)
            .FirstOrDefaultAsync(ct);

        return message ?? "An unexpected error occurred. Please try again later.";
    }
}
