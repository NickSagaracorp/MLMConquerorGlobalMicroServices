using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusBranch;

public record GetCarBonusBranchQuery(string BranchMemberId)
    : IRequest<Result<CarBonusBranchDto>>;
