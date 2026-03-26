namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;

public class AdminGrantTokenRequest
{
    public string MemberId { get; set; } = string.Empty;
    public int TokenTypeId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
