using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs.Auth;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(string UserId, ChangePasswordRequest Request) : IRequest<Result<bool>>;
