using System.Security.Claims;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Middleware;

public class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IErrorTrackingService _errorTracking;
    private readonly IHostEnvironment _env;

    public DomainExceptionMiddleware(
        RequestDelegate next,
        IErrorTrackingService errorTracking,
        IHostEnvironment env)
    {
        _next = next;
        _errorTracking = errorTracking;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            var language = DetectLanguage(context);
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            var memberId = context.User.FindFirstValue("memberId");

            await _errorTracking.TrackAsync(
                apiName: _env.ApplicationName,
                endpoint: endpoint,
                codeSection: ex.GetType().Name,
                errorCode: ex.Code,
                exception: ex,
                memberId: memberId,
                traceId: context.TraceIdentifier,
                language: language,
                httpStatusCode: 400);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                ex.Code,
                ex.Message,
                context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(response);
        }
        catch (Exception ex)
        {
            var language = DetectLanguage(context);
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            var memberId = context.User.FindFirstValue("memberId");

            await _errorTracking.TrackAsync(
                apiName: _env.ApplicationName,
                endpoint: endpoint,
                codeSection: "UnhandledException",
                errorCode: "INTERNAL_ERROR",
                exception: ex,
                memberId: memberId,
                traceId: context.TraceIdentifier,
                language: language,
                httpStatusCode: 500);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "INTERNAL_ERROR",
                "An unexpected error occurred.",
                context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(response);
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
