using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAuditDetail;

public record GetPayoutAuditDetailQuery(long AttemptId) : IRequest<Result<PayoutAuditDetailDto>>;
