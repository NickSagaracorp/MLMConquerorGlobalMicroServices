namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;

/// <summary>
/// Lightweight ticket category projection used by the BizCenter "Create Ticket" modal
/// to populate the category dropdown. Members only see active categories.
/// </summary>
public class TicketCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
