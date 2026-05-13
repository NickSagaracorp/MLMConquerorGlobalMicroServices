using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Stub — Checkout.com EUR account.
/// Reads "CheckoutEUR" ApiCredential. Real Checkout.com SDK wiring pending.
/// </summary>
public class CheckoutEurGatewayService : StubGatewayBase
{
    public CheckoutEurGatewayService(AppDbContext db, ILogger<CheckoutEurGatewayService> logger)
        : base(db, logger) { }

    public override CardProcessor Processor    => CardProcessor.CheckoutEUR;
    protected override string     CredentialKey => "CheckoutEUR";
}
