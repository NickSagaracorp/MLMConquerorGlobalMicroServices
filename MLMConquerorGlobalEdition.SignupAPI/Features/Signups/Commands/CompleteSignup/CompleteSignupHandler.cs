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
                sponsorMemberId:     member.SponsorMemberId ?? string.Empty,
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

                // Sprint-15 Bug A: concurrent signups under the same upline were
                // losing 80+ of 88 EnrollmentPoints increments because EF read-
                // modify-write doesn't serialize. Atomic upsert per ancestor
                // closes the race — see IncrementAncestorStatsAsync.
                foreach (var ancestorId in ancestorIds)
                {
                    var qualDelta = ancestorId == member.SponsorMemberId ? 1 : 0;
                    await IncrementAncestorStatsAsync(
                        ancestorId,
                        enrollmentPointsDelta:         totalQualPoints,
                        enrollmentTeamSizeDelta:       1,
                        qualifiedSponsoredMembersDelta:qualDelta,
                        createdBy:                     member.Email,
                        now:                           now,
                        ct:                            ct);
                }
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

    /// <summary>
    /// Sprint-15 Bug A — atomic upsert of an ancestor's MemberStatistics row.
    ///
    /// On SQL Server we issue a single MERGE …  WITH (HOLDLOCK) statement so two
    /// concurrent signups walking the same ancestor cannot both read 0 and write 1,
    /// losing one of the +1s. HOLDLOCK promotes the MERGE to serializable for the
    /// matched/missed key range, which is what makes the upsert race-free in the
    /// absence of a unique index on MemberId.
    ///
    /// On the in-memory provider used by unit tests there is no concurrency and
    /// no SQL — we fall back to a read-modify-write that hits the change tracker.
    /// </summary>
    private async Task IncrementAncestorStatsAsync(
        string ancestorId,
        int enrollmentPointsDelta,
        int enrollmentTeamSizeDelta,
        int qualifiedSponsoredMembersDelta,
        string createdBy,
        DateTime now,
        CancellationToken ct)
    {
        var providerName = _db.Database.ProviderName ?? string.Empty;
        var isInMemory   = providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

        if (isInMemory)
        {
            var existing = await _db.MemberStatistics
                .FirstOrDefaultAsync(s => s.MemberId == ancestorId, ct);

            if (existing is not null)
            {
                existing.EnrollmentPoints          += enrollmentPointsDelta;
                existing.EnrollmentTeamSize        += enrollmentTeamSizeDelta;
                existing.QualifiedSponsoredMembers += qualifiedSponsoredMembersDelta;
            }
            else
            {
                await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
                {
                    MemberId                  = ancestorId,
                    EnrollmentPoints          = enrollmentPointsDelta,
                    EnrollmentTeamSize        = enrollmentTeamSizeDelta,
                    QualifiedSponsoredMembers = qualifiedSponsoredMembersDelta,
                    CreatedBy                 = createdBy,
                    CreationDate              = now
                }, ct);
            }
            return;
        }

        // SQL Server path — MERGE with HOLDLOCK is the canonical race-free upsert
        // when no unique index covers the merge predicate (MemberId here has no
        // unique constraint as of 2026-05). FormattableString parameters keep
        // the values bound, not interpolated, so no SQL injection surface.
        FormattableString mergeSql = $@"
MERGE INTO MemberStatistics WITH (HOLDLOCK) AS target
USING (SELECT {ancestorId} AS MemberId) AS source
   ON target.MemberId = source.MemberId
WHEN MATCHED THEN
    UPDATE SET
        EnrollmentPoints          = target.EnrollmentPoints          + {enrollmentPointsDelta},
        EnrollmentTeamSize        = target.EnrollmentTeamSize        + {enrollmentTeamSizeDelta},
        QualifiedSponsoredMembers = target.QualifiedSponsoredMembers + {qualifiedSponsoredMembersDelta}
WHEN NOT MATCHED THEN
    INSERT (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize,
            EnrollmentTeamSize, DualTeamPoints, EnrollmentPoints,
            QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers,
            EnrollmentTeamGrowth, DualteamGrowth, EnrollmentTeamPointsGrowth,
            DualTeamPointsGrowth, CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth,
            CurrentYearIncomeGrowth, CreationDate, CreatedBy)
    VALUES (source.MemberId, 0, 0, 0,
            {enrollmentTeamSizeDelta}, 0, {enrollmentPointsDelta},
            {qualifiedSponsoredMembersDelta}, 0,
            0, 0, 0,
            0, 0, 0,
            0, {now}, {createdBy});";

        await _db.Database.ExecuteSqlInterpolatedAsync(mergeSql, ct);
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
