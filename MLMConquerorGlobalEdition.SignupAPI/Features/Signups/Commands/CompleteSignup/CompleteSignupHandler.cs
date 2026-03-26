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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService                  _jwtService;

    public CompleteSignupHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IS3FileService s3,
        ISponsorBonusService sponsorBonus,
        UserManager<ApplicationUser> userManager,
        IJwtService jwtService)
    {
        _db           = db;
        _dateTime     = dateTime;
        _s3           = s3;
        _sponsorBonus = sponsorBonus;
        _userManager  = userManager;
        _jwtService   = jwtService;
    }

    public async Task<Result<SignupResponse>> Handle(CompleteSignupCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var now = _dateTime.Now;

        // ── Load pending order ────────────────────────────────────────────────
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == command.SignupId && o.Status == OrderStatus.Pending, ct);

        if (order is null)
            return Result<SignupResponse>.Failure("SIGNUP_NOT_FOUND", "Pending signup not found.");

        // ── Verify products were selected (only required when product catalog is non-empty) ──
        var productsExistInCatalog = await _db.Products
            .AnyAsync(p => p.IsActive && !p.IsDeleted, ct);

        var hasProducts = await _db.OrderDetails
            .AnyAsync(d => d.OrderId == order.Id, ct);

        if (productsExistInCatalog && !hasProducts)
            return Result<SignupResponse>.Failure(
                "NO_PRODUCTS_SELECTED", "Please select at least one product before completing signup.");

        // ── Load member ───────────────────────────────────────────────────────
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == order.MemberId, ct);

        if (member is null)
            return Result<SignupResponse>.Failure("MEMBER_NOT_FOUND", "Associated member not found.");

        // ── Load pending subscription ─────────────────────────────────────────
        var subscription = await _db.MembershipSubscriptions
            .FirstOrDefaultAsync(
                s => s.MemberId == member.MemberId && s.SubscriptionStatus == MembershipStatus.Pending, ct);

        if (subscription is null)
            return Result<SignupResponse>.Failure("SUBSCRIPTION_NOT_FOUND", "Pending subscription not found.");

        // ── Load inactive Identity user ───────────────────────────────────────
        var appUser = await _userManager.FindByEmailAsync(member.Email);
        if (appUser is null || appUser.IsActive)
            return Result<SignupResponse>.Failure(
                "USER_NOT_FOUND", "Pending user account not found for this signup.");

        // ── Screenshot upload (chargeback evidence) ───────────────────────────
        if (!string.IsNullOrEmpty(req.CheckoutScreenshotBase64))
        {
            var screenshotBytes = Convert.FromBase64String(req.CheckoutScreenshotBase64);
            var extension = req.CheckoutScreenshotContentType.Contains("png") ? "png" : "jpg";
            var s3Key = $"signups/screenshots/{member.MemberId}_{now:yyyyMMddHHmmss}.{extension}";

            using var stream = new MemoryStream(screenshotBytes);
            order.CheckoutScreenshotUrl = await _s3.UploadAsync(
                s3Key, stream, req.CheckoutScreenshotContentType, ct);
        }

        // ── Credit card storage ───────────────────────────────────────────────
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
                ExpiryMonth      = cc.ExpiryMonth,
                ExpiryYear       = cc.ExpiryYear,
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

        // ── Activate order ────────────────────────────────────────────────────
        order.Status         = OrderStatus.Completed;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = member.Email;

        // ── Activate member ───────────────────────────────────────────────────
        member.Status         = MemberAccountStatus.Active;
        member.LastUpdateDate = now;
        member.LastUpdateBy   = member.Email;

        // ── Activate subscription ─────────────────────────────────────────────
        subscription.SubscriptionStatus = MembershipStatus.Active;
        subscription.StartDate          = now;
        subscription.LastUpdateDate     = now;
        subscription.LastUpdateBy       = member.Email;

        // ── Member statistics ─────────────────────────────────────────────────
        var totalQualPoints = await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.OrderId == order.Id)
            .Join(_db.Products.AsNoTracking(), od => od.ProductId, p => p.Id, (od, p) => p.QualificationPoins)
            .SumAsync(ct);

        await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
        {
            MemberId       = member.MemberId,
            PersonalPoints = totalQualPoints,
            CreatedBy      = member.Email,
            CreationDate   = now
        }, ct);

        // ── Propagate enrollment points up the sponsor chain ──────────────────
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

        // ── Sponsor bonus ─────────────────────────────────────────────────────
        await _sponsorBonus.ComputeAsync(
            member.SponsorMemberId, member.MemberId, order.Id,
            order.TotalAmount, member.Email, now, ct);

        await _db.SaveChangesAsync(ct);

        // ── Activate Identity user and issue JWT ──────────────────────────────
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
