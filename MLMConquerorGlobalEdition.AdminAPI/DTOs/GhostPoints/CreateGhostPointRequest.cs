using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints;

public class CreateGhostPointRequest
{
    public string MemberId { get; set; } = string.Empty;
    public string LegMemberId { get; set; } = string.Empty;
    public int Points { get; set; }
    public TreeSide Side { get; set; }
    public string? Notes { get; set; }
}
