using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

public static class RootAmbassadorSeeder
{
    private const string RootMemberCode = "ROOT001";

    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        // Idempotent — if root already exists, skip entirely
        if (await db.MemberProfiles.AnyAsync(m => m.MemberId == RootMemberCode))
        {
            logger.LogDebug("Root ambassador already exists. Skipping seed.");
            return;
        }

        var cfg = configuration.GetSection("RootAmbassador");
        var email     = cfg["Email"]     ?? "root@mlmconqueror.com";
        var firstName = cfg["FirstName"] ?? "Corporate";
        var lastName  = cfg["LastName"]  ?? "Root";
        var password  = cfg["Password"]  ?? throw new InvalidOperationException(
            "RootAmbassador:Password must be configured in appsettings.");

        var now = DateTime.UtcNow;

        // ── 1. MemberProfile ──────────────────────────────────────────────────
        var member = new MemberProfile
        {
            UserId          = Guid.NewGuid(),
            MemberId        = RootMemberCode,
            FirstName       = firstName,
            LastName        = lastName,
            DateOfBirth     = new DateTime(1980, 1, 1),
            MemberType      = MemberType.Ambassador,
            Status          = MemberAccountStatus.Active,
            EnrollDate      = now,
            SponsorMemberId = null,
            CreatedBy       = "SYSTEM",
            CreationDate    = now,
            LastUpdateDate  = now
        };

        // ── 2. Genealogy tree root node ───────────────────────────────────────
        var genealogy = new GenealogyEntity
        {
            MemberId       = RootMemberCode,
            ParentMemberId = null,
            HierarchyPath  = $"/{RootMemberCode}/",
            Level          = 0,
            CreatedBy      = "SYSTEM",
            CreationDate   = now,
            LastUpdateDate = now
        };

        // ── 3. Dual team root node ────────────────────────────────────────────
        var dualTeam = new DualTeamEntity
        {
            MemberId       = RootMemberCode,
            ParentMemberId = null,
            HierarchyPath  = $"/{RootMemberCode}/",
            Side           = TreeSide.Left,
            LeftLegPoints  = 0,
            RightLegPoints = 0,
            CreatedBy      = "SYSTEM",
            CreationDate   = now,
            LastUpdateDate = now
        };

        // ── 4. Membership subscription — highest active level ─────────────────
        var topLevel = await db.MembershipLevels
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.SortOrder)
            .FirstOrDefaultAsync();

        MembershipSubscription? subscription = null;
        if (topLevel is not null)
        {
            subscription = new MembershipSubscription
            {
                Id                 = Guid.NewGuid().ToString(),
                MemberId           = RootMemberCode,
                MembershipLevelId  = topLevel.Id,
                ChangeReason       = SubscriptionChangeReason.New,
                SubscriptionStatus = MembershipStatus.Active,
                StartDate          = now,
                IsFree             = true,
                IsAutoRenew        = false,
                CreatedBy          = "SYSTEM",
                CreationDate       = now,
                LastUpdateDate     = now
            };
        }

        // ── 5. Persist domain entities ────────────────────────────────────────
        await db.MemberProfiles.AddAsync(member);
        await db.GenealogyTree.AddAsync(genealogy);
        await db.DualTeamTree.AddAsync(dualTeam);
        if (subscription is not null)
            await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        // ── 6. ApplicationUser (Identity) ─────────────────────────────────────
        var appUser = new ApplicationUser
        {
            Id                 = Guid.NewGuid().ToString(),
            UserName           = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email              = email,
            NormalizedEmail    = email.ToUpperInvariant(),
            EmailConfirmed     = true,
            MemberProfileId    = RootMemberCode,
            IsActive           = true,
            CreationDate       = now,
            CreatedBy          = "SYSTEM"
        };

        var createResult = await userManager.CreateAsync(appUser, password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create root ambassador user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        // Assign both SuperAdmin and Ambassador roles
        await userManager.AddToRolesAsync(appUser, ["SuperAdmin", "Ambassador"]);

        logger.LogInformation("Root ambassador '{Email}' seeded successfully.", email);
    }
}
