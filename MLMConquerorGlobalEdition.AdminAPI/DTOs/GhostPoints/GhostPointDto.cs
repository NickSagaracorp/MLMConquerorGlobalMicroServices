namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints;

public class GhostPointDto
{
    public string Id { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string LegMemberId { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public string Side { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationDate { get; set; }
}
