namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;

public class WalletDto
{
    public string Id { get; set; } = string.Empty;
    public string WalletType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AccountIdentifier { get; set; }
    public bool IsPreferred { get; set; }
}
