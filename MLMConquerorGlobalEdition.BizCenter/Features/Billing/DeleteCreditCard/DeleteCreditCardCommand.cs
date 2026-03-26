using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.DeleteCreditCard;

public record DeleteCreditCardCommand(string CardId) : IRequest<Result<bool>>;
