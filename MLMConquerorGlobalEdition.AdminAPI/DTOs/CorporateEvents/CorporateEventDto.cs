namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;

public class CorporateEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string? Location { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationDate { get; set; }
}
