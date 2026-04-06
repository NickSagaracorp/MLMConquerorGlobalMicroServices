using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPlacementHistory;

public record GetPlacementHistoryQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PlacementHistoryDto>>>;
