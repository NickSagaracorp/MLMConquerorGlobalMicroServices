using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class DualLegDeltaConfiguration : IEntityTypeConfiguration<DualLegDelta>
{
    public void Configure(EntityTypeBuilder<DualLegDelta> builder)
    {
        builder.ToTable("DualLegDeltas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartMemberId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Points).HasPrecision(18, 4);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        // Revision I8: cada tanda agrupa por arranque, reclama por arranque (DELETE ... WHERE StartMemberId IN) y una
        // retirada borra por arranque. Sin indice, con atraso, cada una recorre la cola entera.
        builder.HasIndex(x => new { x.StartMemberId, x.Id }).HasDatabaseName("IX_DualLegDeltas_StartMemberId_Id");
    }
}
