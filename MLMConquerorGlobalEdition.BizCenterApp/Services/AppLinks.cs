namespace MLMConquerorGlobalEdition.BizCenterApp.Services;

/// <summary>
/// Las direcciones de FUERA de esta aplicación. Se declaran una vez en <c>MauiProgram</c>, que es la
/// raíz de composición, y no dentro de la pantalla que las usa.
/// </summary>
/// <remarks>
/// Hoy solo hay una: la aplicación de alta. El centro de negocios ya no tiene pantalla de alta
/// propia —la que tenía mandaba un campo que el contrato de la API no tiene y guardaba las altas sin
/// patrocinador—, así que el enlace del login sale de esta aplicación hacia otra.
///
/// Es un objeto y no una constante porque el valor cambia de un entorno a otro y tiene que poder
/// salir de configuración. En una MAUI no hay <c>appsettings.json</c> por defecto, así que
/// <c>MauiProgram</c> lo lee de <c>builder.Configuration</c> con un valor de desarrollo por defecto:
/// el día que esta aplicación tenga configuración de verdad, lo único que cambia es esa línea.
/// </remarks>
public sealed record AppLinks
{
    /// <summary>Dónde vive el asistente de alta, que es el único sitio donde se dan las altas.</summary>
    public string? SignupAppUrl { get; init; }
}
