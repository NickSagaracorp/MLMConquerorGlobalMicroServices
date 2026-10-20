using System.Data;
using Microsoft.Data.SqlClient;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Tarea 10: la clave compacta en SQL Server de verdad. (a) dbo.DualChildKey da los mismos bytes que
/// DualBinaryPath.Child; (b) con la comparacion real de varbinary y parametros varbinary(1700), el rango
/// (Lo, Hi) devuelve exactamente el subarbol de cada nodo. Todo en tablas temporales: no escribe en la base.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerClaveCompactaTests
{
    private static SqlConnection Abrir()
    {
        var c = new SqlConnection(SqlServerReportes.ConnectionString);
        c.Open();
        return c;
    }

    [SqlServerFact]
    public void La_funcion_SQL_da_los_mismos_bytes_que_DualBinaryPath()
    {
        var azar = new Random(18102026);
        using var c = Abrir();
        new SqlCommand("CREATE TABLE #k (Padre varbinary(1700) NOT NULL, Prof int NOT NULL, Lado int NOT NULL, Esperada varbinary(1700) NOT NULL)", c).ExecuteNonQuery();
        var tabla = new DataTable();
        tabla.Columns.Add("Padre", typeof(byte[]));
        tabla.Columns.Add("Prof", typeof(int));
        tabla.Columns.Add("Lado", typeof(int));
        tabla.Columns.Add("Esperada", typeof(byte[]));
        for (var i = 0; i < 2000; i++)
        {
            var k = DualBinaryPath.Root(azar.Next(1, int.MaxValue));
            var n = azar.Next(0, 600);
            for (var j = 0; j < n; j++) k = DualBinaryPath.Child(k, azar.Next(2) == 0 ? TreeSide.Left : TreeSide.Right);
            var lado = azar.Next(2) == 0 ? TreeSide.Left : TreeSide.Right;
            tabla.Rows.Add(k, n, (int)lado, DualBinaryPath.Child(k, lado));
        }
        using (var bulk = new SqlBulkCopy(c) { DestinationTableName = "#k" }) bulk.WriteToServer(tabla);

        var distintas = (int)new SqlCommand(
            "SELECT COUNT(*) FROM #k CROSS APPLY dbo.DualChildKey(#k.Padre, #k.Prof, #k.Lado) f WHERE f.ChildKey <> #k.Esperada OR DATALENGTH(f.ChildKey) <> DATALENGTH(#k.Esperada)", c).ExecuteScalar()!;
        distintas.Should().Be(0);
    }

    [SqlServerFact]
    public void El_rango_devuelve_exactamente_el_subarbol_con_la_comparacion_de_SQL_Server()
    {
        var azar = new Random(4000);
        var nodos = new List<(int Id, int? Padre, byte[] Clave)> { (0, null, DualBinaryPath.Root(7)), (-1, null, DualBinaryPath.Root(8)) };
        var libres = new List<(int Padre, TreeSide Lado)> { (0, TreeSide.Left), (0, TreeSide.Right) };
        for (var id = 1; id < 400; id++)
        {
            var i = azar.Next(3) == 0 ? azar.Next(libres.Count) : libres.Count - 1;
            var (padre, lado) = libres[i];
            libres.RemoveAt(i);
            nodos.Add((id, padre, DualBinaryPath.Child(nodos.First(n => n.Id == padre).Clave, lado)));
            libres.Add((id, TreeSide.Left));
            libres.Add((id, TreeSide.Right));
        }

        using var c = Abrir();
        new SqlCommand("CREATE TABLE #n (Id int NOT NULL, Clave varbinary(1700) NOT NULL); CREATE UNIQUE INDEX ux ON #n (Clave);", c).ExecuteNonQuery();
        var tabla = new DataTable();
        tabla.Columns.Add("Id", typeof(int));
        tabla.Columns.Add("Clave", typeof(byte[]));
        foreach (var n in nodos) tabla.Rows.Add(n.Id, n.Clave);
        using (var bulk = new SqlBulkCopy(c) { DestinationTableName = "#n" }) bulk.WriteToServer(tabla);

        var hijos = nodos.Where(n => n.Padre is not null).ToLookup(n => n.Padre!.Value);
        HashSet<int> Subarbol(int id)
        {
            var s = new HashSet<int> { id };
            var pila = new Stack<int>(new[] { id });
            while (pila.Count > 0) foreach (var h in hijos[pila.Pop()]) { s.Add(h.Id); pila.Push(h.Id); }
            return s;
        }

        using var cmd = new SqlCommand("SELECT Id FROM #n WHERE Clave > @lo AND Clave < @hi", c);
        var lo = cmd.Parameters.Add("@lo", SqlDbType.VarBinary, DualBinaryPath.MaxBytes);
        var hi = cmd.Parameters.Add("@hi", SqlDbType.VarBinary, DualBinaryPath.MaxBytes);
        foreach (var x in nodos.Where(n => n.Id >= 0))
        {
            var (l, h) = DualBinaryPath.SubtreeBounds(x.Clave);
            lo.Value = l; hi.Value = h;
            var ids = new HashSet<int>();
            using (var r = cmd.ExecuteReader()) while (r.Read()) ids.Add(r.GetInt32(0));
            ids.Should().BeEquivalentTo(Subarbol(x.Id), $"subarbol del nodo {x.Id}");
        }
    }

    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Plan Q, Tarea 12: colocar y mover ya no escriben la ruta de texto (se quita con la columna, Tarea 13).</summary>
    [SqlServerFact]
    public async Task Ni_colocar_ni_mover_escriben_la_ruta_de_texto()
    {
        var x = BinarioDb.Prefijo("PQR");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                await BinarioDb.SembrarPerfilAsync(db, $"{x}B", $"{x}A");
                await BinarioDb.SembrarPerfilAsync(db, $"{x}C", $"{x}B");
                var motor = new PlacementService(db, new Reloj());
                (await motor.PlaceAsync($"{x}C", $"{x}B")).IsSuccess.Should().BeTrue();   // B raiz provisional
                (await motor.PlaceAsync($"{x}B", $"{x}A")).IsSuccess.Should().BeTrue();   // se mueve con C
            }
            await using (var db = SqlServerReportes.Db())
                (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM DualTeamTree WHERE MemberId LIKE {x + "%"} AND HierarchyPath <> N''").SingleAsync())
                    .Should().Be(0);
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }
}
