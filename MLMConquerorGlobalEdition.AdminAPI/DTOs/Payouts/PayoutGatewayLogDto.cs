using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class PayoutGatewayLogDto
{
    public long Id { get; set; }
    public WalletType WalletType { get; set; }
    public string Operation { get; set; } = string.Empty;
    public int HttpStatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreationDate { get; set; }
}
