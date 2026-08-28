using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.PersonalData;

/// <param name="UserId">
/// Sale de las claims del token de acceso, nunca del cuerpo ni de la query. Aquí importa más que
/// en ningún otro sitio: si el identificador viniera de quien llama, este endpoint descargaría los
/// datos personales de cualquier cuenta con solo cambiar un número.
/// </param>
public record GetPersonalDataQuery(string UserId) : IRequest<Result<PersonalDataResponse>>;
