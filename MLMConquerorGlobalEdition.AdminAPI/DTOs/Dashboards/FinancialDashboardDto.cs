namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;

public class FinancialDashboardDto
{
    /// <summary>Inclusive start of the analysis window (UTC).</summary>
    public DateTime RangeFrom { get; set; }
    /// <summary>Inclusive end of the analysis window (UTC).</summary>
    public DateTime RangeTo { get; set; }

    /// <summary>Currently-active members (point-in-time snapshot, not range-bound).</summary>
    public int TotalMembersActive { get; set; }
    /// <summary>Members whose EnrollDate falls within the window.</summary>
    public int NewMembersInRange { get; set; }

    /// <summary>Order revenue (inflow) within the window.</summary>
    public decimal TotalRevenue { get; set; }
    /// <summary>Commissions EARNED within the window with status Paid.</summary>
    public decimal TotalCommissionsPaid { get; set; }
    /// <summary>Commissions EARNED within the window still Pending.</summary>
    public decimal TotalCommissionsPending { get; set; }

    /// <summary>Revenue minus total commissions (paid + pending) earned in the window.</summary>
    public decimal NetCashFlow { get; set; }
    /// <summary>Total commissions (paid + pending) as a % of revenue in the window (0 when no revenue).</summary>
    public decimal CommissionToRevenuePct { get; set; }
}
