namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class ReceiptVerificationDto
{
    public bool HasReceipt { get; set; }
    public bool HashMatches { get; set; }
    public bool ChainValid { get; set; }
    public bool Anchored { get; set; }
    public string? AnchorRef { get; set; }
    public string Detail { get; set; } = string.Empty;
}
