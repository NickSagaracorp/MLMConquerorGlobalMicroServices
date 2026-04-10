namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;

public class TokenTransactionDto
{
    public string  TokenTypeName    { get; set; } = string.Empty;
    public string  TransactionType  { get; set; } = string.Empty;
    public string? TokenCode        { get; set; }
    public bool    IsUsed           { get; set; }
    public string? UsedByMemberId   { get; set; }
    public string? UsedByMemberName { get; set; }
    public int     Amount           { get; set; }
    public DateTime OccurredAt      { get; set; }
    public string? Notes            { get; set; }
}
