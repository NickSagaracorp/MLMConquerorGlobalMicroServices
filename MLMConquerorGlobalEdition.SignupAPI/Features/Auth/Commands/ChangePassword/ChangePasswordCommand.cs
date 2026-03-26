using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(string UserId, ChangePasswordRequest Request) : IRequest<Result<bool>>;
