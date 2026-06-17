using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GrantRankSeniorityBonus;

public record GrantRankSeniorityBonusCommand(string MemberId, int RankDefinitionId) : IRequest<Result<string>>;
