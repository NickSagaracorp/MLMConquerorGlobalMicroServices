namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;

public class AdminAddCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}
