using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Commission trigger rules per token type.
///
/// CommissionTypeId — primary enrollment commission that fires for this token:
///   1 = Member Bonus VIP   (VIP / Pro / Plus / VIP180 / VIP365 level)
///   2 = Member Bonus Elite (Elite / Elite180 level)
///   3 = Member Bonus Turbo (Turbo membership level)
///  40 = Token Deduction    (no enrollment commission — monthly, upgrade, fee, FREE tokens)
///
/// Trigger flag rules:
///   - FREE tokens (IDs 81-93): no commissions fire on signup (all flags false).
///   - "Guest to X" upgrades (IDs 56-60, 77): guest becomes a first-time paying
///     member → enrollment commissions fire as if a new signup.
///   - Paid-to-paid upgrades (IDs 17, 35-43, 52-53, 61-63, 66):
///     residual eligible; no enrollment commissions.
///   - Monthly / Annual / Fee tokens: residual eligible; no enrollment commissions.
///   - TURBO add-on (in token name): TriggerBuilderBonusTurbo = TriggerSponsorBonusTurbo = true.
///   - Ambassador-only enrollment (ID 8): no membership level → no enrollment commissions.
///   - No-commission tokens (IDs 9, 10, 14, 20, 23, 24, 25, 6, 7): all flags false.
/// </summary>
public class TokenTypeCommissionConfiguration : IEntityTypeConfiguration<TokenTypeCommission>
{
    private static readonly DateTime S = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    // Helper shortcuts
    private static TokenTypeCommission Enroll(int id, int tokenTypeId, int commissionTypeId,
        bool turbo = false, bool guestUpgrade = false) => new()
    {
        Id = id, TokenTypeId = tokenTypeId, CommissionTypeId = commissionTypeId,
        CommissionPerToken = 0m,
        TriggerSponsorBonus       = true,
        TriggerBuilderBonus       = true,
        TriggerSponsorBonusTurbo  = turbo,
        TriggerBuilderBonusTurbo  = turbo,
        TriggerFastStartBonus     = !guestUpgrade, // guest upgrades don't count toward FSB window
        TriggerBoostBonus         = true,
        CarBonusEligible          = true,
        PresidentialBonusEligible = true,
        EligibleMembershipResidual = true,
        EligibleDailyResidual      = true,
        CreationDate = S, CreatedBy = "seed"
    };

    private static TokenTypeCommission Free(int id, int tokenTypeId) => new()
    {
        Id = id, TokenTypeId = tokenTypeId, CommissionTypeId = 40,
        CommissionPerToken = 0m,
        TriggerSponsorBonus = false, TriggerBuilderBonus = false,
        TriggerSponsorBonusTurbo = false, TriggerBuilderBonusTurbo = false,
        TriggerFastStartBonus = false, TriggerBoostBonus = false,
        CarBonusEligible = false, PresidentialBonusEligible = false,
        EligibleMembershipResidual = false, EligibleDailyResidual = false,
        CreationDate = S, CreatedBy = "seed"
    };

    private static TokenTypeCommission Recurring(int id, int tokenTypeId) => new()
    {
        Id = id, TokenTypeId = tokenTypeId, CommissionTypeId = 40,
        CommissionPerToken = 0m,
        TriggerSponsorBonus = false, TriggerBuilderBonus = false,
        TriggerSponsorBonusTurbo = false, TriggerBuilderBonusTurbo = false,
        TriggerFastStartBonus = false, TriggerBoostBonus = false,
        CarBonusEligible = false, PresidentialBonusEligible = false,
        EligibleMembershipResidual = true, EligibleDailyResidual = true,
        CreationDate = S, CreatedBy = "seed"
    };

    private static TokenTypeCommission Upgrade(int id, int tokenTypeId) => new()
    {
        Id = id, TokenTypeId = tokenTypeId, CommissionTypeId = 40,
        CommissionPerToken = 0m,
        TriggerSponsorBonus = false, TriggerBuilderBonus = false,
        TriggerSponsorBonusTurbo = false, TriggerBuilderBonusTurbo = false,
        TriggerFastStartBonus = false, TriggerBoostBonus = false,
        CarBonusEligible = false, PresidentialBonusEligible = false,
        EligibleMembershipResidual = true, EligibleDailyResidual = true,
        CreationDate = S, CreatedBy = "seed"
    };

    private static TokenTypeCommission NoCommission(int id, int tokenTypeId) => new()
    {
        Id = id, TokenTypeId = tokenTypeId, CommissionTypeId = 40,
        CommissionPerToken = 0m,
        TriggerSponsorBonus = false, TriggerBuilderBonus = false,
        TriggerSponsorBonusTurbo = false, TriggerBuilderBonusTurbo = false,
        TriggerFastStartBonus = false, TriggerBoostBonus = false,
        CarBonusEligible = false, PresidentialBonusEligible = false,
        EligibleMembershipResidual = false, EligibleDailyResidual = false,
        CreationDate = S, CreatedBy = "seed"
    };

    public void Configure(EntityTypeBuilder<TokenTypeCommission> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CommissionPerToken).HasColumnType("decimal(12,2)");
        builder.HasIndex(x => new { x.TokenTypeId, x.CommissionTypeId }).IsUnique();

        builder.HasData(

            // ═══════════════════════════════════════════════════════════════════
            // ENROLLMENT: AMBASSADOR + VIP / PRO / PLUS LEVEL  (CommissionTypeId=1)
            // ═══════════════════════════════════════════════════════════════════
            Enroll(id: 1,  tokenTypeId: 1,  commissionTypeId: 1),  // Ambassador + Pro
            Enroll(id: 2,  tokenTypeId: 2,  commissionTypeId: 1),  // Guest Member (IsGuestPass)
            Enroll(id: 12, tokenTypeId: 12, commissionTypeId: 1),  // Ambassador + Pro + Event
            Enroll(id: 13, tokenTypeId: 13, commissionTypeId: 1),  // VIP Member
            Enroll(id: 15, tokenTypeId: 15, commissionTypeId: 1),  // Pro Member
            Enroll(id: 28, tokenTypeId: 28, commissionTypeId: 1),  // Ambassador + Pro180
            Enroll(id: 29, tokenTypeId: 29, commissionTypeId: 1),  // Ambassador + Pro180 + Event
            Enroll(id: 30, tokenTypeId: 30, commissionTypeId: 1),  // Ambassador + Plus180
            Enroll(id: 31, tokenTypeId: 31, commissionTypeId: 1),  // Ambassador + Plus180 + Event
            Enroll(id: 33, tokenTypeId: 33, commissionTypeId: 1),  // Pro180 Member
            Enroll(id: 34, tokenTypeId: 34, commissionTypeId: 1),  // Plus180 Member
            Enroll(id: 49, tokenTypeId: 49, commissionTypeId: 1),  // Ambassador + Plus
            Enroll(id: 50, tokenTypeId: 50, commissionTypeId: 1),  // Ambassador + Plus + Event
            Enroll(id: 54, tokenTypeId: 54, commissionTypeId: 1),  // Plus Member
            Enroll(id: 64, tokenTypeId: 64, commissionTypeId: 1),  // Ambassador + VIP
            Enroll(id: 65, tokenTypeId: 65, commissionTypeId: 1),  // Ambassador + VIP + Event
            Enroll(id: 67, tokenTypeId: 67, commissionTypeId: 1),  // Ambassador + VIP 365
            Enroll(id: 68, tokenTypeId: 68, commissionTypeId: 1),  // Ambassador + VIP 365 + Event
            Enroll(id: 75, tokenTypeId: 75, commissionTypeId: 1),  // Ambassador + VIP 180
            Enroll(id: 76, tokenTypeId: 76, commissionTypeId: 1),  // Ambassador + VIP 180 + Event
            Enroll(id: 79, tokenTypeId: 79, commissionTypeId: 1),  // VIP 180 Member
            Enroll(id: 95, tokenTypeId: 95, commissionTypeId: 1),  // Plus Ambassador SpecialPromo
            Enroll(id: 97, tokenTypeId: 97, commissionTypeId: 1),  // Ambassador + Plus (Help a Friend)

            // ═══════════════════════════════════════════════════════════════════
            // ENROLLMENT: AMBASSADOR + ELITE LEVEL  (CommissionTypeId=2)
            // ═══════════════════════════════════════════════════════════════════
            Enroll(id: 5,  tokenTypeId: 5,  commissionTypeId: 2),  // Ambassador + Elite
            Enroll(id: 11, tokenTypeId: 11, commissionTypeId: 2),  // Ambassador + Elite + Event
            Enroll(id: 16, tokenTypeId: 16, commissionTypeId: 2),  // Elite Member
            Enroll(id: 19, tokenTypeId: 19, commissionTypeId: 2),  // Elite Special
            Enroll(id: 26, tokenTypeId: 26, commissionTypeId: 2),  // Ambassador + Elite180
            Enroll(id: 27, tokenTypeId: 27, commissionTypeId: 2),  // Ambassador + Elite180 + Event
            Enroll(id: 32, tokenTypeId: 32, commissionTypeId: 2),  // Elite180 Member
            Enroll(id: 71, tokenTypeId: 71, commissionTypeId: 2),  // Ambassador + Elite (Coupon)
            Enroll(id: 72, tokenTypeId: 72, commissionTypeId: 2),  // Ambassador + Elite + Event (Coupon)
            Enroll(id: 94, tokenTypeId: 94, commissionTypeId: 2),  // Elite Ambassador SpecialPromo
            Enroll(id: 98, tokenTypeId: 98, commissionTypeId: 2),  // Ambassador + Elite (Help a Friend)

            // ═══════════════════════════════════════════════════════════════════
            // ENROLLMENT: AMBASSADOR + ELITE + TURBO PROGRAM  (CommissionTypeId=2, turbo=true)
            // TURBO is a separate builder-bonus program on top of Elite membership.
            // ═══════════════════════════════════════════════════════════════════
            Enroll(id: 69, tokenTypeId: 69, commissionTypeId: 2, turbo: true),  // Ambassador + Elite + TURBO
            Enroll(id: 70, tokenTypeId: 70, commissionTypeId: 2, turbo: true),  // Ambassador + Elite + Event + TURBO
            Enroll(id: 73, tokenTypeId: 73, commissionTypeId: 2, turbo: true),  // Ambassador + Elite + Event + TURBO (Coupon)
            Enroll(id: 74, tokenTypeId: 74, commissionTypeId: 2, turbo: true),  // Ambassador + Elite + TURBO (Coupon)
            Enroll(id: 80, tokenTypeId: 80, commissionTypeId: 2, turbo: true),  // Elite Member + TURBO
            Enroll(id: 96, tokenTypeId: 96, commissionTypeId: 2, turbo: true),  // Turbo Ambassador SpecialPromo
            Enroll(id: 99, tokenTypeId: 99, commissionTypeId: 2, turbo: true),  // Ambassador + Elite + TURBO (Help a Friend)

            // ═══════════════════════════════════════════════════════════════════
            // UPGRADES: GUEST → PAID  (enrollment commissions fire; FSB excluded)
            // Guest members converting to paid membership for the first time.
            // ═══════════════════════════════════════════════════════════════════
            Enroll(id: 56, tokenTypeId: 56, commissionTypeId: 1, guestUpgrade: true),  // Guest → VIP
            Enroll(id: 57, tokenTypeId: 57, commissionTypeId: 1, guestUpgrade: true),  // Guest → VIP 365
            Enroll(id: 58, tokenTypeId: 58, commissionTypeId: 1, guestUpgrade: true),  // Guest → Plus
            Enroll(id: 59, tokenTypeId: 59, commissionTypeId: 2, guestUpgrade: true),  // Guest → Elite
            Enroll(id: 60, tokenTypeId: 60, commissionTypeId: 2, guestUpgrade: true),  // Guest → Elite180
            Enroll(id: 77, tokenTypeId: 77, commissionTypeId: 1, guestUpgrade: true),  // Guest → VIP 180

            // ═══════════════════════════════════════════════════════════════════
            // UPGRADES: PAID → PAID  (residual eligible; no enrollment commissions)
            // ═══════════════════════════════════════════════════════════════════
            Upgrade(id: 17, tokenTypeId: 17),  // Pro → Elite
            Upgrade(id: 35, tokenTypeId: 35),  // Plus180 → Pro
            Upgrade(id: 36, tokenTypeId: 36),  // Plus180 → Pro180
            Upgrade(id: 37, tokenTypeId: 37),  // Plus180 → Elite
            Upgrade(id: 38, tokenTypeId: 38),  // Plus180 → Elite180
            Upgrade(id: 39, tokenTypeId: 39),  // Pro → Pro180
            Upgrade(id: 40, tokenTypeId: 40),  // Pro → Elite180
            Upgrade(id: 41, tokenTypeId: 41),  // Pro180 → Elite
            Upgrade(id: 42, tokenTypeId: 42),  // Pro180 → Elite180
            Upgrade(id: 43, tokenTypeId: 43),  // Elite → Elite180
            Upgrade(id: 52, tokenTypeId: 52),  // Plus → Elite
            Upgrade(id: 53, tokenTypeId: 53),  // Plus → Elite180
            Upgrade(id: 61, tokenTypeId: 61),  // VIP → Plus
            Upgrade(id: 62, tokenTypeId: 62),  // VIP → Elite
            Upgrade(id: 63, tokenTypeId: 63),  // VIP → Elite180
            Upgrade(id: 66, tokenTypeId: 66),  // Elite → Turbo

            // ═══════════════════════════════════════════════════════════════════
            // MONTHLY / ANNUAL / RECURRING  (residual eligible; no enrollment commissions)
            // ═══════════════════════════════════════════════════════════════════
            Recurring(id: 3,  tokenTypeId: 3),   // Monthly: Elite
            Recurring(id: 4,  tokenTypeId: 4),   // Monthly: VIP
            Recurring(id: 22, tokenTypeId: 22),  // Monthly: Pro
            Recurring(id: 21, tokenTypeId: 21),  // Annual: VIP 365
            Recurring(id: 44, tokenTypeId: 44),  // Monthly: Elite180 Level 2
            Recurring(id: 45, tokenTypeId: 45),  // Monthly: Elite180 Level 3
            Recurring(id: 46, tokenTypeId: 46),  // Monthly: Pro180 Level 2
            Recurring(id: 47, tokenTypeId: 47),  // Monthly: Pro180 Level 3
            Recurring(id: 48, tokenTypeId: 48),  // Monthly: Plus180
            Recurring(id: 51, tokenTypeId: 51),  // Monthly: Plus
            Recurring(id: 55, tokenTypeId: 55),  // Monthly: Elite180 (59.97)
            Recurring(id: 78, tokenTypeId: 78),  // Recurring: VIP 180

            // ═══════════════════════════════════════════════════════════════════
            // FREE TOKENS  — no sponsor commissions fire on signup (all flags false)
            // Rule: FREE ambassador tokens suppress all enrollment commissions.
            // ═══════════════════════════════════════════════════════════════════
            Free(id: 81, tokenTypeId: 81),  // Ambassador + Elite FREE
            Free(id: 82, tokenTypeId: 82),  // Ambassador + Elite (Coupon) FREE
            Free(id: 83, tokenTypeId: 83),  // Ambassador + Elite + TURBO FREE
            Free(id: 84, tokenTypeId: 84),  // Ambassador + Elite + TURBO (Coupon) FREE
            Free(id: 85, tokenTypeId: 85),  // Ambassador + Plus FREE
            Free(id: 86, tokenTypeId: 86),  // Ambassador + VIP FREE
            Free(id: 87, tokenTypeId: 87),  // Ambassador + VIP 180 FREE
            Free(id: 88, tokenTypeId: 88),  // Ambassador FREE
            Free(id: 89, tokenTypeId: 89),  // Elite Member FREE  (no sponsor bonus for Elite level)
            Free(id: 90, tokenTypeId: 90),  // Elite Member + TURBO FREE  (no commissions for Elite or Turbo)
            Free(id: 91, tokenTypeId: 91),  // Plus Member FREE
            Free(id: 92, tokenTypeId: 92),  // VIP Member FREE
            Free(id: 93, tokenTypeId: 93),  // VIP 180 FREE

            // ═══════════════════════════════════════════════════════════════════
            // NO COMMISSION — specialty fees, no-commission products, inactive
            // ═══════════════════════════════════════════════════════════════════
            NoCommission(id: 8,  tokenTypeId: 8),   // Ambassador only (no membership level)
            NoCommission(id: 9,  tokenTypeId: 9),   // Enrollment Pro ($99.97 / no commission)
            NoCommission(id: 6,  tokenTypeId: 6),   // Travel Advantage Elite (Signup)
            NoCommission(id: 7,  tokenTypeId: 7),   // Travel Advantage Lite
            NoCommission(id: 10, tokenTypeId: 10),  // Annual Fee
            NoCommission(id: 14, tokenTypeId: 14),  // Mobile App
            NoCommission(id: 20, tokenTypeId: 20),  // --Available-- (inactive)
            NoCommission(id: 23, tokenTypeId: 23),  // Annual: Biz Center
            NoCommission(id: 24, tokenTypeId: 24),  // Legacy Biz Center Fee
            NoCommission(id: 25, tokenTypeId: 25)   // Monthly: Mall
        );
    }
}
