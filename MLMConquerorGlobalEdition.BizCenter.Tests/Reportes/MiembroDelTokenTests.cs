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
        (typeof(CommissionsController), nameof(CommissionsController.GetDailyResiduals)),
        (typeof(TeamController), nameof(TeamController.GetEnrollmentCustomersGrid)),
        (typeof(TeamReportsController), nameof(TeamReportsController.GetSetup)),
        (typeof(TeamReportsController), nameof(TeamReportsController.GetActivityColumns)),
        (typeof(TeamReportsController), nameof(TeamReportsController.GetActivityGrid)),
        (typeof(TeamReportsController), nameof(TeamReportsController.GetStatusChangesGrid)),
        (typeof(TeamEventsController), nameof(TeamEventsController.GetActive)),
        (typeof(TeamEventsController), nameof(TeamEventsController.GetTeamRegistrationsGrid)),
        (typeof(TeamEventsController), nameof(TeamEventsController.GetTeamRegistrationsTotals)),
        (typeof(AnnualFeeController), nameof(AnnualFeeController.Get)),
        (typeof(AnnualFeeController), nameof(AnnualFeeController.SetPreference)),
        (typeof(AnnualFeeController), nameof(AnnualFeeController.Pay)),
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
