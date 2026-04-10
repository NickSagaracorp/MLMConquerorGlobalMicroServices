using Ganss.Xss;

namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// Wrapper around Ganss.Xss.HtmlSanitizer.
/// Configured with an allowlist appropriate for rich product descriptions:
/// standard formatting tags, links (href sanitised to http/https only),
/// images (src sanitised), tables, and lists.
/// All JavaScript event attributes and javascript: URIs are stripped.
/// </summary>
public class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        _sanitizer.AllowedTags.Clear();
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
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[]
        {
            "href", "src", "alt", "title", "width", "height",
            "class", "style",
            "target", "rel",
            "colspan", "rowspan", "align"
        })
        {
            _sanitizer.AllowedAttributes.Add(attr);
        }

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        _sanitizer.AllowedCssProperties.Clear();
        foreach (var prop in new[]
        {
            "color", "background-color", "font-size", "font-weight", "font-style",
            "text-align", "text-decoration", "line-height", "margin", "padding",
            "border", "border-radius", "width", "height", "max-width",
            "display", "float", "clear"
        })
        {
            _sanitizer.AllowedCssProperties.Add(prop);
        }

        // Enforce rel="noopener noreferrer" on any external link
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement anchor &&
                !string.IsNullOrEmpty(anchor.Href))
            {
                anchor.SetAttribute("rel", "noopener noreferrer");
            }
        };
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return _sanitizer.Sanitize(html);
    }
}
