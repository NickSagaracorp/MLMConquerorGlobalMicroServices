using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using MLMConquerorGlobalEdition.CommissionEngine.Controllers;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests;

/// <summary>
/// LAS SIETE RUTAS QUE MUEVEN DINERO DEL UPLINE, Y QUIÉN LAS ALCANZA.
/// </summary>
/// <remarks>
/// LLEVABAN <c>Roles = "Admin"</c> A SECAS y eran las siete únicas de toda la solución que nombran
/// <c>Admin</c> sin <c>SuperAdmin</c>: de los 105 atributos que mencionan <c>Admin</c>, 98 incluyen
/// también <c>SuperAdmin</c> y los 7 que no estaban todos en este archivo. No era una decisión: las
/// listas que sí están recortadas a propósito en este repositorio
/// —<see cref="AppRoles.CryptoPaymentApprovers"/>, la de tesorería de <c>BillingController</c> y el
/// <c>SuperAdmin</c> solo de <c>SystemUsersController</c>— llevan escrito por qué, y esta no.
/// El efecto era que la cuenta con más privilegios del sistema recibía un 403 al relanzar un
/// cálculo.
///
/// LA LISTA SALE DE LA SUPERFICIE QUE YA EXISTE: <c>AdminCommissionsController</c> y
/// <c>AdminMemberCommissionsController</c>, las dos pantallas desde las que un humano llega aquí,
/// usan <c>SuperAdmin,Admin</c>. Es la misma con la que están cerradas otras 44 rutas de la
/// solución.
/// </remarks>
public class RolesDeCalculoTests
{
    /// <summary>Las siete que lanzan o deshacen un cálculo.</summary>
    public static TheoryData<string> LasSieteDeCalculo() => new()
    {
        "CalculateFastStartBonus",
        "CalculateDailyResidual",
        "CalculateBoostBonus",
        "CalculatePresidentialBonus",
        "CalculateMatchingBonus",
        "CalculateSponsorBonus",
        "ReverseSponsorBonus"
    };

    [Theory]
    [MemberData(nameof(LasSieteDeCalculo))]
    public void CadaCalculo_IncluyeASuperAdmin(string metodo)
    {
        var atributo = typeof(CommissionsController)
            .GetMethod(metodo)!
            .GetCustomAttribute<AuthorizeAttribute>(inherit: false);

        atributo.Should().NotBeNull($"{metodo} mueve dinero del upline");

        atributo!.Roles!.Split(',', StringSplitOptions.TrimEntries)
            .Should().BeEquivalentTo([AppRoles.SuperAdmin, AppRoles.Admin],
                "es la lista de AdminCommissionsController, que es la pantalla desde la que se " +
                "llega a esto. Dejar fuera a SuperAdmin no era una política: era un olvido");
    }

    /// <summary>
    /// La cobertura importa tanto como el contenido: si mañana aparece un método de cálculo nuevo
    /// sin atributo, la lista de arriba no lo ve. Esta prueba comprueba que no queda ninguna ruta
    /// de escritura de este controlador sin lista de roles.
    /// </summary>
    [Fact]
    public void NingunaRutaDeEscritura_SeQuedaSinListaDeRoles()
    {
        var sinRoles = typeof(CommissionsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.HttpPostAttribute>(false).Any()
                     || m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.HttpPutAttribute>(false).Any()
                     || m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>(false).Any())
            .Where(m => m.GetCustomAttribute<AuthorizeAttribute>(inherit: false)?.Roles is null)
            .Select(m => m.Name)
            .ToArray();

        sinRoles.Should().BeEmpty(
            "el [Authorize] pelado de la clase solo comprueba que HAY sesión; una ruta de cálculo " +
            "sin lista de roles la alcanza cualquier miembro autenticado");
    }

    /// <summary>
    /// La otra mitad: las dos lecturas se quedan sin lista a propósito. Si alguien les pone roles,
    /// que sea decidiéndolo y no arrastrado por esta prueba.
    /// </summary>
    [Theory]
    [InlineData("GetRules")]
    [InlineData("GetCollection")]
    public void LasDosLecturas_NoSeCierranPorRol(string metodo)
    {
        typeof(CommissionsController)
            .GetMethod(metodo)!
            .GetCustomAttribute<AuthorizeAttribute>(inherit: false)
            .Should().BeNull();
    }
}
