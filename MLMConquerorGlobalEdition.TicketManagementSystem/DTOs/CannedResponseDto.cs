namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class CannedResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string TagsJson { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public class CreateCannedResponseRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string Scope { get; set; } = "global";       // global | team | personal
    public int? TeamId { get; set; }
}

public class ApplyCannedResponseRequest
{
    public string CannedResponseId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
