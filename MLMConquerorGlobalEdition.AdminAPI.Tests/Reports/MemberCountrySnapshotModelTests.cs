using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>Una fila por día, país, tipo de miembro y estado (especificación: clave única de la foto diaria).</summary>
public class MemberCountrySnapshotModelTests
{
    [Fact]
    public void Clave_unica_por_dia_pais_tipo_y_estado()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Model.FindEntityType(typeof(MemberCountrySnapshot))!.GetIndexes()
            .Should().ContainSingle(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "SnapshotDate", "Country", "MemberType", "Status" }));
    }
}
