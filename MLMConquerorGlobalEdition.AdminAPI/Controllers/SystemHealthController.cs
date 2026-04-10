using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/system")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class SystemHealthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public SystemHealthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        AppDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _db = db;
    }

    /// <summary>
    /// Returns the health status of every service in the platform.
    /// Each remote /health endpoint is called in parallel with a 5-second timeout.
    /// Overall status is Healthy (all up) | Degraded (some up) | Unhealthy (all down).
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetSystemHealth(CancellationToken ct = default)
    {
        // AdminAPI self-check (local DB)
        var adminDbUp = await _db.Database.CanConnectAsync(ct);
        var adminStatus = new ServiceHealthResult
        {
            Service = "MLMConquerorGlobalEdition.AdminAPI",
            Status  = adminDbUp ? "Healthy" : "Unhealthy",
            Checks  = new Dictionary<string, string>
            {
                ["database"] = adminDbUp ? "Healthy" : "Unhealthy"
            }
        };

        // Remote services
        var serviceNames = new[]
        {
            "BizCenter",
            "CommissionEngine",
            "RankEngine",
            "SignupAPI",
            "TicketManagementSystem",
            "Billing",
            "SharedAPICenter"
        };

        var remoteTasks = serviceNames
            .Select(name => PollServiceAsync(name, ct))
            .ToArray();

        var remoteResults = await Task.WhenAll(remoteTasks);

        // Aggregate
        var all = remoteResults.Prepend(adminStatus).ToList();

        var overallStatus = all.All(r => r.Status == "Healthy") ? "Healthy"
            : all.Any(r => r.Status == "Healthy")               ? "Degraded"
            :                                                       "Unhealthy";

        var data = new SystemHealthResponse
        {
            Overall   = overallStatus,
            Services  = all.ToDictionary(r => r.Service),
            CheckedAt = DateTime.UtcNow
        };

        return Ok(ApiResponse<SystemHealthResponse>.Ok(data));
    }

    // ──────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────

    private async Task<ServiceHealthResult> PollServiceAsync(string serviceName, CancellationToken ct)
    {
        var baseUrl = _config[$"Services:{serviceName}"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new ServiceHealthResult
            {
                Service = serviceName,
                Status  = "Unknown",
                Error   = "Base URL not configured in Services section."
            };
        }

        var client = _httpClientFactory.CreateClient("HealthCheck");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var url      = $"{baseUrl.TrimEnd('/')}/health";
            var response = await client.GetAsync(url, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                return new ServiceHealthResult
                {
                    Service = serviceName,
                    Status  = "Healthy"
                };
            }

            return new ServiceHealthResult
            {
                Service = serviceName,
                Status  = "Unhealthy",
                Error   = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
            };
        }
        catch (OperationCanceledException)
        {
            return new ServiceHealthResult
            {
                Service = serviceName,
                Status  = "Unreachable",
                Error   = "Request timed out (5 s)."
            };
        }
        catch (HttpRequestException ex)
        {
            return new ServiceHealthResult
            {
                Service = serviceName,
                Status  = "Unreachable",
                Error   = ex.Message
            };
        }
    }
}

// ──────────────────────────────────────────────────────────────
//  Response models (file-scoped — no extra files for DTOs used
//  only by this controller)
// ──────────────────────────────────────────────────────────────

public sealed class SystemHealthResponse
{
    public string Overall { get; init; } = string.Empty;
    public Dictionary<string, ServiceHealthResult> Services { get; init; } = new();
    public DateTime CheckedAt { get; init; }
}

public sealed class ServiceHealthResult
{
    public string Service { get; init; } = string.Empty;
    public string Status  { get; init; } = string.Empty;
    public Dictionary<string, string>? Checks { get; init; }
    public string? Error  { get; init; }
}
