using MediatR;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;

public record EvaluateRankCommand(string MemberId) : IRequest<Result<RankEvaluationResponse>>;
