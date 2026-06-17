using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetMemberPayoutBalance;

public record GetMemberPayoutBalanceQuery(string MemberId) : IRequest<Result<MemberPayoutBalanceDto>>;

public class MemberPayoutBalanceDto
{
    public string MemberId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
}
