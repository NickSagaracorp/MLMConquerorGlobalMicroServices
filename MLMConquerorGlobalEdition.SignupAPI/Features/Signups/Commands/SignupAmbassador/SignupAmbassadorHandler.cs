using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

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

    public SignupAmbassadorHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _dateTime    = dateTime;
        _userManager = userManager;
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

        if (!string.IsNullOrEmpty(req.SponsorMemberId))
        {
            var sponsorExists = await _db.MemberProfiles
                .AnyAsync(x => x.MemberId == req.SponsorMemberId, ct);
            if (!sponsorExists)
                return Result<SignupResponse>.Failure(
                    "SPONSOR_NOT_FOUND", $"Sponsor '{req.SponsorMemberId}' not found.");
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
            BusinessName      = req.BusinessName,
            ShowBusinessName  = req.ShowBusinessName,
            MemberType        = MemberType.Ambassador,
            Status            = MemberAccountStatus.Pending,
            EnrollDate        = now,
            SponsorMemberId   = req.SponsorMemberId,
            ReplicateSiteSlug = req.ReplicateSiteSlug,
            CreatedBy         = req.Email,
            CreationDate      = now,
            LastUpdateDate    = now
        };

        var orderId = Guid.NewGuid().ToString();
        var order = new Orders
        {
            Id             = orderId,
            MemberId       = memberId,
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
        if (!string.IsNullOrEmpty(req.SponsorMemberId))
        {
            sponsorNode = await _db.GenealogyTree
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.MemberId == req.SponsorMemberId, ct);
        }

        var genealogyNode = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = req.SponsorMemberId,
            HierarchyPath  = sponsorNode is not null
                ? $"{sponsorNode.HierarchyPath}{memberId}/"
                : $"/{memberId}/",
            Level          = sponsorNode is not null ? sponsorNode.Level + 1 : 1,
            CreatedBy      = req.Email,
            CreationDate   = now,
            LastUpdateDate = now
        };

        await _db.MemberProfiles.AddAsync(member, ct);
        await _db.Orders.AddAsync(order, ct);
        await _db.MembershipSubscriptions.AddAsync(subscription, ct);
        await _db.GenealogyTree.AddAsync(genealogyNode, ct);
        await _db.SaveChangesAsync(ct);

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
