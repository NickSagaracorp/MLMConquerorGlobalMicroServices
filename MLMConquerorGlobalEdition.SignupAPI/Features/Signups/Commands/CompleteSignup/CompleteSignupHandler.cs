using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext                 _db;
    private readonly IDateTimeProvider            _dateTime;
    private readonly IS3FileService               _s3;
    private readonly ISponsorBonusService         _sponsorBonus;
    private readonly IFastStartBonusService       _fastStartBonus;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService                  _jwtService;
    private readonly IEncryptionService           _encryption;

    public CompleteSignupHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IS3FileService s3,
        ISponsorBonusService sponsorBonus,
        IFastStartBonusService fastStartBonus,
        UserManager<ApplicationUser> userManager,
        IJwtService jwtService,
        IEncryptionService encryption)
    {
        _db             = db;
        _dateTime       = dateTime;
        _s3             = s3;
        _sponsorBonus   = sponsorBonus;
        _fastStartBonus = fastStartBonus;
        _userManager    = userManager;
        _jwtService     = jwtService;
        _encryption     = encryption;
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

                if (ancestorIds.Count > 0)
                {
                    var existingStats = await _db.MemberStatistics
                        .Where(s => ancestorIds.Contains(s.MemberId))
                        .ToListAsync(ct);

                    var statsDict     = existingStats.ToDictionary(s => s.MemberId);
                    var newStatsList  = new List<MemberStatisticEntity>();

                    foreach (var ancestorId in ancestorIds)
                    {
                        if (statsDict.TryGetValue(ancestorId, out var stat))
                        {
                            stat.EnrollmentPoints  += totalQualPoints;
                            stat.EnrollmentTeamSize += 1;
                            if (ancestorId == member.SponsorMemberId)
                                stat.QualifiedSponsoredMembers += 1;
                        }
                        else
                        {
                            newStatsList.Add(new MemberStatisticEntity
                            {
                                MemberId                  = ancestorId,
                                EnrollmentPoints          = totalQualPoints,
                                EnrollmentTeamSize        = 1,
                                QualifiedSponsoredMembers = ancestorId == member.SponsorMemberId ? 1 : 0,
                                CreatedBy                 = member.Email,
                                CreationDate              = now
                            });
                        }
                    }

                    if (newStatsList.Count > 0)
                        await _db.MemberStatistics.AddRangeAsync(newStatsList, ct);
                }
            }
        }

        await _sponsorBonus.ComputeAsync(
            member.SponsorMemberId, member.MemberId, order.Id,
            order.TotalAmount, member.Email, now, ct);

        await _fastStartBonus.ComputeAsync(
            member.SponsorMemberId, member.MemberId, order.Id,
            now, member.Email, ct);

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
