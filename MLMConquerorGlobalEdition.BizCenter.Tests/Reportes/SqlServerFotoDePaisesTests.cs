using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Lo que InMemory no prueba: el indice unico (fecha, pais, tipo, estado) de la foto diaria. Dos escrituras a la vez
/// del mismo dia (job nocturno y foto manual) no deben fallar ni duplicar filas.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerFotoDePaisesTests
{
    // Un dia lejano que ninguna foto real usa; sus filas se borran al terminar.
    private static readonly DateTime Dia = new(2001, 1, 1);

    [SqlServerFact]
    public async Task Dos_escrituras_a_la_vez_del_mismo_dia_funcionan_y_no_duplican_filas()
    {
        await using var limpieza = SqlServerReportes.Db();
        try
        {
            await limpieza.MemberCountrySnapshots.Where(s => s.SnapshotDate == Dia).ExecuteDeleteAsync();

            async Task<int> Escribir()
            {
                await using var db = SqlServerReportes.Db();
                return await new MemberCountrySnapshotWriter(db, new RelojFijo(ReportesDb.Base)).WriteAsync(Dia);
            }

            var resultados = await Task.WhenAll(Task.Run(Escribir), Task.Run(Escribir));

            resultados[0].Should().Be(resultados[1]);
            await using var db2 = SqlServerReportes.Db();
            var filas = await db2.MemberCountrySnapshots.AsNoTracking().Where(s => s.SnapshotDate == Dia).ToListAsync();
            filas.Should().HaveCount(resultados[0]);
            filas.Select(f => (f.Country.TrimEnd().ToUpper(), f.MemberType, f.Status)).Should().OnlyHaveUniqueItems();
        }
        finally
        {
            await limpieza.MemberCountrySnapshots.Where(s => s.SnapshotDate == Dia).ExecuteDeleteAsync();
        }
    }
}
