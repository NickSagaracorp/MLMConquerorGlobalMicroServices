using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class SlaPolicy : AuditChangesStringKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // First Response targets (minutes)
    public int FirstResponseCriticalMinutes { get; set; } = 15;
    public int FirstResponseHighMinutes { get; set; } = 60;
    public int FirstResponseNormalMinutes { get; set; } = 240;
    public int FirstResponseLowMinutes { get; set; } = 480;

    // Resolution targets (minutes)
    public int ResolutionCriticalMinutes { get; set; } = 240;
    public int ResolutionHighMinutes { get; set; } = 480;
    public int ResolutionNormalMinutes { get; set; } = 1440;
    public int ResolutionLowMinutes { get; set; } = 4320;

    // Business hours
    public string Timezone { get; set; } = "UTC";
    public string WorkdaysJson { get; set; } = "[1,2,3,4,5]";   // ISO weekday: Mon=1 … Sun=7
    public string BusinessHoursStart { get; set; } = "08:00";
    public string BusinessHoursEnd { get; set; } = "18:00";

    // Escalation thresholds
    public int WarningThresholdPercent { get; set; } = 80;
    public int NotifyAgentAtMinutes { get; set; } = 30;
    public int NotifySupervisorAtMinutes { get; set; } = 60;
    public int NotifyManagerAtMinutes { get; set; } = 120;

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
