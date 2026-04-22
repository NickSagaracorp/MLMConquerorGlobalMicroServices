using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeNode;

public record GetDualTreeNodeQuery(string NodeMemberId)
    : IRequest<Result<DualTreeNodeDto>>;
