using Microsoft.AspNetCore.Components.Server.Circuits;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// La puerta por la que un servicio construido FUERA del circuito puede alcanzar los servicios de
/// DENTRO del circuito del usuario.
/// </summary>
/// <remarks>
/// ESTO EXISTE POR UN FALLO CONCRETO Y LLEVA AÑOS DE VIDA EN ÉL. <c>IHttpClientFactory</c> arma la
/// cadena de manejadores de cada cliente HTTP UNA vez, en un ámbito de DI propio, y la reutiliza
/// para todas las llamadas de todos los usuarios. Un <c>DelegatingHandler</c> registrado con
/// <c>AddHttpMessageHandler</c> recibe por su constructor los servicios de ESE ámbito, no los del
/// circuito de quien hace la llamada. Con lo cual:
///
///   • el <c>NavigationManager</c> inyectado no es el de ninguna pantalla, y su <c>NavigateTo</c>
///     lanza "'RemoteNavigationManager' has not been initialized";
///   • el <c>AuthenticationStateProvider</c> inyectado está vacío y no lo escucha nadie.
///
/// La forma soportada de cruzar esa frontera es la de aquí: un accesorio con un
/// <see cref="AsyncLocal{T}"/> que el propio circuito rellena mientras atiende actividad entrante
/// —un clic, un evento, una llamada de vuelta de JavaScript, una lectura de un grid—, de modo que
/// todo lo que cuelgue de esa llamada, manejadores HTTP incluidos, ve el proveedor de servicios
/// bueno.
///
/// LO QUE ESTO NO CUBRE, y por eso no es la única pieza del arreglo: el primer render de un circuito
/// recién abierto no es actividad ENTRANTE, así que ahí <see cref="Services"/> vuelve nulo. Ese
/// hueco lo tapa <see cref="SessionExpiryMiddleware"/>, que mira la sesión en la petición HTTP —una
/// recarga, un enlace, un marcador— antes de que el circuito llegue a existir. Entre las dos piezas
/// no queda camino por el que una sesión muerta acabe pintando un 401 en la pantalla.
/// </remarks>
public sealed class CircuitServicesAccessor
{
    private readonly AsyncLocal<IServiceProvider?> _services = new();

    /// <summary>
    /// Los servicios del circuito que está atendiendo la llamada en curso, o null si esta llamada
    /// no viene de un circuito (render estático, petición HTTP normal, trabajo en segundo plano).
    /// </summary>
    public IServiceProvider? Services
    {
        get => _services.Value;
        set => _services.Value = value;
    }
}

/// <summary>
/// Quien rellena el <see cref="CircuitServicesAccessor"/>: envuelve toda la actividad entrante del
/// circuito y deja a mano sus servicios mientras dura.
/// </summary>
/// <remarks>
/// Se construye una vez por circuito y desde el ámbito del circuito, así que el
/// <see cref="IServiceProvider"/> que recibe es exactamente el que hay que publicar.
/// </remarks>
internal sealed class CircuitServicesAccessorHandler : CircuitHandler
{
    private readonly CircuitServicesAccessor _accessor;
    private readonly IServiceProvider        _circuitServices;

    public CircuitServicesAccessorHandler(
        CircuitServicesAccessor accessor, IServiceProvider circuitServices)
    {
        _accessor        = accessor;
        _circuitServices = circuitServices;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            _accessor.Services = _circuitServices;
            try
            {
                await next(context);
            }
            finally
            {
                // El valor no se escapa de este flujo asíncrono, pero limpiarlo evita dejar vivo un
                // proveedor de servicios de un circuito ya cerrado si el hilo se reutiliza.
                _accessor.Services = null;
            }
        };
}
