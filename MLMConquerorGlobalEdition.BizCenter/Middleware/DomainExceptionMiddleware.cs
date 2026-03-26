using System.Security.Claims;
using System.Text.Json;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Middleware;

public class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DomainExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DomainExceptionMiddleware(
        RequestDelegate next,
        ILogger<DomainExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    // IErrorTrackingService is scoped — injected per-request via method injection
    public async Task InvokeAsync(HttpContext context, IErrorTrackingService errorTracking)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            var language = DetectLanguage(context);
            var userMessage = await errorTracking.GetUserMessageAsync(ex.Code, language);

            _logger.LogWarning("Domain exception [{Code}]: {Message}", ex.Code, ex.Message);

            await errorTracking.TrackAsync(
                apiName: _env.ApplicationName,
                endpoint: $"{context.Request.Method} {context.Request.Path}",
                codeSection: "DomainException",
                errorCode: ex.Code,
                exception: ex,
                memberId: context.User.FindFirstValue("memberId"),
                traceId: context.TraceIdentifier,
                language: language,
                httpStatusCode: StatusCodes.Status422UnprocessableEntity);

            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/json";
            var response = ApiResponse<object>.Fail(ex.Code, ex.Message, context.TraceIdentifier, userMessage);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOpts));
        }
        catch (Exception ex)
        {
            var language = DetectLanguage(context);
            var userMessage = await errorTracking.GetUserMessageAsync("INTERNAL_ERROR", language);

            _logger.LogError(ex, "Unhandled exception");

            await errorTracking.TrackAsync(
                apiName: _env.ApplicationName,
                endpoint: $"{context.Request.Method} {context.Request.Path}",
                codeSection: "UnhandledException",
                errorCode: "INTERNAL_ERROR",
                exception: ex,
                memberId: context.User.FindFirstValue("memberId"),
                traceId: context.TraceIdentifier,
                language: language,
                httpStatusCode: StatusCodes.Status500InternalServerError);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var response = ApiResponse<object>.Fail(
                "INTERNAL_ERROR", "An unexpected error occurred.",
                context.TraceIdentifier, userMessage);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOpts));
        }
    }

    private static string DetectLanguage(HttpContext context)
    {
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
