using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.UpdateTokenBalance;

public record UpdateTokenBalanceCommand(string TokenBalanceId, AdminUpdateTokenBalanceRequest Request)
    : IRequest<Result<AdminTokenBalanceDto>>;
