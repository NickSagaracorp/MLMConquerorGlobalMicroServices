using MediatR;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Payout;

public class PayoutHandler : IRequestHandler<PayoutCommand, Result<PayoutResponse>>
{
    private readonly IPayoutOrchestrator _orchestrator;
    private readonly IDateTimeProvider _dateTime;

    public PayoutHandler(IPayoutOrchestrator orchestrator, IDateTimeProvider dateTime)
    {
        _orchestrator = orchestrator;
        _dateTime = dateTime;
    }

    public async Task<Result<PayoutResponse>> Handle(PayoutCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var result = await _orchestrator.ExecutePayoutAsync(req.MemberId, _dateTime.Now, ct);

        if (!result.IsSuccess)
            return Result<PayoutResponse>.Failure(result.ErrorCode!, result.Error!);

        var payout = result.Value!;
        return Result<PayoutResponse>.Success(new PayoutResponse
        {
            MemberId = payout.MemberId,
            EarningsPaid = payout.Outcome == PayoutOutcome.Success ? payout.EarningsCount : 0,
            TotalPaid = payout.Outcome == PayoutOutcome.Success ? payout.AmountUsd : 0m
        });
    }
}
