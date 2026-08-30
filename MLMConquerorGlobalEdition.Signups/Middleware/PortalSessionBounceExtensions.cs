namespace MLMConquerorGlobalEdition.Signups.Middleware;

/// <summary>El cableado del rebote, en un sitio, para que el <c>Program.cs</c> diga qué y no cómo.</summary>
public static class PortalSessionBounceExtensions
{
    /// <summary>La sección de configuración de la que sale todo.</summary>
    public const string ConfigurationSection = "PortalSessionBounce";

    /// <summary>
    /// Lee la configuración del rebote y registra lo que necesita.
    /// </summary>
    /// <remarks>
    /// SE VALIDA AL ARRANCAR y no en la primera visita. Un portal declarado con una dirección que no
    /// es absoluta produciría, en tiempo de ejecución, un rebote a ninguna parte: el visitante
    /// acabaría en una pantalla de error del navegador en vez de en el alta, y solo se sabría cuando
    /// alguien lo contara. Fallar aquí lo convierte en un arranque que no ocurre, que es un fallo que
    /// se ve.
    ///
    /// Lo que NO es un error: que no haya sección, o que la lista de portales esté vacía. Eso es un
    /// despliegue que no quiere rebote —el alta servida desde otro dominio registrable, donde no
    /// puede funcionar—, y tiene que poder arrancar.
    /// </remarks>
    public static IServiceCollection AddPortalSessionBounce(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ConfigurationSection)
            .Get<PortalSessionBounceOptions>() ?? new PortalSessionBounceOptions();

        Validate(options);

        services.AddSingleton(options);
        services.AddSingleton<PortalReachability>();

        // Cliente propio del sondeo. Sin cookies —no tiene nada que guardar de un portal— y sin
        // seguir redirecciones: lo que se pregunta es si hay alguien escuchando, y un 302 ya lo
        // contesta. Seguirlas sería pagar el viaje entero por una respuesta que ya se tiene.
        services.AddHttpClient(PortalReachability.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies        = false,
                AllowAutoRedirect = false
            });

        return services;
    }

    /// <summary>
    /// Mete el rebote en la tubería. Va delante de todo lo que pinte: el navegador tiene que irse y
    /// volver ANTES de que nadie vea un formulario, no a mitad de rellenarlo.
    /// </summary>
    public static IApplicationBuilder UsePortalSessionBounce(this IApplicationBuilder app) =>
        app.UseMiddleware<PortalSessionBounceMiddleware>();

    private static void Validate(PortalSessionBounceOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PublicBaseUrl) &&
            !Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"{ConfigurationSection}:PublicBaseUrl tiene que ser una dirección absoluta " +
                $"(https://alta.ejemplo.com), y es «{options.PublicBaseUrl}».");
        }

        foreach (var portal in options.Portals)
        {
            if (!Uri.TryCreate(portal.SignOutUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    $"{ConfigurationSection}:Portals el cierre de sesión de «{portal.Name}» tiene " +
                    $"que ser una dirección absoluta —el navegador va a otro origen—, y es " +
                    $"«{portal.SignOutUrl}».");
            }

            if (!string.IsNullOrWhiteSpace(portal.ProbeUrl) &&
                !Uri.TryCreate(portal.ProbeUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    $"{ConfigurationSection}:Portals el sondeo de «{portal.Name}» tiene que ser una " +
                    $"dirección absoluta, y es «{portal.ProbeUrl}».");
            }
        }
    }
}
