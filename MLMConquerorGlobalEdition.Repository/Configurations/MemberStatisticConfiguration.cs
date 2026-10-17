using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M9): LegPointMaintenance une por MemberId en cada colocación, el MERGE WITH (HOLDLOCK) de
/// ApplyMemberStatisticDeltasJob busca por MemberId (sin índice bloquea un rango de toda la tabla) y
/// cada página de reportes hace Contains(ids). No es único: no cambia ningún escritor.
/// </summary>
public class MemberStatisticConfiguration : IEntityTypeConfiguration<MemberStatisticEntity>
{
    public void Configure(EntityTypeBuilder<MemberStatisticEntity> builder)
    {
        // 100, no 20 como MemberProfiles.MemberId: sin FK y con holgura, igual que PaymentHistories (ver la
        // nota de longitudes de OrdersConfiguration). Hoy miden 10 como mucho; la guarda 51028 lo comprueba.
        builder.Property(s => s.MemberId).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.MemberId);
    }
}
