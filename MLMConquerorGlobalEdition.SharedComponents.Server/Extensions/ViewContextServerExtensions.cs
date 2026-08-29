using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;

/// <summary>
/// El alta de la mitad web del contexto de vista.
/// </summary>
public static class ViewContextServerExtensions
{
    /// <summary>
    /// Hace que <c>ViewContextService</c> se auto-inicialice desde el <c>HttpContext</c> de la
    /// petición. Un portal web lo llama; una aplicación MAUI, no.
    /// </summary>
    /// <remarks>
    /// Con <c>Replace</c> y no con <c>Add</c> a propósito: <c>AddSharedComponents()</c> deja puesta
    /// la semilla vacía con <c>TryAdd</c>, y así da igual cuál de los dos métodos llame antes el
    /// <c>Program.cs</c> del portal. Si este va primero, <c>Replace</c> se comporta como un
    /// <c>Add</c> y el <c>TryAdd</c> de después no pisa nada; si va segundo, sustituye la vacía.
    /// El orden de dos líneas de <c>Program.cs</c> no debería decidir de dónde sale el usuario.
    /// </remarks>
    public static IServiceCollection AddHttpContextViewContextSeed(this IServiceCollection services)
    {
        // Dependencia dura de la semilla; TryAdd por dentro, así que no estorba al portal que ya
        // lo llame por su cuenta.
        services.AddHttpContextAccessor();

        services.Replace(ServiceDescriptor.Scoped<IViewContextSeed, HttpContextViewContextSeed>());
        return services;
    }
}
