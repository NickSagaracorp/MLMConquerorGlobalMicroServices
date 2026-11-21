using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedComponents.Lite;

/// <summary>
/// El único sitio de los portales y del alta que pinta HTML que viene de la base (descripciones de
/// productos y de concursos). Lo que llega en <see cref="Html"/> pasa siempre por
/// <see cref="HtmlSeguro.Limpiar"/>, la misma política con la que AdminAPI lo limpia al guardarlo:
/// lo que se guardó antes de esa limpieza, o por otro camino, tampoco ejecuta nada aquí.
/// </summary>
/// <remarks>
/// Es una clase y no un <c>.razor</c> para que el HTML sin limpiar no tenga por dónde llegar al
/// árbol: el único texto que se entrega como marcado es el que devolvió el saneador, y no existe
/// un <c>MarkupString</c> que otro código pueda reutilizar. Para texto plano no se usa este
/// componente: se pinta con <c>@</c> normal.
/// </remarks>
public sealed class HtmlSaneado : ComponentBase
{
    [Parameter] public string? Html { get; set; }

    private string _limpio = string.Empty;
    private string? _ultimo;

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(Html, _ultimo))
        {
            _ultimo = Html;
            _limpio = HtmlSeguro.Limpiar(Html);
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, _limpio);
}
