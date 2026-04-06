using System.Net.Http.Headers;

namespace MLMConquerorGlobalEdition.BizCenterApp.Services;

/// <summary>
/// DelegatingHandler that attaches the current member's JWT Bearer token
/// to every outbound HTTP request made through the default HttpClient.
/// Registered as scoped to share the JwtAuthStateProvider lifetime.
/// </summary>
public class BizCenterAuthHandler : DelegatingHandler
{
    private readonly JwtAuthStateProvider _authProvider;

    public BizCenterAuthHandler(JwtAuthStateProvider authProvider)
    {
        _authProvider = authProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authProvider.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
