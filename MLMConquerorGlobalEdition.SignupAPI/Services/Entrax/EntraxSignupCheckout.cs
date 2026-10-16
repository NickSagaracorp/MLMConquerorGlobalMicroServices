using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public interface IEntraxSignupCheckout
{
    bool IsOfferedFor(string? countryIso2);

    /// <summary>
    /// Reserva el pedido (Processing), guarda el intento y pide el QR a Entrax. GUARDA el contexto:
    /// también lo que el handler dejó pendiente (la fila de estadísticas del alta).
    /// </summary>
    Task<Result<EntraxSignupPaymentDto>> StartAsync(Orders order, MemberProfile member, DateTime now, CancellationToken ct);
}

/// <summary>
/// La emisión del QR del alta. Lo que el legado hacía mal y aquí no:
///  - el importe sale de Orders.TotalAmount (servidor), nunca del cliente, y va en USD: Entrax
///    convierte; nosotros no (el legado usaba el cambio de Spreedly y, si fallaba, mandaba USD
///    como rublos);
///  - el orderId es nuestro, estable y único por intento (SIGNUP-{pedido}-{n}), y se guarda ANTES
///    de llamar, así un callback siempre encuentra a qué alta pertenece;
///  - nada del formulario sale hacia Entrax ni al log.
/// </summary>
public sealed class EntraxSignupCheckout : IEntraxSignupCheckout
{
    private const string UnavailableText =
        "We couldn't create the SBP payment right now. Please try again or choose another payment method.";

    private readonly AppDbContext                    _db;
    private readonly IEntraxClient                   _client;
    private readonly EntraxOptions                   _options;
    private readonly ILogger<EntraxSignupCheckout>   _logger;

    public EntraxSignupCheckout(AppDbContext db, IEntraxClient client, EntraxOptions options, ILogger<EntraxSignupCheckout> logger)
    {
        _db      = db;
        _client  = client;
        _options = options;
        _logger  = logger;
    }

    public bool IsOfferedFor(string? countryIso2) => _options.IsOfferedFor(countryIso2);

    public async Task<Result<EntraxSignupPaymentDto>> StartAsync(Orders order, MemberProfile member, DateTime now, CancellationToken ct)
    {
        var amountUsd = Math.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero);
        if (amountUsd < 0.01m)
            return Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.NothingToPay, "This signup has nothing to pay.");

        var attempt = await _db.EntraxPayments.IgnoreQueryFilters().CountAsync(p => p.OrderId == order.Id, ct) + 1;
        var payment = new EntraxPayment
        {
            OrderId         = order.Id,
            MemberId        = member.MemberId,
            ExternalOrderId = EntraxPayment.BuildExternalOrderId(order.Id, attempt),
            Status          = EntraxPaymentStatus.AwaitingPayment,
            AmountUsd       = amountUsd,
            CreatedBy       = member.Email,
            CreationDate    = now,
            LastUpdateDate  = now,
            LastUpdateBy    = member.Email
        };
        _db.EntraxPayments.Add(payment);

        // Igual que cripto: el pedido EN PROCESO impide que CompleteSignup se repita (solo encuentra
        // Pending) mientras el QR está vivo. Miembro y suscripción siguen en Pending.
        order.Status         = OrderStatus.Processing;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = member.Email;
        member.LastUpdateDate = now;
        member.LastUpdateBy   = member.Email;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Índice único de ExternalOrderId o RowVersion del miembro: otra petición del mismo alta
            // llegó primero. No se llamó a Entrax.
            _db.ChangeTracker.Clear();
            _logger.LogWarning("Entrax: el alta {OrderId} ya tenía un QR en creación (intento {Attempt}).", order.Id, attempt);
            return Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.InProgress,
                "A payment for this signup is already being created. Please wait a moment and try again.");
        }

        // A partir de aquí puede haber un QR vivo en Entrax: nada se cancela con la petición.
        var call = await _client.CreateQrAsync(
            new EntraxQrRequest(amountUsd, EntraxGateway.Currency, payment.ExternalOrderId,
                $"MLM Conqueror signup {order.OrderNo ?? order.Id}", _options.CallbackUrl),
            CancellationToken.None);

        var qr        = call.Value;
        var paymentId = EntraxGateway.NormalizePaymentId(qr?.PaymentId);
        var image     = EntraxQrSanitizer.SafeImage(qr?.ImageDataUri);
        var link      = EntraxQrSanitizer.SafeLink(qr?.Link);

        _db.EntraxApiCalls.Add(EntraxCallLog.From(EntraxCallLog.CreateQr, call.HttpStatus, call.ErrorCode, call.ElapsedMs,
            payment.ExternalOrderId, paymentId, qr is null ? null : "pending", now, member.Email));

        // Antes de guardar o mostrar nada, el QR tiene que ser el que se pidió: el orderId nuestro
        // de este intento, en USD y por el importe del servidor.
        var problem =
              !call.Ok || paymentId is null ? call.ErrorCode ?? EntraxErrorCodes.BadResponse
            : !string.Equals(qr!.OrderId, payment.ExternalOrderId, StringComparison.Ordinal) ? "PROVIDER_MISMATCH"
            : !string.Equals(qr!.Currency, EntraxGateway.Currency, StringComparison.OrdinalIgnoreCase)
              || qr.OriginalAmount != amountUsd ? "QR_AMOUNT_MISMATCH"
            : image is null && link is null ? "QR_UNUSABLE"
            : null;

        if (problem is not null)
        {
            payment.Status            = EntraxPaymentStatus.Failed;
            payment.FailureCode       = problem.Length > 60 ? problem[..60] : problem;
            payment.ExternalPaymentId = paymentId;
            payment.LastUpdateDate    = now;
            payment.LastUpdateBy      = member.Email;
            order.Status              = OrderStatus.Pending;
            order.LastUpdateDate      = now;
            order.LastUpdateBy        = member.Email;
            await _db.SaveChangesAsync(CancellationToken.None);

            if (problem == "PROVIDER_MISMATCH")
                _logger.LogCritical(
                    "Entrax devolvió el QR del orderId {Returned} al pedir {ExternalOrderId}. No se muestra.",
                    qr!.OrderId, payment.ExternalOrderId);
            else if (problem == "QR_AMOUNT_MISMATCH")
                _logger.LogCritical(
                    "Entrax emitió el QR {ExternalOrderId} por {Amount} {Currency} y se pidieron {Expected} USD. No se muestra.",
                    payment.ExternalOrderId, qr!.OriginalAmount, qr.Currency, amountUsd);
            else
                _logger.LogWarning("Entrax: no se pudo crear el QR {ExternalOrderId} ({Code}).", payment.ExternalOrderId, problem);

            return Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.Unavailable, UnavailableText);
        }

        payment.ExternalPaymentId = paymentId;
        payment.AmountRub         = qr!.AmountInRubles;
        payment.ExchangeRate      = qr.ExchangeRate;
        payment.QrLink            = link;
        payment.ExpiresAt         = qr.ExpiresAt?.LocalDateTime ?? now.AddMinutes(15);
        payment.LastUpdateDate    = now;
        payment.LastUpdateBy      = member.Email;
        await _db.SaveChangesAsync(CancellationToken.None);

        return Result<EntraxSignupPaymentDto>.Success(new EntraxSignupPaymentDto
        {
            PaymentRef       = payment.Id,
            QrImage          = image,
            QrLink           = link,
            AmountUsd        = amountUsd,
            AmountRub        = qr.AmountInRubles,
            ExchangeRate     = qr.ExchangeRate,
            ExpiresAt        = payment.ExpiresAt,
            SecondsRemaining = EntraxGateway.SecondsUntil(payment.ExpiresAt, now)
        });
    }
}
