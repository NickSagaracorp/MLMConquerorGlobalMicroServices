using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.VerifyTwoFactor;

public record VerifyTwoFactorCommand(VerifyTwoFactorRequest Request) : IRequest<Result<AuthResponse>>;
