using System.Net.Http.Headers;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminApp.Services;

/// <summary>
/// Typed HTTP client for the AdminAPI REST endpoints.
/// Always attaches the admin Bearer token (not impersonation token).
/// </summary>
public class AdminApiClient
{
    private readonly HttpClient                _http;
    private readonly AdminJwtAuthStateProvider _authProvider;

    public AdminApiClient(HttpClient http, AdminJwtAuthStateProvider authProvider)
    {
        _http         = http;
        _authProvider = authProvider;
    }

    public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        await AttachAdminTokenAsync();
        var response = await _http.GetFromJsonAsync<ApiResponse<T>>(url, ct);
        return response is { Success: true } ? response.Data : default;
    }

    public async Task<ApiResponse<T>?> PostAsync<T>(string url, object body, CancellationToken ct = default)
    {
        await AttachAdminTokenAsync();
        var result = await _http.PostAsJsonAsync(url, body, ct);
        return await result.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
    }

    public async Task<ApiResponse<T>?> PutAsync<T>(string url, object body, CancellationToken ct = default)
    {
        await AttachAdminTokenAsync();
        var result = await _http.PutAsJsonAsync(url, body, ct);
        return await result.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
    }

    public async Task<ApiResponse<T>?> DeleteAsync<T>(string url, CancellationToken ct = default)
    {
        await AttachAdminTokenAsync();
        var result = await _http.DeleteAsync(url, ct);
        return await result.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
    }

    /// <summary>
    /// Starts impersonation. Returns the impersonation token for the caller to store.
    /// </summary>
    public async Task<ImpersonationResult?> StartImpersonationAsync(string memberId, CancellationToken ct = default)
    {
        await AttachAdminTokenAsync();
        var result = await _http.PostAsJsonAsync(
            $"api/v1/admin/members/{memberId}/impersonate", new { }, ct);
        var response = await result.Content.ReadFromJsonAsync<ApiResponse<ImpersonationResult>>(cancellationToken: ct);
        return response?.Data;
    }

    public async Task ExitImpersonationAsync(CancellationToken ct = default)
    {
        await AttachAdminTokenAsync();
        await _http.PostAsJsonAsync("api/v1/admin/impersonation/exit", new { }, ct);
    }

    private async Task AttachAdminTokenAsync()
    {
        // Always use the admin's own token for AdminAPI calls (not impersonation token)
        var token = await _authProvider.GetAdminTokenAsync();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public record ImpersonationResult(string AccessToken, string MemberId, string MemberName, bool IsReadOnly, DateTime ExpiresAt);
}
