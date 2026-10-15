using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// CANJEAR EN EL ALTA FRENTE A DISTRIBUIR (revisión de la Tarea 14, I1). El canje no tomaba candado
/// ni comprobaba el estado al guardar: un canje y una distribución simultáneos del mismo código se
/// pisaban (el último en guardar ganaba) y el código podía gastarse dos veces o "resucitar". Ahora el
/// alta toma el candado de fondos del dueño, el canje relee el código y comprueba que sigue siendo de
/// ese dueño, y los tokens de concurrencia de MemberId/Status paran lo que se cuele.
/// </summary>
public class CanjeDeTokenEnElAltaTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);
    private const string Owner     = "AMB-OWNER";
    private const string Other     = "AMB-OTHER";
    private const string NewMember = "AMB-T1";
    private const string Email     = "t1@example.com";
    private const string OrderId   = "ORD-T1";
    private const string Code      = "TKN0001";
    private const string ProductId = "P-VIP";

    private readonly string _dbName = Guid.NewGuid().ToString();

    private async Task<AppDbContext> SeedAsync()
    {
        var db = InMemoryDbHelper.Create(_dbName);
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = NewMember, Email = Email, FirstName = "T", LastName = "U", DateOfBirth = new DateTime(1990, 1, 1),
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Pending, EnrollDate = Now, Country = "US",
            CreatedBy = Email, CreationDate = Now, LastUpdateDate = Now
        });
        db.Orders.Add(new Orders
        {
            Id = OrderId, MemberId = NewMember, TotalAmount = 80, Status = OrderStatus.Pending, OrderDate = Now,
            CreatedBy = Email, CreationDate = Now, LastUpdateDate = Now
        });
        db.OrderDetails.Add(new OrderDetail
        {
            OrderId = OrderId, ProductId = ProductId, Quantity = 1, UnitPrice = 80, CreatedBy = Email, CreationDate = Now
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "SUB-T1", MemberId = NewMember, MembershipLevelId = 1, ChangeReason = SubscriptionChangeReason.New,
            SubscriptionStatus = MembershipStatus.Pending, StartDate = Now, IsAutoRenew = true,
            CreatedBy = Email, CreationDate = Now, LastUpdateDate = Now
        });
        db.TokenTypes.Add(new TokenType
        {
            Id = 13, Name = "Enrollment: VIP", Category = TokenCategory.Enrollment, IsActive = true,
            CreatedBy = "seed", CreationDate = Now.AddDays(-10)
        });
        db.TokenTypeProducts.Add(new TokenTypeProduct
        {
            Id = 1, TokenTypeId = 13, ProductId = ProductId, Role = TokenProductRole.Granted, QuantityGranted = 1,
            CreatedBy = "seed", CreationDate = Now.AddDays(-10)
        });
        db.TokenTransactions.Add(new TokenTransaction
        {
            Id = 1, MemberId = Owner, OriginalOwnerMemberId = Owner, TokenTypeId = 13,
            TransactionType = TokenTransactionType.Purchased, Quantity = 1, ReferenceId = Code,
            Status = TokenInstanceStatus.Issued, CreatedBy = "seed", CreationDate = Now.AddDays(-1)
        });
        db.TokenBalances.Add(new TokenBalance
        {
            Id = Guid.NewGuid().ToString(), MemberId = Owner, TokenTypeId = 13, Balance = 1,
            CreatedBy = "seed", CreationDate = Now.AddDays(-1)
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private CompleteSignupHandler Handler(AppDbContext db, IMemberFundsGate gate, ITokenRedemptionService? redemption = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);

        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>()))
           .Returns("access");
        jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh");
        jwt.Setup(j => j.AccessTokenExpiry).Returns(TimeSpan.FromMinutes(60));
        jwt.Setup(j => j.RefreshTokenExpiry).Returns(TimeSpan.FromDays(30));

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = Email, Email = Email, MemberProfileId = NewMember,
            IsActive = false, CreationDate = Now, CreatedBy = Email
        });
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var recurring = new Mock<MLMConquerorGlobalEdition.Billing.Services.Recurring.IRecurringBillingEnrollmentService>();
        recurring.Setup(r => r.EnsureStateForSubscriptionAsync(It.IsAny<MembershipSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        return new CompleteSignupHandler(
            db, clock.Object, Mock.Of<IS3FileService>(), userMgr.Object, jwt.Object, Mock.Of<IEncryptionService>(),
            redemption ?? new TokenRedemptionService(db, NullLogger<TokenRedemptionService>.Instance),
            new SignupActivationService(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(), recurring.Object),
            Mock.Of<MLMConquerorGlobalEdition.Repository.Services.Crypto.ICryptoDepositAddressReader>(),
            gate);
    }

    private static CompleteSignupCommand Cmd() =>
        new(OrderId, new CompleteSignupRequest { PaymentMethod = PaymentMethodType.Token, TokenCode = Code.ToLowerInvariant() });

    private async Task<TokenTransaction> CodigoAsync()
    {
        await using var fresh = InMemoryDbHelper.Create(_dbName);
        return await fresh.TokenTransactions.AsNoTracking().SingleAsync(t => t.ReferenceId == Code);
    }

    [Fact]
    public async Task Canjea_bajo_el_candado_del_dueno_del_codigo()
    {
        await using var db = await SeedAsync();
        var gate = new PuertaDeFondosDePrueba(db);

        var r = await Handler(db, gate).Handle(Cmd(), default);

        r.IsSuccess.Should().BeTrue(r.Error);
        gate.Entrados.Should().Equal(Owner);
        var codigo = await CodigoAsync();
        codigo.Status.Should().Be(TokenInstanceStatus.Used);
        codigo.UsedByMemberId.Should().Be(NewMember);
    }

    [Fact]
    public async Task Si_el_dueno_cambio_mientras_esperaba_el_candado_es_TOKEN_NOT_VALID_y_no_activa()
    {
        await using var db = await SeedAsync();
        var gate = new PuertaDeFondosDePrueba(db)
        {
            // Una distribución terminó mientras el alta esperaba el candado de Owner y se llevó el código.
            AlEntrar = async () =>
            {
                await using var otra = InMemoryDbHelper.Create(_dbName);
                var x = await otra.TokenTransactions.SingleAsync(t => t.ReferenceId == Code);
                x.PreviousOwnerMemberId = Owner;
                x.MemberId = Other;
                x.Status   = TokenInstanceStatus.Distributed;
                await otra.SaveChangesAsync();
            }
        };

        var r = await Handler(db, gate).Handle(Cmd(), default);

        r.ErrorCode.Should().Be("TOKEN_NOT_VALID");
        var codigo = await CodigoAsync();
        codigo.MemberId.Should().Be(Other, "el código sigue donde lo dejó la distribución");
        codigo.Status.Should().Be(TokenInstanceStatus.Distributed);
        await using var fresh = InMemoryDbHelper.Create(_dbName);
        (await fresh.Orders.SingleAsync(o => o.Id == OrderId)).Status.Should().Be(OrderStatus.Pending);
    }

    /// <summary>
    /// Canjea de verdad y, antes de que el alta guarde, otro proceso mueve el código: lo que haría una
    /// distribución que no respetara el candado (o una instancia con otro dueño leído).
    /// </summary>
    private sealed class CanjeYLuegoOtroLoMueve(AppDbContext db, string dbName) : ITokenRedemptionService
    {
        public async Task<Result<bool>> RedeemForSignupAsync(string tokenCode, string newMemberId, string orderId,
            IReadOnlyCollection<string> selectedProductIds, string? expectedOwnerMemberId, DateTime now, CancellationToken ct)
        {
            var r = await new TokenRedemptionService(db, NullLogger<TokenRedemptionService>.Instance)
                .RedeemForSignupAsync(tokenCode, newMemberId, orderId, selectedProductIds, expectedOwnerMemberId, now, ct);

            await using var otra = InMemoryDbHelper.Create(dbName);
            var x = await otra.TokenTransactions.SingleAsync(t => t.ReferenceId == Code, ct);
            x.MemberId = Other;
            x.Status   = TokenInstanceStatus.Distributed;
            await otra.SaveChangesAsync(ct);
            return r;
        }
    }

    [Fact]
    public async Task Si_el_guardado_choca_por_concurrencia_es_TOKEN_NOT_VALID_y_no_pisa_el_codigo()
    {
        await using var db = await SeedAsync();

        var r = await Handler(db, new PuertaDeFondosDePrueba(db), new CanjeYLuegoOtroLoMueve(db, _dbName)).Handle(Cmd(), default);

        r.ErrorCode.Should().Be("TOKEN_NOT_VALID");
        var codigo = await CodigoAsync();
        codigo.MemberId.Should().Be(Other, "el canje no pisa lo que guardó la otra operación");
        codigo.Status.Should().Be(TokenInstanceStatus.Distributed);
        codigo.UsedByMemberId.Should().BeNull();
        db.ChangeTracker.Entries().Should().BeEmpty("no queda nada a medias en el contexto");
    }

    [Fact]
    public async Task Con_el_candado_del_dueno_ocupado_es_TOKEN_BUSY_sin_tocar_nada()
    {
        await using var db = await SeedAsync();
        var redemption = new Mock<ITokenRedemptionService>(MockBehavior.Strict);

        var r = await Handler(db, new PuertaDeFondosDePrueba(db, Owner), redemption.Object).Handle(Cmd(), default);

        r.ErrorCode.Should().Be(CompleteSignupHandler.TokenBusyCode);
        (await CodigoAsync()).Status.Should().Be(TokenInstanceStatus.Issued);
        await using var fresh = InMemoryDbHelper.Create(_dbName);
        (await fresh.Orders.SingleAsync(o => o.Id == OrderId)).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task El_controlador_devuelve_409_cuando_el_token_esta_ocupado()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(CompleteSignupHandler.TokenBusyCode, "busy"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        var r = await controller.CompleteSignup(OrderId, new CompleteSignupRequest { PaymentMethod = PaymentMethodType.Token }, default);

        r.Should().BeOfType<ConflictObjectResult>()
         .Which.Value.Should().BeOfType<ApiResponse<SignupResponse>>()
         .Which.ErrorCode.Should().Be(CompleteSignupHandler.TokenBusyCode);
    }
}
