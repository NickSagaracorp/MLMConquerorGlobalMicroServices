using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Toda acción del BizCenter que escribe en las wallets de cobro tiene que exigir step-up. Esta
/// prueba recorre el controlador entero, no una lista de nombres: si mañana alguien agrega una
/// puerta nueva sin el atributo, falla aquí y no en producción.
/// </summary>
public class PuertasDeLaWalletTests
{
    private static IEnumerable<MethodInfo> AccionesQueEscribenWallets() =>
        typeof(WalletController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .Any(a => a.HttpMethods.Any(v => v != "GET")
                       && (a.Template ?? string.Empty).StartsWith("wallet", StringComparison.OrdinalIgnoreCase)));

    [Fact]
    public void Hay_al_menos_las_dos_puertas_conocidas()
    {
        AccionesQueEscribenWallets().Select(m => m.Name)
            .Should().Contain(new[] { nameof(WalletController.SaveWallet), nameof(WalletController.SetDefault) });
    }

    [Fact]
    public void Toda_accion_que_escribe_wallets_exige_step_up_de_cambio_de_cobro()
    {
        foreach (var action in AccionesQueEscribenWallets())
        {
            // Sin atributo no se puede comparar nada: un `?.` aquí dejaría pasar justo la puerta desprotegida.
            var atributo = action.GetCustomAttribute<RequiresStepUpAttribute>();
            atributo.Should().NotBeNull($"{action.Name} cambia a dónde se paga y debe exigir step-up");
            atributo!.OperationKey.Should().Be(StepUpOperations.PayoutMethodChange, $"{action.Name} cambia a dónde se paga");
        }
    }

    [Fact]
    public void La_ruta_vieja_PUT_wallet_ya_no_existe()
    {
        typeof(WalletController).GetMethod("UpdateWallet").Should().BeNull(
            "sobrescribía la cuenta de cobro sin historial y sin step-up");
    }
}
