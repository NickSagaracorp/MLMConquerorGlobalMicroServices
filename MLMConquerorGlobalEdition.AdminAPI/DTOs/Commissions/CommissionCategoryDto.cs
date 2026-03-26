namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;

public class CommissionCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
