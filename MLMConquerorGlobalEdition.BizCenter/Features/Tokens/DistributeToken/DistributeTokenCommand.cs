using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;

public record DistributeTokenCommand(DistributeTokenRequest Request) : IRequest<Result<bool>>;
