using System.Net.Http.Headers;

namespace MLMConquerorGlobalEdition.AdminApp.Services;

/// <summary>
/// DelegatingHandler that attaches the admin's effective JWT Bearer token
/// (admin token or impersonation token) to every outbound HTTP request
/// made through the default HttpClient used by SharedComponents.
/// </summary>
public class AdminAuthHandler : DelegatingHandler
{
    private readonly AdminJwtAuthStateProvider _authProvider;

    public AdminAuthHandler(AdminJwtAuthStateProvider authProvider)
    {
        _authProvider = authProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authProvider.GetEffectiveTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
