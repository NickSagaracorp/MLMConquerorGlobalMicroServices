using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class PayoutStatsDto
{
    public DateTime ProcessDate { get; set; }
    public List<PayoutGatewayStatDto> Gateways { get; set; } = new();
}

public class PayoutGatewayStatDto
{
    public WalletType WalletType { get; set; }
    public decimal PendingTotal { get; set; }
    public int PendingCount { get; set; }
    public decimal PaidTotal { get; set; } // successful payouts completed on ProcessDate's calendar day
}
