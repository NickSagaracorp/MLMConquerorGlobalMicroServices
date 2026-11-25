using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CommissionEarningConfiguration : IEntityTypeConfiguration<CommissionEarning>
{
    public void Configure(EntityTypeBuilder<CommissionEarning> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.BeneficiaryMemberId, x.Status });
        builder.HasIndex(x => x.PeriodDate);
        // Grid de comisiones del admin filtrado por estado (desplegable), lo más reciente primero.
        builder.HasIndex(x => new { x.Status, x.EarnedDate }).HasDatabaseName("IX_CommissionEarnings_Status_EarnedDate")
            .IsDescending(false, true)
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.SourceOrderId, x.CommissionTypeId }).IsUnique()
            .HasFilter("[SourceOrderId] IS NOT NULL");
        // Reportes Earnings: suma por beneficiario hasta una fecha. Sin este índice, el agrupado recorre la tabla entera.
        builder.HasIndex(x => new { x.EarnedDate, x.BeneficiaryMemberId })
               .HasDatabaseName("IX_CommissionEarnings_EarnedDate_Beneficiary")
               .IncludeProperties(x => new { x.Amount, x.Status })
               .HasFilter("[IsDeleted] = 0");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
