using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.SetDefaultCreditCard;

public record SetDefaultCreditCardCommand(string CardId) : IRequest<Result<bool>>;
