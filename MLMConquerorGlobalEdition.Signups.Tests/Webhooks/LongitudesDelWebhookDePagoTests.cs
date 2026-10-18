using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessPaymentWebhook;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Webhooks;

/// <summary>
/// Plan M (R16): con PaymentHistories acotada, un webhook con un campo más largo que su columna
/// fallaría al guardar con un error de truncado y la pasarela reintentaría sin fin. Se rechaza al
/// entrar (400, sin tocar la base), y la pasarela recibe un 4xx que no reintenta.
/// </summary>
public class LongitudesDelWebhookDePagoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 17, 12, 0, 0);

    private static ProcessPaymentWebhookHandler Handler(Repository.Context.AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        return new ProcessPaymentWebhookHandler(db, reloj.Object);
    }

    private static WebhookPaymentPayload Pago(string transaccion, string? pedido = "ORD-1", string? miembro = "AMB-000001") => new()
    {
        Event         = "payment.success",
        TransactionId = transaccion,
        Amount        = 10m,
        OrderId       = pedido,
        MemberId      = miembro,
    };

    [Fact]
    public async Task Un_TransactionId_de_201_caracteres_se_rechaza_sin_guardar()
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Handler(db).Handle(new ProcessPaymentWebhookCommand("stripe", Pago(new string('T', 201))));

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcessPaymentWebhookHandler.FieldTooLongCode);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_TransactionId_que_cabe_justo_se_guarda()
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Handler(db).Handle(new ProcessPaymentWebhookCommand("stripe",
            Pago(new string('T', PaymentHistory.GatewayTransactionIdMaxLength))));

        r.IsSuccess.Should().BeTrue();
        (await db.PaymentHistories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Un_OrderId_o_un_MemberId_que_no_caben_se_rechazan()
    {
        await using var db = InMemoryDbHelper.Create();
        var h = Handler(db);

        (await h.Handle(new ProcessPaymentWebhookCommand("stripe",
            Pago("TX-1", pedido: new string('O', PaymentHistory.OrderIdMaxLength + 1))))).ErrorCode
            .Should().Be(ProcessPaymentWebhookHandler.FieldTooLongCode);
        (await h.Handle(new ProcessPaymentWebhookCommand("stripe",
            Pago("TX-2", miembro: new string('M', PaymentHistory.MemberIdMaxLength + 1))))).ErrorCode
            .Should().Be(ProcessPaymentWebhookHandler.FieldTooLongCode);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Las_longitudes_son_las_de_la_columna()
    {
        PaymentHistory.OrderIdMaxLength.Should().Be(450);
        PaymentHistory.MemberIdMaxLength.Should().Be(100);
        PaymentHistory.GatewayTransactionIdMaxLength.Should().Be(200);
    }

    /// <summary>Revisión M7: el proveedor de la URL acaba en PaymentHistories.GatewayName; tampoco se acepta sin tope.</summary>
    [Fact]
    public async Task Un_proveedor_demasiado_largo_se_rechaza_sin_guardar()
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Handler(db).Handle(new ProcessPaymentWebhookCommand(
            new string('p', PaymentHistory.GatewayNameMaxLength + 1), Pago("TX-3")));

        r.ErrorCode.Should().Be(ProcessPaymentWebhookHandler.FieldTooLongCode);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
        PaymentHistory.GatewayNameMaxLength.Should().Be(100);
    }
}
