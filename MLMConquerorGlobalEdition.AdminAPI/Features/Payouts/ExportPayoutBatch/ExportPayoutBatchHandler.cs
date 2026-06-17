using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ExportPayoutBatch;

public class ExportPayoutBatchHandler
    : IRequestHandler<ExportPayoutBatchCommand, Result<PayoutBatchExportResult>>
{
    private readonly IPayoutBatchExportService _exportService;

    public ExportPayoutBatchHandler(IPayoutBatchExportService exportService)
        => _exportService = exportService;

    public Task<Result<PayoutBatchExportResult>> Handle(
        ExportPayoutBatchCommand request, CancellationToken ct)
        => _exportService.ExportAsync(request.WalletType, request.ProcessDate, ct);
}
