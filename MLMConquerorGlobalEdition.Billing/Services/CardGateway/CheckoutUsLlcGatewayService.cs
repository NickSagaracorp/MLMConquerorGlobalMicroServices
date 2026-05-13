using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Stub — Checkout US LLC account (LatinAmerica routing).
/// Reads "CheckoutUsLlc" ApiCredential. Real Checkout.com SDK wiring pending.
/// </summary>
public class CheckoutUsLlcGatewayService : StubGatewayBase
{
    public CheckoutUsLlcGatewayService(AppDbContext db, ILogger<CheckoutUsLlcGatewayService> logger)
        : base(db, logger) { }

    public override CardProcessor Processor    => CardProcessor.CheckoutUsLlc;
    protected override string     CredentialKey => "CheckoutUsLlc";
}
