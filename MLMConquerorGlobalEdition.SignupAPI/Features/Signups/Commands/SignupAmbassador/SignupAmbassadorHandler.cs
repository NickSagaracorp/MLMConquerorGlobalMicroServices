using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupAmbassador;

/// <summary>
/// Phase 1 of the ambassador signup wizard.
/// Creates a pending member, order, and subscription. Identity user is created but inactive.
/// Returns the signupId (orderId) for subsequent Select-Products and Complete steps.
/// </summary>
public class SignupAmbassadorHandler : IRequestHandler<SignupAmbassadorCommand, Result<SignupResponse>>
{
    private readonly AppDbContext                 _db;
    private readonly IDateTimeProvider            _dateTime;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPushNotificationService     _push;
    private readonly IEncryptionService           _encryption;

    public SignupAmbassadorHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        UserManager<ApplicationUser> userManager,
        IPushNotificationService push,
        IEncryptionService encryption)
    {
        _db          = db;
        _dateTime    = dateTime;
        _userManager = userManager;
        _push        = push;
        _encryption  = encryption;
    }

    public async Task<Result<SignupResponse>> Handle(SignupAmbassadorCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var now = _dateTime.Now;

        var age = now.Year - req.DateOfBirth.Year;
        if (req.DateOfBirth.Date > now.AddYears(-age)) age--;
        if (age < 18)
            return Result<SignupResponse>.Failure("UNDERAGE", "Applicant must be at least 18 years old.");

        var emailTaken = await _userManager.FindByEmailAsync(req.Email);
        if (emailTaken is not null)
            return Result<SignupResponse>.Failure("EMAIL_TAKEN", "This email is already registered.");

        // La cadena vacía NO es NULL, y el índice único de MemberProfiles.ReplicateSiteSlug está
        // filtrado por IS NOT NULL: "" entra en la unicidad como cualquier otro valor. Con lo que
        // había, la primera alta sin nombre de sitio pasaba y TODAS las siguientes reventaban con
        // clave duplicada, que al usuario le salía como "An unexpected error occurred". En un
        // evento, donde casi nadie elige nombre de sitio, se daba de alta el primero y fallaban
        // los demás.
        //
        // Se normaliza AQUÍ, en el servidor, y no en el asistente: el cliente puede mandar "",
        // "   ", null o no mandar nada, y ninguna de esas cuatro cosas puede acabar en la
        // columna. Lo que persiste más abajo es esta variable, no req.ReplicateSiteSlug.
        var replicateSiteSlug = string.IsNullOrWhiteSpace(req.ReplicateSiteSlug)
            ? null
            : req.ReplicateSiteSlug.Trim();

        if (replicateSiteSlug is not null)
        {
            var slugExists = await _db.MemberProfiles
                .AnyAsync(x => x.ReplicateSiteSlug == replicateSiteSlug, ct);
            if (slugExists)
                throw new DuplicateReplicateSiteException(replicateSiteSlug);
        }

        // Resolve sponsor by either replicate-site slug OR MemberId — referral links use both.
        string? sponsorMemberId = null;
        if (!string.IsNullOrEmpty(req.SponsorReplicateSite))
        {
            sponsorMemberId = await _db.MemberProfiles
                .AsNoTracking()
                .Where(x => (x.ReplicateSiteSlug == req.SponsorReplicateSite ||
                             x.MemberId == req.SponsorReplicateSite)
                         && x.Status == MemberAccountStatus.Active)
                .Select(x => x.MemberId)
                .FirstOrDefaultAsync(ct);

            if (sponsorMemberId is null)
                return Result<SignupResponse>.Failure(
                    "SPONSOR_NOT_FOUND", $"Sponsor site '{req.SponsorReplicateSite}' not found or inactive.");
        }

        var membershipLevel = await _db.MembershipLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.MembershipLevelId && x.IsActive, ct);
        if (membershipLevel is null)
            return Result<SignupResponse>.Failure(
                "MEMBERSHIP_LEVEL_NOT_FOUND", "The selected membership level is invalid or inactive.");

        // Build + save the whole signup inside a retry loop. GenerateUniqueMemberIdAsync probes
        // for a free id, but under true simultaneity two requests can pass that existence check
        // for the same id; AK_MemberProfiles_MemberId then rejects the loser's insert. Rather
        // than surface a 500/INTERNAL_ERROR we clear the rejected batch, draw a fresh id and
        // rebuild. The single SaveChanges is one transaction, so a failed attempt leaves no
        // orphan rows.
        string memberId;
        string orderId;
        GenealogyEntity? sponsorNode = null;
        for (var idAttempt = 0; ; idAttempt++)
        {
        var candidateId = await GenerateUniqueMemberIdAsync(ct);
        if (candidateId is null)
            return Result<SignupResponse>.Failure(
                "MEMBER_ID_EXHAUSTED", "Could not allocate a unique member id. The AMB-###### space is saturated.");
        memberId = candidateId;
        try
        {

        // ── Payout defaults ──────────────────────────────────────────────────
        // Company-level frequency seeds every new ambassador; country-level
        // gateway seeds the first MemberProfilesWallet row in Pending status.
        // Both are best-effort: if either lookup misses (no row, or country
        // string doesn't match Countries.NameEn / Iso2) we fall back to the
        // entity defaults — the ambassador can configure both from their
        // BizCenter profile + wallet tab afterwards.
        var companyInfo = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync(ct);
        var defaultFrequency = companyInfo?.DefaultPayoutFrequency ?? PayoutFrequency.Weekly;

        WalletType? defaultWalletType = null;
        var country = await _db.Countries.AsNoTracking()
            .FirstOrDefaultAsync(c => c.NameEn == req.Country || c.Iso2 == req.Country, ct);
        if (country is not null)
        {
            var payoutDefault = await _db.CountryPayoutDefaults.AsNoTracking()
                .FirstOrDefaultAsync(p => p.CountryIso2 == country.Iso2 && p.IsActive, ct);
            defaultWalletType = payoutDefault?.WalletType;
        }

        var member = new MemberProfile
        {
            UserId            = Guid.NewGuid(),
            MemberId          = memberId,
            Email             = req.Email,
            FirstName         = req.FirstName,
            LastName          = req.LastName,
            DateOfBirth       = req.DateOfBirth,
            Phone             = req.Phone,
            WhatsApp          = req.WhatsApp,
            Country           = req.Country,
            State             = req.State,
            City              = req.City,
            Address           = req.Address,
            ZipCode           = req.ZipCode,
            SsnEncrypted      = !string.IsNullOrEmpty(req.Ssn) ? _encryption.Encrypt(req.Ssn) : null,
            EinEncrypted      = !string.IsNullOrEmpty(req.Ein) ? _encryption.Encrypt(req.Ein) : null,
            BusinessName      = req.BusinessName,
            ShowBusinessName  = req.ShowBusinessName,
            MemberType        = MemberType.Ambassador,
            Status            = MemberAccountStatus.Pending,
            EnrollDate        = now,
            SponsorMemberId   = sponsorMemberId,
            ReplicateSiteSlug = replicateSiteSlug,
            PayoutFrequency   = defaultFrequency,
            CreatedBy         = req.Email,
            CreationDate      = now,
            LastUpdateDate    = now
        };

        orderId = Guid.NewGuid().ToString();
        string orderNo;
        do { orderNo = OrderNumberHelper.Generate(membershipLevel.Name, now); }
        while (await _db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct));

        var order = new Orders
        {
            Id             = orderId,
            MemberId       = memberId,
            OrderNo        = orderNo,
            TotalAmount    = 0,
            Status         = OrderStatus.Pending,
            OrderDate      = now,
            Notes          = $"Ambassador signup — {membershipLevel.Name}",
            CreatedBy      = req.Email,
            CreationDate   = now,
            LastUpdateDate = now
        };

        var subscriptionId = Guid.NewGuid().ToString();
        var subscription = new MembershipSubscription
        {
            Id                 = subscriptionId,
            MemberId           = memberId,
            MembershipLevelId  = req.MembershipLevelId,
            ChangeReason       = SubscriptionChangeReason.New,
            SubscriptionStatus = MembershipStatus.Pending,
            StartDate          = now,
            IsFree             = membershipLevel.IsFree,
            IsAutoRenew        = membershipLevel.IsAutoRenew,
            LastOrderId        = orderId,
            CreatedBy          = req.Email,
            CreationDate       = now,
            LastUpdateDate     = now
        };

        order.MembershipSubscriptionId = subscriptionId;

        sponsorNode = null;
        if (!string.IsNullOrEmpty(sponsorMemberId))
        {
            sponsorNode = await _db.GenealogyTree
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.MemberId == sponsorMemberId, ct);
        }

        var genealogyNode = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = sponsorMemberId,
            HierarchyPath  = sponsorNode is not null
                ? $"{sponsorNode.HierarchyPath}{memberId}/"
                : $"/{memberId}/",
            Level          = sponsorNode is not null ? sponsorNode.Level + 1 : 1,
            CreatedBy      = req.Email,
            CreationDate   = now,
            LastUpdateDate = now
        };

        // FSB countdown windows:
        //   Normal W1: 7d  | Extended W1 (promo): 14d | W2: 7d (after W1) | W3: 7d (after W2)
        var fsbCountdown = new MemberCommissionCountDown
        {
            MemberId                     = member.UserId,
            Member                       = member,
            FastStartBonus1Start         = now,
            FastStartBonus1End           = now.AddDays(7),
            FastStartBonus1ExtendedStart = now,
            FastStartBonus1ExtendedEnd   = now.AddDays(14),
            FastStartBonus2Start         = now.AddDays(7),
            FastStartBonus2End           = now.AddDays(14),
            FastStartBonus3Start         = now.AddDays(14),
            FastStartBonus3End           = now.AddDays(21),
            CreatedBy                    = req.Email,
            CreationDate                 = now,
            LastUpdateDate               = now
        };

        await _db.MemberProfiles.AddAsync(member, ct);
        await _db.Orders.AddAsync(order, ct);
        await _db.MembershipSubscriptions.AddAsync(subscription, ct);
        await _db.GenealogyTree.AddAsync(genealogyNode, ct);
        await _db.CommissionCountDowns.AddAsync(fsbCountdown, ct);

        // Sprint-15 Bug D — a sponsor-less ambassador (fresh tree root) used to
        // get only a GenealogyEntity row; the binary tree had no corresponding
        // DualTeamEntity. That left the new root unable to accumulate
        // LeftLegPoints / RightLegPoints and made every downstream PlaceMember
        // call against this root fail with "no root node". Mirror RootAmbassadorSeeder
        // and seed the binary root here too. Members WITH a sponsor are placed
        // separately by PlaceMember (auto or manual) so we deliberately do not
        // create a binary node for them at signup time.
        if (string.IsNullOrEmpty(sponsorMemberId))
        {
            var dualTeamRoot = new DualTeamEntity
            {
                MemberId       = memberId,
                ParentMemberId = null,
                HierarchyPath  = $"/{memberId}/",
                Side           = TreeSide.Left,
                LeftLegPoints  = 0,
                RightLegPoints = 0,
                CreatedBy      = req.Email,
                CreationDate   = now,
                LastUpdateDate = now
            };
            await _db.DualTeamTree.AddAsync(dualTeamRoot, ct);
        }

        // Seed the preferred wallet from the country's default gateway. Status
        // is Pending so the ambassador must complete the gateway-specific
        // fields (Dwolla account, eWallet credentials, etc.) before the
        // commission payout job actually targets it. We do not store any
        // credentials at signup — only the wallet TYPE.
        if (defaultWalletType.HasValue)
        {
            var wallet = new MemberProfilesWallet
            {
                Id             = Guid.NewGuid().ToString(),
                MemberId       = memberId,
                WalletType     = defaultWalletType.Value,
                Status         = WalletStatus.Pending,
                IsPreferred    = true,
                Notes          = $"Auto-assigned from country default ({country!.Iso2}) at signup.",
                CreatedBy      = req.Email,
                CreationDate   = now,
                LastUpdateDate = now
            };
            await _db.Wallets.AddAsync(wallet, ct);
        }

        // Queue rank re-evaluation for every genealogy upline of the sponsor
        if (sponsorNode is not null)
        {
            var uplineIds = sponsorNode.HierarchyPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (var uplineId in uplineIds)
            {
                await _db.RankEvaluationQueue.AddAsync(new RankEvaluationQueue
                {
                    TriggerMemberId  = memberId,
                    EvaluateMemberId = uplineId,
                    TriggerEvent     = RankEvaluationTrigger.Enrollment,
                    TriggerDate      = now,
                    CreatedBy        = req.Email,
                    CreationDate     = now
                }, ct);
            }
        }

            await _db.SaveChangesAsync(ct);
            break; // saved cleanly
        }
        catch (DbUpdateException ex) when (IsDuplicateMemberIdViolation(ex) && idAttempt < 5)
        {
            // Concurrent MemberId collision — drop the rejected batch and rebuild with a fresh id.
            _db.ChangeTracker.Clear();
        }
        }

        // Notify all uplines in the enrollment tree (genealogy)
        if (sponsorNode is not null)
        {
            var uplineIds = sponsorNode.HierarchyPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            foreach (var uplineId in uplineIds)
            {
                _ = _push.SendAsync(
                    uplineId,
                    NotificationEvents.DownlineEnrolled,
                    "New Enrollment in Your Team",
                    $"A new ambassador has enrolled in your downline.",
                    ct);
            }
        }

        var appUser = new ApplicationUser
        {
            Id                 = Guid.NewGuid().ToString(),
            UserName           = req.Email,
            NormalizedUserName = req.Email.ToUpperInvariant(),
            Email              = req.Email,
            NormalizedEmail    = req.Email.ToUpperInvariant(),
            EmailConfirmed     = false,
            MemberProfileId    = memberId,
            IsActive           = false,
            CreationDate       = now,
            CreatedBy          = req.Email
        };

        var createResult = await _userManager.CreateAsync(appUser, req.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<SignupResponse>.Failure("IDENTITY_CREATE_FAILED", errors);
        }

        await _userManager.AddToRoleAsync(appUser, "Ambassador");

        return Result<SignupResponse>.Success(new SignupResponse
        {
            SignupId   = orderId,
            MemberId   = memberId,
            Email      = req.Email,
            MemberType = nameof(MemberType.Ambassador),
            EnrollDate = now
            // AccessToken / RefreshToken are null — populated only after Complete step
        });
    }

    /// <summary>
    /// Allocate a member id that is not already taken. The original implementation returned a
    /// blind <c>Random.Shared.Next(1, 999999)</c> with no uniqueness check; once the AMB-######
    /// space filled up (80k+ members) ~8% of signups collided with an existing id even
    /// single-threaded, surfacing as a UNIQUE KEY violation (AK_MemberProfiles_MemberId) →
    /// DbUpdateException → INTERNAL_ERROR. We now probe the DB and pick a free id. The unique
    /// index remains the hard backstop for the rare concurrent race (two requests drawing the
    /// same free id in the same instant); such a loser fails its single SaveChanges transaction
    /// and rolls back cleanly (no orphan rows). Returns null only if the space is truly saturated.
    /// </summary>
    /// <summary>True when a save failed specifically because the MemberId unique index rejected
    /// a concurrent duplicate (so the caller can retry with a fresh id).</summary>
    private static bool IsDuplicateMemberIdViolation(DbUpdateException ex)
        => ex.InnerException?.Message is { } m
           && m.Contains("AK_MemberProfiles_MemberId", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> GenerateUniqueMemberIdAsync(CancellationToken ct)
    {
        const int maxAttempts = 100;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = $"AMB-{Random.Shared.Next(1, 1_000_000):D6}";
            if (!await _db.MemberProfiles.AsNoTracking().AnyAsync(x => x.MemberId == candidate, ct))
                return candidate;
        }
        return null;
    }
}
