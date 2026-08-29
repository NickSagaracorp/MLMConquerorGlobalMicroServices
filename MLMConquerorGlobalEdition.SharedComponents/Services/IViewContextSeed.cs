using System.Security.Claims;

namespace MLMConquerorGlobalEdition.SharedComponents.Services;

/// <summary>
/// De dónde saca <see cref="ViewContextService"/> su estado inicial cuando nadie se lo ha puesto
/// todavía a mano.
///
/// Existe por lo mismo que <c>IAccessTokenProvider</c> en ClientCore: el dato que hace falta —quién
/// mira y qué pantalla se está sirviendo— lo tiene cada anfitrión en un sitio distinto, y el que lo
/// tiene en el <c>HttpContext</c> es solo uno de los cuatro. Un portal ASP.NET Core lo saca de la
/// petición en curso; una aplicación MAUI no tiene petición ninguna y lo recibe entero por
/// <see cref="ViewContextService.SetContext"/> desde su inicializador, después de leer el JWT.
///
/// La versión anterior de <see cref="ViewContextService"/> pedía <c>IHttpContextAccessor</c> por
/// constructor. Eso ataba la biblioteca entera al framework compartido de ASP.NET Core —que es lo
/// que una MAUI no puede referenciar— y además dejaba a las dos aplicaciones móviles con un
/// servicio que ni siquiera se podía construir en el dispositivo: <c>AddSharedComponents()</c> lo
/// registraba, ningún <c>MauiProgram</c> registraba el accessor, y el ensamblado que declara ese
/// tipo no viajaba en el paquete. Compilaba y reventaba al arrancar.
/// </summary>
public interface IViewContextSeed
{
    /// <summary>
    /// El usuario que el anfitrión ya tiene identificado, o <c>null</c> si no hay ninguno.
    /// <c>ClaimsPrincipal</c> es del runtime base, no de ASP.NET Core, así que sirve igual a los
    /// cuatro anfitriones.
    /// </summary>
    ClaimsPrincipal? GetUser();

    /// <summary>
    /// La ruta de la pantalla que se está sirviendo, o <c>null</c> si el anfitrión no tiene una.
    /// Es lo que distingue el contexto de administración del de miembro cuando los dos portales
    /// comparten dominio.
    /// </summary>
    string? GetPath();
}

/// <summary>
/// La semilla vacía: no sabe nada y lo dice.
///
/// Es la que se registra por defecto en <c>AddSharedComponents()</c>, y la que usan las dos MAUI.
/// Ahí no hay nada que auto-descubrir porque el contexto llega completo por
/// <see cref="ViewContextService.SetContext"/> en cuanto el inicializador lee el JWT; devolver
/// <c>null</c> en las dos consultas deja exactamente ese comportamiento.
/// </summary>
public sealed class NullViewContextSeed : IViewContextSeed
{
    public ClaimsPrincipal? GetUser() => null;
    public string?          GetPath() => null;
}
