using MediatR;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetRankDefinitions;

public record GetRankDefinitionsQuery : IRequest<Result<List<RankDefinitionResponse>>>;
