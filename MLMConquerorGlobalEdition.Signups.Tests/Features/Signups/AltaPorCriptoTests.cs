using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// El alta por cripto de punta a punta.
///
/// Lo que el dueño del producto pidió, textualmente: "la subscripcion por crypto se recibe, pero
/// la membresia queda inactiva hasta que manualmente se confirma que se recibio el pago." Y sobre
/// las comisiones, elegido explícitamente: se generan al confirmar el cobro, no al completar el
/// alta, porque nadie cobra sobre dinero no recibido y si la transferencia nunca llega no hay
/// comisiones que revertir.
///
/// La colocación en la genealogía es la excepción: ocurre en la fase 1 del asistente y no depende
/// de esta vía, para que la estructura del árbol no dependa de cuándo alguien se siente a aprobar.
/// </summary>
public class AltaPorCriptoTests
{
    private static readonly DateTime FixedNow     = new(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedConfirm = new(2026, 3, 28, 9, 30, 0, DateTimeKind.Utc);

    private const string SponsorId    = "AMB-SPONSOR";
    private const string MemberId     = "AMB-CRYPTO";
    private const string MemberEmail  = "crypto@example.com";
    private const string OrderId      = "ORD-CRYPTO";
    private const string SubId        = "SUB-CRYPTO";
    private const string ApproverId   = "usr-admin-001";
    private const string ApproverMail = "billing@mlmconqueror.com";

    // ── Dobles ──────────────────────────────────────────────────────────────────

    private static Mock<IDateTimeProvider> Clock(DateTime at)
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(at);
        return m;
    }

    private static Mock<IS3FileService> S3()
    {
        var m = new Mock<IS3FileService>();
        m.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync("https://s3.example.com/screenshot.png");
        return m;
    }

    private static Mock<IJwtService> Jwt()
    {
        var m = new Mock<IJwtService>();
        m.Setup(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>()))
         .Returns("mock-access-token");
        m.Setup(j => j.GenerateRefreshToken()).Returns("mock-refresh-token");
        m.Setup(j => j.AccessTokenExpiry).Returns(TimeSpan.FromMinutes(60));
        m.Setup(j => j.RefreshTokenExpiry).Returns(TimeSpan.FromDays(30));
        return m;
    }

    private static Mock<IEncryptionService> Encryption()
    {
        var m = new Mock<IEncryptionService>();
        m.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(p => "ENC:" + p);
        m.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(c => c.StartsWith("ENC:") ? c[4..] : c);
        return m;
    }

    private static Mock<ITokenRedemptionService> TokenRedemption()
    {
        var m = new Mock<ITokenRedemptionService>();
        m.Setup(s => s.RedeemForSignupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(MLMConquerorGlobalEdition.SharedKernel.Result<bool>.Success(true));
        return m;
    }

    private static Mock<ISponsorBonusService> SponsorBonus()
    {
        var m = new Mock<ISponsorBonusService>();
        m.Setup(s => s.ComputeAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IFastStartBonusService> FastStartBonus()
    {
        var m = new Mock<IFastStartBonusService>();
        m.Setup(s => s.ComputeAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IRecurringBillingEnrollmentService> RecurringBilling()
    {
        var m = new Mock<IRecurringBillingEnrollmentService>();
        m.Setup(s => s.EnsureStateForSubscriptionAsync(
                It.IsAny<MembershipSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<UserManager<ApplicationUser>> UserMgr(ApplicationUser user)
    {
        var m = UserManagerHelper.Create();
        m.Setup(u => u.FindByEmailAsync(MemberEmail)).ReturnsAsync(user);
        m.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        return m;
    }

    // ── Escenario ───────────────────────────────────────────────────────────────

    private static ApplicationUser BuildInactiveUser() => new()
    {
        Id                 = Guid.NewGuid().ToString(),
        UserName           = MemberEmail,
        NormalizedUserName = MemberEmail.ToUpperInvariant(),
        Email              = MemberEmail,
        NormalizedEmail    = MemberEmail.ToUpperInvariant(),
        EmailConfirmed     = false,
        MemberProfileId    = MemberId,
        IsActive           = false,
        CreationDate       = FixedNow,
        CreatedBy          = MemberEmail
    };

    /// <summary>Un aspirante con patrocinador, un producto de 10 puntos y todo en Pending.</summary>
    private static async Task SeedAsync(AppDbContext db)
    {
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId       = SponsorId,
            Email          = "sponsor@example.com",
            FirstName      = "Maria",
            LastName       = "Gomez",
            DateOfBirth    = new DateTime(1985, 1, 1),
            MemberType     = MemberType.Ambassador,
            Status         = MemberAccountStatus.Active,
            EnrollDate     = FixedNow.AddMonths(-6),
            Country        = "US",
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });

        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId       = SponsorId,
            HierarchyPath  = $"/{SponsorId}/",
            Level          = 1,
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });

        await db.Products.AddAsync(new Product
        {
            Id                 = "P-QUAL",
            Name               = "Qualifying Pack",
            Description        = "Worth 10 qual points",
            ImageUrl           = "https://cdn.example.com/qual.png",
            MonthlyFee         = 80,
            SetupFee           = 0,
            QualificationPoins = 10,
            IsActive           = true,
            CreatedBy          = "seed",
            CreationDate       = FixedNow,
            LastUpdateDate     = FixedNow
        });

        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId        = MemberId,
            Email           = MemberEmail,
            FirstName       = "Carlos",
            LastName        = "Rivera",
            DateOfBirth     = new DateTime(1990, 6, 15),
            MemberType      = MemberType.Ambassador,
            Status          = MemberAccountStatus.Pending,
            EnrollDate      = FixedNow,
            Country         = "US",
            SponsorMemberId = SponsorId,
            CreatedBy       = MemberEmail,
            CreationDate    = FixedNow,
            LastUpdateDate  = FixedNow
        });

        // La colocación en la genealogía ya está hecha: la escribe la fase 1 del asistente.
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId       = MemberId,
            ParentMemberId = SponsorId,
            HierarchyPath  = $"/{SponsorId}/{MemberId}/",
            Level          = 2,
            CreatedBy      = MemberEmail,
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });

        await db.Orders.AddAsync(new Orders
        {
            Id             = OrderId,
            MemberId       = MemberId,
            OrderNo        = "TAE0325XY",
            TotalAmount    = 80,
            Status         = OrderStatus.Pending,
            OrderDate      = FixedNow,
            CreatedBy      = MemberEmail,
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });

        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId      = OrderId,
            ProductId    = "P-QUAL",
            Quantity     = 1,
            UnitPrice    = 80,
            CreatedBy    = MemberEmail,
            CreationDate = FixedNow
        });

        await db.MembershipSubscriptions.AddAsync(new MembershipSubscription
        {
            Id                 = SubId,
            MemberId           = MemberId,
            MembershipLevelId  = 1,
            ChangeReason       = SubscriptionChangeReason.New,
            SubscriptionStatus = MembershipStatus.Pending,
            StartDate          = FixedNow,
            IsFree             = false,
            IsAutoRenew        = true,
            CreatedBy          = MemberEmail,
            CreationDate       = FixedNow,
            LastUpdateDate     = FixedNow
        });

        await db.SaveChangesAsync();
    }

    private static CompleteSignupRequest CryptoRequest() => new()
    {
        PaymentMethod  = PaymentMethodType.Crypto,
        CryptoCurrency = "BTC"
        // CryptoTransactionId a null a propósito: nadie puede producirlo en este momento.
    };

    private static CompleteSignupHandler BuildCompleteHandler(
        AppDbContext db,
        Mock<UserManager<ApplicationUser>> userMgr,
        ISponsorBonusService sponsor,
        IFastStartBonusService fsb,
        IRecurringBillingEnrollmentService recurring)
        => new(db, Clock(FixedNow).Object, S3().Object, userMgr.Object, Jwt().Object,
               Encryption().Object, TokenRedemption().Object,
               new SignupActivationService(db, sponsor, fsb, recurring));

    private static ConfirmCryptoPaymentHandler BuildConfirmHandler(
        AppDbContext db,
        Mock<UserManager<ApplicationUser>> userMgr,
        ISponsorBonusService sponsor,
        IFastStartBonusService fsb,
        IRecurringBillingEnrollmentService recurring)
        => new(db, Clock(FixedConfirm).Object, userMgr.Object,
               new SignupActivationService(db, sponsor, fsb, recurring),
               new Mock<ILogger<ConfirmCryptoPaymentHandler>>().Object);

    // ── 1. Completar por cripto ─────────────────────────────────────────────────

    [Fact]
    public async Task Completar_PorCripto_SinIdentificadorDeTransaccion_Funciona()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var handler = BuildCompleteHandler(db, UserMgr(BuildInactiveUser()),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "el identificador de la transacción se captura al confirmar, no al completar.");
    }

    [Fact]
    public async Task Completar_PorCripto_DejaAlMiembroInactivoYLaSuscripcionEnEspera()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var handler = BuildCompleteHandler(db, UserMgr(BuildInactiveUser()),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        await handler.Handle(new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);

        var member = await db.MemberProfiles.SingleAsync(m => m.MemberId == MemberId);
        member.Status.Should().Be(MemberAccountStatus.Pending,
            "Pending significa 'dado de alta y todavía no activado'; Inactive lo escriben las bajas.");

        var subscription = await db.MembershipSubscriptions.SingleAsync(s => s.Id == SubId);
        subscription.SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        subscription.EndDate.Should().BeNull("el mes de membresía arranca cuando el dinero está cobrado.");

        var order = await db.Orders.SingleAsync(o => o.Id == OrderId);
        order.Status.Should().Be(OrderStatus.Processing,
            "ni Pending —lo recogería la herramienta de altas zombis— ni Completed —el dinero no ha llegado—.");
    }

    [Fact]
    public async Task Completar_PorCripto_NoGeneraNiComisionesNiDeltas()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var sponsor   = SponsorBonus();
        var fsb       = FastStartBonus();
        var recurring = RecurringBilling();

        var handler = BuildCompleteHandler(db, UserMgr(BuildInactiveUser()),
            sponsor.Object, fsb.Object, recurring.Object);

        await handler.Handle(new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);

        (await db.MemberStatisticDeltas.CountAsync()).Should().Be(0,
            "los deltas del upline se encolan al confirmar el cobro, no antes.");

        sponsor.Verify(s => s.ComputeAsync(
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never, "nadie cobra sobre dinero no recibido.");

        fsb.Verify(s => s.ComputeAsync(
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        recurring.Verify(s => s.EnsureStateForSubscriptionAsync(
            It.IsAny<MembershipSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "una suscripción que no está activa no entra en el cobro recurrente.");
    }

    [Fact]
    public async Task Completar_PorCripto_MantieneLaColocacionEnElArbol()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var handler = BuildCompleteHandler(db, UserMgr(BuildInactiveUser()),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        await handler.Handle(new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);

        var node = await db.GenealogyTree.SingleAsync(g => g.MemberId == MemberId);
        node.ParentMemberId.Should().Be(SponsorId);
        node.HierarchyPath.Should().Be($"/{SponsorId}/{MemberId}/",
            "la estructura no puede depender de cuándo alguien apruebe el cobro.");
    }

    [Fact]
    public async Task Completar_PorCripto_NoDaAccesoAlPortal()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var appUser = BuildInactiveUser();
        var userMgr = UserMgr(appUser);

        var handler = BuildCompleteHandler(db, userMgr,
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);

        appUser.IsActive.Should().BeFalse("LoginHandler rechaza a los usuarios con IsActive = false.");
        result.Value!.AccessToken.Should().BeNull();
        result.Value.RefreshToken.Should().BeNull();
        userMgr.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Completar_PorCripto_DejaLaFilaDeConfirmacionEnEspera()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var handler = BuildCompleteHandler(db, UserMgr(BuildInactiveUser()),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        await handler.Handle(new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);

        var confirmation = await db.CryptoPaymentConfirmations.SingleAsync();
        confirmation.OrderId.Should().Be(OrderId);
        confirmation.MemberId.Should().Be(MemberId);
        confirmation.MemberEmail.Should().Be(MemberEmail);
        confirmation.CryptoCurrency.Should().Be("BTC");
        confirmation.AmountDue.Should().Be(80);
        confirmation.Status.Should().Be(CryptoPaymentConfirmationStatus.AwaitingPayment);
        confirmation.CryptoTransactionId.Should().BeNull();
        confirmation.ConfirmedByUserId.Should().BeNull();
        confirmation.ConfirmedAt.Should().BeNull();
    }

    [Fact]
    public async Task Completar_PorCripto_DosVeces_LaSegundaNoEncuentraElAlta()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var handler = BuildCompleteHandler(db, UserMgr(BuildInactiveUser()),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        var primera = await handler.Handle(new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);
        var segunda = await handler.Handle(new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);

        primera.IsSuccess.Should().BeTrue();
        segunda.IsSuccess.Should().BeFalse();
        segunda.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");

        (await db.CryptoPaymentConfirmations.CountAsync()).Should().Be(1,
            "el pedido pasa a Processing y CompleteSignup solo mira los que están en Pending.");
    }

    // ── 2. Confirmar el cobro ───────────────────────────────────────────────────

    /// <summary>Completa por cripto y devuelve el contexto listo para confirmar.</summary>
    private static async Task CompletarPorCriptoAsync(AppDbContext db, ApplicationUser appUser)
    {
        var handler = BuildCompleteHandler(db, UserMgr(appUser),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);
        var r = await handler.Handle(new CompleteSignupCommand(OrderId, CryptoRequest()), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
    }

    private static ConfirmCryptoPaymentCommand ConfirmCommand(string txId = "a1b2c3d4e5f6") =>
        new(OrderId,
            new ConfirmCryptoPaymentRequest { CryptoTransactionId = txId, Notes = "Recibido en la red BTC." },
            ApproverId, ApproverMail);

    [Fact]
    public async Task Confirmar_ActivaAlMiembroYLaSuscripcion()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);
        var appUser = BuildInactiveUser();
        await CompletarPorCriptoAsync(db, appUser);

        var handler = BuildConfirmHandler(db, UserMgr(appUser),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        var result = await handler.Handle(ConfirmCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var member = await db.MemberProfiles.SingleAsync(m => m.MemberId == MemberId);
        member.Status.Should().Be(MemberAccountStatus.Active);

        var subscription = await db.MembershipSubscriptions.SingleAsync(s => s.Id == SubId);
        subscription.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        subscription.StartDate.Should().Be(FixedConfirm, "el mes arranca cuando el dinero está cobrado.");
        subscription.EndDate.Should().Be(FixedConfirm.AddMonths(1));

        var order = await db.Orders.SingleAsync(o => o.Id == OrderId);
        order.Status.Should().Be(OrderStatus.Completed);

        appUser.IsActive.Should().BeTrue();
        appUser.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task Confirmar_GeneraLasComisionesYLosDeltasDelUpline()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);
        var appUser = BuildInactiveUser();
        await CompletarPorCriptoAsync(db, appUser);

        var sponsor   = SponsorBonus();
        var fsb       = FastStartBonus();
        var recurring = RecurringBilling();

        var handler = BuildConfirmHandler(db, UserMgr(appUser), sponsor.Object, fsb.Object, recurring.Object);

        await handler.Handle(ConfirmCommand(), CancellationToken.None);

        var deltas = await db.MemberStatisticDeltas.ToListAsync();
        deltas.Should().HaveCount(1, "un delta por ancestro; aquí el upline es solo el patrocinador.");
        deltas[0].MemberId.Should().Be(SponsorId);
        deltas[0].EnrollmentPointsDelta.Should().Be(10);
        deltas[0].EnrollmentTeamSizeDelta.Should().Be(1);
        deltas[0].QualifiedSponsoredMembersDelta.Should().Be(1);
        deltas[0].SourceMemberId.Should().Be(MemberId);
        deltas[0].IsApplied.Should().BeFalse();

        sponsor.Verify(s => s.ComputeAsync(
            SponsorId, MemberId, OrderId, 80m, It.IsAny<string>(), FixedConfirm, It.IsAny<CancellationToken>()),
            Times.Once);

        fsb.Verify(s => s.ComputeAsync(
            SponsorId, MemberId, OrderId, FixedConfirm, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        recurring.Verify(s => s.EnsureStateForSubscriptionAsync(
            It.IsAny<MembershipSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Confirmar_GuardaElRastroDeAuditoria()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);
        var appUser = BuildInactiveUser();
        await CompletarPorCriptoAsync(db, appUser);

        var handler = BuildConfirmHandler(db, UserMgr(appUser),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        await handler.Handle(ConfirmCommand("deadbeef1234"), CancellationToken.None);

        var confirmation = await db.CryptoPaymentConfirmations.SingleAsync();
        confirmation.Status.Should().Be(CryptoPaymentConfirmationStatus.Confirmed);
        confirmation.CryptoTransactionId.Should().Be("deadbeef1234");
        confirmation.ConfirmedByUserId.Should().Be(ApproverId);
        confirmation.ConfirmedByEmail.Should().Be(ApproverMail);
        confirmation.ConfirmedAt.Should().Be(FixedConfirm);
        confirmation.Notes.Should().Be("Recibido en la red BTC.");
    }

    // ── 3. La doble aprobación ──────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_DosVeces_NoDuplicaComisionesNiDeltas()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);
        var appUser = BuildInactiveUser();
        await CompletarPorCriptoAsync(db, appUser);

        var sponsor   = SponsorBonus();
        var fsb       = FastStartBonus();
        var recurring = RecurringBilling();

        var handler = BuildConfirmHandler(db, UserMgr(appUser), sponsor.Object, fsb.Object, recurring.Object);

        var primera = await handler.Handle(ConfirmCommand("aaaa1111"), CancellationToken.None);
        var segunda = await handler.Handle(ConfirmCommand("bbbb2222"), CancellationToken.None);

        primera.IsSuccess.Should().BeTrue();
        segunda.IsSuccess.Should().BeFalse();
        segunda.ErrorCode.Should().Be("CRYPTO_PAYMENT_ALREADY_CONFIRMED");

        (await db.MemberStatisticDeltas.CountAsync()).Should().Be(1,
            "la segunda aprobación no puede volver a encolar el delta del upline.");

        sponsor.Verify(s => s.ComputeAsync(
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        fsb.Verify(s => s.ComputeAsync(
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // El rastro se queda con la PRIMERA aprobación; la segunda no reescribe nada.
        var confirmation = await db.CryptoPaymentConfirmations.SingleAsync();
        confirmation.CryptoTransactionId.Should().Be("aaaa1111");
    }

    [Fact]
    public async Task Confirmar_UnPedidoQueNoEstaEsperandoCobro_NoEncuentraNada()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var handler = BuildConfirmHandler(db, UserMgr(BuildInactiveUser()),
            SponsorBonus().Object, FastStartBonus().Object, RecurringBilling().Object);

        var result = await handler.Handle(
            new ConfirmCryptoPaymentCommand(
                "ORD-QUE-NO-EXISTE",
                new ConfirmCryptoPaymentRequest { CryptoTransactionId = "abc123" },
                ApproverId, ApproverMail),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CRYPTO_PAYMENT_NOT_FOUND");
    }

    // ── 4. Las otras vías no se tocan ───────────────────────────────────────────

    [Theory]
    [InlineData(PaymentMethodType.Token)]
    [InlineData(PaymentMethodType.CreditCard)]
    [InlineData(PaymentMethodType.DiscountCode)]
    public async Task Completar_PorLasViasQueYaCobraron_SigueActivandoYComisionando(PaymentMethodType metodo)
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db);

        var appUser = BuildInactiveUser();
        var sponsor = SponsorBonus();

        var handler = BuildCompleteHandler(db, UserMgr(appUser),
            sponsor.Object, FastStartBonus().Object, RecurringBilling().Object);

        var request = new CompleteSignupRequest
        {
            PaymentMethod = metodo,
            TokenCode     = metodo == PaymentMethodType.Token ? "ABCD1234" : null,
            DiscountCode  = metodo == PaymentMethodType.DiscountCode ? "FREE100" : null
        };

        var result = await handler.Handle(new CompleteSignupCommand(OrderId, request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().NotBeNull();

        (await db.MemberProfiles.SingleAsync(m => m.MemberId == MemberId))
            .Status.Should().Be(MemberAccountStatus.Active);
        (await db.Orders.SingleAsync(o => o.Id == OrderId)).Status.Should().Be(OrderStatus.Completed);
        (await db.MemberStatisticDeltas.CountAsync()).Should().Be(1);
        (await db.CryptoPaymentConfirmations.CountAsync()).Should().Be(0);

        sponsor.Verify(s => s.ComputeAsync(
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
