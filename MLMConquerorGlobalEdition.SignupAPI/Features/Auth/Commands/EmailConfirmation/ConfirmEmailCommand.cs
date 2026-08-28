using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;

public record ConfirmEmailCommand(ConfirmEmailRequest Request) : IRequest<Result<bool>>;
