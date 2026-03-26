namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;

public class FinancialDashboardDto
{
    public int TotalMembersActive { get; set; }
    public decimal TotalCommissionsPaid { get; set; }
    public decimal TotalCommissionsPending { get; set; }
    public decimal TotalRevenueThisMonth { get; set; }
}
