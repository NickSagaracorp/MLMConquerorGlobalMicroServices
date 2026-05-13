using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Stub — NMI Direct API (fallback-only gateway).
/// Reads "NmiDirect" ApiCredential. Real HTTP call to NMI API pending.
/// </summary>
public class NmiDirectGatewayService : StubGatewayBase
{
    public NmiDirectGatewayService(AppDbContext db, ILogger<NmiDirectGatewayService> logger)
        : base(db, logger) { }

    public override CardProcessor Processor    => CardProcessor.NmiDirect;
    protected override string     CredentialKey => "NmiDirect";
}
