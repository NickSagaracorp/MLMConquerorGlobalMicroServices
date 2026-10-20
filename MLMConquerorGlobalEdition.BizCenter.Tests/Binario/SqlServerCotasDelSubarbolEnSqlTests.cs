using Microsoft.Data.SqlClient;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Revision del bloque 3: los scripts de desarrollo filtran el equipo por el rango de la clave con
/// Scripts/sql/dual-subtree-bounds.sql. Sus cotas tienen que ser las de DualBinaryPath.SubtreeBounds (con acarreo,
/// en la raiz y en el borde de un byte). Solo calcula: no lee ni escribe tablas.
/// </summary>
public class SqlServerCotasDelSubarbolEnSqlTests
{
    private static IEnumerable<byte[]> Claves()
    {
        yield return DualBinaryPath.Root(1);
        yield return DualBinaryPath.Root(255);                  // acarreo dentro del ordinal
        yield return DualBinaryPath.Root(int.MaxValue);
        var rnd = new Random(20261020);
        foreach (var ordinal in new[] { 1, 255, 65_535 })
        foreach (var lado in new[] { TreeSide.Left, TreeSide.Right, (TreeSide)(-1) })
        {
            var k = DualBinaryPath.Root(ordinal);
            for (var i = 1; i <= 40; i++)
            {
                var l = (int)lado == -1 ? (rnd.Next(2) == 0 ? TreeSide.Left : TreeSide.Right) : lado;
                k = DualBinaryPath.Child(k, l);
                yield return k;                                  // todas las profundidades: cada bit de cada byte
            }
        }
    }

    [SqlServerFact]
    public async Task Las_cotas_del_script_son_las_de_DualBinaryPath()
    {
        var fragmento = await File.ReadAllTextAsync(
            Path.Combine(ReportesDb.RaizDelRepositorio(), "Scripts", "sql", "dual-subtree-bounds.sql"));
        await using var cn = new SqlConnection(SqlServerReportes.ConnectionString);
        await cn.OpenAsync();
        foreach (var clave in Claves())
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DECLARE @k varbinary(1700) = @clave;\n" + fragmento + "\nSELECT @lo, @hi;";
            cmd.Parameters.Add(new SqlParameter("@clave", System.Data.SqlDbType.VarBinary, 1700) { Value = clave });
            await using var r = await cmd.ExecuteReaderAsync();
            (await r.ReadAsync()).Should().BeTrue();
            var (lo, hi) = DualBinaryPath.SubtreeBounds(clave);
            var hex = Convert.ToHexString(clave);
            DualBinaryPath.SqlCompare((byte[])r[0], lo).Should().Be(0, $"lo de {hex}");
            DualBinaryPath.SqlCompare((byte[])r[1], hi).Should().Be(0, $"hi de {hex}");
        }
    }

    [SqlServerFact]
    public async Task Sin_clave_las_cotas_son_nulas_y_no_sale_nadie()
    {
        var fragmento = await File.ReadAllTextAsync(
            Path.Combine(ReportesDb.RaizDelRepositorio(), "Scripts", "sql", "dual-subtree-bounds.sql"));
        await using var cn = new SqlConnection(SqlServerReportes.ConnectionString);
        await cn.OpenAsync();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "DECLARE @k varbinary(1700) = NULL;\n" + fragmento + "\nSELECT CASE WHEN @lo IS NULL AND @hi IS NULL THEN 1 ELSE 0 END;";
        ((int)(await cmd.ExecuteScalarAsync())!).Should().Be(1);
    }
}
