using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.AddCreditCard;

public record AddCreditCardCommand(AddCreditCardRequest Request) : IRequest<Result<CreditCardDto>>;
