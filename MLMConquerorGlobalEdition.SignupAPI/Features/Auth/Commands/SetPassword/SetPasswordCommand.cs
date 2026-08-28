using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.SetPassword;

/// <param name="UserId">
/// Sale de las claims del token de acceso, nunca del cuerpo: si el identificador viniera de quien
/// llama, este comando fijaría la contraseña de la cuenta ajena que se le indicara.
/// </param>
public record SetPasswordCommand(string UserId, SetPasswordRequest Request) : IRequest<Result<bool>>;
