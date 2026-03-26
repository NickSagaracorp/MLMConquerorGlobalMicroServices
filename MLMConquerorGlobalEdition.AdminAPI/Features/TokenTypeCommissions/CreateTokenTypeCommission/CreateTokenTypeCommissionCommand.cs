using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.CreateTokenTypeCommission;

public record CreateTokenTypeCommissionCommand(CreateTokenTypeCommissionRequest Request)
    : IRequest<Result<int>>;
