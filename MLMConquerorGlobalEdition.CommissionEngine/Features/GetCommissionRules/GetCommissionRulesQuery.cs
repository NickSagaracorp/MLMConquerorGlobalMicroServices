using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionRules;

public record GetCommissionRulesQuery : IRequest<Result<List<CommissionTypeResponse>>>;
