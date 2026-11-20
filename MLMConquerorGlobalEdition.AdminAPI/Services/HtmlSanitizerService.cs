using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// Limpia el HTML enriquecido antes de guardarlo. La política vive en <see cref="HtmlSeguro"/>,
/// la misma que aplican los portales al pintarlo.
/// </summary>
public class HtmlSanitizerService : IHtmlSanitizerService
{
    public string Sanitize(string? html) => HtmlSeguro.Limpiar(html);
}
