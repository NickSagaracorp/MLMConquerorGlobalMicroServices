using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPayments;

public record GetAdminPaymentsQuery(PagedRequest Page, string? StatusFilter, string? MemberId)
    : IRequest<Result<PagedResult<AdminPaymentDto>>>;
