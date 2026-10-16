namespace MLMConquerorGlobalEdition.Repository.Services.Links;

/// <summary>
/// Plantillas de "Mis enlaces" (sección <c>MyLinks</c> del appsettings del BizCenter). Viven en
/// configuración y no en GlobalParameters a propósito (Plan J, J1): un enlace lo reparten todos los
/// miembros, así que cambiarlo pasa por revisión de código y por <see cref="MyLinksOptionsGuard"/>.
/// </summary>
public sealed class MyLinksOptions
{
    public const string SectionName = "MyLinks";

    /// <summary>Marcador que se sustituye por el slug del sitio replicado del miembro.</summary>
    public const string SlugToken = "{slug}";

    public const int MaxLinks = 30;

    public List<MyLinkTemplate> Links { get; set; } = new();
}

public sealed class MyLinkTemplate
{
    /// <summary>Identificador estable (letras, números y guiones). Sirve de clave en la pantalla.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Texto que ve el miembro. Son marcas: no se traduce.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>URL https absoluta; <c>{slug}</c> como mucho una vez y solo en la ruta o la query.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Lleva el slug del miembro: es un enlace personal.</summary>
    public bool IsPersonal => Url.Contains(MyLinksOptions.SlugToken, StringComparison.Ordinal);
}
