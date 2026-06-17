using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;

/// <summary>
/// Phase 3 of the signup wizard — activates the member, processes payment evidence, issues JWT.
/// </summary>
public class CompleteSignupHandler : IRequestHandler<CompleteSignupCommand, Result<SignupResponse>>
{
    private readonly AppDbContext                       _db;
    private readonly IDateTimeProvider                  _dateTime;
    private readonly IS3FileService                     _s3;
    private readonly ISponsorBonusService               _sponsorBonus;
    private readonly IFastStartBonusService             _fastStartBonus;
    private readonly UserManager<ApplicationUser>       _userManager;
    private readonly IJwtService                        _jwtService;
    private readonly IEncryptionService                 _encryption;
    private readonly ITokenRedemptionService            _tokenRedemption;
    private readonly IRecurringBillingEnrollmentService _recurringBillingEnrollment;

    public CompleteSignupHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IS3FileService s3,
        ISponsorBonusService sponsorBonus,
        IFastStartBonusService fastStartBonus,
        UserManager<ApplicationUser> userManager,
        IJwtService jwtService,
        IEncryptionService encryption,
        ITokenRedemptionService tokenRedemption,
        IRecurringBillingEnrollmentService recurringBillingEnrollment)
    {
        _db                          = db;
        _dateTime                    = dateTime;
        _s3                          = s3;
        _sponsorBonus                = sponsorBonus;
        _fastStartBonus              = fastStartBonus;
        _userManager                 = userManager;
        _jwtService                  = jwtService;
        _encryption                  = encryption;
        _tokenRedemption             = tokenRedemption;
        _recurringBillingEnrollment  = recurringBillingEnrollment;
    }

    public async Task<Result<SignupResponse>> Handle(CompleteSignupCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var now = _dateTime.Now;

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == command.SignupId && o.Status == OrderStatus.Pending, ct);

        if (order is null)
            return Result<SignupResponse>.Failure("SIGNUP_NOT_FOUND", "Pending signup not found.");

        var productsExistInCatalog = await _db.Products
            .AnyAsync(p => p.IsActive && !p.IsDeleted, ct);

        var hasProducts = await _db.OrderDetails
            .AnyAsync(d => d.OrderId == order.Id, ct);

        if (productsExistInCatalog && !hasProducts)
            return Result<SignupResponse>.Failure(
                "NO_PRODUCTS_SELECTED", "Please select at least one product before completing signup.");

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == order.MemberId, ct);

        if (member is null)
            return Result<SignupResponse>.Failure("MEMBER_NOT_FOUND", "Associated member not found.");

        var subscription = await _db.MembershipSubscriptions
            .FirstOrDefaultAsync(
                s => s.MemberId == member.MemberId && s.SubscriptionStatus == MembershipStatus.Pending, ct);

        if (subscription is null)
            return Result<SignupResponse>.Failure("SUBSCRIPTION_NOT_FOUND", "Pending subscription not found.");

        var appUser = await _userManager.FindByEmailAsync(member.Email);
        if (appUser is null || appUser.IsActive)
            return Result<SignupResponse>.Failure(
                "USER_NOT_FOUND", "Pending user account not found for this signup.");

        // Token-based payment: validate, then consume the token instance.
        // Token always covers the full order amount — no other payment processing happens.
        if (req.PaymentMethod == PaymentMethodType.Token)
        {
            // Selected products on the pending order — the validator needs this to enforce
            // that the user can only pick products covered by the TokenType.
            var selectedProductIds = await _db.OrderDetails
                .AsNoTracking()
                .Where(od => od.OrderId == order.Id)
                .Select(od => od.ProductId)
                .ToListAsync(ct);

            var redemption = await _tokenRedemption.RedeemForSignupAsync(
                tokenCode:           req.TokenCode ?? string.Empty,
                newMemberId:         member.MemberId,
                orderId:             order.Id,
                selectedProductIds:  selectedProductIds,
                now:                 now,
                ct:                  ct);

            if (!redemption.IsSuccess)
                return Result<SignupResponse>.Failure(redemption.ErrorCode!, redemption.Error!);
        }

        if (!string.IsNullOrEmpty(req.CheckoutScreenshotBase64))
        {
            var screenshotBytes = Convert.FromBase64String(req.CheckoutScreenshotBase64);
            var extension = req.CheckoutScreenshotContentType.Contains("png") ? "png" : "jpg";
            var s3Key = $"signups/screenshots/{member.MemberId}_{now:yyyyMMddHHmmss}.{extension}";

            using var stream = new MemoryStream(screenshotBytes);
            order.CheckoutScreenshotUrl = await _s3.UploadAsync(
                s3Key, stream, req.CheckoutScreenshotContentType, ct);
        }

        if (req.PaymentMethod == PaymentMethodType.CreditCard && req.CreditCard is not null)
        {
            var cc = req.CreditCard;
            await _db.CreditCards.AddAsync(new MemberCreditCard
            {
                MemberId         = member.MemberId,
                Last4            = cc.Last4,
                First6           = cc.First6,
                MaskedCardNumber = BuildMaskedCardNumber(cc.First6, cc.Last4),
                CardBrand        = cc.CardBrand,
                EncryptedExpiry  = _encryption.Encrypt($"{cc.ExpiryMonth:00}/{cc.ExpiryYear:0000}"),
                EncryptedCvv     = null, // signup flow does not capture CVV — gateway already tokenized
                Gateway          = cc.Gateway,
                GatewayToken     = cc.GatewayToken,
                CardToken        = cc.CardToken,
                IsDefault        = true,
                IsExpired        = false,
                CreatedBy        = member.Email,
                CreationDate     = now,
                LastUpdateDate   = now
            }, ct);
        }

        order.Status         = OrderStatus.Completed;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = member.Email;

        member.Status         = MemberAccountStatus.Active;
        member.LastUpdateDate = now;
        member.LastUpdateBy   = member.Email;

        subscription.SubscriptionStatus = MembershipStatus.Active;
        subscription.StartDate          = now;
        subscription.EndDate            = now.AddMonths(1);
        subscription.RenewalDate        = now.AddMonths(1);
        subscription.LastUpdateDate     = now;
        subscription.LastUpdateBy       = member.Email;

        var totalQualPoints = await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.OrderId == order.Id)
            .Join(_db.Products.AsNoTracking(), od => od.ProductId, p => p.Id, (od, p) => p.QualificationPoins)
            .SumAsync(ct);

        // EnrollmentPoints is the sum of personal points across the downline INCLUDING this
        // member's own — so a brand-new leaf must seed with its own PersonalPoints. Each
        // ancestor row will be incremented separately below as we walk the upline.
        //
        // Sprint-15 Bug A: the new leaf's own stat row never races (member just created)
        // so we can still use AddAsync here. Ancestor rows DO race — see below.
        await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
        {
            MemberId         = member.MemberId,
            PersonalPoints   = totalQualPoints,
            EnrollmentPoints = totalQualPoints,
            CreatedBy        = member.Email,
            CreationDate     = now
        }, ct);

        if (!string.IsNullOrEmpty(member.SponsorMemberId))
        {
            var sponsorNode = await _db.GenealogyTree
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.MemberId == member.SponsorMemberId, ct);

            if (sponsorNode is not null)
            {
                var ancestorIds = ParseHierarchyPath(sponsorNode.HierarchyPath);

                // Sprint-16 — eventual-consistency pipeline. Previously this loop
                // executed one MERGE … WITH (HOLDLOCK) per ancestor (Sprint-15 Bug A
                // fix) which was correct but serialised on shared rows near the
                // root and pushed mean signup latency to ~16s at 350-concurrent.
                //
                // We now enqueue one MemberStatisticDelta row per ancestor in a
                // single batch insert. ApplyMemberStatisticDeltasJob (recurring,
                // every minute, "signups" queue) groups by MemberId and rolls
                // the summed delta into MemberStatistics with one MERGE per
                // distinct upline per cycle — so 350 concurrent signups under
                // the same upline produce one MERGE instead of 350.
                //
                // Stat freshness lag: up to ~1 min. Rank evaluation runs every
                // 5 min via ProcessRankQueueJob so the deltas are always caught
                // up before evaluation; for extra safety the apply job re-enqueues
                // a RankEvaluationQueue entry for each member whose stats it just
                // updated (mirrors SignupAmbassadorHandler L255).
                var deltas = new List<MemberStatisticDelta>(ancestorIds.Count);
                foreach (var ancestorId in ancestorIds)
                {
                    var qualDelta = ancestorId == member.SponsorMemberId ? 1 : 0;
                    deltas.Add(new MemberStatisticDelta
                    {
                        MemberId                       = ancestorId,
                        EnrollmentPointsDelta          = totalQualPoints,
                        EnrollmentTeamSizeDelta        = 1,
                        QualifiedSponsoredMembersDelta = qualDelta,
                        SourceMemberId                 = member.MemberId,
                        IsApplied                      = false,
                        CreatedBy                      = member.Email,
                        CreationDate                   = now
                    });
                }

                if (deltas.Count > 0)
                    await _db.MemberStatisticDeltas.AddRangeAsync(deltas, ct);
            }
        }

        await _sponsorBonus.ComputeAsync(
            member.SponsorMemberId, member.MemberId, order.Id,
            order.TotalAmount, member.Email, now, ct);

        await _fastStartBonus.ComputeAsync(
            member.SponsorMemberId, member.MemberId, order.Id,
            now, member.Email, ct);

        // Create or update the SubscriptionBillingState so the dunning sweep
        // knows this subscription is enrolled in recurring billing from today.
        await _recurringBillingEnrollment.EnsureStateForSubscriptionAsync(subscription, member.Email, ct);

        await _db.SaveChangesAsync(ct);

        appUser.IsActive       = true;
        appUser.EmailConfirmed = true;

        var role         = member.MemberType == MemberType.Ambassador ? "Ambassador" : "Member";
        var accessToken  = _jwtService.GenerateAccessToken(appUser.Id, member.MemberId, member.Email, [role]);
        var refreshToken = _jwtService.GenerateRefreshToken();

        appUser.RefreshToken       = HashToken(refreshToken);
        appUser.RefreshTokenExpiry = now.Add(_jwtService.RefreshTokenExpiry);
        await _userManager.UpdateAsync(appUser);

        return Result<SignupResponse>.Success(new SignupResponse
        {
            SignupId     = order.Id,
            MemberId     = member.MemberId,
            Email        = member.Email,
            MemberType   = member.MemberType.ToString(),
            EnrollDate   = member.EnrollDate,
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            TokenExpiry  = now.Add(_jwtService.AccessTokenExpiry)
        });
    }

    private static string BuildMaskedCardNumber(string first6, string last4)
        => string.IsNullOrEmpty(first6) || string.IsNullOrEmpty(last4)
            ? $"******{last4}"
            : $"{first6}******{last4}";

    private static List<string> ParseHierarchyPath(string hierarchyPath)
        => hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string HashToken(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
