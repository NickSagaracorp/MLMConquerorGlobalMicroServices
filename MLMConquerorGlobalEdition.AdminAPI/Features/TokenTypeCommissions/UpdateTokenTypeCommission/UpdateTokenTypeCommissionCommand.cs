using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.UpdateTokenTypeCommission;

public record UpdateTokenTypeCommissionCommand(int Id, UpdateTokenTypeCommissionRequest Request)
    : IRequest<Result<bool>>;
