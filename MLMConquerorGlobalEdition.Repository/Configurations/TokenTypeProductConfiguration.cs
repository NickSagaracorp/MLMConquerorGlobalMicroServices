using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// TokenTypeProduct association table. Each row links a TokenType to a Product
/// with a Role (Granted | UpgradeFrom | UpgradeTo).
///
/// Seed data covers ONLY tokens whose products exist in the current Travel
/// Advantage catalog (Guest/VIP/Elite/Turbo/Subscription). Legacy MWR token
/// variants referencing Pro / Plus / 180 / 365 / Mall products are left
/// unlinked — admins can configure those via the admin UI when those products
/// are added to the system.
/// </summary>
public class TokenTypeProductConfiguration : IEntityTypeConfiguration<TokenTypeProduct>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    // Travel Advantage product IDs (canonical, from migrations)
    private const string GuestMemberId = "00000001-prod-0000-0000-000000000001";
    private const string VipId         = "00000002-prod-0000-0000-000000000002";
    private const string EliteId       = "00000003-prod-0000-0000-000000000003";
    private const string TurboId       = "00000004-prod-0000-0000-000000000004";
    private const string SubscriptionId = "00000005-prod-0000-0000-000000000005"; // annual ambassador fee

    public void Configure(EntityTypeBuilder<TokenTypeProduct> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Role).HasConversion<int>().HasDefaultValue(TokenProductRole.Granted);
        builder.Property(x => x.QuantityGranted).HasDefaultValue(1);

        // A given (TokenType, Product, Role) tuple must be unique.
        // Allows the same product to be Granted AND UpgradeTo on different tokens,
        // but a single token can't list the same product twice with the same role.
        builder.HasIndex(x => new { x.TokenTypeId, x.ProductId, x.Role }).IsUnique();

        var i = 1; // sequential seed PK
        TokenTypeProduct Granted(int tokenTypeId, string productId)
            => new()
            {
                Id              = i++,
                TokenTypeId     = tokenTypeId,
                ProductId       = productId,
                Role            = TokenProductRole.Granted,
                QuantityGranted = 1,
                CreatedBy       = "seed",
                CreationDate    = SeedDate
            };
        TokenTypeProduct UpgradeFrom(int tokenTypeId, string productId)
            => new()
            {
                Id              = i++,
                TokenTypeId     = tokenTypeId,
                ProductId       = productId,
                Role            = TokenProductRole.UpgradeFrom,
                QuantityGranted = 0,
                CreatedBy       = "seed",
                CreationDate    = SeedDate
            };
        TokenTypeProduct UpgradeTo(int tokenTypeId, string productId)
            => new()
            {
                Id              = i++,
                TokenTypeId     = tokenTypeId,
                ProductId       = productId,
                Role            = TokenProductRole.UpgradeTo,
                QuantityGranted = 1,
                CreatedBy       = "seed",
                CreationDate    = SeedDate
            };

        builder.HasData(

            // ─── Enrollment: Guest Member token grants the Guest product ───
            Granted(2,  GuestMemberId),

            // ─── Enrollment: Ambassador-only ───────────────────────────────
            Granted(8,  SubscriptionId),
            Granted(88, SubscriptionId), // FREE variant

            // ─── Enrollment: Ambassador + VIP ──────────────────────────────
            Granted(64, SubscriptionId),
            Granted(64, VipId),
            Granted(65, SubscriptionId), // + Event
            Granted(65, VipId),
            Granted(86, SubscriptionId), // FREE variant
            Granted(86, VipId),

            // ─── Enrollment: Ambassador + Elite ────────────────────────────
            Granted(5,  SubscriptionId),
            Granted(5,  EliteId),
            Granted(11, SubscriptionId), // + Event
            Granted(11, EliteId),
            Granted(71, SubscriptionId), // (Coupon)
            Granted(71, EliteId),
            Granted(72, SubscriptionId), // + Event (Coupon)
            Granted(72, EliteId),
            Granted(81, SubscriptionId), // FREE
            Granted(81, EliteId),
            Granted(82, SubscriptionId), // (Coupon) FREE
            Granted(82, EliteId),
            Granted(98, SubscriptionId), // (Help a Friend)
            Granted(98, EliteId),

            // ─── Enrollment: Ambassador + Elite + TURBO ────────────────────
            Granted(69, SubscriptionId),
            Granted(69, EliteId),
            Granted(69, TurboId),
            Granted(70, SubscriptionId), // + Event
            Granted(70, EliteId),
            Granted(70, TurboId),
            Granted(73, SubscriptionId), // + Event (Coupon)
            Granted(73, EliteId),
            Granted(73, TurboId),
            Granted(74, SubscriptionId), // (Coupon)
            Granted(74, EliteId),
            Granted(74, TurboId),
            Granted(83, SubscriptionId), // FREE
            Granted(83, EliteId),
            Granted(83, TurboId),
            Granted(84, SubscriptionId), // (Coupon) FREE
            Granted(84, EliteId),
            Granted(84, TurboId),
            Granted(99, SubscriptionId), // (Help a Friend)
            Granted(99, EliteId),
            Granted(99, TurboId),

            // ─── Enrollment: Member-only (no Ambassador subscription) ──────
            Granted(13, VipId),     // VIP Member
            Granted(16, EliteId),   // Elite Member
            Granted(19, EliteId),   // Elite Special
            Granted(80, EliteId),   // Elite Member + TURBO
            Granted(80, TurboId),
            Granted(89, EliteId),   // Elite Member FREE
            Granted(90, EliteId),   // Elite Member + TURBO FREE
            Granted(90, TurboId),
            Granted(92, VipId),     // VIP Member FREE
            Granted(6,  EliteId),   // Travel Advantage Elite (Signup)

            // ─── Special promo / shortcut tokens ───────────────────────────
            Granted(94, SubscriptionId), // Elite Ambassador SpecialPromo
            Granted(94, EliteId),
            Granted(96, SubscriptionId), // Turbo Ambassador SpecialPromo
            Granted(96, EliteId),
            Granted(96, TurboId),

            // ─── Monthly tokens that map to existing products ──────────────
            Granted(3, EliteId),         // Monthly: Elite
            Granted(4, VipId),           // Monthly: VIP

            // ─── Annual: Biz Center → Subscription (ambassador renewal) ────
            Granted(23, SubscriptionId),
            Granted(10, SubscriptionId), // Annual Fee → Subscription

            // ─── Upgrade: Guest → VIP ──────────────────────────────────────
            UpgradeFrom(56, GuestMemberId),
            UpgradeTo  (56, VipId),

            // ─── Upgrade: Guest → Elite ────────────────────────────────────
            UpgradeFrom(59, GuestMemberId),
            UpgradeTo  (59, EliteId),

            // ─── Upgrade: VIP → Elite ──────────────────────────────────────
            UpgradeFrom(62, VipId),
            UpgradeTo  (62, EliteId),

            // ─── Upgrade: Elite → Turbo ────────────────────────────────────
            UpgradeFrom(66, EliteId),
            UpgradeTo  (66, TurboId)
        );
    }
}
