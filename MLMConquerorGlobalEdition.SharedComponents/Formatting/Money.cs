using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedComponents.Formatting;

/// <summary>
/// Importes en dólares. Los precios, saldos y totales que muestran las pantallas están en USD, así que
/// no dependen de la cultura de quien mira: con "C" y la cultura del navegador, un miembro en pt-BR
/// leía "R$ 99,99" por un importe que en realidad son 99,99 dólares. El formato es el de
/// <see cref="NumeroCorto.Monto(decimal)"/>, el mismo en todos los portales.
/// </summary>
public static class Money
{
    /// <summary>"$1,234.50" sea cual sea la cultura de quien mira (negativo: "-$1,234.50").</summary>
    public static string Usd(decimal amount) => NumeroCorto.Monto(amount);

    /// <summary>Como <see cref="Usd(decimal)"/>; null se muestra como "—".</summary>
    public static string Usd(decimal? amount) => amount is decimal a ? Usd(a) : "—";
}
