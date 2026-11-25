using System.Globalization;

namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// Formato de cantidades y montos para pantalla. Las cantidades van con separador de miles (1,000,003) y los montos
/// con símbolo y centavos ($61,354,648.34). Donde el espacio es corto (tarjetas KPI) se pasa un máximo de caracteres:
/// si el valor completo no cabe, se abrevia con K, M, B o T y tantos decimales como quepan, truncados para no
/// exagerar la cifra (1,000,003 → 1M; $105,240,000 → $105.2M).
/// </summary>
public static class NumeroCorto
{
    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    private static readonly (decimal Valor, string Sufijo)[] Unidades =
    {
        (1_000_000_000_000m, "T"), (1_000_000_000m, "B"), (1_000_000m, "M"), (1_000m, "K"),
    };

    /// <summary>Cantidad con separador de miles: 1,000,003.</summary>
    public static string Cantidad(long valor) => valor.ToString("N0", Cultura);

    /// <summary>Cantidad que no pasa de <paramref name="maxCaracteres"/>: completa si cabe, abreviada si no.</summary>
    public static string Cantidad(long valor, int maxCaracteres) => Ajustar(valor, "", Cantidad(valor), maxCaracteres);

    /// <summary>Monto con símbolo y centavos: $61,354,648.34 (negativo: -$1,200.00).</summary>
    public static string Monto(decimal valor) =>
        (valor < 0 ? "-$" : "$") + Math.Abs(valor).ToString("N2", Cultura);

    /// <summary>Monto que no pasa de <paramref name="maxCaracteres"/>: completo si cabe, abreviado si no.</summary>
    public static string Monto(decimal valor, int maxCaracteres) => Ajustar(valor, "$", Monto(valor), maxCaracteres);

    private static string Ajustar(decimal valor, string prefijo, string completo, int maxCaracteres)
    {
        if (completo.Length <= maxCaracteres) return completo;

        var abs   = Math.Abs(valor);
        var signo = valor < 0 ? "-" : "";
        var (div, sufijo) = Unidades.FirstOrDefault(u => abs >= u.Valor);
        if (div == 0) return completo; // menos de mil y aun así no cabe (centavos): el CSS lo recorta

        var cociente = abs / div;
        string texto = "";
        for (var decimales = 2; decimales >= 0; decimales--)
        {
            var factor    = (decimal)Math.Pow(10, decimales);
            var truncado  = Math.Truncate(cociente * factor) / factor;
            var formato   = decimales == 0 ? "0" : "0." + new string('#', decimales);
            texto = signo + prefijo + truncado.ToString(formato, Cultura) + sufijo;
            if (texto.Length <= maxCaracteres) break;
        }
        return texto;
    }
}
