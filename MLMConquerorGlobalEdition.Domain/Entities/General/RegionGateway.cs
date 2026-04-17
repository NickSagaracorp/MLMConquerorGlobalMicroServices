using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// Maps a billing region to one or more payment gateways.
/// Priority 1 = primary gateway; higher numbers are fallbacks.
/// The actual charge routing logic lives in the Billing service (R2+).
/// </summary>
public class RegionGateway : AuditChangesIntKey
{
    public int        RegionId    { get; set; }
    public WalletType GatewayType { get; set; }
    public int        Priority    { get; set; } = 1; // 1 = primary
    public bool       IsActive    { get; set; } = true;

    public Region Region { get; set; } = null!;
}
