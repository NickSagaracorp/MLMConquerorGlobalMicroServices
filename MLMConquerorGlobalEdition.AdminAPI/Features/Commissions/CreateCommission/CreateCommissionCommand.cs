using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CreateCommission;

public record CreateCommissionCommand(CreateCommissionRequest Request) : IRequest<Result<AdminCommissionDto>>;
