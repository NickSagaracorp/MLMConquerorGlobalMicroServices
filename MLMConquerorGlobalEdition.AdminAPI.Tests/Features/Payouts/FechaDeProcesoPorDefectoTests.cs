using MediatR;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ExportPayoutBatch;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPendingPayouts;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ProcessMemberPayout;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payouts;

/// <summary>
/// Sin fecha de proceso, pagar, exportar el lote y listar pendientes usan la hora de servidor: las
/// comisiones guardan su PaymentDate en hora de servidor y cortar con UTC movía el corte unas horas.
/// </summary>
public class FechaDeProcesoPorDefectoTests
{
    private static readonly DateTime ServerNow = new(2026, 10, 15, 21, 30, 0, DateTimeKind.Local);

    private static (AdminPayoutsController Controller, Mock<IMediator> Mediator) Build()
    {
        var mediator = new Mock<IMediator>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(ServerNow);
        clock.Setup(c => c.UtcNow).Returns(ServerNow.AddHours(4));
        return (new AdminPayoutsController(mediator.Object, InMemoryDbHelper.Create(), Mock.Of<IReceiptStorage>(), clock.Object), mediator);
    }

    [Fact]
    public async Task Pagar_sin_fecha_usa_la_hora_de_servidor()
    {
        var (controller, mediator) = Build();
        mediator.Setup(m => m.Send(It.IsAny<ProcessMemberPayoutCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<MLMConquerorGlobalEdition.Billing.Services.Payout.PayoutResult>.Failure("X", "x"));

        await controller.Process("AMB-1", new AdminPayoutsController.ProcessPayoutBody(null));

        mediator.Verify(m => m.Send(It.Is<ProcessMemberPayoutCommand>(c => c.ProcessDate == ServerNow), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Exportar_y_listar_sin_fecha_usan_la_hora_de_servidor()
    {
        var (controller, mediator) = Build();
        mediator.Setup(m => m.Send(It.IsAny<ExportPayoutBatchCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<MLMConquerorGlobalEdition.Billing.Services.Payout.Batch.PayoutBatchExportResult>.Failure("X", "x"));
        mediator.Setup(m => m.Send(It.IsAny<GetPendingPayoutsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PagedResult<MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts.PendingPayoutRowDto>>.Failure("X", "x"));

        await controller.ExportBatch(new AdminPayoutsController.ExportBatchBody(WalletType.eWallet, null));
        await controller.GetPending(null);

        mediator.Verify(m => m.Send(It.Is<ExportPayoutBatchCommand>(c => c.ProcessDate == ServerNow), It.IsAny<CancellationToken>()));
        mediator.Verify(m => m.Send(It.Is<GetPendingPayoutsQuery>(q => q.ProcessDate == ServerNow), It.IsAny<CancellationToken>()));
    }
}
