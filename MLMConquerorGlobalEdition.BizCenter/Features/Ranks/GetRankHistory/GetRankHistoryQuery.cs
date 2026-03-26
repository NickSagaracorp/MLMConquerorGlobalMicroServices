using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankHistory;

public record GetRankHistoryQuery(int Page, int PageSize) : IRequest<Result<PagedResult<RankHistoryDto>>>;
