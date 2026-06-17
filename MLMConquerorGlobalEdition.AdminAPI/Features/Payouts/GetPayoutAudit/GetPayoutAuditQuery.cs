using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAudit;

public record GetPayoutAuditQuery(
    DateTime? From,
    DateTime? To,
    string? MemberId,
    WalletType? WalletType,
    string? Outcome,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PayoutAuditRowDto>>>;
