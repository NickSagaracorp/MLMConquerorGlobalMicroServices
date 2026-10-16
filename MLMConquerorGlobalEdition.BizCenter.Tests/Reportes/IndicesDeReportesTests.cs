using MLMConquerorGlobalEdition.Domain.Entities.Commission;

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

    // (índices de los bloques siguientes)
}
