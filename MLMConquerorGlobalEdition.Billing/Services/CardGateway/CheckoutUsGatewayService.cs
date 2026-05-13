using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Stub — Checkout.com US account.
/// Reads "CheckoutUS" ApiCredential. Real Checkout.com SDK wiring pending.
/// </summary>
public class CheckoutUsGatewayService : StubGatewayBase
{
    public CheckoutUsGatewayService(AppDbContext db, ILogger<CheckoutUsGatewayService> logger)
        : base(db, logger) { }

    public override CardProcessor Processor    => CardProcessor.CheckoutUS;
    protected override string     CredentialKey => "CheckoutUS";
}
