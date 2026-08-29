using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;

/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record ConfirmAccountEnrollmentCommand(string UserId, ConfirmAccountEnrollmentRequest Request)
    : IRequest<Result<bool>>;
