using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using ICacheService            = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>Quién compra (siempre del token), con qué identidad queda el rastro y qué pide.</summary>
public sealed record TokenPurchaseInput(string MemberId, string Actor, PurchaseTokensRequest Request);

public interface ITokenPurchaseService
{
    Task<TokenCatalogDto> GetCatalogAsync(string memberId, CancellationToken ct = default);
    Task<TokenPurchaseFundsDto> GetFundsAsync(string memberId, CancellationToken ct = default);
    Task<Result<TokenPurchaseResultDto>> PurchaseAsync(TokenPurchaseInput input, CancellationToken ct = default);
    Task<Result<TokenPurchaseResultDto>> ConfirmCryptoAsync(
        string orderId, ConfirmCryptoTokenPurchaseRequest request, string confirmedByUserId, string confirmedByEmail,
        CancellationToken ct = default);
    Task<Result<TokenPurchaseResultDto>> ReconcileAsync(
        string purchaseId, ReconcileTokenPurchaseRequest request, string actor, CancellationToken ct = default);

    /// <summary>El miembro retira su compra en cripto sin pagar (D18). Solo la suya.</summary>
    Task<Result<TokenPurchaseResultDto>> CancelCryptoAsync(
        string memberId, string purchaseId, string actor, CancellationToken ct = default);

    /// <summary>Un aprobador de cripto rechaza la compra: la transferencia no llegó o no vale (D18).</summary>
    Task<Result<TokenPurchaseResultDto>> RejectCryptoAsync(
        string orderId, RejectCryptoTokenPurchaseRequest request, string rejectedByUserId, string rejectedByEmail,
        CancellationToken ct = default);

    /// <summary>Caduca las cripto sin confirmar de <paramref name="expiryDays"/> días o más. Devuelve cuántas.</summary>
    Task<int> ExpireStaleCryptoAsync(int expiryDays, string actor, CancellationToken ct = default);

    /// <summary>
    /// Pasa a conciliar las compras con tarjeta o eWallet que llevan <paramref name="minutes"/> minutos o
    /// más en Pending: el proceso murió entre el cobro y la emisión. Nunca las libera. Devuelve cuántas.
    /// </summary>
    Task<int> MoveStalePendingToReconciliationAsync(int minutes, string actor, CancellationToken ct = default);
}

/// <summary>
/// La compra de tokens. Cuatro caminos según dónde está el dinero:
///
///   • COMISIONES: todo en UNA transacción bajo el candado del miembro. El saldo se comprueba antes de
///     escribir; si algo falla a mitad, se deshace entero.
///   • EWALLET (i-Payout): (1) reservar cupo y confirmar; (2) con el candado otra vez tomado, releer el
///     saldo, debitar, verificar por saldo (como el legado) y emitir o dejar a conciliar. El candado en
///     la fase 2 es lo que hace fiable la verificación por saldo.
///   • TARJETA (apagada por defecto, Billing:TokenPurchase:CardEnabled): (1) reservar; (2) cobrar FUERA
///     de la transacción, paso a paso; (3) emitir bajo el candado, con CancellationToken.None, porque el
///     dinero ya se movió.
///   • CRIPTO: reservar cupo, dejar el pedido en Processing con su CryptoPaymentConfirmation y devolver
///     dirección, moneda e importe. Emite <see cref="ConfirmCryptoAsync"/>.
///
/// Lo dudoso nunca se reintenta: queda NeedsReconciliation y lo cierra <see cref="ReconcileAsync"/>.
/// Ninguna compra queda en Pending por un fallo nuestro: antes de mover dinero se abandona (libera);
/// después, pasa a conciliar; y si ni eso se puede guardar, lo recoge
/// <see cref="MoveStalePendingToReconciliationAsync"/>.
/// Una cripto sin pagar sale por <see cref="CancelCryptoAsync"/>, <see cref="RejectCryptoAsync"/> o
/// <see cref="ExpireStaleCryptoAsync"/>, y las tres liberan el cupo.
/// No reembolsable.
/// </summary>
public sealed class TokenPurchaseService : ITokenPurchaseService
{
    // Mensajes que ve el miembro. Fijos a propósito: el texto de i-Payout o del gateway se queda en el
    // log y en las notas de la compra, nunca en la respuesta (puede traer datos de la cuenta o cuerpos).
    private const string EWalletUnavailableMessage  = "We couldn't reach your eWallet right now. Try again later.";
    private const string EWalletInsufficientMessage = "Your eWallet balance is not enough for this purchase.";
    private const string EWalletRejectedMessage     = "Your eWallet declined the charge.";
    private const string EWalletReviewMessage       = "We couldn't confirm the eWallet charge. It will be reviewed; do not retry.";
    private const string CardReviewMessage          = "We couldn't confirm the card charge. It will be reviewed; do not retry.";
    private const string CardDeclinedMessage        = "The card payment was declined.";
    private const string NoGatewayMessage           = "No card gateway is available right now.";
    private const string UnavailableMessage         = "The payment service is not available right now. Try again in a moment.";
    private const string BusyMessage                = "Another purchase for this account is in progress. Try again in a moment.";

    private readonly AppDbContext                _db;
    private readonly IDateTimeProvider           _clock;
    private readonly ITokenPricingService        _pricing;
    private readonly ITokenMonthlyQuota          _quota;
    private readonly IMemberFundsGate            _gate;
    private readonly ITokenIssuer                _issuer;
    private readonly ICommissionBalanceService   _commissions;
    private readonly IEWalletFunds               _eWallet;
    private readonly IGatewayRouter              _router;
    private readonly IGatewayChargeOrchestrator  _orchestrator;
    private readonly ICardBrandDetector          _brands;
    private readonly ICryptoDepositAddressReader _cryptoAddresses;
    private readonly ITokenPurchaseReceipts      _receipts;
    private readonly ICacheService               _cache;
    private readonly IPushNotificationService    _push;
    private readonly ILogger<TokenPurchaseService> _logger;
    private readonly IConfiguration              _config;

    public TokenPurchaseService(
        AppDbContext db, IDateTimeProvider clock, ITokenPricingService pricing, ITokenMonthlyQuota quota,
        IMemberFundsGate gate, ITokenIssuer issuer, ICommissionBalanceService commissions, IEWalletFunds eWallet,
        IGatewayRouter router, IGatewayChargeOrchestrator orchestrator, ICardBrandDetector brands,
        ICryptoDepositAddressReader cryptoAddresses, ITokenPurchaseReceipts receipts,
        ICacheService cache, IPushNotificationService push, ILogger<TokenPurchaseService> logger,
        IConfiguration config)
    {
        _db              = db;
        _clock           = clock;
        _pricing         = pricing;
        _quota           = quota;
        _gate            = gate;
        _issuer          = issuer;
        _commissions     = commissions;
        _eWallet         = eWallet;
        _router          = router;
        _orchestrator    = orchestrator;
        _brands          = brands;
        _cryptoAddresses = cryptoAddresses;
        _receipts        = receipts;
        _cache           = cache;
        _push            = push;
        _logger          = logger;
        _config          = config;
    }

    /// <summary>
    /// Se lee en cada llamada, no al arrancar: apagar la tarjeta en caliente no exige reiniciar Billing.
    /// Apagada por defecto mientras el gateway sea un stub (bloqueante de producción).
    /// </summary>
    private bool CardEnabled => _config.GetValue(TokenPurchaseRules.CardEnabledConfigKey, false);

    // ── Lecturas ──────────────────────────────────────────────────────────────

    public async Task<TokenCatalogDto> GetCatalogAsync(string memberId, CancellationToken ct = default)
    {
        var items = await _pricing.GetCatalogAsync(ct);
        var used  = await _quota.UsedAsync(memberId, _clock.Now, ct);

        return new TokenCatalogDto
        {
            MonthlyLimit       = TokenPurchaseRules.MonthlyLimit,
            PurchasedThisMonth = used,
            MonthlyRemaining   = Math.Max(0, TokenPurchaseRules.MonthlyLimit - used),
            CryptoDeposits     = (await _cryptoAddresses.GetConfiguredAsync(ct)).ToList(),
            CardEnabled        = CardEnabled,
            Items = items.Select(q => new TokenCatalogItemDto
            {
                TokenTypeId = q.TokenTypeId, Name = q.Name, Description = q.Description, IsGuestPass = q.IsGuestPass,
                UnitPrice = q.UnitPrice, Purchasable = q.Purchasable, NotPurchasableReason = q.NotPurchasableReason
            }).ToList()
        };
    }

    public async Task<TokenPurchaseFundsDto> GetFundsAsync(string memberId, CancellationToken ct = default)
    {
        var funds = new TokenPurchaseFundsDto
        {
            CommissionBalance = await _commissions.GetSpendableAsync(memberId, ct),
            CardEnabled       = CardEnabled
        };

        var userName = await _eWallet.UserNameOfAsync(memberId, ct);
        if (userName is null) return funds;

        // Si i-Payout falla o no responde, el saldo sale como "no disponible" (null) y no como un 500
        // que tumbaría la pantalla entera, comisiones incluidas.
        funds.HasEWallet = true;
        var balance = await SafeBalanceAsync(memberId, userName, ct);
        funds.EWalletBalance = balance.IsSuccess ? balance.Value : null;
        return funds;
    }

    // ── Compra ────────────────────────────────────────────────────────────────

    /// <summary>Lo que identifica una compra para su clave: otro tipo, cantidad o método no es un reintento.</summary>
    private sealed record Intent(int TokenTypeId, int Quantity, TokenPaymentMethod Method);

    private sealed record Draft(
        string MemberId, string MemberEmail, string? Country, string Actor,
        TokenQuote Quote, int Quantity, TokenPaymentMethod Method, string IdempotencyKey)
    {
        public decimal Total => Quote.UnitPrice!.Value * Quantity;
        public Intent Intent => new(Quote.TokenTypeId, Quantity, Method);
    }

    public async Task<Result<TokenPurchaseResultDto>> PurchaseAsync(TokenPurchaseInput input, CancellationToken ct = default)
    {
        var req = input.Request;

        // Solo por nombre: Enum.TryParse también acepta "1", y la ruta con step-up se elige mirando el
        // nombre (TokenPaymentMethods.IsBalance), así que un número pagaría con saldo sin step-up.
        if (!TryParseMethod(req.PaymentMethod, out var method))
            return Fail(TokenPurchaseErrors.PaymentMethodNotSupported, "Payment method not supported for tokens.");
        // Antes de reservar nada: con la tarjeta apagada no hay pedido, ni cupo, ni cobro.
        if (method == TokenPaymentMethod.CreditCard && !CardEnabled)
            return Fail(TokenPurchaseErrors.CardPaymentsDisabled, "Card payments are not available right now.");
        if (req.Quantity < 1 || req.Quantity > TokenPurchaseRules.MonthlyLimit)
            return Fail(TokenPurchaseErrors.InvalidQuantity, $"Quantity must be between 1 and {TokenPurchaseRules.MonthlyLimit}.");
        if (!TokenPurchaseRules.IsValidIdempotencyKey(req.IdempotencyKey))
            return Fail(TokenPurchaseErrors.IdempotencyKeyRequired, "A valid idempotency key is required.");

        var intent = new Intent(req.TokenTypeId, req.Quantity, method);
        var replay = await ReplayAsync(input.MemberId, req.IdempotencyKey, intent, ct);
        if (replay is not null) return replay;

        var member = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == input.MemberId && !m.IsDeleted, ct);
        if (member is null) return Fail(TokenPurchaseErrors.MemberNotFound, "Member not found.");
        if (member.Status != MemberAccountStatus.Active)
            return Fail(TokenPurchaseErrors.MemberInactive, "Only active members can buy tokens.");

        var quote = await _pricing.QuoteAsync(req.TokenTypeId, ct);
        if (!quote.IsSuccess) return Fail(quote.ErrorCode!, quote.Error!);

        var d = new Draft(input.MemberId, member.Email, member.Country, input.Actor,
                          quote.Value!, req.Quantity, method, req.IdempotencyKey);
        try
        {
            return method switch
            {
                TokenPaymentMethod.CommissionBalance => await PayWithCommissionsAsync(d, ct),
                TokenPaymentMethod.EWallet           => await PayWithEWalletAsync(d, ct),
                TokenPaymentMethod.CreditCard        => await PayWithCardAsync(d, req.CreditCardId, ct),
                TokenPaymentMethod.Crypto            => await PayWithCryptoAsync(d, req.CryptoCurrency, ct),
                _ => Fail(TokenPurchaseErrors.PaymentMethodNotSupported, "Payment method not supported for tokens.")
            };
        }
        catch (MemberFundsBusyException)
        {
            return Fail(TokenPurchaseErrors.Busy, BusyMessage);
        }
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithCommissionsAsync(Draft d, CancellationToken ct)
    {
        var now = _clock.Now;
        await using var tx = await _gate.EnterAsync(d.MemberId, ct);

        var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, d.Intent, ct);
        if (replay is not null) return replay;

        var used = await _quota.UsedAsync(d.MemberId, now, ct);
        if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

        var spendable = await _commissions.GetSpendableAsync(d.MemberId, ct);
        if (spendable < d.Total)
            return Fail(TokenPurchaseErrors.InsufficientCommissionBalance, $"Commission balance {spendable:F2} is below {d.Total:F2}.");

        var (order, purchase) = Start(d, TokenPurchaseStatus.Pending, creditCardId: null, now);
        var raced = await SaveReservationAsync(tx, d, ct);
        if (raced is not null) return raced;

        var fund = await _commissions.FundTokenPurchaseAsync(d.MemberId, d.Total, order.Id, d.Actor, ct);
        if (!fund.IsSuccess) return await RollbackAsync(tx, fund.ErrorCode!, fund.Error!);

        var codes = await _issuer.IssueAsync(
            new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, now), ct);
        purchase.Complete(fund.Value!, now, d.Actor);
        MarkPaid(order, d.Actor, now);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await AfterIssueAsync(purchase, order, d.Quote.Name);
        return Done(purchase, order, codes, used + d.Quantity);
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithEWalletAsync(Draft d, CancellationToken ct)
    {
        var userName = await _eWallet.UserNameOfAsync(d.MemberId, ct);
        if (userName is null)
            return Fail(TokenPurchaseErrors.EWalletNotFound, "The member has no registered eWallet account.");

        var charge = EWalletFunds.ChargeAmount(d.Total);

        // Comprobación temprana para responder rápido; la que cuenta se repite bajo el candado.
        var early = await SafeBalanceAsync(d.MemberId, userName, ct);
        if (!early.IsSuccess) return Fail(TokenPurchaseErrors.EWalletUnavailable, EWalletUnavailableMessage);
        if (early.Value < charge) return Fail(TokenPurchaseErrors.InsufficientEWalletBalance, EWalletInsufficientMessage);

        var now = _clock.Now;
        Orders order;
        TokenPurchase purchase;
        int usedAfter;

        // Fase 1 — reservar cupo bajo el candado y dejarlo escrito antes de tocar i-Payout. Si algo falla
        // aquí la transacción se deshace entera: no queda ni la reserva.
        await using (var tx = await _gate.EnterAsync(d.MemberId, ct))
        {
            var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, d.Intent, ct);
            if (replay is not null) return replay;

            var used = await _quota.UsedAsync(d.MemberId, now, ct);
            if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

            (order, purchase) = Start(d, TokenPurchaseStatus.Pending, creditCardId: null, now);
            var raced = await SaveReservationAsync(tx, d, ct);
            if (raced is not null) return raced;
            await tx.CommitAsync(ct);
            usedAfter = used + d.Quantity;
        }

        // Fase 2 — con el candado: ningún otro débito nuestro del mismo miembro puede cruzarse con la
        // verificación por saldo. Sin cancelación: desde aquí puede moverse dinero.
        IDbContextTransaction tx2;
        try
        {
            tx2 = await _gate.EnterAsync(d.MemberId, CancellationToken.None);
        }
        catch (MemberFundsBusyException)
        {
            // Todavía no se tocó i-Payout: se puede soltar la reserva.
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.Busy, d.Actor, BusyMessage);
        }
        catch (Exception ex)
        {
            // La base o la red fallaron al tomar el candado. Tampoco se tocó i-Payout: se suelta.
            _logger.LogError(ex, "Compra de tokens {PurchaseId}: no se pudo tomar el candado antes del débito eWallet.", purchase.Id);
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.PaymentServiceUnavailable, d.Actor, UnavailableMessage);
        }

        await using (tx2)
        {
            var before = await SafeBalanceAsync(d.MemberId, userName, CancellationToken.None);
            if (!before.IsSuccess || before.Value < charge)
            {
                await ReleaseOpenTransactionAsync();
                return before.IsSuccess
                    ? await AbandonAsync(order, purchase, TokenPurchaseErrors.InsufficientEWalletBalance, d.Actor, EWalletInsufficientMessage)
                    : await AbandonAsync(order, purchase, TokenPurchaseErrors.EWalletUnavailable, d.Actor, EWalletUnavailableMessage);
            }

            EWalletDebitOutcome outcome;
            try
            {
                outcome = await _eWallet.DebitAsync(userName, d.Total, before.Value, purchase.Id,
                    $"Token purchase {order.OrderNo}", CancellationToken.None);
            }
            catch (Exception ex)
            {
                // El débito pudo haber salido antes de la excepción: a conciliar, nunca liberar ni reintentar.
                _logger.LogError(ex, "Compra de tokens {PurchaseId}: el débito eWallet lanzó una excepción.", purchase.Id);
                outcome = new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_OUTCOME_UNKNOWN", ex.GetType().Name);
            }

            if (outcome.Status != EWalletDebitStatus.Completed)
                _logger.LogWarning("Compra de tokens {PurchaseId}: débito eWallet de {User} {Status} {Code} {Detail}",
                    purchase.Id, EWalletClient.MaskUserName(userName), outcome.Status, outcome.Code, outcome.Detail);

            var at = _clock.Now;
            IReadOnlyList<string> codes;
            try
            {
                switch (outcome.Status)
                {
                    case EWalletDebitStatus.Rejected:
                        purchase.Fail(TokenPurchaseErrors.EWalletRejected, at, d.Actor);
                        purchase.Notes = Truncate($"{outcome.Code}: {outcome.Detail}");
                        Cancel(order, d.Actor, at);
                        await _db.SaveChangesAsync(CancellationToken.None);
                        await tx2.CommitAsync(CancellationToken.None);
                        return Fail(TokenPurchaseErrors.EWalletRejected, EWalletRejectedMessage);

                    case EWalletDebitStatus.NeedsReconciliation:
                        purchase.MarkForReconciliation(outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN", Truncate(outcome.Detail), at, d.Actor);
                        await _db.SaveChangesAsync(CancellationToken.None);
                        await tx2.CommitAsync(CancellationToken.None);
                        return Fail(TokenPurchaseErrors.NeedsReview, EWalletReviewMessage);
                }

                // Lo cobrado es el importe a centavos que mandó EWalletFunds, no los 4 decimales del precio.
                var payment = AddPaymentHistory(order, charge, EWalletGatewayName, purchase.Id, d.Actor, at);
                codes = await _issuer.IssueAsync(
                    new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, at),
                    CancellationToken.None);
                purchase.Complete(payment.Id, at, d.Actor);
                MarkPaid(order, d.Actor, at);
                await _db.SaveChangesAsync(CancellationToken.None);
                await tx2.CommitAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // No se pudo dejar constancia del resultado del débito (base caída, colisión de códigos…).
                // El dinero pudo salir: la compra no puede quedar en Pending ni liberarse.
                var code = outcome.Status switch
                {
                    EWalletDebitStatus.Completed => "EWALLET_CHARGED_NOT_ISSUED",
                    EWalletDebitStatus.Rejected  => "EWALLET_REJECTED_NOT_SAVED",
                    _                            => outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN"
                };
                return await RescueToReconciliationAsync(purchase.Id, code, ex.GetType().Name, d.Actor, ex, EWalletReviewMessage);
            }

            await AfterIssueAsync(purchase, order, d.Quote.Name);
            return Done(purchase, order, codes, usedAfter);
        }
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithCardAsync(Draft d, string? creditCardId, CancellationToken ct)
    {
        var card = string.IsNullOrWhiteSpace(creditCardId)
            ? null
            : await _db.CreditCards.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == creditCardId && c.MemberId == d.MemberId && !c.IsDeleted, ct);

        if (card is null) return Fail(TokenPurchaseErrors.CreditCardNotFound, "Card not found.");
        if (card.IsExpired) return Fail(TokenPurchaseErrors.CreditCardExpired, "The card is expired.");

        var cardRef = !string.IsNullOrWhiteSpace(card.SpreedlyPaymentMethodToken) ? card.SpreedlyPaymentMethodToken : card.CardToken;
        if (string.IsNullOrWhiteSpace(cardRef))
            return Fail(TokenPurchaseErrors.CreditCardNotChargeable, "The card has no chargeable token.");

        var now = _clock.Now;
        Orders order;
        TokenPurchase purchase;
        int usedAfter;

        // Fase 1 — reservar cupo bajo el candado.
        await using (var tx = await _gate.EnterAsync(d.MemberId, ct))
        {
            var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, d.Intent, ct);
            if (replay is not null) return replay;

            var used = await _quota.UsedAsync(d.MemberId, now, ct);
            if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

            (order, purchase) = Start(d, TokenPurchaseStatus.Pending, card.Id, now);
            var raced = await SaveReservationAsync(tx, d, ct);
            if (raced is not null) return raced;
            await tx.CommitAsync(ct);
            usedAfter = used + d.Quantity;
        }

        // Fase 2 — cobrar fuera de la transacción.
        var routingCtx = new GatewayRoutingContext
        {
            OperationType        = BillingOperationType.Payment,
            CardBrand            = _brands.Detect(card.First6),
            CardholderCountryIso = string.IsNullOrWhiteSpace(d.Country) ? "US" : d.Country!,
            AmountUsd            = d.Total,
            MemberId             = d.MemberId
        };

        GatewayRoutingPlan? plan;
        try
        {
            var resolved = await _router.ResolveAsync(routingCtx, ct);
            plan = resolved.IsSuccess ? resolved.Value : null;
        }
        catch (Exception ex)
        {
            // Todavía no se llamó a ningún gateway: se suelta la reserva.
            _logger.LogError(ex, "Compra de tokens {PurchaseId}: el ruteo de tarjeta falló.", purchase.Id);
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.NoImmediateGateway, d.Actor, NoGatewayMessage);
        }

        // Solo pasos inmediatos: uno con espera lo cobraría DelayedFallbackChargeJob, que no emite tokens.
        var steps = plan?.Steps.Where(s => s.DelayMinutes == 0).ToList() ?? new List<GatewayAttemptPlan>();
        if (plan is null || steps.Count == 0)
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.NoImmediateGateway, d.Actor, NoGatewayMessage);

        var chargeRequest = new OrchestratorChargeRequest
        {
            MemberId = d.MemberId, TokenizedCardRef = cardRef, Description = $"Token purchase {order.OrderNo}",
            OrderId = order.Id, IsRecurring = false
        };

        // Un paso cada vez. El orquestador pasa al siguiente gateway ante CUALQUIER fallo, también un
        // timeout en el que el emisor pudo cargar: con varios pasos de una vez, eso es un cobro doble.
        // Aquí solo se sigue al siguiente cuando el fallo prueba que no hubo cobro (CardChargeOutcomes);
        // lo demás va a conciliar. Sin cancelación: el orquestador cobra y DESPUÉS guarda el
        // PaymentHistory, y cortarlo entre medias dejaría un cobro sin rastro.
        OrchestratorChargeResult? charged = null;
        foreach (var step in steps)
        {
            Result<OrchestratorChargeResult> attempt;
            try
            {
                attempt = await _orchestrator.ExecuteAsync(
                    new GatewayRoutingPlan { RouteBucketKey = plan.RouteBucketKey, Steps = [step] },
                    routingCtx, chargeRequest, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Timeout o caída a mitad del cobro: no se sabe si el emisor cargó. Ni tokens ni liberar el
                // cupo: tesorería lo mira en el gateway y lo cierra con ReconcileAsync.
                _logger.LogError(ex, "Compra de tokens {PurchaseId}: el cobro con tarjeta en {Processor} terminó sin resultado.",
                    purchase.Id, step.CardProcessor);
                return await ToReconciliationAsync(purchase, "CARD_OUTCOME_UNKNOWN",
                    $"{step.CardProcessor}: {ex.GetType().Name}", d.Actor, CardReviewMessage);
            }

            if (attempt.IsSuccess)
            {
                var value = attempt.Value;
                if (value is null || value.Status != "Success")
                {
                    // Solo pasan pasos inmediatos, así que "Scheduled" no debería llegar; si llega, hay un
                    // cobro programado que cobraría más tarde sin emitir: a revisión, no a liberar.
                    var status = value?.Status ?? "EMPTY";
                    _logger.LogError("Compra de tokens {PurchaseId}: el orquestador devolvió {Status} con solo pasos inmediatos.",
                        purchase.Id, status);
                    return await ToReconciliationAsync(purchase, "CARD_CHARGE_" + status.ToUpperInvariant(),
                        value?.ScheduledJobId, d.Actor, CardReviewMessage);
                }

                charged = value;
                break;
            }

            if (!CardChargeOutcomes.IsExplicitNoCharge(attempt.ErrorCode))
            {
                // El orquestador no distingue un rechazo de un resultado desconocido: lo que no consta
                // como "no cobrado" se revisa a mano.
                _logger.LogWarning("Compra de tokens {PurchaseId}: la tarjeta falló en {Processor} con {Code}; a conciliar.",
                    purchase.Id, step.CardProcessor, attempt.ErrorCode);
                return await ToReconciliationAsync(purchase, "CARD_OUTCOME_UNKNOWN",
                    $"{step.CardProcessor}: {attempt.ErrorCode}", d.Actor, CardReviewMessage);
            }

            _logger.LogWarning("Compra de tokens {PurchaseId}: {Processor} no cobró ({Code}).", purchase.Id, step.CardProcessor, attempt.ErrorCode);
        }

        if (charged is null)
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.CardChargeFailed, d.Actor, CardDeclinedMessage);

        if (CardChargeOutcomes.IsSimulated(charged.GatewayTransactionId))
        {
            // El gateway sigue siendo un stub: un "éxito" simulado no es dinero. Nunca se emite por él.
            _logger.LogCritical("Compra de tokens {PurchaseId}: {Processor} devolvió un cobro simulado ({TxId}). " +
                                "Billing:TokenPurchase:CardEnabled no debe estar encendido hasta que el cobro con tarjeta sea real.",
                purchase.Id, charged.ProcessorUsed, charged.GatewayTransactionId);
            return await ToReconciliationAsync(purchase, TokenPurchaseErrors.CardSimulatedCharge,
                $"{charged.ProcessorUsed}: {charged.GatewayTransactionId}", d.Actor, CardReviewMessage);
        }

        // Fase 3 — emitir bajo el candado del miembro (TokenBalance no tiene control de concurrencia) y
        // sin cancelación. Cobrado ya: si no se puede emitir, a conciliar, nunca fallar.
        IDbContextTransaction tx3;
        try
        {
            tx3 = await _gate.EnterAsync(d.MemberId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compra de tokens {PurchaseId}: cobrada pero sin candado para emitir.", purchase.Id);
            return await ToReconciliationAsync(purchase,
                ex is MemberFundsBusyException ? "CARD_CHARGED_GATE_BUSY" : "CARD_CHARGED_NOT_ISSUED",
                $"{charged.ProcessorUsed}: {charged.GatewayTransactionId}", d.Actor, CardReviewMessage);
        }

        IReadOnlyList<string> cardCodes;
        await using (tx3)
        {
            try
            {
                var issuedAt = _clock.Now;
                cardCodes = await _issuer.IssueAsync(
                    new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, issuedAt),
                    CancellationToken.None);
                purchase.Complete(charged.PaymentHistoryId, issuedAt, d.Actor);
                MarkPaid(order, d.Actor, issuedAt);
                await _db.SaveChangesAsync(CancellationToken.None);
                await tx3.CommitAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                return await RescueToReconciliationAsync(purchase.Id, "CARD_CHARGED_NOT_ISSUED",
                    $"{charged.ProcessorUsed}: {charged.GatewayTransactionId} ({ex.GetType().Name})", d.Actor, ex, CardReviewMessage);
            }
        }

        await AfterIssueAsync(purchase, order, d.Quote.Name);
        return Done(purchase, order, cardCodes, usedAfter);
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithCryptoAsync(Draft d, string? cryptoCurrency, CancellationToken ct)
    {
        var currency = cryptoCurrency?.Trim().ToUpperInvariant();
        if (currency is null || !TokenPurchaseRules.CryptoCurrencies.Contains(currency))
            return Fail(TokenPurchaseErrors.CryptoCurrencyNotSupported,
                $"Supported currencies: {string.Join(", ", TokenPurchaseRules.CryptoCurrencies)}.");

        var deposit = (await _cryptoAddresses.GetConfiguredAsync(ct)).FirstOrDefault(c => c.Currency == currency);
        if (deposit is null)
            return Fail(TokenPurchaseErrors.CryptoAddressNotConfigured, $"Payments in {currency} are not available right now.");

        var now = _clock.Now;
        await using var tx = await _gate.EnterAsync(d.MemberId, ct);

        var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, d.Intent, ct);
        if (replay is not null) return replay;

        var used = await _quota.UsedAsync(d.MemberId, now, ct);
        if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

        var (order, purchase) = Start(d, TokenPurchaseStatus.AwaitingCryptoConfirmation, creditCardId: null, now);
        _db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = order.Id, MemberId = d.MemberId, MemberEmail = d.MemberEmail, CryptoCurrency = currency,
            AmountDue = d.Total, Status = CryptoPaymentConfirmationStatus.AwaitingPayment,
            Purpose = CryptoPaymentPurpose.TokenPurchase, CreatedBy = d.Actor, CreationDate = now, LastUpdateDate = now
        });

        var raced = await SaveReservationAsync(tx, d, ct);
        if (raced is not null) return raced;
        await tx.CommitAsync(ct);

        return Done(purchase, order, Array.Empty<string>(), used + d.Quantity, deposit);
    }

    // ── Confirmación de cripto ────────────────────────────────────────────────

    /// <remarks>
    /// Mismas capas que ConfirmCryptoPaymentHandler (altas): máquina de estados, RowVersion de la
    /// confirmación en el mismo SaveChanges que los tokens y unicidad de OrderId. Solo filas de compra.
    /// NO vuelve a comprobar el tope (D3): el cupo se comprobó al reservar, en el mes de su PeriodKey, y
    /// los tokens que emite cuentan en ese mes aunque se confirme en el siguiente.
    /// Si el guardado choca con el índice único de ReferenceId (otro proceso emitió el mismo código
    /// entre la comprobación de TokenIssuer y el guardado), se repite una vez con códigos nuevos.
    /// </remarks>
    public async Task<Result<TokenPurchaseResultDto>> ConfirmCryptoAsync(
        string orderId, ConfirmCryptoTokenPurchaseRequest request, string confirmedByUserId, string confirmedByEmail,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CryptoTransactionId))
            return Fail(TokenPurchaseErrors.CryptoTransactionIdRequired, "The transaction id is required.");

        return await WithOneRetryOnCollisionAsync(
            () => ConfirmCryptoOnceAsync(orderId, request, confirmedByUserId, confirmedByEmail, ct),
            $"confirmar la cripto del pedido {orderId}");
    }

    private async Task<Result<TokenPurchaseResultDto>> ConfirmCryptoOnceAsync(
        string orderId, ConfirmCryptoTokenPurchaseRequest request, string confirmedByUserId, string confirmedByEmail,
        CancellationToken ct)
    {
        var confirmation = await _db.CryptoPaymentConfirmations
            .FirstOrDefaultAsync(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.TokenPurchase, ct);
        if (confirmation is null)
            return Fail(TokenPurchaseErrors.CryptoPaymentNotFound, "No crypto token purchase awaiting confirmation was found for that order.");
        if (confirmation.Status == CryptoPaymentConfirmationStatus.Cancelled)
            return Fail(TokenPurchaseErrors.NotAwaitingCrypto, "This crypto purchase was cancelled, rejected or expired.");
        if (confirmation.Status != CryptoPaymentConfirmationStatus.AwaitingPayment)
            return Fail(TokenPurchaseErrors.CryptoAlreadyConfirmed, "This crypto payment has already been confirmed.");

        var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
        var order    = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (purchase is null || order is null)
            return Fail(TokenPurchaseErrors.CryptoPaymentNotFound, "The token purchase for this payment no longer exists.");
        if (purchase.Status != TokenPurchaseStatus.AwaitingCryptoConfirmation)
            return Fail(TokenPurchaseErrors.CryptoAlreadyConfirmed, "This crypto payment has already been confirmed.");

        var now = _clock.Now;
        confirmation.Status              = CryptoPaymentConfirmationStatus.Confirmed;
        confirmation.CryptoTransactionId = request.CryptoTransactionId.Trim();
        confirmation.Notes               = request.Notes?.Trim();
        confirmation.ConfirmedByUserId   = confirmedByUserId;
        confirmation.ConfirmedByEmail    = confirmedByEmail;
        confirmation.ConfirmedAt         = now;
        confirmation.LastUpdateDate      = now;
        confirmation.LastUpdateBy        = confirmedByEmail;

        var payment = AddPaymentHistory(order, confirmation.AmountDue, $"Crypto:{confirmation.CryptoCurrency}",
                                        confirmation.CryptoTransactionId, confirmedByEmail, now);
        var codes = await _issuer.IssueAsync(
            new TokenIssue(purchase.MemberId, purchase.TokenTypeId, purchase.Quantity, purchase.Id, order.OrderNo, confirmedByEmail, now), ct);
        purchase.Complete(payment.Id, now, confirmedByEmail);
        MarkPaid(order, confirmedByEmail, now);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Fail(TokenPurchaseErrors.CryptoAlreadyConfirmed, "This crypto payment has already been confirmed.");
        }

        await AfterIssueAsync(purchase, order, await TokenTypeNameAsync(purchase.TokenTypeId, ct));
        return Done(purchase, order, codes, await _quota.UsedAsync(purchase.MemberId, now, ct));
    }

    // ── Conciliación ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tesorería mira el gateway y dice si el dinero salió. Sí → se emiten los tokens; no → falla y
    /// libera el cupo. RowVersion de TokenPurchase impide cerrar la misma compra dos veces.
    /// </summary>
    public async Task<Result<TokenPurchaseResultDto>> ReconcileAsync(
        string purchaseId, ReconcileTokenPurchaseRequest request, string actor, CancellationToken ct = default) =>
        await WithOneRetryOnCollisionAsync(
            () => ReconcileOnceAsync(purchaseId, request, actor, ct),
            $"conciliar la compra {purchaseId}");

    /// <remarks>
    /// El PaymentHistory se escribe como en los caminos normales: importe a centavos (lo que pide
    /// i-Payout, ver <see cref="EWalletFunds.ChargeAmount"/>) y el nombre de gateway de siempre. Si ya
    /// hay uno del pedido (la tarjeta se cobró y el orquestador lo escribió antes de que fallara la
    /// emisión), se enlaza ese y no se añade otro: serían dos cobros en los informes para uno solo.
    /// </remarks>
    private async Task<Result<TokenPurchaseResultDto>> ReconcileOnceAsync(
        string purchaseId, ReconcileTokenPurchaseRequest request, string actor, CancellationToken ct)
    {
        var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId, ct);
        if (purchase is null) return Fail(TokenPurchaseErrors.PurchaseNotFound, "Token purchase not found.");
        if (purchase.Status != TokenPurchaseStatus.NeedsReconciliation)
            return Fail(TokenPurchaseErrors.NotUnderReview, "This purchase is not waiting for reconciliation.");

        var order = await _db.Orders.FirstAsync(o => o.Id == purchase.OrderId, ct);
        var existingPayment = await _db.PaymentHistories
            .Where(p => p.OrderId == order.Id)
            .OrderByDescending(p => p.CreationDate)
            .FirstOrDefaultAsync(ct);
        var now   = _clock.Now;
        var note  = Truncate($"{purchase.Notes} | Reconciled by {actor}: {(request.Charged ? "charged" : "not charged")}. {request.Notes}".Trim());
        var reference = string.IsNullOrWhiteSpace(request.GatewayReference) ? null : request.GatewayReference.Trim();
        IReadOnlyList<string> codes = Array.Empty<string>();

        if (request.Charged)
        {
            PaymentHistory payment;
            if (existingPayment is not null)
            {
                existingPayment.TransactionStatus = PaymentHistoryTransactionStatus.Captured;
                if (reference is not null) existingPayment.GatewayTransactionId = reference;
                existingPayment.LastUpdateDate = now;
                existingPayment.LastUpdateBy   = actor;
                payment = existingPayment;
            }
            else
            {
                payment = AddPaymentHistory(order, EWalletFunds.ChargeAmount(purchase.TotalAmount), GatewayNameOf(purchase),
                                            reference ?? purchase.Id, actor, now);
            }

            codes = await _issuer.IssueAsync(
                new TokenIssue(purchase.MemberId, purchase.TokenTypeId, purchase.Quantity, purchase.Id, order.OrderNo, actor, now), ct);
            purchase.Complete(payment.Id, now, actor);
            MarkPaid(order, actor, now);
        }
        else
        {
            purchase.Fail("NOT_CHARGED", now, actor);
            Cancel(order, actor, now);

            // Un PaymentHistory "Captured" de un cobro que tesorería da por no hecho (p. ej. un éxito
            // simulado del stub) mentiría en los informes.
            if (existingPayment is { TransactionStatus: PaymentHistoryTransactionStatus.Captured })
            {
                existingPayment.TransactionStatus = PaymentHistoryTransactionStatus.Failed;
                existingPayment.LastUpdateDate    = now;
                existingPayment.LastUpdateBy      = actor;
            }
        }
        purchase.Notes = note;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Fail(TokenPurchaseErrors.NotUnderReview, "This purchase was already reconciled.");
        }

        if (request.Charged)
            await AfterIssueAsync(purchase, order, await TokenTypeNameAsync(purchase.TokenTypeId, ct));

        return Done(purchase, order, codes, await _quota.UsedAsync(purchase.MemberId, now, ct));
    }

    // ── Cripto sin pagar (D18) ────────────────────────────────────────────────
    // Sin candado de fondos: cerrar una reserva solo puede BAJAR lo usado del mes, así que no rompe el
    // tope aunque se cruce con una compra. Contra una confirmación simultánea protege el RowVersion.

    public async Task<Result<TokenPurchaseResultDto>> CancelCryptoAsync(
        string memberId, string purchaseId, string actor, CancellationToken ct = default)
    {
        // Por MemberId además de Id: una compra de otro es "no existe", no "no es tuya".
        var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId && p.MemberId == memberId, ct);
        if (purchase is null) return Fail(TokenPurchaseErrors.PurchaseNotFound, "Token purchase not found.");

        return await CloseUnpaidCryptoAsync(purchase, p => p.CancelByMember(_clock.Now, actor),
            "Cancelled by the member before paying.", actor, ct);
    }

    public async Task<Result<TokenPurchaseResultDto>> RejectCryptoAsync(
        string orderId, RejectCryptoTokenPurchaseRequest request, string rejectedByUserId, string rejectedByEmail,
        CancellationToken ct = default)
    {
        var purchase = await _db.TokenPurchases
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.PaymentMethod == TokenPaymentMethod.Crypto, ct);
        if (purchase is null)
            return Fail(TokenPurchaseErrors.CryptoPaymentNotFound, "No crypto token purchase was found for that order.");

        var actor  = string.IsNullOrWhiteSpace(rejectedByEmail) ? rejectedByUserId : rejectedByEmail;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Rejected by an administrator." : request.Reason.Trim();
        return await CloseUnpaidCryptoAsync(purchase, p => p.Reject(reason, _clock.Now, actor),
            $"Rejected by {actor}: {reason}", actor, ct);
    }

    /// <remarks>
    /// Lo llama TokenPurchaseCryptoExpiryJob cada hora con Billing:TokenPurchase:CryptoExpiryDays. Cada
    /// compra se guarda por separado: si un administrador confirma una justo entonces, el conflicto de
    /// RowVersion la deja confirmada y las demás siguen.
    /// </remarks>
    public async Task<int> ExpireStaleCryptoAsync(int expiryDays, string actor, CancellationToken ct = default)
    {
        if (expiryDays <= 0) return 0;

        var cutoff = _clock.Now.AddDays(-expiryDays);
        var ids = await _db.TokenPurchases.AsNoTracking()
            .Where(p => p.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation && p.CreationDate <= cutoff)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in ids)
        {
            var purchase = await _db.TokenPurchases.FirstAsync(p => p.Id == id, ct);
            var result = await CloseUnpaidCryptoAsync(purchase, p => p.Expire(_clock.Now, actor),
                $"Expired after {expiryDays} days without confirmation.", actor, ct);
            if (result.IsSuccess) expired++;
        }

        return expired;
    }

    // ── Red de seguridad: Pending olvidadas ───────────────────────────────────

    /// <remarks>
    /// Lo llama TokenPurchaseStalePendingJob cada hora con Billing:TokenPurchase:StalePendingMinutes. Una
    /// compra con tarjeta o eWallet solo sigue en Pending tanto tiempo si el proceso murió (o no pudo
    /// guardar) entre el cobro y la emisión: el dinero pudo moverse, así que va a conciliar y NUNCA se
    /// libera. Las de comisiones no se miran: su Pending vive dentro de una transacción que, si muere,
    /// se deshace entera. Cada una se guarda por separado; si la petición original la cierra a la vez,
    /// gana quien guarde primero (RowVersion) y la otra se salta.
    /// </remarks>
    public async Task<int> MoveStalePendingToReconciliationAsync(int minutes, string actor, CancellationToken ct = default)
    {
        if (minutes <= 0) return 0;

        var now    = _clock.Now;
        var cutoff = now.AddMinutes(-minutes);
        var ids = await _db.TokenPurchases.AsNoTracking()
            .Where(p => p.Status == TokenPurchaseStatus.Pending
                     && (p.PaymentMethod == TokenPaymentMethod.CreditCard || p.PaymentMethod == TokenPaymentMethod.EWallet)
                     && p.CreationDate <= cutoff)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var moved = 0;
        foreach (var id in ids)
        {
            var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (purchase is null || purchase.Status != TokenPurchaseStatus.Pending) continue;

            purchase.MarkForReconciliation("STALE_PENDING",
                $"Pending for more than {minutes} minutes: the process may have stopped after charging. Check the gateway before closing it.",
                _clock.Now, actor);
            try
            {
                await _db.SaveChangesAsync(ct);
                moved++;
                _logger.LogWarning("Compra de tokens {PurchaseId} ({Method}) llevaba más de {Minutes} min en Pending: pasa a conciliar.",
                    purchase.Id, purchase.PaymentMethod, minutes);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
            }
        }

        return moved;
    }

    /// <summary>
    /// Cierra sin cobro una compra en cripto: la transición de la compra, el pedido cancelado y la
    /// confirmación en Cancelled (sale de la lista del panel y ya no se puede confirmar), en un único
    /// SaveChanges.
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>> CloseUnpaidCryptoAsync(
        TokenPurchase purchase, Action<TokenPurchase> transition, string note, string actor, CancellationToken ct)
    {
        if (purchase.Status != TokenPurchaseStatus.AwaitingCryptoConfirmation)
            return Fail(TokenPurchaseErrors.NotAwaitingCrypto, "This purchase is not a crypto payment awaiting confirmation.");

        var order        = await _db.Orders.FirstAsync(o => o.Id == purchase.OrderId, ct);
        var confirmation = await _db.CryptoPaymentConfirmations.FirstOrDefaultAsync(c => c.OrderId == purchase.OrderId, ct);
        var now          = _clock.Now;

        transition(purchase);
        Cancel(order, actor, now);
        if (confirmation is not null)
        {
            confirmation.Status         = CryptoPaymentConfirmationStatus.Cancelled;
            confirmation.Notes          = Truncate(note);
            confirmation.LastUpdateDate = now;
            confirmation.LastUpdateBy   = actor;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Fail(TokenPurchaseErrors.NotAwaitingCrypto, "This crypto payment was closed by someone else.");
        }

        return Done(purchase, order, Array.Empty<string>(), await _quota.UsedAsync(purchase.MemberId, now, ct));
    }

    // ── Piezas ────────────────────────────────────────────────────────────────

    private const string EWalletGatewayName = "eWallet";

    /// <summary>
    /// Pedido en Processing y compra con su reserva. SIN OrderDetails: los puntos de rango se suman de
    /// los OrderDetails de pedidos Completed y comprar no da puntos (llegan al canjear el token).
    /// </summary>
    private (Orders Order, TokenPurchase Purchase) Start(Draft d, TokenPurchaseStatus status, string? creditCardId, DateTime now)
    {
        var order = new Orders
        {
            MemberId = d.MemberId, OrderNo = OrderNumberHelper.Generate("Token Purchase", now),
            TotalAmount = d.Total, Status = OrderStatus.Processing, OrderDate = now,
            Notes = $"Token purchase: {d.Quantity} x {d.Quote.Name}",
            CreationDate = now, CreatedBy = d.Actor, LastUpdateDate = now, LastUpdateBy = d.Actor
        };

        var purchase = new TokenPurchase
        {
            MemberId = d.MemberId, TokenTypeId = d.Quote.TokenTypeId,
            GrantedProductIds = string.Join(",", d.Quote.ProductIds),
            Quantity = d.Quantity, UnitPrice = d.Quote.UnitPrice!.Value, TotalAmount = d.Total,
            PaymentMethod = d.Method, Status = status, OrderId = order.Id, IdempotencyKey = d.IdempotencyKey,
            PeriodKey = TokenPurchaseRules.PeriodKey(now), CreditCardId = creditCardId,
            CreatedBy = d.Actor, CreationDate = now, LastUpdateDate = now
        };

        _db.Orders.Add(order);
        _db.TokenPurchases.Add(purchase);
        return (order, purchase);
    }

    /// <summary>
    /// Guarda la reserva. Si choca con el índice único (MemberId, IdempotencyKey) es que otra petición
    /// con la misma clave ganó la carrera: se devuelve lo que hizo esa (o 409 si la clave se usó para
    /// otra compra) en vez de un 500. Null = guardada.
    /// </summary>
    /// <remarks>
    /// Con el candado real las dos peticiones se serializan y la segunda ya la ve en ReplayAsync; esto
    /// cubre el resto (otra instancia sin candado compartido, lecturas con instantánea).
    /// </remarks>
    private async Task<Result<TokenPurchaseResultDto>?> SaveReservationAsync(IDbContextTransaction tx, Draft d, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();

            var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, d.Intent, ct);
            if (replay is not null)
            {
                _logger.LogInformation("Compra de tokens con clave repetida de {MemberId}: la ganó otra petición.", d.MemberId);
                return replay;
            }
            throw;
        }
    }

    private PaymentHistory AddPaymentHistory(Orders order, decimal amount, string gateway, string? gatewayTxId, string actor, DateTime now)
    {
        var payment = new PaymentHistory
        {
            OrderId = order.Id, MemberId = order.MemberId, Amount = amount, GatewayName = gateway,
            GatewayTransactionId = gatewayTxId, TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt = now, CreationDate = now, CreatedBy = actor, LastUpdateDate = now, LastUpdateBy = actor
        };
        _db.PaymentHistories.Add(payment);
        return payment;
    }

    /// <summary>
    /// El nombre de gateway de un PaymentHistory que crea la conciliación. eWallet como en la fase 2.
    /// La tarjeta escribe el procesador (NmiSpreedly…) desde el orquestador; si se llega aquí sin
    /// PaymentHistory es que el orquestador no llegó a escribirlo y no consta cuál cobró (está en las
    /// notas de la compra): queda el método.
    /// </summary>
    private static string GatewayNameOf(TokenPurchase purchase) =>
        purchase.PaymentMethod == TokenPaymentMethod.EWallet ? EWalletGatewayName : purchase.PaymentMethod.ToString();

    private static void MarkPaid(Orders order, string actor, DateTime now)
    {
        order.Status = OrderStatus.Paid;
        order.LastUpdateDate = now;
        order.LastUpdateBy = actor;
    }

    private static void Cancel(Orders order, string actor, DateTime now)
    {
        order.Status = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy = actor;
    }

    private async Task<Result<TokenPurchaseResultDto>> RollbackAsync(IDbContextTransaction tx, string code, string message)
    {
        await tx.RollbackAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();
        return Fail(code, message);
    }

    /// <summary>
    /// El saldo de i-Payout sin que su texto llegue al miembro. Un fallo o una excepción (red, timeout)
    /// se registran aquí, con el usuario enmascarado, y vuelven como EWALLET_UNAVAILABLE; quien llama
    /// responde con un mensaje fijo.
    /// </summary>
    private async Task<Result<decimal>> SafeBalanceAsync(string memberId, string userName, CancellationToken ct)
    {
        try
        {
            var balance = await _eWallet.BalanceAsync(userName, ct);
            if (balance.IsSuccess) return balance;

            _logger.LogWarning("Saldo eWallet de {MemberId} ({User}) no disponible: {Code} {Error}",
                memberId, EWalletClient.MaskUserName(userName), balance.ErrorCode, balance.Error);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Saldo eWallet de {MemberId} ({User}): i-Payout no respondió.",
                memberId, EWalletClient.MaskUserName(userName));
        }

        return Result<decimal>.Failure(TokenPurchaseErrors.EWalletUnavailable, EWalletUnavailableMessage);
    }

    /// <summary>
    /// El cobro pudo hacerse o no: la compra queda a conciliar con el cupo reservado y el pedido en
    /// Processing. La clave devuelve PURCHASE_NEEDS_REVIEW, así que no hay reintento que cobre dos veces.
    /// Si ni eso se puede guardar, <see cref="RescueToReconciliationAsync"/>.
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>> ToReconciliationAsync(
        TokenPurchase purchase, string code, string? detail, string actor, string message)
    {
        try
        {
            purchase.MarkForReconciliation(code, Truncate(detail), _clock.Now, actor);
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            return await RescueToReconciliationAsync(purchase.Id, code, detail, actor, ex, message);
        }

        // El pedido se queda en Processing hasta que tesorería decida.
        return Fail(TokenPurchaseErrors.NeedsReview, message);
    }

    /// <summary>
    /// Último recurso cuando no se pudo guardar el resultado de un cobro (hecho o dudoso): se suelta la
    /// transacción que hubiera, se olvida lo que el contexto tenía a medias, se relee la compra y se
    /// deja a conciliar con CancellationToken.None. Si tampoco se puede, queda en el log como Critical
    /// con su id y la recoge el barrido de Pending (TokenPurchaseStalePendingJob). Nunca libera.
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>> RescueToReconciliationAsync(
        string purchaseId, string code, string? detail, string actor, Exception cause, string message)
    {
        _logger.LogError(cause, "Compra de tokens {PurchaseId}: no se pudo guardar el resultado del cobro ({Code}); se deja a conciliar.",
            purchaseId, code);
        try
        {
            await ReleaseOpenTransactionAsync();
            _db.ChangeTracker.Clear();

            var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId, CancellationToken.None);
            if (purchase is null)
            {
                _logger.LogCritical("Compra de tokens {PurchaseId}: no aparece al releerla tras un cobro ({Code}). Revisar a mano.", purchaseId, code);
            }
            else if (purchase.Status == TokenPurchaseStatus.Pending)
            {
                purchase.MarkForReconciliation(code, Truncate(detail), _clock.Now, actor);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            else
            {
                // Ya la cerró otro (el barrido, o el commit sí llegó a la base): no se toca.
                _logger.LogWarning("Compra de tokens {PurchaseId}: al rescatarla ya estaba en {Status}.", purchaseId, purchase.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "Compra de tokens {PurchaseId} quedó en Pending con un cobro posiblemente hecho ({Code}) y no se pudo pasar a conciliar. " +
                "La recogerá el barrido de Pending ({ConfigKey}); no liberar sin mirar el gateway.",
                purchaseId, code, TokenPurchaseRules.StalePendingMinutesConfigKey);
        }

        return Fail(TokenPurchaseErrors.NeedsReview, message);
    }

    /// <summary>Deshace y suelta la transacción abierta en el contexto, si la hay, sin lanzar.</summary>
    private async Task ReleaseOpenTransactionAsync()
    {
        if (_db.Database.CurrentTransaction is not { } open) return;
        try
        {
            await open.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Si la conexión cayó, deshacer también falla y SQL Server la descarta solo; lo que importa es
            // soltarla del contexto para que lo siguiente no se guarde dentro de ella.
            _logger.LogDebug(ex, "No se pudo deshacer la transacción abierta; se descarta.");
        }
        finally
        {
            await open.DisposeAsync();
        }
    }

    private static bool TryParseMethod(string? value, out TokenPaymentMethod method)
    {
        method = default;
        var name = Enum.GetNames<TokenPaymentMethod>()
            .FirstOrDefault(n => string.Equals(n, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is not null && Enum.TryParse(name, out method);
    }

    /// <summary>
    /// Suelta una reserva cuando consta que NO se movió dinero. Si el guardado falla, la compra se queda
    /// en Pending y el barrido la pasa a conciliar (lado seguro: tesorería la cierra como no cobrada).
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>> AbandonAsync(
        Orders order, TokenPurchase purchase, string code, string actor, string message)
    {
        try
        {
            var now = _clock.Now;
            purchase.Fail(code, now, actor);
            Cancel(order, actor, now);
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compra de tokens {PurchaseId}: no se pudo soltar la reserva ({Code}); queda para el barrido de Pending.",
                purchase.Id, code);
        }
        return Fail(code, message);
    }

    /// <summary>
    /// Confirmar y conciliar emiten códigos: si dos procesos acuñan el mismo a la vez, el índice único
    /// de ReferenceId rechaza el guardado. Como no se guardó nada, se repite una vez desde cero (con
    /// códigos nuevos); si vuelve a fallar, un error claro y sin efectos que se puede reintentar.
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>> WithOneRetryOnCollisionAsync(
        Func<Task<Result<TokenPurchaseResultDto>>> operation, string what)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                if (attempt >= 2)
                {
                    _logger.LogError(ex, "No se pudo {What}: el guardado falló dos veces.", what);
                    return Fail(TokenPurchaseErrors.IssueFailed, "The tokens could not be issued. Nothing was saved; try again.");
                }
                _logger.LogWarning(ex, "Al {What} el guardado chocó con otro; se repite una vez.", what);
            }
        }
    }

    /// <summary>
    /// La misma clave del mismo miembro devuelve lo que ya pasó: Completed o cripto → la compra (con
    /// códigos o con la dirección); Pending → en curso; NeedsReconciliation → en revisión; Failed → el
    /// mismo fallo (la pantalla genera otra clave). Con otro tipo, cantidad o método no es un reintento
    /// sino un error de quien llama: IDEMPOTENCY_KEY_REUSED, sin devolver la compra de antes.
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>?> ReplayAsync(string memberId, string key, Intent intent, CancellationToken ct)
    {
        var existing = await _db.TokenPurchases.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.IdempotencyKey == key, ct);
        if (existing is null) return null;

        if (existing.TokenTypeId != intent.TokenTypeId || existing.Quantity != intent.Quantity
            || existing.PaymentMethod != intent.Method)
            return Fail(TokenPurchaseErrors.IdempotencyKeyReused,
                "This idempotency key was already used for a different purchase. Start a new one.");

        switch (existing.Status)
        {
            case TokenPurchaseStatus.Pending:
                return Fail(TokenPurchaseErrors.InProgress, "This purchase is still being processed.");
            case TokenPurchaseStatus.NeedsReconciliation:
                return Fail(TokenPurchaseErrors.NeedsReview, "This purchase is being reviewed; do not retry.");
            case TokenPurchaseStatus.Failed:
                return Fail(existing.FailureCode ?? TokenPurchaseErrors.CardChargeFailed, "This purchase already failed. Start a new one.");
            case TokenPurchaseStatus.Cancelled:
            case TokenPurchaseStatus.Rejected:
            case TokenPurchaseStatus.Expired:
                return Fail(existing.FailureCode ?? TokenPurchaseErrors.NotAwaitingCrypto,
                    "This crypto purchase was closed without payment. Start a new one.");
        }

        var order = await _db.Orders.AsNoTracking().FirstAsync(o => o.Id == existing.OrderId, ct);
        var codes = await _db.TokenTransactions.AsNoTracking()
            .Where(t => t.TokenPurchaseId == existing.Id && t.ReferenceId != null)
            .OrderBy(t => t.Id).Select(t => t.ReferenceId!).ToListAsync(ct);
        var used = await _quota.UsedAsync(memberId, _clock.Now, ct);

        CryptoDepositAddressDto? deposit = null;
        if (existing.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation)
        {
            var currency = await _db.CryptoPaymentConfirmations.AsNoTracking()
                .Where(c => c.OrderId == existing.OrderId).Select(c => c.CryptoCurrency).FirstOrDefaultAsync(ct);
            deposit = (await _cryptoAddresses.GetConfiguredAsync(ct)).FirstOrDefault(c => c.Currency == currency)
                      ?? (currency is null ? null : new CryptoDepositAddressDto { Currency = currency });
        }

        return Done(existing, order, codes, used, deposit);
    }

    private async Task AfterIssueAsync(TokenPurchase purchase, Orders order, string tokenTypeName)
    {
        try
        {
            await _cache.RemoveAsync(CacheKeys.MemberTokenBalances(purchase.MemberId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo invalidar la caché de saldos de tokens de {MemberId}.", purchase.MemberId);
        }

        try
        {
            _ = _push.SendAsync(purchase.MemberId, NotificationEvents.TokenReceived,
                "Tokens purchased", $"{purchase.Quantity} token(s) were added to your account.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo avisar por push la compra de tokens {PurchaseId}.", purchase.Id);
        }

        // El recibo ya no lanza, pero esto corre DESPUÉS de cobrar y emitir: un fallo aquí no puede
        // convertir una compra hecha en un error que invite a pagar otra vez.
        try
        {
            await _receipts.SendAsync(purchase, order.OrderNo, tokenTypeName, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el recibo de la compra de tokens {PurchaseId}.", purchase.Id);
        }
    }

    private async Task<string> TokenTypeNameAsync(int tokenTypeId, CancellationToken ct) =>
        await _db.TokenTypes.AsNoTracking().Where(t => t.Id == tokenTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct)
        ?? $"Token type {tokenTypeId}";

    private static string? Truncate(string? s) => s is null || s.Length <= 500 ? s : s[..500];

    private static Result<TokenPurchaseResultDto> LimitReached(int used) =>
        Fail(TokenPurchaseErrors.MonthlyLimitReached,
            $"The monthly limit is {TokenPurchaseRules.MonthlyLimit} tokens; {Math.Max(0, TokenPurchaseRules.MonthlyLimit - used)} left this month.");

    private static Result<TokenPurchaseResultDto> Fail(string code, string message) =>
        Result<TokenPurchaseResultDto>.Failure(code, message);

    private static Result<TokenPurchaseResultDto> Done(
        TokenPurchase p, Orders o, IReadOnlyList<string> codes, int usedAfter, CryptoDepositAddressDto? deposit = null) =>
        Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto
        {
            PurchaseId = p.Id, OrderId = o.Id, OrderNo = o.OrderNo,
            Status = p.Status switch
            {
                TokenPurchaseStatus.Completed                  => TokenPurchaseStatuses.Completed,
                TokenPurchaseStatus.AwaitingCryptoConfirmation => TokenPurchaseStatuses.AwaitingCryptoConfirmation,
                TokenPurchaseStatus.NeedsReconciliation        => TokenPurchaseStatuses.NeedsReconciliation,
                TokenPurchaseStatus.Failed                     => TokenPurchaseStatuses.Failed,
                TokenPurchaseStatus.Cancelled                  => TokenPurchaseStatuses.Cancelled,
                TokenPurchaseStatus.Rejected                   => TokenPurchaseStatuses.Rejected,
                TokenPurchaseStatus.Expired                    => TokenPurchaseStatuses.Expired,
                _                                              => TokenPurchaseStatuses.Pending
            },
            TokenTypeId = p.TokenTypeId, Quantity = p.Quantity, UnitPrice = p.UnitPrice, TotalAmount = p.TotalAmount,
            PaymentMethod = p.PaymentMethod.ToString(), TokenCodes = codes.ToList(),
            MonthlyRemaining = Math.Max(0, TokenPurchaseRules.MonthlyLimit - usedAfter),
            CryptoCurrency = deposit?.Currency, CryptoDepositAddress = deposit?.Address, CryptoNetwork = deposit?.Network
        });
}
