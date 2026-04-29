using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.ReorderCreditCards;

public record ReorderCreditCardsCommand(ReorderCreditCardsRequest Request) : IRequest<Result<bool>>;
