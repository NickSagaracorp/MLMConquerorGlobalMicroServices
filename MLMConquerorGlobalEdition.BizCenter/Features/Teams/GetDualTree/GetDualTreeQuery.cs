using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTree;

public record GetDualTreeQuery() : IRequest<Result<IEnumerable<DualTreeMemberDto>>>;
