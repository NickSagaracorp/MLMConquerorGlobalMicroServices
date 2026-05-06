using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Full product/token type catalog matching the MWR Life platform (IDs 1-99).
/// IsGuestPass = true only for ID 2 (Guest Member) — given to prospects for free access.
/// FREE tokens (IDs 81-93) are promotions that do not trigger sponsor commissions on signup.
/// ID 18 is a reserved gap. ID 20 is inactive (--Available--).
///
/// Category is set on every row to drive product-relation rules:
///   None       — no product association needed
///   Enrollment — token grants product(s) on signup (TokenTypeProduct rows with Role=Granted)
///   Upgrade    — migrates a member from one product to another (UpgradeFrom + UpgradeTo)
///   Monthly    — recurring monthly billing token
///   Annual     — recurring annual / 180-day billing token
///   Other      — misc fees, mobile app, legacy items
/// </summary>
public class TokenTypeConfiguration : IEntityTypeConfiguration<TokenType>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<TokenType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.TemplateUrl).HasMaxLength(1000);
        builder.Property(x => x.Category).HasConversion<int>().HasDefaultValue(TokenCategory.None);

        builder.HasMany(x => x.ProductLinks)
               .WithOne()
               .HasForeignKey(p => p.TokenTypeId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(

            // ── Enrollment tokens ───────────────────────────────────────────
            new TokenType { Id = 1,  Name = "Enrollment: Ambassador + Pro",                      Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 2,  Name = "Guest Member",                                      Category = TokenCategory.Enrollment, IsGuestPass = true,  IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 5,  Name = "Enrollment: Ambassador + Elite",                    Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 8,  Name = "Enrollment: Ambassador",                            Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 11, Name = "Enrollment: Ambassador + Elite + Event",            Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 12, Name = "Enrollment: Ambassador + Pro + Event",              Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 26, Name = "Enrollment: Ambassador + Elite180",                 Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 27, Name = "Enrollment: Ambassador + Elite180 + Event",         Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 28, Name = "Enrollment: Ambassador + Pro180",                   Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 29, Name = "Enrollment: Ambassador + Pro180 + Event",           Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 30, Name = "Enrollment: Ambassador + Plus180",                  Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 31, Name = "Enrollment: Ambassador + Plus180 + Event",          Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 49, Name = "Enrollment: Ambassador + Plus",                     Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 50, Name = "Enrollment: Ambassador + Plus + Event",             Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 64, Name = "Enrollment: Ambassador + VIP",                      Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 65, Name = "Enrollment: Ambassador + VIP + Event",              Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 67, Name = "Enrollment: Ambassador + VIP 365",                  Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 68, Name = "Enrollment: Ambassador + VIP 365 + Event",          Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 75, Name = "Enrollment: Ambassador + VIP 180",                  Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 76, Name = "Enrollment: Ambassador + VIP 180 + Event",          Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            new TokenType { Id = 69, Name = "Enrollment: Ambassador + Elite + TURBO",             Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 70, Name = "Enrollment: Ambassador + Elite + Event + TURBO",     Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 71, Name = "Enrollment: Ambassador + Elite (Coupon)",            Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 72, Name = "Enrollment: Ambassador + Elite + Event (Coupon)",    Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 73, Name = "Enrollment: Ambassador + Elite + Event + TURBO (Coupon)", Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 74, Name = "Enrollment: Ambassador + Elite + TURBO (Coupon)",   Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 80, Name = "Enrollment: Elite Member + TURBO",                   Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            new TokenType { Id = 13, Name = "Enrollment: VIP Member",                            Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 15, Name = "Enrollment: Pro Member",                            Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 16, Name = "Enrollment: Elite Member",                          Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 19, Name = "Enrollment: Elite Special",                         Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 32, Name = "Enrollment: Elite180 Member",                       Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 33, Name = "Enrollment: Pro180 Member",                         Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 34, Name = "Enrollment: Plus180 Member",                        Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 54, Name = "Enrollment: Plus Member",                           Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 79, Name = "Enrollment: VIP 180 Member",                        Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            new TokenType { Id = 9,  Name = "Enrollment Pro ($99.97 cost / no commission)",      Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            new TokenType { Id = 81, Name = "Enrollment: Ambassador + Elite FREE",               Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 82, Name = "Enrollment: Ambassador + Elite (Coupon) FREE",      Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 83, Name = "Enrollment: Ambassador + Elite + TURBO FREE",       Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 84, Name = "Enrollment: Ambassador + Elite + TURBO (Coupon) FREE", Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 85, Name = "Enrollment: Ambassador + Plus FREE",                Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 86, Name = "Enrollment: Ambassador + VIP FREE",                 Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 87, Name = "Enrollment: Ambassador + VIP 180 FREE",             Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 88, Name = "Enrollment: Ambassador FREE",                       Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 89, Name = "Enrollment: Elite Member FREE",                     Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 90, Name = "Enrollment: Elite Member + TURBO FREE",             Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 91, Name = "Enrollment: Plus Member FREE",                      Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 92, Name = "Enrollment: VIP Member FREE",                       Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 93, Name = "Enrollment: VIP 180 FREE",                          Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            new TokenType { Id = 94, Name = "Elite Ambassador SpecialPromo",                     Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 95, Name = "Plus Ambassador SpecialPromo",                      Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 96, Name = "Turbo Ambassador SpecialPromo",                     Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 97, Name = "Enrollment: Ambassador + Plus (Help a Friend)",     Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 98, Name = "Enrollment: Ambassador + Elite (Help a Friend)",    Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 99, Name = "Enrollment: Ambassador + Elite + TURBO (Help a Friend)", Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            // ── Monthly tokens ──────────────────────────────────────────────
            new TokenType { Id = 3,  Name = "Monthly: Elite",                                    Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 4,  Name = "Monthly: VIP",                                      Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 22, Name = "Monthly: Pro",                                      Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 44, Name = "Monthly: Elite180 Level 2 (79.97)",                 Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 45, Name = "Monthly: Elite180 Level 3 (39.97)",                 Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 46, Name = "Monthly: Pro180 Level 2",                           Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 47, Name = "Monthly: Pro180 Level 3",                           Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 48, Name = "Monthly: Plus180",                                  Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 51, Name = "Monthly: Plus",                                     Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 55, Name = "Monthly: Elite180 (59.97)",                         Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 25, Name = "Monthly: Mall",                                     Category = TokenCategory.Monthly, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            // ── Annual / Recurring ─────────────────────────────────────────
            new TokenType { Id = 21, Name = "Annual: VIP 365",                                   Category = TokenCategory.Annual, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 23, Name = "Annual: Biz Center",                                Category = TokenCategory.Annual, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 78, Name = "Recurring: VIP 180",                                Category = TokenCategory.Annual, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            // ── Upgrade tokens (pair UpgradeFrom + UpgradeTo) ──────────────
            new TokenType { Id = 56, Name = "Upgrade: Guest to VIP",                             Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 57, Name = "Upgrade: Guest to VIP 365",                         Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 58, Name = "Upgrade: Guest to Plus",                            Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 59, Name = "Upgrade: Guest to Elite",                           Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 60, Name = "Upgrade: Guest to Elite180",                        Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 77, Name = "Upgrade: Guest to VIP 180",                         Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            new TokenType { Id = 17, Name = "Upgrade: Pro to Elite",                             Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 35, Name = "Upgrade: Plus180 to Pro",                           Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 36, Name = "Upgrade: Plus180 to Pro180",                        Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 37, Name = "Upgrade: Plus180 to Elite",                         Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 38, Name = "Upgrade: Plus180 to Elite180",                      Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 39, Name = "Upgrade: Pro to Pro180",                            Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 40, Name = "Upgrade: Pro to Elite180",                          Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 41, Name = "Upgrade: Pro180 to Elite",                          Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 42, Name = "Upgrade: Pro180 to Elite180",                       Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 43, Name = "Upgrade: Elite to Elite180",                        Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 52, Name = "Upgrade: Plus to Elite",                            Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 53, Name = "Upgrade: Plus to Elite180",                         Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 61, Name = "Upgrade: VIP to Plus",                              Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 62, Name = "Upgrade: VIP to Elite",                             Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 63, Name = "Upgrade: VIP to Elite180",                          Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 66, Name = "Upgrade: Elite to Turbo",                           Category = TokenCategory.Upgrade, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },

            // ── Other / legacy ──────────────────────────────────────────────
            new TokenType { Id = 6,  Name = "Travel Advantage Elite (Signup)",                   Category = TokenCategory.Enrollment, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 7,  Name = "Travel Advantage Lite",                             Category = TokenCategory.Other, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 10, Name = "Annual Fee",                                        Category = TokenCategory.Annual, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 14, Name = "Mobile App",                                        Category = TokenCategory.Other, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 24, Name = "Legacy Biz Center Fee",                             Category = TokenCategory.Other, IsGuestPass = false, IsActive = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new TokenType { Id = 20, Name = "--Available--",                                     Category = TokenCategory.None, IsGuestPass = false, IsActive = false, CreationDate = SeedDate, CreatedBy = "seed" }
        );
    }
}
