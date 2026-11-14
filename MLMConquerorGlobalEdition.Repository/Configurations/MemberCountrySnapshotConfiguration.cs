using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberCountrySnapshotConfiguration : IEntityTypeConfiguration<MemberCountrySnapshot>
{
    public void Configure(EntityTypeBuilder<MemberCountrySnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotDate).HasColumnType("date");
        builder.Property(x => x.Country).IsRequired().HasMaxLength(100);   // como MemberProfiles.Country
        builder.Property(x => x.CreatedBy).HasMaxLength(100);

        // Una fila por dia, pais, tipo y estado. Tambien sirve a todas las lecturas del reporte (por dia o rango de dias).
        builder.HasIndex(x => new { x.SnapshotDate, x.Country, x.MemberType, x.Status })
               .IsUnique()
               .HasDatabaseName("UX_MemberCountrySnapshots_Date_Country_Type_Status");
    }
}
