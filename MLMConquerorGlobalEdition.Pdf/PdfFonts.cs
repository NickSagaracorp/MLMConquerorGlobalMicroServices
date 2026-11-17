using System.Reflection;
using PdfSharp.Fonts;

namespace MLMConquerorGlobalEdition.Pdf;

/// <summary>
/// Las fuentes de los PDF del sistema, incrustadas en este ensamblado. PDFsharp no trae fuentes y,
/// fuera de Windows, no encuentra ninguna por su cuenta: sin un resolvedor, el primer
/// <c>XFont</c> lanza.
/// </summary>
public static class PdfFonts
{
    /// <summary>Con serifa, métricas de Times.</summary>
    public const string Serif = "Liberation Serif";

    /// <summary>Sin serifa, métricas de Helvetica.</summary>
    public const string Sans = "Liberation Sans";

    private static readonly object Gate = new();

    /// <summary>
    /// Registra el resolvedor una sola vez por proceso. PDFsharp solo admite fijarlo antes de usar
    /// la primera fuente, así que cada renderizador lo llama en su constructor.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (GlobalFontSettings.FontResolver is EmbeddedFontResolver) return;

        lock (Gate)
        {
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
        }
    }

    private sealed class EmbeddedFontResolver : IFontResolver
    {
        private const string SerifRegular = "LiberationSerif-Regular";
        private const string SansRegular  = "LiberationSans-Regular";
        private const string SansBold     = "LiberationSans-Bold";

        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        {
            // Serif solo existe en regular: el certificado no usa otra. Una negrita pedida a la
            // serifa la simula PDFsharp. Cualquier otra familia cae en la sin serifa.
            if (string.Equals(familyName, Serif, StringComparison.OrdinalIgnoreCase))
                return new FontResolverInfo(SerifRegular, mustSimulateBold: bold, mustSimulateItalic: italic);

            return bold
                ? new FontResolverInfo(SansBold, mustSimulateBold: false, mustSimulateItalic: italic)
                : new FontResolverInfo(SansRegular, mustSimulateBold: false, mustSimulateItalic: italic);
        }

        public byte[]? GetFont(string faceName)
        {
            var assembly = typeof(PdfFonts).Assembly;
            var resource = $"{typeof(PdfFonts).Namespace}.Fonts.{faceName}.ttf";
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null) return null;

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
