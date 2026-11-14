using System.Globalization;
using System.Text;

namespace MLMConquerorGlobalEdition.AdminAPI.Reports;

/// <summary>
/// Convierte filas de cualquier reporte del admin en CSV: UTF-8 con BOM, comas, CRLF y cultura invariante. Un texto que
/// empieza por =, +, - o @ se antepone con ' para que una hoja de cálculo no lo ejecute como fórmula; los números no.
/// </summary>
public class CsvReportWriter
{
    public const string ContentType = "text/csv";

    public byte[] Write<T>(IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", columns.Select(c => Escape(c.Header, isNumber: false)))).Append("\r\n");
        foreach (var row in rows)
            sb.Append(string.Join(",", columns.Select(c => Cell(c.Value(row))))).Append("\r\n");

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Cell(object? value) => value switch
    {
        null           => string.Empty,
        DateTime d     => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => Escape(f.ToString(null, CultureInfo.InvariantCulture), isNumber: true),
        _              => Escape(value.ToString() ?? string.Empty, isNumber: false)
    };

    private static string Escape(string value, bool isNumber)
    {
        var formula = !isNumber && value.Length > 0 && value[0] is '=' or '+' or '-' or '@';
        if (formula) value = "'" + value;
        var quote = formula || value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        return quote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
