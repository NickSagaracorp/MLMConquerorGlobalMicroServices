using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Stub — NMI via Spreedly Vault API.
/// Reads "NmiSpreedly" ApiCredential. Real HTTP call to Spreedly NMI gateway pending.
/// </summary>
public class NmiSpreedlyGatewayService : StubGatewayBase
{
    public NmiSpreedlyGatewayService(AppDbContext db, ILogger<NmiSpreedlyGatewayService> logger)
        : base(db, logger) { }

    public override CardProcessor Processor    => CardProcessor.NmiSpreedly;
    protected override string     CredentialKey => "NmiSpreedly";
}
