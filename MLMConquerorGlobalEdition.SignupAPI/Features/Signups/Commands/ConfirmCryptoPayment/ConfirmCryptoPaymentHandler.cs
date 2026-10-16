using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;

/// <summary>
/// El otro extremo del alta por cripto: alguien de la casa cotejó la transferencia y dice que
/// entró. AQUÍ es donde se activa la membresía y donde se generan las comisiones y los deltas,
/// no al completar el alta.
///
/// CÓMO SE EVITA APROBAR DOS VECES, en tres capas y por este orden:
///
///  1. La máquina de estados. La fila de confirmación tiene que estar en AwaitingPayment y el
///     pedido en Processing. Una segunda llamada, aunque sea diez minutos después, encuentra
///     Confirmed / Completed y se va con ALREADY_CONFIRMED sin tocar nada. Esto cubre el caso
///     real y aburrido: alguien pulsa el botón dos veces, o dos personas lo pulsan con un rato
///     de diferencia.
///
///  2. El token de concurrencia. Si las dos llamadas caen a la vez, las dos leen AwaitingPayment
///     y las dos pasan el paso 1. Entonces SQL Server decide: el UPDATE de la fila de
///     confirmación lleva su RowVersion en el WHERE, el segundo afecta a cero filas y EF lanza
///     DbUpdateConcurrencyException. Como las comisiones se escriben en ESE MISMO lote, el
///     perdedor no deja ni media comisión detrás — o entra todo, o no entra nada. Comprobado en
///     caliente lanzando cuatro confirmaciones simultáneas del mismo pedido: una devolvió 200 y
///     tres CRYPTO_PAYMENT_ALREADY_CONFIRMED, con una sola fila de confirmación y un solo delta.
///
///  3. La idempotencia de los servicios de bonos. SponsorBonusService y FastStartBonusService
///     ya se saltan el cálculo si existe una CommissionEarning para el mismo SourceOrderId, y
///     hay un índice único (SourceOrderId, CommissionTypeId) en la base que lo respalda. Si por
///     un camino que no se me ocurre ahora las dos primeras capas fallaran, esta tercera sigue
///     impidiendo la comisión duplicada.
/// </summary>
public class ConfirmCryptoPaymentHandler
    : IRequestHandler<ConfirmCryptoPaymentCommand, Result<ConfirmCryptoPaymentResponse>>
{
    private const string AlreadyConfirmedCode = "CRYPTO_PAYMENT_ALREADY_CONFIRMED";
    private const string AlreadyConfirmedText = "This crypto payment has already been confirmed.";

    private readonly AppDbContext                          _db;
    private readonly IDateTimeProvider                     _dateTime;
    private readonly UserManager<ApplicationUser>          _userManager;
    private readonly ISignupActivationService              _activation;
    private readonly ILogger<ConfirmCryptoPaymentHandler>  _logger;

    public ConfirmCryptoPaymentHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        UserManager<ApplicationUser> userManager,
        ISignupActivationService activation,
        ILogger<ConfirmCryptoPaymentHandler> logger)
    {
        _db          = db;
        _dateTime    = dateTime;
        _userManager = userManager;
        _activation  = activation;
        _logger      = logger;
    }

    public async Task<Result<ConfirmCryptoPaymentResponse>> Handle(
        ConfirmCryptoPaymentCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Solo altas. Las compras de tokens en cripto las confirma Billing (Plan F): aquí no hay
        // suscripción que activar y activar una membresía por una compra de tokens sería un regalo.
        var confirmation = await _db.CryptoPaymentConfirmations
            .FirstOrDefaultAsync(c => c.OrderId == command.OrderId && c.Purpose == CryptoPaymentPurpose.Signup, ct);

        if (confirmation is null)
            return Result<ConfirmCryptoPaymentResponse>.Failure(
                "CRYPTO_PAYMENT_NOT_FOUND",
                "No crypto payment awaiting confirmation was found for that order.");

        // Capa 1 — la máquina de estados.
        if (confirmation.Status != CryptoPaymentConfirmationStatus.AwaitingPayment)
            return Result<ConfirmCryptoPaymentResponse>.Failure(AlreadyConfirmedCode, AlreadyConfirmedText);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);
        if (order is null)
            return Result<ConfirmCryptoPaymentResponse>.Failure(
                "SIGNUP_NOT_FOUND", "The signup order for this crypto payment no longer exists.");

        if (order.Status != OrderStatus.Processing)
            return Result<ConfirmCryptoPaymentResponse>.Failure(AlreadyConfirmedCode, AlreadyConfirmedText);

        // El rastro se marca ANTES de activar, para que la fila de confirmación —con su
        // RowVersion— viaje en el mismo lote de UPDATE que el resto. Es lo que decide la carrera.
        confirmation.Status              = CryptoPaymentConfirmationStatus.Confirmed;
        confirmation.CryptoTransactionId = command.Request.CryptoTransactionId.Trim();
        confirmation.Notes               = command.Request.Notes?.Trim();
        confirmation.ConfirmedByUserId   = command.ConfirmedByUserId;
        confirmation.ConfirmedByEmail    = command.ConfirmedByEmail;
        confirmation.ConfirmedAt         = now;
        confirmation.LastUpdateDate      = now;
        confirmation.LastUpdateBy        = command.ConfirmedByEmail;

        MemberProfile member;
        try
        {
            // La transacción envuelve la transición atómica del pedido (SignupOrderClaim, un UPDATE
            // que se ejecuta en el acto) y todo lo que guarda la activación: o entra todo o nada.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Exactamente lo mismo que hacen la vía de tarjeta al completar y Entrax al cobrar:
            // pedido cerrado, miembro y suscripción activos, deltas encolados al upline y
            // comisiones calculadas. Un solo sitio (ISignupActivationService).
            //
            // La llamada va DENTRO del try aunque no sea ella quien persiste: por dentro,
            // RecurringBillingEnrollmentService.EnsureStateForSubscriptionAsync llama a
            // SaveChangesAsync por su cuenta (su interfaz dice que no lo hace, pero lo hace), y
            // ese guardado arrastra todo el rastreador de cambios —la fila de confirmación
            // incluida—. O sea que la carrera se resuelve ahí dentro, no en el SaveChanges de
            // abajo. Con el try solo alrededor del SaveChanges explícito, al perdedor le salía
            // "An unexpected error occurred" en vez de "ya está confirmado". Verificado en
            // caliente con cuatro confirmaciones simultáneas.
            var activation = await _activation.ActivatePendingSignupAsync(order, now, ct);
            if (!activation.IsSuccess)
            {
                // No se activa nada: se descarta la marca de la confirmación para que no viaje en
                // ningún guardado posterior.
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();

                // Otro pago (Entrax, u otra confirmación que ganó la carrera) ya activó el pedido.
                if (activation.ErrorCode == SignupOrderClaim.AlreadySettledCode)
                {
                    _logger.LogWarning(
                        "ConfirmCryptoPayment: el pedido {OrderId} ya estaba activado por otro pago; no se confirma.",
                        command.OrderId);
                    return Result<ConfirmCryptoPaymentResponse>.Failure(AlreadyConfirmedCode, AlreadyConfirmedText);
                }

                return Result<ConfirmCryptoPaymentResponse>.Failure(activation.ErrorCode!, activation.Error!);
            }

            member = activation.Value!;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "ConfirmCryptoPayment: carrera al confirmar el pedido {OrderId}; gana la primera confirmación.",
                command.OrderId);
            return Result<ConfirmCryptoPaymentResponse>.Failure(AlreadyConfirmedCode, AlreadyConfirmedText);
        }

        // Solo ahora la cuenta puede entrar al portal. Hasta este punto LoginHandler la rechazaba
        // por IsActive = false, que es lo que se quería mientras el dinero no estuviera cobrado.
        await SignupUserActivation.ActivateAsync(_userManager, member.Email);

        return Result<ConfirmCryptoPaymentResponse>.Success(new ConfirmCryptoPaymentResponse
        {
            OrderId             = order.Id,
            MemberId            = member.MemberId,
            Email               = member.Email,
            MemberStatus        = member.Status.ToString(),
            CryptoTransactionId = confirmation.CryptoTransactionId!,
            ConfirmedByEmail    = confirmation.ConfirmedByEmail!,
            ConfirmedAt         = now
        });
    }
}
