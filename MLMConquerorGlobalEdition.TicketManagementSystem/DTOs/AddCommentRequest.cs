namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class AddCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
}
