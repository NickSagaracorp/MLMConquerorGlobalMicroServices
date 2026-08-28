using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;

public record ConfirmEnrollmentCommand(ConfirmEnrollmentRequest Request) : IRequest<Result<AuthResponse>>;
