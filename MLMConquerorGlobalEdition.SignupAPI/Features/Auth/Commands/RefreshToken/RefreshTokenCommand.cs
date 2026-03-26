using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string Token) : IRequest<Result<AuthResponse>>;
