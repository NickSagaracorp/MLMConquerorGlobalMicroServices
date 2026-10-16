namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;

public class TokenBalanceDto
{
    public int TokenTypeId { get; set; }
    public string TokenTypeName { get; set; } = string.Empty;
    public bool IsGuestPass { get; set; }
    public int Balance { get; set; }
}
