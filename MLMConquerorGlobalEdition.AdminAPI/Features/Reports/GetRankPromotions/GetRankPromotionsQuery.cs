using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Reports;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Reports.GetRankPromotions;

/// <summary>
/// Returns ambassadors who achieved a rank FOR THE FIRST TIME within the specified date range.
/// Re-promotions (member who previously held the same rank and re-achieved it) are excluded.
/// </summary>
public record GetRankPromotionsQuery(
    DateTime From,
    DateTime To,
    int?     RankDefinitionId = null) : IRequest<Result<IEnumerable<RankPromotionDto>>>;
