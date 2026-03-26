using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;

public record LoginCommand(LoginRequest Request) : IRequest<Result<AuthResponse>>;
