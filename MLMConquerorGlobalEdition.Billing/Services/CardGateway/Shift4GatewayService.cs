using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Stub — Shift4 gateway (European split routing).
/// Reads "Shift4" ApiCredential. Real Shift4 SDK wiring pending.
/// </summary>
public class Shift4GatewayService : StubGatewayBase
{
    public Shift4GatewayService(AppDbContext db, ILogger<Shift4GatewayService> logger)
        : base(db, logger) { }

    public override CardProcessor Processor    => CardProcessor.Shift4;
    protected override string     CredentialKey => "Shift4";
}
