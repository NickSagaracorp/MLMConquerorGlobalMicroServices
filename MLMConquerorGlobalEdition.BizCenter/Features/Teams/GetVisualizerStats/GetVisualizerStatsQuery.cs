using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerStats;

/// <summary>
/// Returns aggregated member status counts for the current user's entire enrollment downline.
/// </summary>
public record GetVisualizerStatsQuery : IRequest<Result<EnrollmentVisualizerStatsDto>>;
