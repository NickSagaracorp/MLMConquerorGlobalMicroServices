namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class AddAttachmentRequest
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
}
