using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

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

    /// <summary>
    /// SponsorMemberId a nvarchar(450): la longitud de GenealogyTree.MemberId/ParentMemberId y
    /// DualTeamTree, con los que se une, y la máxima que admite índice. En la solución del cliente
    /// hay patrocinadores de 50 caracteres: nvarchar(20) los habría rechazado (corrección de Nick).
    /// </summary>
    [Fact]
    public void Perfiles_por_patrocinador_y_fecha_de_alta_con_patrocinador_de_450()
    {
        TieneIndice(typeof(MemberProfile), "SponsorMemberId", "EnrollDate").Should().BeTrue();
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(typeof(MemberProfile))!.FindProperty("SponsorMemberId")!
            .GetMaxLength().Should().Be(450);
    }

    [Fact]
    public void Perfiles_por_fecha_de_alta()
        => TieneIndice(typeof(MemberProfile), "EnrollDate").Should().BeTrue();

    [Theory]
    [InlineData(typeof(MemberStatusHistory))]
    [InlineData(typeof(MembershipStatusHistory))]
    public void Historiales_por_miembro_y_fecha_y_por_fecha(Type historial)
    {
        TieneIndice(historial, "MemberId", "ChangedAt").Should().BeTrue();
        TieneIndice(historial, "ChangedAt").Should().BeTrue();
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(historial)!.FindProperty("MemberId")!.GetMaxLength().Should().Be(450);
    }

    /// <summary>
    /// Corrección de Nick N3: la marca de mantener calificado existe, es obligatoria y las filas que ya
    /// hay quedan en false (valor por defecto de la columna), así que nadie pasa a calificado solo.
    /// </summary>
    [Fact]
    public void Perfiles_con_marca_de_mantener_calificado_por_defecto_false()
    {
        using var db = ReportesDb.Nueva();
        var propiedad = db.Model.FindEntityType(typeof(MemberProfile))!.FindProperty(nameof(MemberProfile.KeepQualified))!;

        propiedad.IsNullable.Should().BeFalse();
        propiedad.GetDefaultValue().Should().Be(false);
        new MemberProfile().KeepQualified.Should().BeFalse();
    }

    [Fact]
    public void Inscripciones_a_eventos_unicas_por_evento_y_referencia_externa()
    {
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(typeof(CorporateEventRegistration))!.GetIndexes()
            .Should().Contain(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "EventId", "ExternalRef" }));
        TieneIndice(typeof(CorporateEventRegistration), "EventId", "MemberId").Should().BeTrue();
    }

    // (índices de los bloques siguientes)
}
