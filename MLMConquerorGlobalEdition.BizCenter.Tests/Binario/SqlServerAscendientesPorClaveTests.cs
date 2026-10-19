using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan Q: dbo.DualAncestorKeys (SQL) y DualBinaryPath.Ancestor/SideBelow (C#) dan las mismas claves y lados.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerAscendientesPorClaveTests
{
    private sealed record Fila(int Depth, byte[] AncestorKey, int Side);

    [SqlServerFact]
    public async Task DualAncestorKeys_coincide_con_DualBinaryPath()
    {
        await using var db = SqlServerReportes.Db();
        var rnd = new Random(1019);
        foreach (var prof in new[] { 0, 1, 7, 8, 9, 15, 16, 17, 500, 8507 })
        {
            var k = DualBinaryPath.Root(rnd.Next(1, int.MaxValue));
            for (var i = 0; i < prof; i++) k = DualBinaryPath.Child(k, rnd.Next(2) == 0 ? TreeSide.Left : TreeSide.Right);
            var propio = rnd.Next(2);

            var filas = await db.Database
                .SqlQuery<Fila>($"SELECT Depth, AncestorKey, Side FROM dbo.DualAncestorKeys({k}, {propio}) ORDER BY Depth")
                .ToListAsync();

            filas.Should().HaveCount(prof + 1);
            for (var j = 0; j <= prof; j++)
            {
                filas[j].Depth.Should().Be(j);
                filas[j].AncestorKey.Should().Equal(DualBinaryPath.Ancestor(k, j));
                filas[j].Side.Should().Be(j == prof ? propio : (int)DualBinaryPath.SideBelow(k, j));
            }
        }
    }
}
