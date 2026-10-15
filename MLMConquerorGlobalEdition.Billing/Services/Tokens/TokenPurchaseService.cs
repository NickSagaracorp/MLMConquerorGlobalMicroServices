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
}

/// <summary>
/// La compra de tokens. Cuatro caminos según dónde está el dinero:
///
///   • COMISIONES: todo en UNA transacción bajo el candado del miembro. El saldo se comprueba antes de
///     escribir; si algo falla a mitad, se deshace entero.
///   • EWALLET (i-Payout): (1) reservar cupo y confirmar; (2) con el candado otra vez tomado, releer el
///     saldo, debitar, verificar por saldo (como el legado) y emitir o dejar a conciliar. El candado en
///     la fase 2 es lo que hace fiable la verificación por saldo.
///   • TARJETA: (1) reservar; (2) cobrar FUERA de la transacción; (3) emitir en un único SaveChanges con
///     CancellationToken.None, porque el dinero ya se movió.
///   • CRIPTO: reservar cupo, dejar el pedido en Processing con su CryptoPaymentConfirmation y devolver
///     dirección, moneda e importe. Emite <see cref="ConfirmCryptoAsync"/>.
///
/// Lo dudoso nunca se reintenta: queda NeedsReconciliation y lo cierra <see cref="ReconcileAsync"/>.
/// Una cripto sin pagar sale por <see cref="CancelCryptoAsync"/>, <see cref="RejectCryptoAsync"/> o
/// <see cref="ExpireStaleCryptoAsync"/>, y las tres liberan el cupo.
/// No reembolsable.
/// </summary>
public sealed class TokenPurchaseService : ITokenPurchaseService
{
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

    public TokenPurchaseService(
        AppDbContext db, IDateTimeProvider clock, ITokenPricingService pricing, ITokenMonthlyQuota quota,
        IMemberFundsGate gate, ITokenIssuer issuer, ICommissionBalanceService commissions, IEWalletFunds eWallet,
        IGatewayRouter router, IGatewayChargeOrchestrator orchestrator, ICardBrandDetector brands,
        ICryptoDepositAddressReader cryptoAddresses, ITokenPurchaseReceipts receipts,
        ICacheService cache, IPushNotificationService push, ILogger<TokenPurchaseService> logger)
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
    }

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
            Items = items.Select(q => new TokenCatalogItemDto
            {
                TokenTypeId = q.TokenTypeId, Name = q.Name, Description = q.Description, IsGuestPass = q.IsGuestPass,
                UnitPrice = q.UnitPrice, Purchasable = q.Purchasable, NotPurchasableReason = q.NotPurchasableReason
            }).ToList()
        };
    }

    public async Task<TokenPurchaseFundsDto> GetFundsAsync(string memberId, CancellationToken ct = default)
    {
        var funds = new TokenPurchaseFundsDto { CommissionBalance = await _commissions.GetSpendableAsync(memberId, ct) };

        var userName = await _eWallet.UserNameOfAsync(memberId, ct);
        if (userName is null) return funds;

        funds.HasEWallet = true;
        var balance = await _eWallet.BalanceAsync(userName, ct);
        funds.EWalletBalance = balance.IsSuccess ? balance.Value : null;
        return funds;
    }

    // ── Compra ────────────────────────────────────────────────────────────────

    private sealed record Draft(
        string MemberId, string MemberEmail, string? Country, string Actor,
        TokenQuote Quote, int Quantity, TokenPaymentMethod Method, string IdempotencyKey)
    {
        public decimal Total => Quote.UnitPrice!.Value * Quantity;
    }

    public async Task<Result<TokenPurchaseResultDto>> PurchaseAsync(TokenPurchaseInput input, CancellationToken ct = default)
    {
        var req = input.Request;

        // Solo por nombre: Enum.TryParse también acepta "1", y la ruta con step-up se elige mirando el
        // nombre (TokenPaymentMethods.IsBalance), así que un número pagaría con saldo sin step-up.
        if (!TryParseMethod(req.PaymentMethod, out var method))
            return Fail(TokenPurchaseErrors.PaymentMethodNotSupported, "Payment method not supported for tokens.");
        if (req.Quantity < 1 || req.Quantity > TokenPurchaseRules.MonthlyLimit)
            return Fail(TokenPurchaseErrors.InvalidQuantity, $"Quantity must be between 1 and {TokenPurchaseRules.MonthlyLimit}.");
        if (!TokenPurchaseRules.IsValidIdempotencyKey(req.IdempotencyKey))
            return Fail(TokenPurchaseErrors.IdempotencyKeyRequired, "A valid idempotency key is required.");

        var replay = await ReplayAsync(input.MemberId, req.IdempotencyKey, ct);
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
            return Fail(TokenPurchaseErrors.Busy, "Another purchase for this account is in progress. Try again in a moment.");
        }
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithCommissionsAsync(Draft d, CancellationToken ct)
    {
        var now = _clock.Now;
        await using var tx = await _gate.EnterAsync(d.MemberId, ct);

        var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
        if (replay is not null) return replay;

        var used = await _quota.UsedAsync(d.MemberId, now, ct);
        if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

        var spendable = await _commissions.GetSpendableAsync(d.MemberId, ct);
        if (spendable < d.Total)
            return Fail(TokenPurchaseErrors.InsufficientCommissionBalance, $"Commission balance {spendable:F2} is below {d.Total:F2}.");

        var (order, purchase) = Start(d, TokenPurchaseStatus.Pending, creditCardId: null, now);
        await _db.SaveChangesAsync(ct);

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

        // Comprobación temprana para responder rápido; la que cuenta se repite bajo el candado.
        var early = await _eWallet.BalanceAsync(userName, ct);
        if (!early.IsSuccess) return Fail(TokenPurchaseErrors.EWalletUnavailable, early.Error ?? "eWallet unavailable.");
        if (early.Value < EWalletFunds.ChargeAmount(d.Total))
            return Fail(TokenPurchaseErrors.InsufficientEWalletBalance, $"eWallet balance {early.Value:F2} is below {d.Total:F2}.");

        var now = _clock.Now;
        Orders order;
        TokenPurchase purchase;
        int usedAfter;

        // Fase 1 — reservar cupo bajo el candado y dejarlo escrito antes de tocar i-Payout.
        await using (var tx = await _gate.EnterAsync(d.MemberId, ct))
        {
            var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
            if (replay is not null) return replay;

            var used = await _quota.UsedAsync(d.MemberId, now, ct);
            if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

            (order, purchase) = Start(d, TokenPurchaseStatus.Pending, creditCardId: null, now);
            await _db.SaveChangesAsync(ct);
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
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.Busy, d.Actor,
                "Another purchase for this account is in progress. Try again in a moment.");
        }

        await using (tx2)
        {
            var before = await _eWallet.BalanceAsync(userName, CancellationToken.None);
            if (!before.IsSuccess || before.Value < EWalletFunds.ChargeAmount(d.Total))
            {
                await tx2.RollbackAsync(CancellationToken.None);
                return await AbandonAsync(order, purchase,
                    before.IsSuccess ? TokenPurchaseErrors.InsufficientEWalletBalance : TokenPurchaseErrors.EWalletUnavailable,
                    d.Actor, before.IsSuccess ? "Insufficient eWallet balance." : (before.Error ?? "eWallet unavailable."));
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
            var at = _clock.Now;

            switch (outcome.Status)
            {
                case EWalletDebitStatus.Rejected:
                    purchase.Fail(TokenPurchaseErrors.EWalletRejected, at, d.Actor);
                    purchase.Notes = Truncate($"{outcome.Code}: {outcome.Detail}");
                    Cancel(order, d.Actor, at);
                    await _db.SaveChangesAsync(CancellationToken.None);
                    await tx2.CommitAsync(CancellationToken.None);
                    return Fail(TokenPurchaseErrors.EWalletRejected, "i-Payout rejected the charge.");

                case EWalletDebitStatus.NeedsReconciliation:
                    purchase.MarkForReconciliation(outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN", Truncate(outcome.Detail), at, d.Actor);
                    await _db.SaveChangesAsync(CancellationToken.None);
                    await tx2.CommitAsync(CancellationToken.None);
                    _logger.LogWarning("Compra de tokens {PurchaseId} a conciliar: {Code} {Detail}", purchase.Id, outcome.Code, outcome.Detail);
                    return Fail(TokenPurchaseErrors.NeedsReview, "We couldn't confirm the eWallet charge. It will be reviewed; do not retry.");
            }

            // Lo cobrado es el importe a centavos que mandó EWalletFunds, no los 4 decimales del precio.
            var payment = AddPaymentHistory(order, EWalletFunds.ChargeAmount(d.Total), "eWallet", purchase.Id, d.Actor, at);
            var codes = await _issuer.IssueAsync(
                new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, at),
                CancellationToken.None);
            purchase.Complete(payment.Id, at, d.Actor);
            MarkPaid(order, d.Actor, at);
            await _db.SaveChangesAsync(CancellationToken.None);
            await tx2.CommitAsync(CancellationToken.None);

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
            var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
            if (replay is not null) return replay;

            var used = await _quota.UsedAsync(d.MemberId, now, ct);
            if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

            (order, purchase) = Start(d, TokenPurchaseStatus.Pending, card.Id, now);
            await _db.SaveChangesAsync(ct);
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

        var plan = await _router.ResolveAsync(routingCtx, ct);
        if (!plan.IsSuccess)
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.NoImmediateGateway, d.Actor, "No card gateway is available right now.");

        // Solo pasos inmediatos: uno con espera lo cobraría DelayedFallbackChargeJob, que no emite tokens.
        var immediate = new GatewayRoutingPlan
        {
            RouteBucketKey = plan.Value!.RouteBucketKey,
            Steps          = plan.Value.Steps.Where(s => s.DelayMinutes == 0).ToList()
        };
        if (immediate.Steps.Count == 0)
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.NoImmediateGateway, d.Actor, "No card gateway is available right now.");

        // Sin cancelación desde aquí: el orquestador cobra y DESPUÉS guarda el PaymentHistory; cortarlo
        // entre medias dejaría un cobro sin rastro.
        Result<OrchestratorChargeResult> charge;
        try
        {
            charge = await _orchestrator.ExecuteAsync(immediate, routingCtx, new OrchestratorChargeRequest
            {
                MemberId = d.MemberId, TokenizedCardRef = cardRef, Description = $"Token purchase {order.OrderNo}",
                OrderId = order.Id, IsRecurring = false
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Timeout o caída a mitad del cobro: no se sabe si el emisor cargó. Ni tokens ni liberar el
            // cupo: tesorería lo mira en el gateway y lo cierra con ReconcileAsync.
            _logger.LogError(ex, "Compra de tokens {PurchaseId}: el cobro con tarjeta terminó sin resultado.", purchase.Id);
            return await ToReconciliationAsync(order, purchase, "CARD_OUTCOME_UNKNOWN", ex.GetType().Name, d.Actor);
        }

        if (charge.IsSuccess && charge.Value!.Status != "Success")
        {
            // Solo pasan pasos inmediatos, así que "Scheduled" no debería llegar; si llega, hay un cobro
            // programado que emitiría sin tokens o cobraría más tarde: a revisión, no a liberar.
            _logger.LogError("Compra de tokens {PurchaseId}: el orquestador devolvió {Status} con solo pasos inmediatos.",
                purchase.Id, charge.Value.Status);
            return await ToReconciliationAsync(order, purchase, "CARD_CHARGE_" + charge.Value.Status.ToUpperInvariant(),
                charge.Value.ScheduledJobId, d.Actor);
        }

        if (!charge.IsSuccess)
        {
            _logger.LogWarning("Compra de tokens {PurchaseId}: la tarjeta no se cobró ({Code}) {Error}", purchase.Id, charge.ErrorCode, charge.Error);
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.CardChargeFailed, d.Actor, "The card payment was declined.");
        }

        // Fase 3 — emitir en un único SaveChanges, sin cancelación ni candado.
        var issuedAt = _clock.Now;
        var codes = await _issuer.IssueAsync(
            new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, issuedAt),
            CancellationToken.None);
        purchase.Complete(charge.Value.PaymentHistoryId, issuedAt, d.Actor);
        MarkPaid(order, d.Actor, issuedAt);
        await _db.SaveChangesAsync(CancellationToken.None);

        await AfterIssueAsync(purchase, order, d.Quote.Name);
        return Done(purchase, order, codes, usedAfter);
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

        var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
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

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Done(purchase, order, Array.Empty<string>(), used + d.Quantity, deposit);
    }

    // ── Confirmación de cripto ────────────────────────────────────────────────

    /// <remarks>
    /// Mismas capas que ConfirmCryptoPaymentHandler (altas): máquina de estados, RowVersion de la
    /// confirmación en el mismo SaveChanges que los tokens y unicidad de OrderId. Solo filas de compra.
    /// NO vuelve a comprobar el tope (D3): el cupo se comprobó al reservar, en el mes de su PeriodKey, y
    /// los tokens que emite cuentan en ese mes aunque se confirme en el siguiente.
    /// </remarks>
    public async Task<Result<TokenPurchaseResultDto>> ConfirmCryptoAsync(
        string orderId, ConfirmCryptoTokenPurchaseRequest request, string confirmedByUserId, string confirmedByEmail,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CryptoTransactionId))
            return Fail(TokenPurchaseErrors.CryptoTransactionIdRequired, "The transaction id is required.");

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
    /// Tesorería mira i-Payout y dice si el dinero salió. Sí → se emiten los tokens; no → falla y libera
    /// el cupo. RowVersion de TokenPurchase impide cerrar la misma compra dos veces.
    /// </summary>
    public async Task<Result<TokenPurchaseResultDto>> ReconcileAsync(
        string purchaseId, ReconcileTokenPurchaseRequest request, string actor, CancellationToken ct = default)
    {
        var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId, ct);
        if (purchase is null) return Fail(TokenPurchaseErrors.PurchaseNotFound, "Token purchase not found.");
        if (purchase.Status != TokenPurchaseStatus.NeedsReconciliation)
            return Fail(TokenPurchaseErrors.NotUnderReview, "This purchase is not waiting for reconciliation.");

        var order = await _db.Orders.FirstAsync(o => o.Id == purchase.OrderId, ct);
        var now   = _clock.Now;
        var note  = Truncate($"{purchase.Notes} | Reconciled by {actor}: {(request.Charged ? "charged" : "not charged")}. {request.Notes}".Trim());
        IReadOnlyList<string> codes = Array.Empty<string>();

        if (request.Charged)
        {
            var payment = AddPaymentHistory(order, purchase.TotalAmount, purchase.PaymentMethod.ToString(),
                                            string.IsNullOrWhiteSpace(request.GatewayReference) ? purchase.Id : request.GatewayReference.Trim(),
                                            actor, now);
            codes = await _issuer.IssueAsync(
                new TokenIssue(purchase.MemberId, purchase.TokenTypeId, purchase.Quantity, purchase.Id, order.OrderNo, actor, now), ct);
            purchase.Complete(payment.Id, now, actor);
            MarkPaid(order, actor, now);
        }
        else
        {
            purchase.Fail("NOT_CHARGED", now, actor);
            Cancel(order, actor, now);
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
    /// El cobro pudo hacerse o no: la compra queda a conciliar con el cupo reservado y el pedido en
    /// Processing. La clave devuelve PURCHASE_NEEDS_REVIEW, así que no hay reintento que cobre dos veces.
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>> ToReconciliationAsync(
        Orders order, TokenPurchase purchase, string code, string? detail, string actor)
    {
        purchase.MarkForReconciliation(code, Truncate(detail), _clock.Now, actor);
        await _db.SaveChangesAsync(CancellationToken.None);
        return Fail(TokenPurchaseErrors.NeedsReview, "We couldn't confirm the card charge. It will be reviewed; do not retry.");
    }

    private static bool TryParseMethod(string? value, out TokenPaymentMethod method)
    {
        method = default;
        var name = Enum.GetNames<TokenPaymentMethod>()
            .FirstOrDefault(n => string.Equals(n, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is not null && Enum.TryParse(name, out method);
    }

    /// <summary>Suelta una reserva cuando consta que NO se movió dinero.</summary>
    private async Task<Result<TokenPurchaseResultDto>> AbandonAsync(
        Orders order, TokenPurchase purchase, string code, string actor, string message)
    {
        var now = _clock.Now;
        purchase.Fail(code, now, actor);
        Cancel(order, actor, now);
        await _db.SaveChangesAsync(CancellationToken.None);
        return Fail(code, message);
    }

    /// <summary>
    /// La misma clave del mismo miembro devuelve lo que ya pasó: Completed o cripto → la compra (con
    /// códigos o con la dirección); Pending → en curso; NeedsReconciliation → en revisión; Failed → el
    /// mismo fallo (la pantalla genera otra clave).
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>?> ReplayAsync(string memberId, string key, CancellationToken ct)
    {
        var existing = await _db.TokenPurchases.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.IdempotencyKey == key, ct);
        if (existing is null) return null;

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
