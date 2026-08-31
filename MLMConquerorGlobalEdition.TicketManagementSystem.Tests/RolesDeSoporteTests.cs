using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.TicketManagementSystem.Controllers;
using MLMConquerorGlobalEdition.TicketManagementSystem.Security;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests;

/// <summary>
/// LAS SUPERFICIES DE SOPORTE DECLARAN SUS ROLES EN LA TUBERÍA, no solo en el navegador.
/// </summary>
/// <remarks>
/// LO QUE HABÍA. Los seis controladores llevaban un <c>[Authorize]</c> pelado y toda la
/// autorización vivía dentro de los manejadores. Cuatro rutas de personal no tenían comprobación
/// ninguna —sus manejadores ni inyectan <c>ICurrentUserService</c>—: los equipos de soporte con su
/// supervisor, la matriz de SLA entera y el texto de todas las plantillas internas de respuesta se
/// leían con el token de un miembro cualquiera. Las páginas de AdminWeb sí declaraban los roles,
/// pero un atributo en un componente Blazor no protege una API: el navegador no es la puerta.
///
/// SE COMPRUEBA EL ATRIBUTO Y NO EL CÓDIGO DEL MANEJADOR, por lo mismo que en
/// <c>Billing.Tests/PropiedadDeLaCuentaTests</c>: quien decide un <c>[Authorize(Roles = …)]</c> es
/// la tubería, y una comprobación dentro del manejador ya se demostró que se olvida.
///
/// LAS LISTAS NO SE ESCRIBEN AQUÍ NI ALLÍ: salen de <see cref="HelpdeskRoles"/>, que a su vez las
/// compone de <see cref="AppRoles"/>. Si alguien cambia la lista en la página de AdminWeb y no
/// aquí, esta prueba no lo ve —eso ya sería otra prueba— pero al menos la API no puede acabar con
/// una lista inventada a mano en el propio controlador.
/// </remarks>
public class RolesDeSoporteTests
{
    public static TheoryData<Type, string> LasCuatroSuperficiesCerradas() => new()
    {
        { typeof(CannedResponsesController),   HelpdeskRoles.Soporte },
        { typeof(HelpdeskDashboardController), HelpdeskRoles.Soporte },
        { typeof(SlaController),               HelpdeskRoles.Coordinacion },
        { typeof(SupportAdminController),      HelpdeskRoles.Coordinacion }
    };

    [Theory]
    [MemberData(nameof(LasCuatroSuperficiesCerradas))]
    public void CadaSuperficieDeSoporte_ExigeSuListaDeRoles(Type controlador, string esperada)
    {
        var atributo = controlador.GetCustomAttribute<AuthorizeAttribute>(inherit: false);

        atributo.Should().NotBeNull($"{controlador.Name} es una superficie de personal");
        atributo!.Roles.Should().Be(esperada,
            "la lista sale de HelpdeskRoles, que la compone de AppRoles: escribirla a mano aquí " +
            "sería inventar una política nueva en un sitio nuevo");
    }

    [Theory]
    [MemberData(nameof(LasCuatroSuperficiesCerradas))]
    public void NingunMetodoDeEsasSuperficies_SeAbrePorSuCuenta(Type controlador, string _)
    {
        var abiertos = controlador
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<AllowAnonymousAttribute>(inherit: false).Any())
            .Select(m => m.Name)
            .ToArray();

        abiertos.Should().BeEmpty($"{controlador.Name} no tiene nada que ofrecer sin sesión");
    }

    /// <summary>
    /// Las cuatro escrituras de la base de conocimiento son de redacción; sus cuatro lecturas son el
    /// centro de ayuda del miembro y filtran por visibilidad. Por eso los roles bajan al método y no
    /// están en la clase — y por eso la prueba fija las dos mitades, no solo la cerrada.
    /// </summary>
    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Publish")]
    [InlineData("Delete")]
    public void LasEscriturasDeLaBaseDeConocimiento_ExigenRolDeSoporte(string metodo)
    {
        var atributo = typeof(KnowledgeBaseController)
            .GetMethod(metodo)!
            .GetCustomAttribute<AuthorizeAttribute>(inherit: false);

        atributo.Should().NotBeNull($"{metodo} escribe en la base de conocimiento");
        atributo!.Roles.Should().Be(HelpdeskRoles.Soporte);
    }

    [Theory]
    [InlineData("Search")]
    [InlineData("GetSuggestions")]
    [InlineData("GetBySlug")]
    public void LasLecturasDeLaBaseDeConocimiento_NoSeCierranPorRol(string metodo)
    {
        var atributo = typeof(KnowledgeBaseController)
            .GetMethod(metodo)!
            .GetCustomAttribute<AuthorizeAttribute>(inherit: false);

        atributo.Should().BeNull(
            "son el centro de ayuda del miembro y ya filtran por visibilidad: un no-agente solo ve " +
            "lo Public. Cerrarlas por rol convertiría la ayuda en una pantalla de personal");
    }

    /// <summary>
    /// <c>TicketsController</c> mezcla autoservicio del dueño del ticket con operaciones de
    /// personal, y sus manejadores ya distinguen las dos por la propiedad del ticket. Una lista de
    /// roles a nivel de clase dejaría fuera al miembro que abre su propio ticket, y no hay
    /// superficie equivalente de la que copiar una lista para una clase mixta. Se deja como está a
    /// propósito; esta prueba fija esa decisión para que se vea que no es un olvido.
    /// </summary>
    [Fact]
    public void ElControladorDeTickets_SeQuedaConAutorizacionSinRoles()
    {
        var atributo = typeof(TicketsController).GetCustomAttribute<AuthorizeAttribute>(inherit: false);

        atributo.Should().NotBeNull("sigue exigiendo sesión");
        atributo!.Roles.Should().BeNull(
            "es una superficie mixta: el miembro dueño del ticket lo crea, comenta y cierra, y el " +
            "personal asigna, fusiona y escala. Si alguien le pone una lista de roles aquí, tiene " +
            "que separar antes las rutas de autoservicio de las de personal");
    }

    /// <summary>
    /// Las dos listas salen de constantes de <see cref="AppRoles"/> y no de cadenas sueltas. Sin
    /// esto, un renombrado de rol dejaría una lista muerta que no protege a nadie y que compila.
    /// </summary>
    [Fact]
    public void LasListasSalenDeLasConstantesDeRoles()
    {
        HelpdeskRoles.Soporte.Split(',')
            .Should().BeEquivalentTo(
            [
                AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.SupportManager,
                AppRoles.SupportLevel1, AppRoles.SupportLevel2, AppRoles.SupportLevel3, AppRoles.IT
            ],
            "la misma lista que AdminCannedResponses.razor, AdminKnowledgeBase.razor y " +
            "AdminHelpdeskDashboard.razor");

        HelpdeskRoles.Coordinacion.Split(',')
            .Should().BeEquivalentTo(
            [
                AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.SupportManager, AppRoles.IT
            ],
            "la misma lista que AdminSlaPolicies.razor y AdminSupportAgents.razor: los niveles 1 a " +
            "3 quedan fuera de la configuración del soporte allí, y quedan fuera aquí");
    }
}
