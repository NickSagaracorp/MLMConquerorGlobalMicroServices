using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Tokens.ValidateToken;

public record ValidateTokenQuery(ValidateTokenRequest Request) : IRequest<Result<ValidateTokenResponse>>;
