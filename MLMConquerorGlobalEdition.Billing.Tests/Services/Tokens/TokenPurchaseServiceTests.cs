using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using ICacheService            = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// La compra de punta a punta contra InMemory: cada método cobra y emite o no deja nada; el tope y la
/// clave aguantan; la tarjeta usa solo pasos inmediatos; la eWallet verifica y lo dudoso va a
/// conciliar; la cripto ofrece solo monedas con dirección; confirmar y conciliar emiten una vez.
/// </summary>
public class TokenPurchaseServiceTests
{
    private const string  MemberId  = "AMB-100001";
    private const string  Actor     = "user-001";
    private const int     VipType   = 13;
    private const int     FreeType  = 92;
    private const decimal UnitPrice = 99.99m; // SetupFee 50 + MonthlyFee 49.99
    private static readonly DateTime FixedNow = new(2026, 10, 15, 10, 0, 0);

    private readonly AppDbContext _db = TestDbContextFactory.Create();
    private readonly Mock<IGatewayRouter> _router = new();
    private readonly Mock<IGatewayChargeOrchestrator> _orchestrator = new();
    private readonly Mock<IEWalletFunds> _eWallet = new();
    private readonly Mock<ITokenPurchaseReceipts> _receipts = new();
    private IMemberFundsGate? _gate;

    public TokenPurchaseServiceTests()
    {
        _db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = MemberId, Email = "amb@example.com", FirstName = "Ana", LastName = "Ruiz",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
            EnrollDate = FixedNow.AddYears(-1), Country = "US",
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        _db.Products.Add(new Product { Id = "prod-vip", Name = "VIP", Description = "VIP", ImageUrl = "", SetupFee = 50m, MonthlyFee = 49.99m, IsActive = true, CreatedBy = "seed", CreationDate = FixedNow });
        _db.TokenTypes.AddRange(
            new TokenType { Id = VipType, Name = "Enrollment: VIP Member", IsActive = true, IsPurchasable = true, Category = TokenCategory.Enrollment, CreatedBy = "seed", CreationDate = FixedNow },
            new TokenType { Id = FreeType, Name = "Enrollment: VIP Member FREE", IsActive = true, IsPurchasable = false, Category = TokenCategory.Enrollment, CreatedBy = "seed", CreationDate = FixedNow });
        _db.TokenTypeProducts.AddRange(
            new TokenTypeProduct { TokenTypeId = VipType, ProductId = "prod-vip", Role = TokenProductRole.Granted, CreatedBy = "seed", CreationDate = FixedNow },
            new TokenTypeProduct { TokenTypeId = FreeType, ProductId = "prod-vip", Role = TokenProductRole.Granted, CreatedBy = "seed", CreationDate = FixedNow });
        GlobalParameter P(string k, string v) => new() { Key = k, Value = v, CreatedBy = "seed", CreationDate = FixedNow };
        _db.GlobalParameters.AddRange(
            P(CryptoDepositKeys.AddressKey("BTC"), "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"),
            P(CryptoDepositKeys.AddressKey("USDT"), "TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5"),
            P(CryptoDepositKeys.NetworkKey("USDT"), "TRC-20"));
        _db.SaveChanges();
    }

    private TokenPurchaseService Service()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(FixedNow);
        clock.Setup(c => c.UtcNow).Returns(FixedNow);
        var brands = new Mock<ICardBrandDetector>();
        brands.Setup(b => b.Detect(It.IsAny<string>())).Returns(CardBrand.Visa);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _receipts.Setup(r => r.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        return new TokenPurchaseService(
            _db, clock.Object,
            new TokenPricingService(_db), new TokenMonthlyQuota(_db), _gate ?? new TransactionOnlyGate(_db),
            new TokenIssuer(_db), new CommissionBalanceService(_db, clock.Object), _eWallet.Object,
            _router.Object, _orchestrator.Object, brands.Object, new CryptoDepositAddressReader(_db),
            _receipts.Object, cache.Object, push.Object, NullLogger<TokenPurchaseService>.Instance);
    }

    private static PurchaseTokensRequest Request(
        string method, int qty = 1, string? key = null, string? card = null, string? crypto = null, int type = VipType) => new()
    {
        TokenTypeId = type, Quantity = qty, PaymentMethod = method,
        CreditCardId = card, CryptoCurrency = crypto, IdempotencyKey = key ?? Guid.NewGuid().ToString("N")
    };

    private Task<Result<TokenPurchaseResultDto>> Buy(PurchaseTokensRequest r) =>
        Service().PurchaseAsync(new TokenPurchaseInput(MemberId, Actor, r));

    private void SeedCommissions(decimal amount)
    {
        _db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = MemberId, CommissionTypeId = 1, Amount = amount,
            Status = CommissionEarningStatus.Pending, EarnedDate = FixedNow.AddDays(-10),
            PaymentDate = FixedNow.AddDays(-1), CreatedBy = "seed",
            CreationDate = FixedNow.AddDays(-10), LastUpdateDate = FixedNow.AddDays(-10)
        });
        _db.SaveChanges();
    }

    private void SeedPurchased(int count, DateTime at)
    {
        for (var i = 0; i < count; i++)
            _db.TokenTransactions.Add(new TokenTransaction
            {
                MemberId = MemberId, OriginalOwnerMemberId = MemberId, TokenTypeId = VipType,
                TransactionType = TokenTransactionType.Purchased, Quantity = 1, ReferenceId = $"OLD{i:0000}",
                Status = TokenInstanceStatus.Issued, CreatedBy = "seed", CreationDate = at
            });
        _db.SaveChanges();

        // AuditInterceptor pisa CreationDate con la hora de la máquina al insertar: se vuelve a poner
        // para que la prueba no dependa del día en que se corre.
        foreach (var t in _db.TokenTransactions.Where(t => t.ReferenceId!.StartsWith("OLD")).ToList())
            t.CreationDate = at;
        _db.SaveChanges();
    }

    private void SeedCard(string id, string owner)
    {
        _db.CreditCards.Add(new MemberCreditCard
        {
            Id = id, MemberId = owner, First6 = "411111", Last4 = "1111", CardBrand = "Visa",
            CardToken = "tok_1", SpreedlyPaymentMethodToken = "spr_1", CreatedBy = "seed", CreationDate = FixedNow
        });
        _db.SaveChanges();
    }

    private void RouterWithImmediateAndDelayedSteps() =>
        _router.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<GatewayRoutingPlan>.Success(new GatewayRoutingPlan
               {
                   RouteBucketKey = "US-VISA",
                   Steps = new[]
                   {
                       new GatewayAttemptPlan { CardProcessor = CardProcessor.NmiSpreedly, Amount = UnitPrice, FallbackStepIndex = 0 },
                       new GatewayAttemptPlan { CardProcessor = CardProcessor.NmiDirect, Amount = UnitPrice, FallbackStepIndex = 1, DelayMinutes = 60 }
                   }
               }));

    private void EWalletWith(decimal balance, EWalletDebitStatus outcome, string? code = null)
    {
        _eWallet.Setup(e => e.UserNameOfAsync(MemberId, It.IsAny<CancellationToken>())).ReturnsAsync("ipayout-1");
        _eWallet.Setup(e => e.BalanceAsync("ipayout-1", It.IsAny<CancellationToken>())).ReturnsAsync(Result<decimal>.Success(balance));
        _eWallet.Setup(e => e.DebitAsync("ipayout-1", It.IsAny<decimal>(), balance, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EWalletDebitOutcome(outcome, code, "detalle"));
    }

    // ── Comisiones ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Comisiones_con_saldo_cobra_emite_manda_recibo_y_deja_el_pedido_pagado_sin_detalle()
    {
        SeedCommissions(500m);

        var r = await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 2));

        r.IsSuccess.Should().BeTrue();
        r.Value!.Status.Should().Be(TokenPurchaseStatuses.Completed);
        r.Value.TotalAmount.Should().Be(UnitPrice * 2);
        r.Value.MonthlyRemaining.Should().Be(48);
        var tokens = await _db.TokenTransactions.Where(t => t.ReferenceId != null).ToListAsync();
        tokens.Should().HaveCount(2).And.OnlyContain(t =>
            t.TransactionType == TokenTransactionType.Purchased && t.OriginalOwnerMemberId == MemberId &&
            t.Status == TokenInstanceStatus.Issued && t.TokenPurchaseId == r.Value.PurchaseId);
        tokens.Select(t => t.ReferenceId).Should().BeEquivalentTo(r.Value.TokenCodes);
        (await _db.CommissionEarnings.SingleAsync(e => e.Amount < 0)).Amount.Should().Be(-(UnitPrice * 2));
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        _db.OrderDetails.Should().BeEmpty("comprar no genera puntos: llegan al canjear");
        _receipts.Verify(x => x.SendAsync(It.Is<TokenPurchase>(p => p.Id == r.Value.PurchaseId), It.IsAny<string?>(),
                                          "Enrollment: VIP Member", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Comisiones_sin_saldo_no_escribe_nada()
    {
        SeedCommissions(50m);

        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).ErrorCode
            .Should().Be(TokenPurchaseErrors.InsufficientCommissionBalance);
        _db.Orders.Should().BeEmpty();
        _db.TokenPurchases.Should().BeEmpty();
    }

    // ── Tope, clave y reglas ──────────────────────────────────────────────────

    [Fact]
    public async Task Con_49_comprados_este_mes_no_deja_comprar_2_pero_si_1()
    {
        SeedCommissions(500m);
        SeedPurchased(49, FixedNow.AddDays(-3));

        (await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 2))).ErrorCode.Should().Be(TokenPurchaseErrors.MonthlyLimitReached);
        (await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 1))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Los_comprados_el_mes_pasado_no_cuentan()
    {
        SeedCommissions(500m);
        SeedPurchased(50, FixedNow.AddMonths(-1));

        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_misma_clave_devuelve_la_misma_compra_y_no_cobra_dos_veces()
    {
        SeedCommissions(500m);
        var req = Request(TokenPaymentMethods.CommissionBalance, key: "clave-unica-1");

        var first  = await Buy(req);
        var second = await Buy(req);

        second.Value!.PurchaseId.Should().Be(first.Value!.PurchaseId);
        second.Value.TokenCodes.Should().BeEquivalentTo(first.Value.TokenCodes);
        _db.TokenPurchases.Should().HaveCount(1);
        _db.CommissionEarnings.Count(e => e.Amount < 0).Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tiene espacios")]
    public async Task Sin_clave_valida_no_compra(string key)
        => (await Buy(Request(TokenPaymentMethods.CommissionBalance, key: key))).ErrorCode
            .Should().Be(TokenPurchaseErrors.IdempotencyKeyRequired);

    [Fact]
    public async Task Una_variante_que_no_se_vende_lo_dice()
        => (await Buy(Request(TokenPaymentMethods.CommissionBalance, type: FreeType))).ErrorCode
            .Should().Be(TokenPurchaseErrors.NotForSale);

    [Fact]
    public async Task SafetyPay_no_es_un_metodo()
        => (await Buy(Request("SafetyPay"))).ErrorCode.Should().Be(TokenPurchaseErrors.PaymentMethodNotSupported);

    [Fact]
    public async Task Un_miembro_inactivo_no_compra()
    {
        (await _db.MemberProfiles.SingleAsync()).Status = MemberAccountStatus.Inactive;
        await _db.SaveChangesAsync();

        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).ErrorCode.Should().Be(TokenPurchaseErrors.MemberInactive);
    }

    // ── eWallet ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task eWallet_cobrada_emite_con_la_compra_como_referencia()
    {
        EWalletWith(500m, EWalletDebitStatus.Completed);

        var r = await Buy(Request(TokenPaymentMethods.EWallet));

        r.IsSuccess.Should().BeTrue();
        r.Value!.TokenCodes.Should().HaveCount(1);
        var pago = await _db.PaymentHistories.SingleAsync();
        pago.GatewayName.Should().Be("eWallet");
        pago.GatewayTransactionId.Should().Be(r.Value.PurchaseId);
        _eWallet.Verify(e => e.DebitAsync("ipayout-1", UnitPrice, 500m, r.Value.PurchaseId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task eWallet_dudosa_queda_a_conciliar_sin_tokens_y_la_clave_no_reintenta()
    {
        EWalletWith(500m, EWalletDebitStatus.NeedsReconciliation, "EWALLET_DEBIT_PENDING");
        var req = Request(TokenPaymentMethods.EWallet, key: "clave-ew-1");

        var r = await Buy(req);

        r.ErrorCode.Should().Be(TokenPurchaseErrors.NeedsReview);
        var compra = await _db.TokenPurchases.SingleAsync();
        compra.Status.Should().Be(TokenPurchaseStatus.NeedsReconciliation);
        compra.FailureCode.Should().Be("EWALLET_DEBIT_PENDING");
        _db.TokenTransactions.Should().BeEmpty();
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(1);

        (await Buy(req)).ErrorCode.Should().Be(TokenPurchaseErrors.NeedsReview);
        _eWallet.Verify(e => e.DebitAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task eWallet_rechazada_falla_y_cancela_el_pedido()
    {
        EWalletWith(500m, EWalletDebitStatus.Rejected, "EWALLET_ITEM_-5");

        (await Buy(Request(TokenPaymentMethods.EWallet))).ErrorCode.Should().Be(TokenPurchaseErrors.EWalletRejected);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Failed);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task eWallet_sin_saldo_o_sin_cuenta_no_escribe()
    {
        EWalletWith(10m, EWalletDebitStatus.Completed);
        (await Buy(Request(TokenPaymentMethods.EWallet))).ErrorCode.Should().Be(TokenPurchaseErrors.InsufficientEWalletBalance);

        _eWallet.Setup(e => e.UserNameOfAsync(MemberId, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        (await Buy(Request(TokenPaymentMethods.EWallet))).ErrorCode.Should().Be(TokenPurchaseErrors.EWalletNotFound);

        _db.Orders.Should().BeEmpty();
    }

    // ── Tarjeta ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tarjeta_cobra_solo_los_pasos_inmediatos_con_el_token_de_Spreedly_y_emite()
    {
        SeedCard("card-1", MemberId);
        RouterWithImmediateAndDelayedSteps();
        GatewayRoutingPlan? usado = null;
        OrchestratorChargeRequest? pedido = null;
        _orchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayRoutingPlan, GatewayRoutingContext, OrchestratorChargeRequest, CancellationToken>((p, _, r, _) => { usado = p; pedido = r; })
            .ReturnsAsync(Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
            {
                Status = "Success", PaymentHistoryId = "ph-card", GatewayTransactionId = "tx-1", ProcessorUsed = "NmiSpreedly", AmountCharged = UnitPrice
            }));

        var r = await Buy(Request(TokenPaymentMethods.CreditCard, card: "card-1"));

        r.IsSuccess.Should().BeTrue();
        usado!.Steps.Should().OnlyContain(s => s.DelayMinutes == 0);
        pedido!.TokenizedCardRef.Should().Be("spr_1");
        pedido.IsRecurring.Should().BeFalse();
        (await _db.TokenPurchases.SingleAsync()).PaymentHistoryId.Should().Be("ph-card");
    }

    [Fact]
    public async Task Tarjeta_rechazada_marca_la_compra_fallida_y_libera_el_cupo()
    {
        SeedCard("card-1", MemberId);
        RouterWithImmediateAndDelayedSteps();
        _orchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorChargeResult>.Failure("DECLINED", "Do not honor"));

        (await Buy(Request(TokenPaymentMethods.CreditCard, card: "card-1"))).ErrorCode.Should().Be(TokenPurchaseErrors.CardChargeFailed);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Failed);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);
    }

    [Fact]
    public async Task La_tarjeta_de_otro_no_se_cobra()
    {
        SeedCard("card-otro", "AMB-999999");

        (await Buy(Request(TokenPaymentMethods.CreditCard, card: "card-otro"))).ErrorCode.Should().Be(TokenPurchaseErrors.CreditCardNotFound);
        _orchestrator.VerifyNoOtherCalls();
    }

    // ── Cripto ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cripto_devuelve_la_direccion_reserva_cupo_y_no_emite()
    {
        var r = await Buy(Request(TokenPaymentMethods.Crypto, qty: 3, crypto: "usdt"));

        r.Value!.Status.Should().Be(TokenPurchaseStatuses.AwaitingCryptoConfirmation);
        r.Value.CryptoCurrency.Should().Be("USDT");
        r.Value.CryptoDepositAddress.Should().Be("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5");
        r.Value.CryptoNetwork.Should().Be("TRC-20");
        r.Value.TotalAmount.Should().Be(UnitPrice * 3);
        var conf = await _db.CryptoPaymentConfirmations.SingleAsync();
        conf.Purpose.Should().Be(CryptoPaymentPurpose.TokenPurchase);
        conf.AmountDue.Should().Be(UnitPrice * 3);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(3);
        _db.TokenTransactions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ETH", TokenPurchaseErrors.CryptoAddressNotConfigured)]
    [InlineData("DOGE", TokenPurchaseErrors.CryptoCurrencyNotSupported)]
    public async Task Cripto_sin_direccion_o_no_admitida_no_crea_pedido(string moneda, string codigo)
    {
        (await Buy(Request(TokenPaymentMethods.Crypto, crypto: moneda))).ErrorCode.Should().Be(codigo);
        _db.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task El_catalogo_trae_las_monedas_con_direccion()
        => (await Service().GetCatalogAsync(MemberId)).CryptoDeposits.Select(d => d.Currency).Should().Equal("BTC", "USDT");

    [Fact]
    public async Task Confirmar_cripto_emite_manda_recibo_y_no_se_confirma_dos_veces()
    {
        var orderId = (await Buy(Request(TokenPaymentMethods.Crypto, qty: 2, crypto: "BTC"))).Value!.OrderId;

        var ok = await Service().ConfirmCryptoAsync(orderId,
            new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = " 0xabc ", Notes = "red BTC" }, "admin-1", "admin@x.com");

        ok.Value!.TokenCodes.Should().HaveCount(2);
        var conf = await _db.CryptoPaymentConfirmations.SingleAsync();
        conf.Status.Should().Be(CryptoPaymentConfirmationStatus.Confirmed);
        conf.CryptoTransactionId.Should().Be("0xabc");
        conf.ConfirmedByEmail.Should().Be("admin@x.com");
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        _receipts.Verify(x => x.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        (await Service().ConfirmCryptoAsync(orderId, new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0xabc" }, "admin-1", "admin@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.CryptoAlreadyConfirmed);
        _db.TokenTransactions.Count(t => t.ReferenceId != null).Should().Be(2);
    }

    [Fact]
    public async Task Billing_no_confirma_el_cobro_en_cripto_de_un_alta()
    {
        _db.Orders.Add(new Orders { Id = "ord-signup", MemberId = MemberId, Status = OrderStatus.Processing, OrderDate = FixedNow, CreatedBy = "seed", CreationDate = FixedNow });
        _db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = "ord-signup", MemberId = MemberId, MemberEmail = "amb@example.com", CryptoCurrency = "BTC",
            AmountDue = 80m, Purpose = CryptoPaymentPurpose.Signup, CreatedBy = "seed", CreationDate = FixedNow
        });
        await _db.SaveChangesAsync();

        (await Service().ConfirmCryptoAsync("ord-signup", new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0x1" }, "admin-1", "admin@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.CryptoPaymentNotFound);
    }

    // ── Conciliación ──────────────────────────────────────────────────────────

    private async Task<string> CompraAConciliar()
    {
        EWalletWith(500m, EWalletDebitStatus.NeedsReconciliation, "EWALLET_OUTCOME_UNKNOWN");
        await Buy(Request(TokenPaymentMethods.EWallet, qty: 2));
        return (await _db.TokenPurchases.SingleAsync()).Id;
    }

    [Fact]
    public async Task Conciliar_como_cobrada_emite_y_manda_recibo()
    {
        var id = await CompraAConciliar();

        var r = await Service().ReconcileAsync(id,
            new ReconcileTokenPurchaseRequest { Charged = true, GatewayReference = "ipayout-tx-9", Notes = "visto en i-Payout" }, "tesoreria@x.com");

        r.Value!.TokenCodes.Should().HaveCount(2);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Completed);
        (await _db.PaymentHistories.SingleAsync()).GatewayTransactionId.Should().Be("ipayout-tx-9");
        _receipts.Verify(x => x.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Conciliar_como_no_cobrada_libera_el_cupo_y_no_se_concilia_dos_veces()
    {
        var id = await CompraAConciliar();

        (await Service().ReconcileAsync(id, new ReconcileTokenPurchaseRequest { Charged = false }, "tesoreria@x.com")).IsSuccess.Should().BeTrue();
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Failed);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);

        (await Service().ReconcileAsync(id, new ReconcileTokenPurchaseRequest { Charged = true }, "tesoreria@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.NotUnderReview);
    }

    // ── Cripto sin pagar: cancelar, rechazar, caducar (D18) ────────────────────

    private async Task<TokenPurchaseResultDto> CriptoPendiente(int qty = 3, string? key = null) =>
        (await Buy(Request(TokenPaymentMethods.Crypto, qty: qty, crypto: "USDT", key: key))).Value!;

    [Fact]
    public async Task El_miembro_cancela_su_cripto_sin_pagar_libera_el_cupo_y_ya_no_se_confirma()
    {
        var p = await CriptoPendiente();

        var r = await Service().CancelCryptoAsync(MemberId, p.PurchaseId, Actor);

        r.Value!.Status.Should().Be(TokenPurchaseStatuses.Cancelled);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Cancelled);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await _db.CryptoPaymentConfirmations.SingleAsync()).Status.Should().Be(CryptoPaymentConfirmationStatus.Cancelled);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);

        (await Service().ConfirmCryptoAsync(p.OrderId, new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0x1" }, "admin-1", "admin@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.NotAwaitingCrypto);
        _db.TokenTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Nadie_cancela_la_compra_de_otro_ni_una_que_no_es_cripto_pendiente()
    {
        var p = await CriptoPendiente();
        (await Service().CancelCryptoAsync("AMB-999999", p.PurchaseId, "otro")).ErrorCode.Should().Be(TokenPurchaseErrors.PurchaseNotFound);

        SeedCommissions(500m);
        var pagada = (await Buy(Request(TokenPaymentMethods.CommissionBalance))).Value!;
        (await Service().CancelCryptoAsync(MemberId, pagada.PurchaseId, Actor)).ErrorCode.Should().Be(TokenPurchaseErrors.NotAwaitingCrypto);
    }

    [Fact]
    public async Task El_admin_rechaza_con_motivo_y_la_misma_clave_lo_devuelve()
    {
        var p = await CriptoPendiente(2, key: "clave-cripto-1");

        var r = await Service().RejectCryptoAsync(p.OrderId, new RejectCryptoTokenPurchaseRequest { Reason = "No llegó" }, "admin-1", "admin@x.com");

        r.Value!.Status.Should().Be(TokenPurchaseStatuses.Rejected);
        (await _db.TokenPurchases.SingleAsync()).Notes.Should().Be("No llegó");
        (await _db.CryptoPaymentConfirmations.SingleAsync()).Notes.Should().Contain("No llegó");
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);
        (await Buy(Request(TokenPaymentMethods.Crypto, qty: 2, crypto: "USDT", key: "clave-cripto-1")))
            .ErrorCode.Should().Be(TokenPurchase.RejectedByAdminCode);
    }

    [Fact]
    public async Task Caducan_las_cripto_sin_confirmar_pasado_el_plazo_y_no_las_recientes()
    {
        var vieja    = await CriptoPendiente(2);
        var reciente = await CriptoPendiente(1);
        // AuditInterceptor sella CreationDate al insertar; se reescribe para fijar la antigüedad.
        foreach (var c in await _db.TokenPurchases.ToListAsync())
            c.CreationDate = c.Id == vieja.PurchaseId ? FixedNow.AddDays(-4) : FixedNow.AddHours(-1);
        await _db.SaveChangesAsync();

        var caducadas = await Service().ExpireStaleCryptoAsync(expiryDays: 3, actor: "token-purchase-crypto-expiry");

        caducadas.Should().Be(1);
        (await _db.TokenPurchases.SingleAsync(x => x.Id == vieja.PurchaseId)).Status.Should().Be(TokenPurchaseStatus.Expired);
        (await _db.TokenPurchases.SingleAsync(x => x.Id == reciente.PurchaseId)).Status.Should().Be(TokenPurchaseStatus.AwaitingCryptoConfirmation);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(1);
    }

    [Fact]
    public async Task Confirmar_en_octubre_una_cripto_de_septiembre_no_vuelve_a_mirar_el_tope()
    {
        // Septiembre: 50 en cripto sin pagar. Octubre: el miembro agota su cupo con otros 50.
        var septiembre = await CriptoPendiente(50);
        (await _db.TokenPurchases.SingleAsync()).PeriodKey = 202609;
        await _db.SaveChangesAsync();
        SeedCommissions(10000m);
        (await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 50))).IsSuccess.Should().BeTrue();

        var ok = await Service().ConfirmCryptoAsync(septiembre.OrderId,
            new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0xsep" }, "admin-1", "admin@x.com");

        ok.IsSuccess.Should().BeTrue("el cupo se comprobó al reservar, en septiembre");
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(50, "octubre solo cuenta lo suyo");
    }

    // ── Invariantes de dinero ────────────────────────────────────────────────

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public async Task Un_metodo_en_numero_no_cuela(string metodo)
        // Si "1" valiera como CommissionBalance, entraría por la ruta sin step-up: el BizCenter y
        // Billing deciden si piden el permiso mirando el NOMBRE del método.
        => (await Buy(Request(metodo))).ErrorCode.Should().Be(TokenPurchaseErrors.PaymentMethodNotSupported);

    [Fact]
    public async Task Comisiones_y_eWallet_pasan_por_el_candado_del_miembro()
    {
        var gate = new TransactionOnlyGate(_db);
        _gate = gate;
        SeedCommissions(500m);
        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).IsSuccess.Should().BeTrue();
        gate.Entered.Should().Equal(MemberId);

        gate.Entered.Clear();
        EWalletWith(500m, EWalletDebitStatus.Completed);
        (await Buy(Request(TokenPaymentMethods.EWallet))).IsSuccess.Should().BeTrue();
        gate.Entered.Should().Equal(MemberId, MemberId); // reserva y débito, cada uno con el candado
    }

    [Fact]
    public async Task Con_el_candado_ocupado_no_cobra_ni_escribe()
    {
        var busy = new Mock<IMemberFundsGate>();
        busy.Setup(g => g.EnterAsync(MemberId, It.IsAny<CancellationToken>())).ThrowsAsync(new MemberFundsBusyException(MemberId));
        _gate = busy.Object;
        SeedCommissions(500m);

        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).ErrorCode.Should().Be(TokenPurchaseErrors.Busy);
        _db.TokenPurchases.Should().BeEmpty();
        _db.CommissionEarnings.Count(e => e.Amount < 0).Should().Be(0);
    }

    [Fact]
    public async Task eWallet_que_lanza_al_debitar_queda_a_conciliar_y_no_emite()
    {
        EWalletWith(500m, EWalletDebitStatus.Completed);
        _eWallet.Setup(e => e.DebitAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("socket cerrado"));

        (await Buy(Request(TokenPaymentMethods.EWallet))).ErrorCode.Should().Be(TokenPurchaseErrors.NeedsReview);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.NeedsReconciliation);
        _db.TokenTransactions.Should().BeEmpty();
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(1);
    }

    [Fact]
    public async Task Tarjeta_con_resultado_desconocido_queda_a_conciliar_sin_tokens()
    {
        SeedCard("card-1", MemberId);
        RouterWithImmediateAndDelayedSteps();
        var tokens = new List<CancellationToken>();
        _orchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayRoutingPlan, GatewayRoutingContext, OrchestratorChargeRequest, CancellationToken>((_, _, _, c) => tokens.Add(c))
            .ThrowsAsync(new TaskCanceledException("timeout del gateway"));
        using var cts = new CancellationTokenSource();

        var r = await Service().PurchaseAsync(
            new TokenPurchaseInput(MemberId, Actor, Request(TokenPaymentMethods.CreditCard, card: "card-1")), cts.Token);

        r.ErrorCode.Should().Be(TokenPurchaseErrors.NeedsReview);
        tokens.Should().ContainSingle().Which.CanBeCanceled.Should().BeFalse("desde el cobro, cancelar la petición no puede cortar el registro");
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.NeedsReconciliation);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
        _db.TokenTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Tarjeta_programada_para_despues_no_emite_ni_libera_el_cupo()
    {
        SeedCard("card-1", MemberId);
        RouterWithImmediateAndDelayedSteps();
        _orchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult { Status = "Scheduled", ScheduledJobId = "job-1" }));

        (await Buy(Request(TokenPaymentMethods.CreditCard, card: "card-1"))).ErrorCode.Should().Be(TokenPurchaseErrors.NeedsReview);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.NeedsReconciliation);
        _db.TokenTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_el_recibo_falla_la_compra_sigue_hecha()
    {
        SeedCommissions(500m);
        var svc = Service();
        _receipts.Setup(r => r.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("SES caído"));

        var r = await svc.PurchaseAsync(new TokenPurchaseInput(MemberId, Actor, Request(TokenPaymentMethods.CommissionBalance)));

        r.IsSuccess.Should().BeTrue();
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Completed);
    }

    [Fact]
    public async Task Conciliar_como_cobrada_no_vuelve_a_mirar_el_tope()
    {
        var id = await CompraAConciliar(); // 2 reservados
        SeedCommissions(10000m);
        (await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 48))).IsSuccess.Should().BeTrue();

        var r = await Service().ReconcileAsync(id, new ReconcileTokenPurchaseRequest { Charged = true }, "tesoreria@x.com");

        r.IsSuccess.Should().BeTrue("el cupo se reservó al comprar");
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(50);
    }
}
