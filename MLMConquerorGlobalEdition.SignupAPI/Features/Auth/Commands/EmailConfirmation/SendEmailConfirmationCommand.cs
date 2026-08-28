using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;

public record SendEmailConfirmationCommand(string Email) : IRequest<Result<bool>>;
