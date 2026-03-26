namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;

public class HealthDashboardDto
{
    public int ActiveMembers { get; set; }
    public int PendingPayments { get; set; }
    public int OpenTickets { get; set; }
    public string Status { get; set; } = "healthy";
}
