namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;

public class AdminTokenBalanceDto
{
    public string TokenBalanceId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string MemberFullName { get; set; } = string.Empty;
    public string TokenTypeName { get; set; } = string.Empty;
    public bool IsGuestPass { get; set; }
    public int Balance { get; set; }
    public List<string> GeneratedTokenCodes { get; set; } = new();
}
