using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
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

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// Las dos carreras que InMemory no puede probar, contra SQL Server de verdad: el RowVersion de la
/// confirmación de cripto y el candado de fondos con la misma clave de idempotencia. Cada prueba usa
/// un miembro sintético propio y borra todo lo que escribió, pase o falle. Se saltan sin servidor.
/// </summary>
public class SqlServerTokenPurchaseTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TST-{Guid.NewGuid():N}"[..20];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!SqlServerTestDb.Reachable) return;

        await using var db = SqlServerTestDb.Db();
        var m = _member;
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM TokenTransactions WHERE MemberId = {m} OR OriginalOwnerMemberId = {m}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM TokenBalances WHERE MemberId = {m}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PaymentHistories WHERE MemberId = {m}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CryptoPaymentConfirmations WHERE MemberId = {m}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM TokenPurchases WHERE MemberId = {m}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Orders WHERE MemberId = {m}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CommissionEarnings WHERE BeneficiaryMemberId = {m}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberProfiles WHERE MemberId = {m}");
    }

    private TokenPurchaseService Service(AppDbContext db, ITokenIssuer? issuer = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(() => DateTime.Now);
        clock.Setup(c => c.UtcNow).Returns(() => DateTime.UtcNow);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var receipts = new Mock<ITokenPurchaseReceipts>();
        receipts.Setup(r => r.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        return new TokenPurchaseService(
            db, clock.Object, new TokenPricingService(db), new TokenMonthlyQuota(db), new SqlServerMemberFundsGate(db),
            issuer ?? new TokenIssuer(db), new CommissionBalanceService(db, clock.Object), Mock.Of<IEWalletFunds>(),
            Mock.Of<IGatewayRouter>(), Mock.Of<IGatewayChargeOrchestrator>(), Mock.Of<ICardBrandDetector>(),
            new CryptoDepositAddressReader(db), receipts.Object, cache.Object, push.Object,
            NullLogger<TokenPurchaseService>.Instance, new ConfigurationBuilder().Build());
    }

    private async Task SeedMemberAsync(AppDbContext db, decimal commissions = 0m)
    {
        var now = DateTime.Now;
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = _member, Email = $"{_member}@example.com", FirstName = "Prueba", LastName = "SQL",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = now.AddYears(-1),
            Country = "US", CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        });
        if (commissions > 0)
            db.CommissionEarnings.Add(new CommissionEarning
            {
                BeneficiaryMemberId = _member, CommissionTypeId = 1, Amount = commissions,
                Status = CommissionEarningStatus.Pending, EarnedDate = now.AddDays(-10), PaymentDate = now.AddDays(-1),
                IsManualEntry = true, Notes = "sql-test", CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
            });
        await db.SaveChangesAsync();
    }

    /// <summary>Un tipo vendible de la base, el más barato: las pruebas no crean tipos ni productos.</summary>
    private static async Task<TokenQuote> CheapestPurchasableAsync(AppDbContext db)
    {
        var catalog = await new TokenPricingService(db).GetCatalogAsync();
        var quote = catalog.Where(q => q.Purchasable && q.UnitPrice > 0).OrderBy(q => q.UnitPrice).FirstOrDefault();
        quote.Should().NotBeNull("la base de desarrollo debe tener al menos un tipo de token vendible");
        return quote!;
    }

    /// <summary>
    /// Emite y espera a que las dos confirmaciones hayan llegado aquí antes de guardar: así las dos leen
    /// la confirmación en AwaitingPayment y la carrera la decide el RowVersion, no el azar.
    /// </summary>
    private sealed class BarrierIssuer(ITokenIssuer inner, TaskCompletionSource both, Func<int> arrive) : ITokenIssuer
    {
        public async Task<IReadOnlyList<string>> IssueAsync(TokenIssue issue, CancellationToken ct = default)
        {
            var codes = await inner.IssueAsync(issue, ct);
            if (arrive() == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            return codes;
        }
    }

    [SqlServerFact]
    public async Task Dos_confirmaciones_simultaneas_de_la_misma_cripto_emiten_una_sola_vez()
    {
        await using (var seed = SqlServerTestDb.Db())
        {
            await SeedMemberAsync(seed);
            var quote = await CheapestPurchasableAsync(seed);
            var now = DateTime.Now;
            var order = new Orders
            {
                MemberId = _member, OrderNo = $"TP-{_member}", TotalAmount = quote.UnitPrice!.Value * 2,
                Status = OrderStatus.Processing, OrderDate = now, CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
            };
            seed.Orders.Add(order);
            seed.TokenPurchases.Add(new TokenPurchase
            {
                MemberId = _member, TokenTypeId = quote.TokenTypeId, GrantedProductIds = string.Join(",", quote.ProductIds),
                Quantity = 2, UnitPrice = quote.UnitPrice.Value, TotalAmount = quote.UnitPrice.Value * 2,
                PaymentMethod = TokenPaymentMethod.Crypto, Status = TokenPurchaseStatus.AwaitingCryptoConfirmation,
                OrderId = order.Id, IdempotencyKey = "sql-confirm", PeriodKey = TokenPurchaseRules.PeriodKey(now),
                CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
            });
            seed.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
            {
                OrderId = order.Id, MemberId = _member, MemberEmail = $"{_member}@example.com", CryptoCurrency = "BTC",
                AmountDue = quote.UnitPrice.Value * 2, Status = CryptoPaymentConfirmationStatus.AwaitingPayment,
                Purpose = CryptoPaymentPurpose.TokenPurchase, CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
            });
            await seed.SaveChangesAsync();
        }

        string orderId;
        await using (var read = SqlServerTestDb.Db())
            orderId = await read.TokenPurchases.Where(p => p.MemberId == _member).Select(p => p.OrderId).SingleAsync();

        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        int Arrive() => Interlocked.Increment(ref arrived);

        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        var svcA = Service(dbA, new BarrierIssuer(new TokenIssuer(dbA), both, Arrive));
        var svcB = Service(dbB, new BarrierIssuer(new TokenIssuer(dbB), both, Arrive));
        var req = new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0xsql" };

        var results = await Task.WhenAll(
            Task.Run(() => svcA.ConfirmCryptoAsync(orderId, req, "admin-a", "a@x.com")),
            Task.Run(() => svcB.ConfirmCryptoAsync(orderId, req, "admin-b", "b@x.com")));

        results.Count(r => r.IsSuccess).Should().Be(1, "el RowVersion deja pasar a una sola");
        results.Single(r => !r.IsSuccess).ErrorCode.Should().Be(TokenPurchaseErrors.CryptoAlreadyConfirmed);

        await using var check = SqlServerTestDb.Db();
        (await check.TokenTransactions.CountAsync(t => t.OriginalOwnerMemberId == _member && t.ReferenceId != null)).Should().Be(2);
        (await check.PaymentHistories.CountAsync(p => p.OrderId == orderId)).Should().Be(1);
        (await check.TokenBalances.Where(b => b.MemberId == _member).SumAsync(b => b.Balance)).Should().Be(2);
        (await check.TokenPurchases.SingleAsync(p => p.MemberId == _member)).Status.Should().Be(TokenPurchaseStatus.Completed);
    }

    [SqlServerFact]
    public async Task Dos_compras_simultaneas_con_la_misma_clave_cobran_y_emiten_una_sola_vez()
    {
        TokenQuote quote;
        await using (var seed = SqlServerTestDb.Db())
        {
            quote = await CheapestPurchasableAsync(seed);
            await SeedMemberAsync(seed, commissions: quote.UnitPrice!.Value * 3);
        }

        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        var req = new PurchaseTokensRequest
        {
            TokenTypeId = quote.TokenTypeId, Quantity = 1, PaymentMethod = TokenPaymentMethods.CommissionBalance,
            IdempotencyKey = "sql-same-key"
        };

        var results = await Task.WhenAll(
            Task.Run(() => Service(dbA).PurchaseAsync(new TokenPurchaseInput(_member, "sql-a", req))),
            Task.Run(() => Service(dbB).PurchaseAsync(new TokenPurchaseInput(_member, "sql-b", req))));

        // La segunda espera el candado y, ya dentro, encuentra la compra de la primera: la devuelve.
        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Value!.PurchaseId).Distinct().Should().HaveCount(1);
        results.Select(r => r.Value!.TokenCodes.Single()).Distinct().Should().HaveCount(1);

        await using var check = SqlServerTestDb.Db();
        (await check.TokenPurchases.CountAsync(p => p.MemberId == _member)).Should().Be(1);
        (await check.CommissionEarnings.CountAsync(e => e.BeneficiaryMemberId == _member && e.Amount < 0)).Should().Be(1, "un solo cobro");
        (await check.TokenTransactions.CountAsync(t => t.OriginalOwnerMemberId == _member && t.ReferenceId != null)).Should().Be(1);
    }
}
