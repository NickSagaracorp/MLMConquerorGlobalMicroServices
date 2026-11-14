using System.Text;
using MLMConquerorGlobalEdition.AdminAPI.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class CsvReportWriterTests
{
    private sealed record Fila(int Rank, string? Name, decimal Total);

    private static string Texto(byte[] bytes)
    {
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF }, "lleva BOM para que se lea como UTF-8");
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    }

    [Fact]
    public void Escribe_encabezados_y_filas_con_numeros_invariantes()
    {
        var csv = Texto(new CsvReportWriter().Write(
            new[] { new Fila(1, "Ana", 1234.5m), new Fila(2, "Beto", 10m) },
            ("Rank", f => f.Rank), ("Member Name", f => f.Name), ("Total", f => f.Total)));

        csv.Should().Be("Rank,Member Name,Total\r\n1,Ana,1234.5\r\n2,Beto,10\r\n");
    }

    [Fact]
    public void Entrecomilla_comas_comillas_y_saltos()
    {
        var csv = Texto(new CsvReportWriter().Write(
            new[] { new Fila(1, "Lopez, \"Ana\"\nJr", 1m) },
            ("Name", f => f.Name)));

        csv.Should().Be("Name\r\n\"Lopez, \"\"Ana\"\"\nJr\"\r\n");
    }

    [Fact]
    public void Neutraliza_formulas()
    {
        var csv = Texto(new CsvReportWriter().Write(
            new[] { new Fila(1, "=HYPERLINK(\"x\")", 1m), new Fila(2, "@cmd", 1m) },
            ("Name", f => f.Name)));

        csv.Should().Be("Name\r\n\"'=HYPERLINK(\"\"x\"\")\"\r\n\"'@cmd\"\r\n");
    }

    [Fact]
    public void Neutraliza_tabulador_y_retorno_al_inicio()
    {
        var csv = Texto(new CsvReportWriter().Write(
            new[] { new Fila(1, "\tcmd", 1m), new Fila(2, "\rcmd", 1m) },
            ("Name", f => f.Name)));

        csv.Should().Be("Name\r\n\"'\tcmd\"\r\n\"'\rcmd\"\r\n");
    }

    [Fact]
    public void Nulos_salen_vacios()
    {
        var csv = Texto(new CsvReportWriter().Write(new[] { new Fila(1, null, 1m) }, ("Name", f => f.Name)));

        csv.Should().Be("Name\r\n\r\n");
    }
}
