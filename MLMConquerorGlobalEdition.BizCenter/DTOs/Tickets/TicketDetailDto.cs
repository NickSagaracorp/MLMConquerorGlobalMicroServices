namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;

public class TicketCommentDto
{
    public long Id { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsStaff { get; set; }
    public DateTime CreationDate { get; set; }
}

public class TicketDetailDto : TicketDto
{
    public IEnumerable<TicketCommentDto>    Comments    { get; set; } = new List<TicketCommentDto>();
    public IEnumerable<TicketAttachmentDto> Attachments { get; set; } = new List<TicketAttachmentDto>();
}
