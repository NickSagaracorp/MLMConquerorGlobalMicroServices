using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.SignupMember;

/// <summary>
/// Phase 1 of the member signup wizard.
/// Creates a pending member, order, and subscription. Identity user is created but inactive.
/// Returns the signupId (orderId) for subsequent Select-Products and Complete steps.
/// </summary>
public class SignupMemberHandler : IRequestHandler<SignupMemberCommand, Result<SignupResponse>>
{
    private readonly AppDbContext                 _db;
    private readonly IDateTimeProvider            _dateTime;
    private readonly UserManager<ApplicationUser> _userManager;

    public SignupMemberHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _dateTime    = dateTime;
        _userManager = userManager;
    }

    public async Task<Result<SignupResponse>> Handle(SignupMemberCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var now = _dateTime.Now;

        // ── Duplicate email check ─────────────────────────────────────────────
        var emailTaken = await _userManager.FindByEmailAsync(req.Email);
        if (emailTaken is not null)
            return Result<SignupResponse>.Failure("EMAIL_TAKEN", "This email is already registered.");

        // ── Sponsor validation ────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(req.SponsorMemberId))
        {
            var sponsorExists = await _db.MemberProfiles
                .AnyAsync(x => x.MemberId == req.SponsorMemberId, ct);
            if (!sponsorExists)
                return Result<SignupResponse>.Failure(
                    "SPONSOR_NOT_FOUND", $"Sponsor '{req.SponsorMemberId}' not found.");
        }

        // ── Membership level ──────────────────────────────────────────────────
        var membershipLevel = await _db.MembershipLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.MembershipLevelId && x.IsActive, ct);
        if (membershipLevel is null)
            return Result<SignupResponse>.Failure(
                "MEMBERSHIP_LEVEL_NOT_FOUND", "The selected membership level is invalid or inactive.");

        // ── Build pending member ──────────────────────────────────────────────
        var memberId = $"MBR-{Random.Shared.Next(1, 999999):D6}";

        var member = new MemberProfile
        {
            UserId         = Guid.NewGuid(),
            MemberId       = memberId,
            Email          = req.Email,
            FirstName      = req.FirstName,
            LastName       = req.LastName,
            Phone          = req.Phone,
            Country        = req.Country,
            State          = req.State,
            City           = req.City,
            Address        = req.Address,
            MemberType     = MemberType.ExternalMember,
            Status         = MemberAccountStatus.Pending,
            EnrollDate     = now,
            SponsorMemberId = req.SponsorMemberId,
            CreatedBy      = req.Email,
            CreationDate   = now,
            LastUpdateDate = now
        };

        // ── Pending order ─────────────────────────────────────────────────────
        var orderId = Guid.NewGuid().ToString();
        var order = new Orders
        {
            Id             = orderId,
            MemberId       = memberId,
            TotalAmount    = 0,
            Status         = OrderStatus.Pending,
            OrderDate      = now,
            Notes          = $"Member signup — {membershipLevel.Name}",
            CreatedBy      = req.Email,
            CreationDate   = now,
            LastUpdateDate = now
        };

        // ── Pending subscription ──────────────────────────────────────────────
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

        // ── Enrollment tree ───────────────────────────────────────────────────
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

        // ── Persist domain records ────────────────────────────────────────────
        await _db.MemberProfiles.AddAsync(member, ct);
        await _db.Orders.AddAsync(order, ct);
        await _db.MembershipSubscriptions.AddAsync(subscription, ct);
        await _db.GenealogyTree.AddAsync(genealogyNode, ct);
        await _db.SaveChangesAsync(ct);

        // ── Create inactive Identity user ─────────────────────────────────────
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

        await _userManager.AddToRoleAsync(appUser, "Member");

        return Result<SignupResponse>.Success(new SignupResponse
        {
            SignupId   = orderId,
            MemberId   = memberId,
            Email      = req.Email,
            MemberType = nameof(MemberType.ExternalMember),
            EnrollDate = now
        });
    }
}
