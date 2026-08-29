using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;

/// <param name="UserId">Sale de las claims del token de acceso: no hay cuerpo que manipular.</param>
public record BeginAccountEnrollmentCommand(string UserId) : IRequest<Result<EnrollmentResponse>>;
