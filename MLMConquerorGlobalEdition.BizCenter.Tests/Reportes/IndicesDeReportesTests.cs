using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Los índices que piden las consultas del Plan J están declarados en el modelo. EF InMemory no los
/// usa; que existan en la base lo comprueban las pruebas [SqlServerFact] de cada bloque.
/// </summary>
public class IndicesDeReportesTests
{
    private static bool TieneIndice(Type entidad, params string[] columnas)
    {
        using var db = ReportesDb.Nueva();
        return db.Model.FindEntityType(entidad)!.GetIndexes()
            .Any(i => i.Properties.Select(p => p.Name).SequenceEqual(columnas));
    }

    [Fact]
    public void Residuales_diarios_por_beneficiario_y_fecha()
        => TieneIndice(typeof(DailyResidualEarning), "BeneficiaryMemberId", "EarnedDate").Should().BeTrue();

    [Fact]
    public void El_indice_de_residuales_incluye_importe_y_estado()
    {
        // Los totales del periodo y el gráfico solo leen Amount y Status: con ellos incluidos, el
        // índice cubre la consulta y no hace falta ir a la tabla por cada fila.
        using var db = ReportesDb.Nueva();
        // Las columnas incluidas son configuración de diseño: no están en el modelo de ejecución.
        var modelo = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
        var indice = modelo.FindEntityType(typeof(DailyResidualEarning))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "BeneficiaryMemberId", "EarnedDate" }));

        indice.GetIncludeProperties().Should().BeEquivalentTo("Amount", "Status");
    }

    [Fact]
    public void Perfiles_por_patrocinador_y_fecha_de_alta_con_patrocinador_de_veinte()
    {
        TieneIndice(typeof(MemberProfile), "SponsorMemberId", "EnrollDate").Should().BeTrue();
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(typeof(MemberProfile))!.FindProperty("SponsorMemberId")!
            .GetMaxLength().Should().Be(20);
    }

    // (índices de los bloques siguientes)
}
