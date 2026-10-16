using System.Globalization;

namespace MLMConquerorGlobalEdition.SharedComponents.Formatting;

/// <summary>
/// Importes en dólares. Los precios, saldos y totales que muestran las pantallas están en USD, así que
/// se formatean siempre con la cultura en-US: con "C" y la cultura del navegador, un miembro en pt-BR
/// leía "R$ 99,99" por un importe que en realidad son 99,99 dólares.
/// </summary>
public static class Money
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>"$1,234.50" sea cual sea la cultura de quien mira.</summary>
    public static string Usd(decimal amount) => amount.ToString("C", UsCulture);

    /// <summary>Como <see cref="Usd(decimal)"/>; null se muestra como "—".</summary>
    public static string Usd(decimal? amount) => amount is decimal a ? Usd(a) : "—";
}
