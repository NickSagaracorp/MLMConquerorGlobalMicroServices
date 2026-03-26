using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetCreditCards;

public record GetCreditCardsQuery() : IRequest<Result<IEnumerable<CreditCardDto>>>;
