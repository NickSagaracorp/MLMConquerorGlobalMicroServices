namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class TicketAttachmentDto
{
    public long Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}
