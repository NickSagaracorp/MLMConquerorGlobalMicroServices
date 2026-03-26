using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CancelCommission;

public record CancelCommissionCommand(string CommissionId) : IRequest<Result<bool>>;
