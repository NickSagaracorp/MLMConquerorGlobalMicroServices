using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// El lado web de <see cref="IViewContextSeed"/>: el usuario y la ruta salen del
/// <c>HttpContext</c> de la petición en curso.
///
/// Es literalmente lo que <c>ViewContextService</c> hacía por dentro antes de que la semilla se
/// abstrajera; se movió aquí entero, sin cambiarle el comportamiento, porque
/// <c>IHttpContextAccessor</c> es exactamente la dependencia de alojamiento web que no podía seguir
/// en una biblioteca que también compilan dos aplicaciones MAUI.
///
/// De ámbito de petición, como todo lo que lee del <c>HttpContext</c>.
/// </summary>
public sealed class HttpContextViewContextSeed : IViewContextSeed
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextViewContextSeed(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    public ClaimsPrincipal? GetUser() => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public string? GetPath() => _httpContextAccessor.HttpContext?.Request.Path.Value;
}
