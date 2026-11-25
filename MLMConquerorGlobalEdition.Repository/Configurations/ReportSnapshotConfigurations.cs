using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Clave agrupada (fecha, ventana, posición): leer una página de la foto es una lectura por rango de la clave, y la
/// última foto sale del principio del índice. Los largos son los de MemberProfiles y RankDefinitions.
/// </summary>
public class EarningsRankingSnapshotConfiguration : IEntityTypeConfiguration<EarningsRankingSnapshot>
{
    public void Configure(EntityTypeBuilder<EarningsRankingSnapshot> builder)
    {
        builder.HasKey(x => new { x.SnapshotDate, x.WindowDays, x.Rank });
        builder.Property(x => x.SnapshotDate).HasColumnType("date");
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Username).HasMaxLength(100);
        builder.Property(x => x.MemberName).IsRequired().HasMaxLength(201);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Total).HasPrecision(19, 4);
    }
}

public class RankStandingSnapshotConfiguration : IEntityTypeConfiguration<RankStandingSnapshot>
{
    public void Configure(EntityTypeBuilder<RankStandingSnapshot> builder)
    {
        builder.HasKey(x => new { x.SnapshotDate, x.Mode, x.Position });
        builder.Property(x => x.SnapshotDate).HasColumnType("date");
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.MemberName).IsRequired().HasMaxLength(201);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(100);
        builder.Property(x => x.RankName).IsRequired().HasMaxLength(200);
    }
}
