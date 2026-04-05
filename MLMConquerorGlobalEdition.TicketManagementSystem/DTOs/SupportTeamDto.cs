namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class SupportTeamDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SupervisorAgentId { get; set; }
    public string RoutingMethod { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int AgentCount { get; set; }
}

public class CreateSupportTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SupervisorAgentId { get; set; }
    public string RoutingMethod { get; set; } = "round_robin";
}

public class AgentWorkloadDto
{
    public string AgentId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Tier { get; set; }
    public string Availability { get; set; } = string.Empty;
    public int CurrentTicketCount { get; set; }
    public int MaxConcurrentTickets { get; set; }
    public double WorkloadPercent { get; set; }
}

public class CreateSupportAgentRequest
{
    public string UserId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public int Tier { get; set; } = 1;
    public string SkillsJson { get; set; } = "[]";
    public string LanguagesJson { get; set; } = "[\"es\"]";
    public int MaxConcurrentTickets { get; set; } = 10;
}
