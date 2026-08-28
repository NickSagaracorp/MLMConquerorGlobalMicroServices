using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;

public record BeginEnrollmentCommand(BeginEnrollmentRequest Request) : IRequest<Result<EnrollmentResponse>>;
