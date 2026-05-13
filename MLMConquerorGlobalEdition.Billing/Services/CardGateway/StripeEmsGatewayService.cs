using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Stub — Stripe EMS credential variant for Maestro/Bancontact/JCB and last-resort fallback.
/// Reads "StripeEms" ApiCredential. Real Stripe SDK wiring (using EMS-specific secret) pending.
/// </summary>
public class StripeEmsGatewayService : StubGatewayBase
{
    public StripeEmsGatewayService(AppDbContext db, ILogger<StripeEmsGatewayService> logger)
        : base(db, logger) { }

    public override CardProcessor Processor    => CardProcessor.StripeEms;
    protected override string     CredentialKey => "StripeEms";
}
