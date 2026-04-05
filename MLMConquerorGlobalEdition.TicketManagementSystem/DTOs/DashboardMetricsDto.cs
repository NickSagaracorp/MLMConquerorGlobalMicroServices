namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class DashboardMetricsDto
{
    // 4 core metrics
    public double FrtMinutes { get; set; }
    public double FrtDeltaPercent { get; set; }
    public double MttrMinutes { get; set; }
    public double MttrDeltaPercent { get; set; }
    public double FcrPercent { get; set; }
    public double FcrDeltaPercent { get; set; }
    public double CsatAverage { get; set; }
    public double CsatDelta { get; set; }

    // Tickets by status (real-time)
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int WaitingCustomerCount { get; set; }
    public int EscalatedCount { get; set; }
    public int ResolvedCount { get; set; }

    // SLA
    public double SlaComplianceRate { get; set; }
    public int FrtBreachesToday { get; set; }
    public int ResolutionBreachesToday { get; set; }

    // Distributions
    public Dictionary<string, int> TicketsByChannel { get; set; } = new();
    public Dictionary<string, int> TicketsByCategory { get; set; } = new();

    // Agent workload
    public List<AgentWorkloadDto> AgentWorkloads { get; set; } = new();

    public DateTime GeneratedAt { get; set; }
}

public class DashboardTrendDto
{
    public DateTime Date { get; set; }
    public int Created { get; set; }
    public int Resolved { get; set; }
}
