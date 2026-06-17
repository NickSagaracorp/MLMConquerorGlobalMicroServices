using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankFirstAchievements;

/// <param name="RankDefinitionId">Null = all ranks.</param>
public record GetRankFirstAchievementsQuery(int Year, int Month, int? RankDefinitionId,
    int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<RankFirstAchievementRowDto>>>;
