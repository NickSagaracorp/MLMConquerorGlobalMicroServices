using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetBranchDetail;

public record GetBranchDetailQuery(string BranchMemberId)
    : IRequest<Result<BranchDetailDto>>;
