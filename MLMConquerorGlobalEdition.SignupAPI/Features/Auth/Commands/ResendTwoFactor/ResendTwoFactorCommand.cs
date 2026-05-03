using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResendTwoFactor;

public record ResendTwoFactorCommand(ResendTwoFactorRequest Request) : IRequest<Result<AuthResponse>>;
