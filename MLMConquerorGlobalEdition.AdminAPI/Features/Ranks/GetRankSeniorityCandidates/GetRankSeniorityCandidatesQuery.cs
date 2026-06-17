using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankSeniorityCandidates;

public record GetRankSeniorityCandidatesQuery(int? RankDefinitionId, int MinDays = 14,
    int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<RankSeniorityRowDto>>>;
