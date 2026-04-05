namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class SlaStatusDto
{
    public string TicketId { get; set; } = string.Empty;
    public string? SlaPolicyId { get; set; }
    public DateTime? SlaFirstResponseDue { get; set; }
    public DateTime? SlaFirstResponseAt { get; set; }
    public DateTime? SlaResolutionDue { get; set; }
    public bool IsSlaFirstResponseBreached { get; set; }
    public bool IsSlaResolutionBreached { get; set; }
    public bool IsSlaPaused { get; set; }
    public double RemainingResolutionMinutes { get; set; }
    public double PercentElapsed { get; set; }
    public string StatusColor { get; set; } = "green";   // green | yellow | red
}
