using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GrantTokens;

public record GrantTokensCommand(AdminGrantTokenRequest Request) : IRequest<Result<AdminTokenBalanceDto>>;
