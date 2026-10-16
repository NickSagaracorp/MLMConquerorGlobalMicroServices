using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Regla del Plan J: el miembro sale SIEMPRE del token. Ninguna acción nueva del BizCenter recibe
/// un memberId, ni suelto ni dentro del cuerpo. Los parámetros [FromServices] (el servicio, el
/// usuario actual) no vienen de la petición y no se miran. Cada bloque añade aquí sus acciones.
/// </summary>
public class MiembroDelTokenTests
{
    public static IEnumerable<object[]> Acciones() => new (Type Controlador, string Accion)[]
    {
        (typeof(MyLinksController), nameof(MyLinksController.Get)),
        // (acciones de los bloques siguientes)
    }.Select(a => new object[] { a.Controlador, a.Accion });

    [Theory]
    [MemberData(nameof(Acciones))]
    public void Ninguna_accion_nueva_recibe_un_memberId(Type controlador, string accion)
    {
        var metodo = controlador.GetMethod(accion, BindingFlags.Public | BindingFlags.Instance);
        metodo.Should().NotBeNull($"{controlador.Name}.{accion} tiene que existir");

        foreach (var parametro in metodo!.GetParameters()
                     .Where(p => p.GetCustomAttribute<FromServicesAttribute>() is null))
        {
            parametro.Name!.Should().NotContainEquivalentOf("memberId",
                $"{controlador.Name}.{accion} saca el miembro del token");
            parametro.ParameterType.GetProperty("MemberId").Should().BeNull(
                $"el cuerpo de {controlador.Name}.{accion} no puede traer un MemberId");
        }
    }
}
