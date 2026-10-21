using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberRankHistoryConfiguration : IEntityTypeConfiguration<MemberRankHistory>
{
    public void Configure(EntityTypeBuilder<MemberRankHistory> builder)
    {
        builder.HasKey(x => x.Id);

        // Plan Q (visualizador del admin): era nvarchar(max) sin indice, y el rango vigente de 127 miembros recorria la
        // tabla dos veces con una cola de trabajo (segundos de CPU). 450 como los ids de los arboles y del historial de
        // estados. El indice da, por miembro, sus filas de la mas reciente a la mas antigua (y la clave Id, en orden
        // ascendente, desempata como MemberRankQueries), con el rango incluido: el rango vigente y el maximo se
        // resuelven con busquedas, sin tocar la tabla.
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => new { x.MemberId, x.AchievedAt })
            .IsDescending(false, true)
            .IncludeProperties(x => x.RankDefinitionId);
    }
}
