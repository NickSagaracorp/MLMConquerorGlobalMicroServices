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
/// Fase 3 del asistente de alta.
///
/// Con tarjeta, token o código de descuento el dinero ya está cobrado cuando se llega aquí, así
/// que esta fase activa al miembro y dispara las comisiones.
///
/// CON CRIPTO NO. El cobro llega por fuera —no hay pasarela integrada, es el diseño— y hasta que
/// alguien de la casa confirme que la transferencia entró, el alta queda registrada pero la
/// membresía no se activa y no se genera ni una comisión. Lo que sí ocurre igual es la
/// COLOCACIÓN: el nodo de genealogía se creó en la fase 1 y no se toca aquí, para que la
/// estructura del árbol no dependa de cuándo alguien se siente a aprobar.
///
/// La razón de no adelantar las comisiones la dio el dueño del producto: nadie cobra sobre dinero
/// no recibido, y si la transferencia nunca llega no hay comisiones que revertir.
/// </summary>
public class CompleteSignupHandler : IRequestHandler<CompleteSignupCommand, Result<SignupResponse>>
{
    private readonly AppDbContext                 _db;
    private readonly IDateTimeProvider            _dateTime;
    private readonly IS3FileService               _s3;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService                  _jwtService;
    private readonly IEncryptionService           _encryption;
    private readonly ITokenRedemptionService      _tokenRedemption;
    private readonly ISignupActivationService     _activation;

    public CompleteSignupHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IS3FileService s3,
        UserManager<ApplicationUser> userManager,
        IJwtService jwtService,
        IEncryptionService encryption,
        ITokenRedemptionService tokenRedemption,
        ISignupActivationService activation)
    {
        _db              = db;
        _dateTime        = dateTime;
        _s3              = s3;
        _userManager     = userManager;
        _jwtService      = jwtService;
        _encryption      = encryption;
        _tokenRedemption = tokenRedemption;
        _activation      = activation;
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

        if (req.PaymentMethod == PaymentMethodType.Crypto)
        {
            // El pedido queda EN PROCESO, no completado: el dinero está anunciado pero no
            // recibido. Y es lo que impide que esta misma llamada se repita —CompleteSignup solo
            // encuentra pedidos en Pending— y que la herramienta de altas zombis
            // (AdminSignupsController.RetryComplete, que también filtra por Pending) active por
            // detrás un alta que está esperando cobro.
            order.Status         = OrderStatus.Processing;
            order.LastUpdateDate = now;
            order.LastUpdateBy   = member.Email;

            // El miembro se queda en Pending, que es donde lo dejó la fase 1. Pending, y no
            // Inactive, porque en esta base Inactive significa "estuvo activo y dejó de estarlo":
            // lo escriben CancelMembershipHandler y ProcessScheduledCancellationsJob al dar de
            // baja. Pending significa "dado de alta, todavía no activado", que es exactamente
            // esto. Marcarlo Inactive lo contaría como bajas en GetMemberStatsHandler y en el
            // panel del CEO, y ensuciaría la métrica de cancelaciones con gente que nunca llegó
            // a entrar. La suscripción, por lo mismo, sigue en MembershipStatus.Pending.
            member.LastUpdateDate = now;
            member.LastUpdateBy   = member.Email;

            await _db.CryptoPaymentConfirmations.AddAsync(new CryptoPaymentConfirmation
            {
                OrderId        = order.Id,
                MemberId       = member.MemberId,
                MemberEmail    = member.Email,
                CryptoCurrency = req.CryptoCurrency ?? string.Empty,
                AmountDue      = order.TotalAmount,
                Status         = CryptoPaymentConfirmationStatus.AwaitingPayment,
                // CryptoTransactionId se queda a null a propósito: el identificador de la
                // transferencia lo captura quien confirma el cobro, no el aspirante.
                CreatedBy      = member.Email,
                CreationDate   = now,
                LastUpdateDate = now
            }, ct);

            await _db.SaveChangesAsync(ct);

            // Ni IsActive ni EmailConfirmed ni tokens: la cuenta no puede entrar al portal hasta
            // que el cobro esté confirmado. LoginHandler rechaza a los usuarios con IsActive=false.
            return Result<SignupResponse>.Success(new SignupResponse
            {
                SignupId   = order.Id,
                MemberId   = member.MemberId,
                Email      = member.Email,
                MemberType = member.MemberType.ToString(),
                EnrollDate = member.EnrollDate
            });
        }

        await _activation.ActivateAsync(
            order, member, subscription, totalQualPoints, now, member.Email, ct);

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

    private static string HashToken(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
