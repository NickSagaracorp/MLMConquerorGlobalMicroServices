using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ValidateMemberPayoutAccount;

public record ValidateMemberPayoutAccountCommand(string MemberId) : IRequest<Result<PayoutAccountValidationDto>>;

public class PayoutAccountValidationDto
{
    public string MemberId { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string? GatewayMessage { get; set; }
}
