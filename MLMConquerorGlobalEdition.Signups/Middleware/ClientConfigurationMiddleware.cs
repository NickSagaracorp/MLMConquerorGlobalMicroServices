using System.Text.Json;

namespace MLMConquerorGlobalEdition.Signups.Middleware;

/// <summary>
/// Sirve el <c>appsettings.json</c> del cliente WebAssembly desde la configuración del servidor, en
/// vez del archivo fijo de <c>Signups.Client/wwwroot</c>. Así la misma imagen sirve en desarrollo,
/// en Docker local y en EKS: la dirección de SignupAPI que usa el navegador se pone al arrancar.
/// <para>
/// El navegador habla directo con SignupAPI, así que necesita su dirección PÚBLICA
/// (<see cref="PublicUrlKey"/>). <c>SignupApiBaseUrl</c> es la que usa el servidor para el prerender
/// y en un clúster es la interna; solo se usa para el navegador si no hay dirección pública.
/// </para>
/// </summary>
public sealed class ClientConfigurationMiddleware
{
    public const string RequestPath   = "/appsettings.json";
    public const string PublicUrlKey  = "SignupApiPublicUrl";
    public const string ServerUrlKey  = "SignupApiBaseUrl";
    public const string DefaultUrl    = "https://localhost:7005";

    private readonly RequestDelegate _next;
    private readonly byte[] _body;

    public ClientConfigurationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;

        var url = configuration[PublicUrlKey];
        if (string.IsNullOrWhiteSpace(url)) url = configuration[ServerUrlKey];
        if (string.IsNullOrWhiteSpace(url)) url = DefaultUrl;

        _body = JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string> { [ServerUrlKey] = url.Trim() });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && request.Path.Equals(RequestPath, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode  = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.ContentLength = _body.Length;
            if (HttpMethods.IsGet(request.Method))
                await context.Response.Body.WriteAsync(_body, context.RequestAborted);
            return;
        }

        await _next(context);
    }
}

public static class ClientConfigurationExtensions
{
    /// <summary>Va delante de los estáticos: si no, se serviría el archivo fijo del cliente.</summary>
    public static IApplicationBuilder UseClientConfiguration(this IApplicationBuilder app)
        => app.UseMiddleware<ClientConfigurationMiddleware>();
}
