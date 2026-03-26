namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;

public class TicketCommentDto
{
    public long Id { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}

public class TicketDetailDto : TicketDto
{
    public IEnumerable<TicketCommentDto> Comments { get; set; } = new List<TicketCommentDto>();
}
