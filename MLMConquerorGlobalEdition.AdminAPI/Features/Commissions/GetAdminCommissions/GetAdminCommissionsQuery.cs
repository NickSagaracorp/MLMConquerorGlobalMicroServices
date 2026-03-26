using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissions;

public record GetAdminCommissionsQuery(PagedRequest Page) : IRequest<Result<PagedResult<AdminCommissionDto>>>;
