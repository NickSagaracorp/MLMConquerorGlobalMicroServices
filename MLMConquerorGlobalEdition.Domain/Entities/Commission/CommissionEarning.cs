using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

public class CommissionEarning : AuditChangesStringKey
{
    public string BeneficiaryMemberId { get; set; } = string.Empty;
    public string? SourceMemberId { get; set; }
    public string? SourceOrderId { get; set; }
    public int CommissionTypeId { get; set; }
    public decimal Amount { get; set; }
    public CommissionEarningStatus Status { get; set; } = CommissionEarningStatus.Pending;
    public DateTime EarnedDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime? PeriodDate { get; set; }
    public virtual CommissionType? CommissionType { get; set; }
    public virtual CommissionOperationType? CommissionOperationType { get; set; }
    public bool IsManualEntry { get; set; }
    public string? CsvImportBatchId { get; set; }
    public string? Notes { get; set; }



    public void Cancel(string reason)
    {
        if (Status == CommissionEarningStatus.Paid)
            throw new CommissionAlreadyPaidException();

        Status = CommissionEarningStatus.Cancelled;
        Notes = reason;
    }
}
