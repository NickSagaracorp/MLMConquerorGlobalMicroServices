using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Product catalog seed data.
///
/// Products that earn points and trigger commissions:
///   Travel Advantage Guest Member  — 0 pts, no commissions (guest/free access)
///   Travel Advantage VIP           — 3 pts, full commissions, MembershipLevelId=2
///   Travel Advantage Elite         — 6 pts, full commissions, MembershipLevelId=3
///   Travel Advantage Turbo         — 6 pts, full commissions + Turbo program, MembershipLevelId=4
///
/// Operational/administrative products (no points, no commissions):
///   Subscription       — annual ambassador business fee
///   Monthly Subscription — recurring monthly operational fee
///
/// QualificationPoins drives binary tree point accumulation per billing cycle.
/// Pricing values are defaults — configure via AdminAPI before going live.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    // Fixed deterministic IDs — never change; referenced by TokenTypeProduct and OrderDetail.
    public static readonly string GuestMemberId      = "00000001-prod-0000-0000-000000000001";
    public static readonly string VipId              = "00000002-prod-0000-0000-000000000002";
    public static readonly string EliteId            = "00000003-prod-0000-0000-000000000003";
    public static readonly string TurboId            = "00000004-prod-0000-0000-000000000004";
    public static readonly string SubscriptionId     = "00000005-prod-0000-0000-000000000005";
    public static readonly string MonthlySubId       = "00000006-prod-0000-0000-000000000006";

    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.DescriptionPromo).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ImageUrlPromo).HasMaxLength(500);
        builder.Property(x => x.ThemeClass).HasMaxLength(50);

        builder.Property(x => x.MonthlyFee).IsRequired().HasColumnType("decimal(10,2)");
        builder.Property(x => x.SetupFee).IsRequired().HasColumnType("decimal(10,2)");
        builder.Property(x => x.Price90Days).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Price180Days).HasColumnType("decimal(10,2)");
        builder.Property(x => x.AnnualPrice).HasColumnType("decimal(10,2)");
        builder.Property(x => x.MonthlyFeePromo).HasColumnType("decimal(10,2)");
        builder.Property(x => x.SetupFeePromo).HasColumnType("decimal(10,2)");

        builder.Property(x => x.CorporateFee).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.JoinPageMembership).IsRequired().HasDefaultValue(false);

        builder.HasOne(x => x.MembershipLevel)
            .WithMany()
            .HasForeignKey(x => x.MembershipLevelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasData(

            new Product
            {
                Id = GuestMemberId,
                Name        = "Travel Advantage Guest Member",
                Description = "Free guest access to the Travel Advantage platform. No qualification points. No commissions triggered. Upgrade required to earn full benefits.",
                ImageUrl    = string.Empty,
                MonthlyFee  = 0m, SetupFee = 0m,
                QualificationPoins = 0,
                MembershipLevelId  = null,
                OldSystemProductId = 1,
                ThemeClass   = "theme-product-guest",
                CorporateFee = false, JoinPageMembership = false,
                IsActive = true, IsDeleted = false,
                CreationDate = SeedDate, CreatedBy = "seed",
                LastUpdateDate = SeedDate
            },

            new Product
            {
                Id = VipId,
                Name        = "Travel Advantage VIP",
                Description = "Entry-level Travel Advantage membership. Earns 3 qualification points per billing cycle. Triggers VIP Member Bonus ($20) and all standard enrollment commissions.",
                ImageUrl    = string.Empty,
                MonthlyFee  = 40m, SetupFee = 0m,
                QualificationPoins = 3,
                MembershipLevelId  = 2,
                OldSystemProductId = 2,
                ThemeClass   = "theme-product-vip",
                CorporateFee = false, JoinPageMembership = false,
                IsActive = true, IsDeleted = false,
                CreationDate = SeedDate, CreatedBy = "seed",
                LastUpdateDate = SeedDate
            },

            new Product
            {
                Id = EliteId,
                Name        = "Travel Advantage Elite",
                Description = "Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.",
                ImageUrl    = string.Empty,
                MonthlyFee  = 99m, SetupFee = 0m,
                QualificationPoins = 6,
                MembershipLevelId  = 3,
                OldSystemProductId = 3,
                ThemeClass   = "theme-product-elite",
                CorporateFee = false, JoinPageMembership = true,
                IsActive = true, IsDeleted = false,
                CreationDate = SeedDate, CreatedBy = "seed",
                LastUpdateDate = SeedDate
            },

            new Product
            {
                Id = TurboId,
                Name        = "Travel Advantage Turbo",
                Description = "Premium Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Turbo Member Bonus ($80), full commissions, and Builder Bonus Turbo program.",
                ImageUrl    = string.Empty,
                MonthlyFee  = 199m, SetupFee = 0m,
                QualificationPoins = 6,
                MembershipLevelId  = 4,
                OldSystemProductId = 4,
                ThemeClass   = "theme-product-turbo",
                CorporateFee = false, JoinPageMembership = true,
                IsActive = true, IsDeleted = false,
                CreationDate = SeedDate, CreatedBy = "seed",
                LastUpdateDate = SeedDate
            },

            new Product
            {
                Id = SubscriptionId,
                Name        = "Subscription",
                Description = "Annual ambassador business fee. Operational/administrative product. Does not earn qualification points and does not trigger commissions.",
                ImageUrl    = string.Empty,
                MonthlyFee  = 0m, SetupFee = 99m,
                AnnualPrice = 99m,
                QualificationPoins = 0,
                MembershipLevelId  = 1,
                OldSystemProductId = 5,
                ThemeClass   = null,
                CorporateFee = true, JoinPageMembership = false,
                IsActive = true, IsDeleted = false,
                CreationDate = SeedDate, CreatedBy = "seed",
                LastUpdateDate = SeedDate
            },

            new Product
            {
                Id = MonthlySubId,
                Name        = "Monthly Subscription",
                Description = "Generic recurring monthly subscription. Operational/administrative product. Does not earn qualification points and does not trigger commissions.",
                ImageUrl    = string.Empty,
                MonthlyFee  = 0m, SetupFee = 0m,
                QualificationPoins = 0,
                MembershipLevelId  = null,
                OldSystemProductId = 6,
                ThemeClass   = null,
                CorporateFee = false, JoinPageMembership = false,
                IsActive = true, IsDeleted = false,
                CreationDate = SeedDate, CreatedBy = "seed",
                LastUpdateDate = SeedDate
            }
        );
    }
}
