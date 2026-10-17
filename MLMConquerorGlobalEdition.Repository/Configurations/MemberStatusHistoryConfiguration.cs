using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberStatusHistoryConfiguration : IEntityTypeConfiguration<MemberStatusHistory>
{
    public void Configure(EntityTypeBuilder<MemberStatusHistory> builder)
    {
        builder.HasKey(x => x.Id);

        // Plan J: era nvarchar(max) sin índice. El reporte de cambios busca por fecha y la última
        // fecha de cada miembro. 450 como los ids de los árboles (en el cliente hay ids de 50).
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => new { x.MemberId, x.ChangedAt });
        builder.HasIndex(x => x.ChangedAt);
    }
}
