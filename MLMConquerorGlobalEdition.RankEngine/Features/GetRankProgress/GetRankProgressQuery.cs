using MediatR;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;

public record GetRankProgressQuery(string MemberId) : IRequest<Result<RankProgressResponse>>;
