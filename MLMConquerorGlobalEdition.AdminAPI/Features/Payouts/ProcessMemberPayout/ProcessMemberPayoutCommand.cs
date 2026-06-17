using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ProcessMemberPayout;

public record ProcessMemberPayoutCommand(string MemberId, DateTime ProcessDate)
    : IRequest<Result<PayoutResult>>;
