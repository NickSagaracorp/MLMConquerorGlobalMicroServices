using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.Refund;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Tests.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.Billing.Tests.Features;

/// <summary>
/// Revision de Q3 (I2): la devolucion desde el admin contra SQL Server con el candado de puntos del miembro tomado por otro.
/// La pasarela ya devolvio el dinero: el pago y el pedido quedan Refunded aunque no se llegue a tomar el candado, y
/// reintentar no vuelve a pedir la devolucion a la pasarela.
/// </summary>
public class SqlServerDevolucionConCandadoOcupadoTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TDV-{Guid.NewGuid():N}"[..20];
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 30, 0);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!SqlServerTestDb.Reachable) return;
        for (var intento = 1; ; intento++)
        {
            try
            {
                await using var db = SqlServerTestDb.Db();
                var m = _member;
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET DEADLOCK_PRIORITY HIGH;
                    DECLARE @o TABLE (Id nvarchar(450) PRIMARY KEY);
                    INSERT @o SELECT Id FROM Orders WITH (NOLOCK) WHERE MemberId = {m};
                    DELETE FROM SubscriptionBillingStates WHERE MemberId = {m};
                    DELETE FROM DualLegDeltas WHERE StartMemberId = {m} AND OwnChange = 1;
                    DELETE FROM MemberStatisticDeltas WHERE SourceMemberId = {m} OR MemberId = {m};
                    DELETE FROM MemberStatistics WHERE MemberId = {m};
                    DELETE FROM RankEvaluationQueue WHERE TriggerMemberId = {m} OR EvaluateMemberId = {m};
                    DELETE FROM MembershipStatusHistories WHERE MemberId = {m};
                    DELETE FROM MembershipSubscriptions WHERE MemberId = {m};
                    DELETE FROM PaymentHistories WHERE MemberId = {m};
                    DELETE x FROM Orders x JOIN @o o ON o.Id = x.Id;
                    DELETE FROM MemberProfiles WHERE MemberId = {m};");
                return;
            }
            catch (SqlException ex) when (ex.Number is 1205 or -2 && intento < 8)
            {
                await Task.Delay(500 * intento);
            }
        }
    }

    private async Task<string> SembrarAsync()
    {
        await using var db = SqlServerTestDb.Db();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = _member, UserId = Guid.NewGuid(), Email = $"{_member}@example.com", FirstName = "Prueba", LastName = "I2",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = Ahora.AddYears(-1),
            Country = "US", CreatedBy = "sql-test", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        var pedido = new OrdersEntity
        {
            MemberId = _member, OrderDate = Ahora.AddDays(-10), Status = OrderStatus.Completed, TotalAmount = 100m,
            CreatedBy = "sql-test", CreationDate = Ahora, LastUpdateDate = Ahora
        };
        db.Orders.Add(pedido);
        var pago = new PaymentHistory
        {
            OrderId = pedido.Id, MemberId = _member, Amount = 100m, GatewayName = nameof(WalletType.Dwolla),
            GatewayTransactionId = "tx-" + _member, TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            CreatedBy = "sql-test", CreationDate = Ahora, LastUpdateDate = Ahora
        };
        db.PaymentHistories.Add(pago);
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = _member, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New, StartDate = Ahora.AddDays(-10), IsAutoRenew = true,
            LastOrderId = pedido.Id, CreatedBy = "sql-test", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();
        return pago.Id;
    }

    [SqlServerFact]
    public async Task Si_el_candado_no_se_obtiene_el_pago_y_el_pedido_quedan_devueltos_y_reintentar_no_devuelve_dos_veces()
    {
        var pagoId = await SembrarAsync();
        await using var otro = new SqlConnection(SqlServerTestDb.ConnectionString);
        await otro.OpenAsync();
        await using (var cmd = new SqlCommand(
                         "EXEC sp_getapplock @Resource = @r, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;", otro))
        {
            cmd.Parameters.AddWithValue("@r", MemberPointsLocks.Resource(_member));
            await cmd.ExecuteNonQueryAsync();
        }

        var pasarela = new Mock<IGatewayService>();
        pasarela.Setup(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        var resolutor = new Mock<IGatewayResolver>();
        resolutor.Setup(r => r.Resolve(It.IsAny<WalletType>())).Returns(pasarela.Object);
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("admin-1");
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        var comando = new RefundCommand(new RefundRequest { PaymentHistoryId = pagoId, Reason = "prueba" });

        Result<bool> r;
        await using (var db = SqlServerTestDb.Db())
        {
            r = await new RefundHandler(db, resolutor.Object, usuario.Object, reloj.Object, lockTimeoutMs: 200)
                .Handle(comando, CancellationToken.None);
            db.Database.GetDbConnection().State.Should().Be(ConnectionState.Closed);
        }

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(RefundHandler.MembershipNotCancelledCode);
        await using (var db = SqlServerTestDb.Db())
        {
            (await db.PaymentHistories.AsNoTracking().SingleAsync(p => p.Id == pagoId)).TransactionStatus
                .Should().Be(PaymentHistoryTransactionStatus.Refunded);
            (await db.Orders.AsNoTracking().SingleAsync(o => o.MemberId == _member)).Status.Should().Be(OrderStatus.Refunded);
            (await db.MembershipSubscriptions.AsNoTracking().SingleAsync(s => s.MemberId == _member)).SubscriptionStatus
                .Should().Be(MembershipStatus.Active, "sin el candado no se cancela; el error lo dice");
        }

        // Reintentar: el pago ya no es Captured; la pasarela no se vuelve a llamar.
        await using (var db = SqlServerTestDb.Db())
        {
            var otra = await new RefundHandler(db, resolutor.Object, usuario.Object, reloj.Object, lockTimeoutMs: 200)
                .Handle(comando, CancellationToken.None);
            otra.ErrorCode.Should().Be("PAYMENT_NOT_REFUNDABLE");
        }
        pasarela.Verify(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
