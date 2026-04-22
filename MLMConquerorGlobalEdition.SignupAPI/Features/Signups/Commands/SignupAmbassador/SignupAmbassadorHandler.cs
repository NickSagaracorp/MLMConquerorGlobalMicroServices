using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
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

        if (!string.IsNullOrEmpty(req.ReplicateSiteSlug))
        {
            var slugExists = await _db.MemberProfiles
                .AnyAsync(x => x.ReplicateSiteSlug == req.ReplicateSiteSlug, ct);
            if (slugExists)
                throw new DuplicateReplicateSiteException(req.ReplicateSiteSlug);
        }

        // Resolve sponsor by replicate site slug — this is the public identifier ambassadors share.
        string? sponsorMemberId = null;
        if (!string.IsNullOrEmpty(req.SponsorReplicateSite))
        {
            sponsorMemberId = await _db.MemberProfiles
                .AsNoTracking()
                .Where(x => x.ReplicateSiteSlug == req.SponsorReplicateSite
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

        var memberId = GenerateMemberId();

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
            BusinessName      = req.BusinessName,
            ShowBusinessName  = req.ShowBusinessName,
            MemberType        = MemberType.Ambassador,
            Status            = MemberAccountStatus.Pending,
            EnrollDate        = now,
            SponsorMemberId   = sponsorMemberId,
            ReplicateSiteSlug = req.ReplicateSiteSlug,
            CreatedBy         = req.Email,
            CreationDate      = now,
            LastUpdateDate    = now
        };

        var orderId = Guid.NewGuid().ToString();
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

        GenealogyEntity? sponsorNode = null;
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

    private static string GenerateMemberId()
    {
        var number = Random.Shared.Next(1, 999999);
        return $"AMB-{number:D6}";
    }
}
