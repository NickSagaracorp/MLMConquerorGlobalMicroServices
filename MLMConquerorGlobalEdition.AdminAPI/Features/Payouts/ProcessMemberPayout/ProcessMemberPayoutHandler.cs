using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ProcessMemberPayout;

public class ProcessMemberPayoutHandler : IRequestHandler<ProcessMemberPayoutCommand, Result<PayoutResult>>
{
    private readonly IPayoutOrchestrator _orchestrator;

    public ProcessMemberPayoutHandler(IPayoutOrchestrator orchestrator) => _orchestrator = orchestrator;

    public Task<Result<PayoutResult>> Handle(ProcessMemberPayoutCommand request, CancellationToken ct)
        => _orchestrator.ExecutePayoutAsync(request.MemberId, request.ProcessDate, ct);
}
