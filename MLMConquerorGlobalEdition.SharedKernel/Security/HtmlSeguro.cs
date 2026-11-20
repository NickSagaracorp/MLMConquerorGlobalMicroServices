using Ganss.Xss;

namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// La única política de HTML enriquecido de la solución (descripciones de productos y de
/// concursos): etiquetas de formato, enlaces http/https/mailto, imágenes, tablas y listas. Fuera
/// scripts, atributos de evento, <c>javascript:</c> y todo lo que no esté en la lista.
/// </summary>
/// <remarks>
/// Se aplica dos veces a propósito: al guardar (AdminAPI) y al pintar (componente
/// <c>HtmlSaneado</c>). La segunda cubre lo que se guardó antes de la primera o por otro camino.
/// </remarks>
public static class HtmlSeguro
{
    private static readonly HtmlSanitizer Sanitizador = Crear();

    public static string Limpiar(string? html) =>
        string.IsNullOrWhiteSpace(html) ? string.Empty : Sanitizador.Sanitize(html);

    private static HtmlSanitizer Crear()
    {
        var s = new HtmlSanitizer();

        s.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "strong", "b", "em", "i", "u", "s", "strike",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "ul", "ol", "li",
            "blockquote", "pre", "code",
            "a", "img",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td",
            "div", "span", "hr",
            "sub", "sup"
        })
            s.AllowedTags.Add(tag);

        s.AllowedAttributes.Clear();
        foreach (var attr in new[]
        {
            "href", "src", "alt", "title", "width", "height",
            "class", "style",
            "target", "rel",
            "colspan", "rowspan", "align"
        })
            s.AllowedAttributes.Add(attr);

        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("mailto");

        s.AllowedCssProperties.Clear();
        foreach (var prop in new[]
        {
            "color", "background-color", "font-size", "font-weight", "font-style",
            "text-align", "text-decoration", "line-height", "margin", "padding",
            "border", "border-radius", "width", "height", "max-width",
            "display", "float", "clear"
        })
            s.AllowedCssProperties.Add(prop);

        // Todo enlace sale con rel="noopener noreferrer".
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement anchor
                && !string.IsNullOrEmpty(anchor.Href))
                anchor.SetAttribute("rel", "noopener noreferrer");
        };

        return s;
    }
}
