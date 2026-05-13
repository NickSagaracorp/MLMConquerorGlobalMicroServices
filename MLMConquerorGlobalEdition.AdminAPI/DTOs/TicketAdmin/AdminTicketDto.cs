namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;

public class AdminTicketDto
{
    public string Id { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? AssignedToUserId { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime CreationDate { get; set; }
    public int CommentCount { get; set; }
}

public class AdminTicketCommentDto
{
    public long Id { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsStaff { get; set; }
    public DateTime CreationDate { get; set; }
}

public class AdminTicketDetailDto : AdminTicketDto
{
    public IEnumerable<AdminTicketCommentDto>    Comments    { get; set; } = new List<AdminTicketCommentDto>();
    public IEnumerable<AdminTicketAttachmentDto> Attachments { get; set; } = new List<AdminTicketAttachmentDto>();
}

public class AdminTicketAttachmentDto
{
    public long     Id            { get; set; }
    public string   FileName      { get; set; } = string.Empty;
    public long     FileSizeBytes { get; set; }
    public string   ContentType   { get; set; } = string.Empty;
    public string   DownloadUrl   { get; set; } = string.Empty;
    public DateTime CreationDate  { get; set; }
    public string   UploadedBy    { get; set; } = string.Empty;
}
