namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class TicketDto
{
    public string Id { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string EscalationLevel { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public int? AssignedTeamId { get; set; }
    public string? SlaPolicyId { get; set; }
    public DateTime? SlaResolutionDue { get; set; }
    public bool IsSlaBreached { get; set; }
    public double? SlaPercentElapsed { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? CsatScore { get; set; }
    public DateTime CreationDate { get; set; }
    public int CommentCount { get; set; }
}
