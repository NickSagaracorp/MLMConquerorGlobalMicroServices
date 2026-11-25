using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccessAuditGrid;

public record GetAccessAuditGridQuery(GridDataRequest Grid) : IRequest<Result<PagedResult<AccessAuditEntryDto>>>;
