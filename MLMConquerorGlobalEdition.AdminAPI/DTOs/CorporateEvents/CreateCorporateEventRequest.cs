namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;

public class CreateCorporateEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string? Location { get; set; }
    public string? ImageUrl { get; set; }
}
