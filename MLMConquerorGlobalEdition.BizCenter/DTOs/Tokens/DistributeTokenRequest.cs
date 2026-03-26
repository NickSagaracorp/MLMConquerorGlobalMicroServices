namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;

public class DistributeTokenRequest
{
    public int TokenTypeId { get; set; }
    public string RecipientMemberId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
