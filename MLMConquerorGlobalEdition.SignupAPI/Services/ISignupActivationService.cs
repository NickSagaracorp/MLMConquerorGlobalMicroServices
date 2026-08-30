using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Lo que pasa cuando el dinero de un alta está cobrado: se cierra el pedido, se activan miembro
/// y suscripción, se encolan los deltas del upline y se calculan las comisiones.
///
/// Existe porque ahora hay DOS momentos en los que eso ocurre. Con tarjeta, con token y con código
/// de descuento ocurre al completar el alta, porque el cobro ya está hecho. Con cripto ocurre más
/// tarde, cuando alguien de la casa confirma a mano que la transferencia entró. Escribirlo dos
/// veces sería tener dos versiones de las comisiones de alta, y la segunda se quedaría atrás.
///
/// No llama a SaveChangesAsync: quien lo invoca decide la frontera de la transacción, y en el
/// caso de la confirmación manual eso importa —el token de concurrencia de la fila de
/// confirmación tiene que viajar en el mismo UPDATE que las comisiones—.
/// </summary>
public interface ISignupActivationService
{
    Task ActivateAsync(
        Orders order,
        MemberProfile member,
        MembershipSubscription subscription,
        int totalQualificationPoints,
        DateTime now,
        string actorEmail,
        CancellationToken ct);
}
