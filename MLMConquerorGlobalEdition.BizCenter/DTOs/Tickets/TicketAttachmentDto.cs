namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;

/// <summary>
/// Attachment projection returned alongside a ticket detail. The <see cref="DownloadUrl"/>
/// is an absolute URL that points to the static-files endpoint serving uploaded files.
/// </summary>
public class TicketAttachmentDto
{
    public long     Id            { get; set; }
    public string   FileName      { get; set; } = string.Empty;
    public long     FileSizeBytes { get; set; }
    public string   ContentType   { get; set; } = string.Empty;
    public string   DownloadUrl   { get; set; } = string.Empty;
    public DateTime CreationDate  { get; set; }
    public string   UploadedBy    { get; set; } = string.Empty;
}
