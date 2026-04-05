using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class TicketMetricsDaily : AuditChangesIntKey
{
    public DateTime Date { get; set; }
    public int TotalCreated { get; set; }
    public int TotalResolved { get; set; }
    public int TotalClosed { get; set; }
    public double AvgFirstResponseMinutes { get; set; }
    public double AvgResolutionMinutes { get; set; }
    public double FirstContactResolutionRate { get; set; }
    public double SlaComplianceRate { get; set; }
    public double CsatAverage { get; set; }
    public int CsatResponseCount { get; set; }
    public int FrtBreaches { get; set; }
    public int ResolutionBreaches { get; set; }
    public int DeflectionAttempts { get; set; }
    public int DeflectionSuccesses { get; set; }
    public string TicketsByPriorityJson { get; set; } = "{}";
    public string TicketsByCategoryJson { get; set; } = "{}";
    public string TicketsByChannelJson { get; set; } = "{}";
    public string TicketsByAgentJson { get; set; } = "{}";
}
