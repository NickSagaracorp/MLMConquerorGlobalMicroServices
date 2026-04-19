using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsBreakdown;

/// <summary>Returns commission breakdown grouped by type for a payment date. For pending commissions, supply EarnedDate to narrow to that specific batch.</summary>
public record GetCommissionsBreakdownQuery(DateTime PaymentDate, DateTime? EarnedDate = null) : IRequest<Result<List<CommissionBreakdownDto>>>;
