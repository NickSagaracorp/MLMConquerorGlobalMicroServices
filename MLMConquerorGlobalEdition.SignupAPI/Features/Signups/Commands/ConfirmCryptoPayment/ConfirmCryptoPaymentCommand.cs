using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;

/// <summary>
/// Confirma a mano que el cobro en cripto de un alta entró.
///
/// ConfirmedByUserId y ConfirmedByEmail los pone el CONTROLADOR desde los claims del JWT, nunca
/// el cuerpo de la petición: si el que aprueba pudiera escribir a nombre de quién queda el
/// rastro, el rastro no sirve de nada.
/// </summary>
public record ConfirmCryptoPaymentCommand(
    string OrderId,
    ConfirmCryptoPaymentRequest Request,
    string ConfirmedByUserId,
    string ConfirmedByEmail) : IRequest<Result<ConfirmCryptoPaymentResponse>>;
