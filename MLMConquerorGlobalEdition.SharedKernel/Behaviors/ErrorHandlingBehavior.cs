using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedKernel.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps every handler in a try/catch.
/// On exception:
///   1. Detects language from JWT "lang" claim or Accept-Language header.
///   2. Persists the exception via IErrorTrackingService (isolated DB scope).
///   3. Returns Result&lt;T&gt;.Failure("INTERNAL_ERROR", ...) so controllers never
///      receive a raw exception — keeping the Result pattern intact.
///
/// Register once per project:
///   cfg.AddBehavior(typeof(IPipelineBehavior&lt;,&gt;), typeof(ErrorHandlingBehavior&lt;,&gt;));
/// </summary>
public class ErrorHandlingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IErrorTrackingService _errorTracking;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment _env;
    private readonly Microsoft.Extensions.Logging.ILogger<ErrorHandlingBehavior<TRequest, TResponse>> _logger;

    public ErrorHandlingBehavior(
        IErrorTrackingService errorTracking,
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment env,
        Microsoft.Extensions.Logging.ILogger<ErrorHandlingBehavior<TRequest, TResponse>> logger)
    {
        _errorTracking = errorTracking;
        _httpContextAccessor = httpContextAccessor;
        _env = env;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            if (_env.IsDevelopment())
                _logger.LogError(ex, "[ErrorHandlingBehavior] Unhandled exception in {Request}", typeof(TRequest).Name);

            var httpCtx = _httpContextAccessor.HttpContext;
            var language = DetectLanguage(httpCtx);
            var endpoint = httpCtx is not null
                ? $"{httpCtx.Request.Method} {httpCtx.Request.Path}"
                : string.Empty;

            await _errorTracking.TrackAsync(
                apiName: _env.ApplicationName,
                endpoint: endpoint,
                codeSection: typeof(TRequest).Name,
                errorCode: "INTERNAL_ERROR",
                exception: ex,
                memberId: httpCtx?.User.FindFirstValue("memberId"),
                requestData: null,
                traceId: httpCtx?.TraceIdentifier,
                language: language,
                httpStatusCode: 500,
                ct: default); // use default — original ct may already be cancelled

            // Return Result<T>.Failure() without leaking exception details to the caller
            var responseType = typeof(TResponse);
            if (responseType.IsGenericType &&
                responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var failureMethod = responseType.GetMethod(
                    "Failure",
                    BindingFlags.Public | BindingFlags.Static,
                    new[] { typeof(string), typeof(string) });

                if (failureMethod is not null)
                {
                    var failure = failureMethod.Invoke(
                        null,
                        new object[] { "INTERNAL_ERROR", "An unexpected error occurred." });
                    return (TResponse)failure!;
                }
            }

            // Non-Result<T> response type — let the middleware handle it
            throw;
        }
    }

    private static string DetectLanguage(HttpContext? context)
    {
        if (context is null) return "en";

        // Prefer explicit "lang" claim over Accept-Language header
        var claim = context.User.FindFirstValue("lang");
        if (!string.IsNullOrWhiteSpace(claim)) return claim.ToLowerInvariant();

        var header = context.Request.Headers.AcceptLanguage.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header))
        {
            var first = header.Split(',')[0].Split(';')[0].Trim();
            if (first.Length >= 2) return first[..2].ToLowerInvariant();
        }

        return "en";
    }
}
