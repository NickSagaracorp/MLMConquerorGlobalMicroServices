using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetThirdParties;

public class GetThirdPartiesHandler : IRequestHandler<GetThirdPartiesQuery, Result<IEnumerable<string>>>
{
    public Task<Result<IEnumerable<string>>> Handle(
        GetThirdPartiesQuery request, CancellationToken cancellationToken)
    {
        var gateways = new List<string>
        {
            "Stripe",
            "Braintree",
            "Crypto (Coinbase / NOWPayments)",
            "eWallet",
            "Dwolla",
            "ACH / Wire Transfer"
        };

        return Task.FromResult(Result<IEnumerable<string>>.Success(gateways));
    }
}
