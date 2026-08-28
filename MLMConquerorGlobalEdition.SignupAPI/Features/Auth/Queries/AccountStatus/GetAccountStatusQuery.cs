using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.AccountStatus;

/// <param name="UserId">
/// Sale de las claims del token de acceso, nunca del cuerpo ni de la query: nadie debe poder
/// consultar el estado de la cuenta de otro.
/// </param>
public record GetAccountStatusQuery(string UserId) : IRequest<Result<AccountStatusResponse>>;
