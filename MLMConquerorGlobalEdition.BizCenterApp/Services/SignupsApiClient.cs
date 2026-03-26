using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenterApp.Services;

/// <summary>
/// Typed HTTP client for the public Signups API (unauthenticated endpoints).
/// Base address: https://localhost:7147
/// </summary>
public class SignupsApiClient
{
    private readonly HttpClient _http;

    public SignupsApiClient(HttpClient http) => _http = http;

    public async Task<ApiResponse<T>?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<ApiResponse<T>>(url, ct);
        return response;
    }

    public async Task<ApiResponse<T>?> PostAsync<T>(string url, object body, CancellationToken ct = default)
    {
        var result = await _http.PostAsJsonAsync(url, body, ct);
        return await result.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
    }
}
