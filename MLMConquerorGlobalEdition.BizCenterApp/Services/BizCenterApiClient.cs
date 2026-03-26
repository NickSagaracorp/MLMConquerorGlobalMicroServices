using System.Net.Http.Headers;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenterApp.Services;

/// <summary>
/// Typed HTTP client for the BizCenter REST API.
/// Automatically attaches the Bearer token from SecureStorage on each request.
/// </summary>
public class BizCenterApiClient
{
    private readonly HttpClient _http;
    private readonly JwtAuthStateProvider _authProvider;

    public BizCenterApiClient(HttpClient http, JwtAuthStateProvider authProvider)
    {
        _http = http;
        _authProvider = authProvider;
    }

    public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        await AttachTokenAsync();
        var response = await _http.GetFromJsonAsync<ApiResponse<T>>(url, ct);
        return response is { Success: true } ? response.Data : default;
    }

    public async Task<ApiResponse<T>?> PostAsync<T>(string url, object body, CancellationToken ct = default)
    {
        await AttachTokenAsync();
        var result = await _http.PostAsJsonAsync(url, body, ct);
        return await result.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
    }

    public async Task<ApiResponse<T>?> PutAsync<T>(string url, object body, CancellationToken ct = default)
    {
        await AttachTokenAsync();
        var result = await _http.PutAsJsonAsync(url, body, ct);
        return await result.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
    }

    private async Task AttachTokenAsync()
    {
        var token = await _authProvider.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
