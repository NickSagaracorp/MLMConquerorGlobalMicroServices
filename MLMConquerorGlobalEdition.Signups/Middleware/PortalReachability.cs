using System.Collections.Concurrent;

namespace MLMConquerorGlobalEdition.Signups.Middleware;

/// <summary>
/// ¿ESTÁ EN PIE ESE PORTAL? Es la pregunta que decide si el alta puede permitirse mandar el
/// navegador allí, y existe por una razón sola: EL ALTA NO SE PUEDE QUEDAR BLOQUEADA.
/// </summary>
/// <remarks>
/// EL PROBLEMA, dicho entero. El rebote es una navegación del navegador —tiene que serlo: es lo
/// único que arrastra la cookie del portal—, y una navegación no tiene plan B. Si el portal está
/// caído, el navegador se queda con la pantalla de error del navegador y la persona que venía a
/// darse de alta se queda sin darse de alta. Eso es cambiar un riesgo de seguridad, que ocurre a
/// veces, por una caída del negocio, que ocurriría siempre; y en un evento, para toda la sala a la
/// vez.
///
/// POR QUÉ NO HAY OTRA SALIDA. Un temporizador en la página no sirve: cuando el navegador ya se ha
/// ido, esa página ya no corre. Y un <c>fetch</c> al portal desde el alta tampoco: es una petición
/// entre sitios y la cookie <c>SameSite=Strict</c> del portal no viajaría, que es justo por lo que
/// esto es una navegación y no una llamada. Así que la decisión hay que tomarla ANTES de soltar el
/// navegador, y aquí, en el servidor del alta, es donde se puede tomar.
///
/// LO QUE SE DECIDE CUANDO NO CONTESTA: se salta ese portal y el alta se abre. Es fallar hacia el
/// lado del negocio a sabiendas — con el portal caído tampoco había mucho que temer de su sesión, y
/// la sesión seguirá ahí cuando vuelva. Queda un aviso en el registro, que es lo que permite
/// enterarse de que la protección estuvo apagada un rato.
///
/// CUALQUIER RESPUESTA HTTP CUENTA COMO "EN PIE", incluido un 404 o un 500. Lo que se pregunta no es
/// si el portal funciona bien: es si hay algo escuchando que vaya a atender la navegación en vez de
/// dejar al navegador con un error de conexión.
/// </remarks>
public sealed class PortalReachability
{
    /// <summary>El nombre del cliente HTTP del sondeo. Suyo propio: lleva su tiempo de espera.</summary>
    public const string HttpClientName = "PortalProbe";

    private readonly IHttpClientFactory                            _clients;
    private readonly PortalSessionBounceOptions                    _options;
    private readonly TimeProvider                                  _clock;
    private readonly ILogger<PortalReachability>                   _logger;
    private readonly ConcurrentDictionary<string, CachedAnswer>    _answers = new(StringComparer.Ordinal);

    public PortalReachability(
        IHttpClientFactory          clients,
        PortalSessionBounceOptions  options,
        ILogger<PortalReachability> logger,
        TimeProvider?               clock = null)
    {
        _clients = clients;
        _options = options;
        _logger  = logger;
        _clock   = clock ?? TimeProvider.System;
    }

    /// <summary>¿Se puede mandar el navegador a este portal ahora mismo?</summary>
    public async Task<bool> IsUpAsync(PortalStopOptions portal, CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(portal.ProbeUrl) ? portal.SignOutUrl : portal.ProbeUrl!;
        if (string.IsNullOrWhiteSpace(url)) return false;

        var now = _clock.GetUtcNow();

        if (_answers.TryGetValue(url, out var cached) && cached.GoodUntil > now)
            return cached.Up;

        var up = await AskAsync(portal, url, ct);

        _answers[url] = new CachedAnswer(
            up, now.AddSeconds(Math.Max(0, _options.ProbeCacheSeconds)));

        return up;
    }

    private async Task<bool> AskAsync(PortalStopOptions portal, string url, CancellationToken ct)
    {
        // Propio y no el de la petición: el sondeo tiene su propio tiempo de espera y no puede
        // heredar la vida de la navegación que lo provocó.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(
            Math.Max(1, _options.ProbeTimeoutMilliseconds)));

        try
        {
            var client = _clients.CreateClient(HttpClientName);

            // Sin leer el cuerpo: con las cabeceras basta para saber que hay alguien al otro lado, y
            // así un portal que devuelva una página entera no cuesta lo que cuesta esa página.
            using var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, url),
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Si quien canceló fue el navegador —se fue de la página—, esto no es una caída del
            // portal y no hay nada que avisar.
            if (ct.IsCancellationRequested) return false;

            _logger.LogWarning(
                ex,
                "El portal {Portal} no contesta en {Url}: se abre el alta SIN cerrar su sesión. La " +
                "protección queda apagada para ese portal hasta que vuelva.",
                string.IsNullOrWhiteSpace(portal.Name) ? url : portal.Name,
                url);

            return false;
        }
    }

    private readonly record struct CachedAnswer(bool Up, DateTimeOffset GoodUntil);
}
