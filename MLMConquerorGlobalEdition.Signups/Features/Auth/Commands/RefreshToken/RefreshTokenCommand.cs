using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs.Auth;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string Token) : IRequest<Result<AuthResponse>>;
