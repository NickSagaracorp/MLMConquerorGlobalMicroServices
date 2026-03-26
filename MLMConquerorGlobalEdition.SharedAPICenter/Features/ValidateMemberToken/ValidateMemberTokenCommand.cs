using MediatR;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.ValidateMemberToken;

/// <summary>
/// Command dispatched by POST /api/v1/external/member/validate-token.
/// Checks whether the member holds sufficient balance of the specified token type.
/// </summary>
/// <param name="Request">Validated inbound request body.</param>
public record ValidateMemberTokenCommand(ValidateTokenRequest Request)
    : IRequest<Result<ValidateTokenResponse>>;
