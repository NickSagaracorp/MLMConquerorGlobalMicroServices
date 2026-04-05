namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class SlaPolicyDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int FirstResponseCriticalMinutes { get; set; }
    public int FirstResponseHighMinutes { get; set; }
    public int FirstResponseNormalMinutes { get; set; }
    public int FirstResponseLowMinutes { get; set; }
    public int ResolutionCriticalMinutes { get; set; }
    public int ResolutionHighMinutes { get; set; }
    public int ResolutionNormalMinutes { get; set; }
    public int ResolutionLowMinutes { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string WorkdaysJson { get; set; } = string.Empty;
    public string BusinessHoursStart { get; set; } = string.Empty;
    public string BusinessHoursEnd { get; set; } = string.Empty;
    public int WarningThresholdPercent { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSlaPolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int FirstResponseCriticalMinutes { get; set; } = 15;
    public int FirstResponseHighMinutes { get; set; } = 60;
    public int FirstResponseNormalMinutes { get; set; } = 240;
    public int FirstResponseLowMinutes { get; set; } = 480;
    public int ResolutionCriticalMinutes { get; set; } = 240;
    public int ResolutionHighMinutes { get; set; } = 480;
    public int ResolutionNormalMinutes { get; set; } = 1440;
    public int ResolutionLowMinutes { get; set; } = 4320;
    public string Timezone { get; set; } = "UTC";
    public string WorkdaysJson { get; set; } = "[1,2,3,4,5]";
    public string BusinessHoursStart { get; set; } = "08:00";
    public string BusinessHoursEnd { get; set; } = "18:00";
    public int WarningThresholdPercent { get; set; } = 80;
    public int NotifyAgentAtMinutes { get; set; } = 30;
    public int NotifySupervisorAtMinutes { get; set; } = 60;
    public int NotifyManagerAtMinutes { get; set; } = 120;
    public bool IsDefault { get; set; }
}
