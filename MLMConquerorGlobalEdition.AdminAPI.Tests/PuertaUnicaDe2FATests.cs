using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StartImpersonation;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests;

/// <summary>
/// El guardián de la PUERTA ÚNICA por el lado de AdminAPI: aquí no se entra.
///
/// LO QUE PASÓ. AdminAPI tenía su propio <c>POST /api/v1/auth/login</c>. Comprobaba la contraseña,
/// leía los roles del usuario y devolvía un token de acceso COMPLETO CON ESOS ROLES. No mencionaba
/// el segundo factor en ninguna línea. Era peor que el bypass del reto que se cerró justo antes
/// —aquel token no llevaba claims de rol y este sí—, así que abría de una vez los casi trescientos
/// endpoints del panel protegidos por rol: quien tuviera la contraseña de un administrador entraba
/// entero, sin segundo factor. Lo usaba la MAUI de administración, que ahora entra por SignupAPI.
///
/// LO QUE ESTAS PRUEBAS VIGILAN. No que aquel archivo concreto siga borrado —eso lo sabría
/// cualquiera—, sino que no aparezca OTRO igual con otro nombre. Por eso no se busca
/// "AuthController": se busca la FORMA de una puerta, que es lo que se repetiría sin querer.
///
/// SI ALGUNA SE PONE EN ROJO: casi seguro que alguien está añadiendo una segunda puerta. La
/// autenticación —credenciales, segundo factor, enrolamiento, refresco, recuperación— vive entera
/// en SignupAPI. Lo que haga falta se añade allí, detrás del mismo reto, no aquí. La única excepción
/// admitida es la impersonación, y las pruebas de abajo dicen exactamente por qué.
/// </summary>
public class PuertaUnicaDe2FATests
{
    private static Assembly AdminApiAssembly => typeof(StartImpersonationHandler).Assembly;

    /// <summary>
    /// Que la prueba mire el ensamblado que cree. Sin esto, un renombrado la dejaría verde para
    /// siempre sin comprobar nada.
    /// </summary>
    [Fact]
    public void LasPruebasMiranElEnsambladoDeAdminApi()
    {
        AdminApiAssembly.GetName().Name.Should().Be("MLMConquerorGlobalEdition.AdminAPI");
        ControllerTypes().Should().NotBeEmpty("un ensamblado sin controladores significa que no se leyó bien");
    }

    /// <summary>
    /// NINGÚN endpoint de AdminAPI cuelga de <c>api/v1/auth</c>. Es la ruta de la puerta y es de
    /// SignupAPI; una ruta con ese prefijo aquí es, por definición, una segunda puerta.
    /// </summary>
    [Fact]
    public void AdminApi_NoExponeNingunaRutaDeAutenticacion()
    {
        var rutas = ControllerTypes()
            .SelectMany(RutasDe)
            .Where(r => r.Contains("auth", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        rutas.Should().BeEmpty(
            "la autenticación entera vive en SignupAPI, que es la única puerta y la única que sabe " +
            "de segundo factor. Un endpoint de auth en AdminAPI es una puerta paralela aunque hoy " +
            "no emita tokens: mañana los emitirá.");
    }

    /// <summary>
    /// Ningún endpoint de AdminAPI se llama "login", "signin" o "token", aunque cuelgue de otra
    /// ruta. Cubre al que evite el prefijo <c>auth</c> y monte la puerta en cualquier otro sitio.
    /// </summary>
    [Fact]
    public void AdminApi_NoTieneNingunEndpointConFormaDeLogin()
    {
        string[] sospechosas = ["login", "signin", "sign-in", "authenticate"];

        var rutas = ControllerTypes()
            .SelectMany(RutasDe)
            .Where(r => sospechosas.Any(s => r.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        rutas.Should().BeEmpty(
            "iniciar sesión se hace en SignupAPI. Si esto está rojo, mira si lo que se añadió " +
            "comprueba una contraseña: si lo hace, su sitio es SignupAPI, detrás del segundo factor.");
    }

    /// <summary>
    /// NINGÚN CONTROLADOR de AdminAPI pide <see cref="IJwtService"/>. Es el emisor de tokens de
    /// acceso, y un controlador que lo tenga a mano está a una línea de firmar uno.
    /// </summary>
    /// <remarks>
    /// La impersonación no cae aquí porque su controlador no lo pide: el que firma es el handler de
    /// MediatR que hay detrás, y ese está cubierto por la prueba siguiente.
    /// </remarks>
    [Fact]
    public void NingunControladorDeAdminApi_PideElEmisorDeTokens()
    {
        var infractores = ControllerTypes()
            .Where(PideElEmisorDeTokens)
            .Select(t => t.FullName!)
            .ToArray();

        infractores.Should().BeEmpty(
            "un controlador con IJwtService inyectado puede firmar un token de acceso con los " +
            "roles que quiera, y AdminAPI no valida ningún segundo factor. Lo que necesite emitir " +
            "una sesión va en SignupAPI.");
    }

    /// <summary>
    /// En TODO AdminAPI, el único que pide el emisor de tokens es el handler de la impersonación.
    /// </summary>
    /// <remarks>
    /// POR QUÉ LA IMPERSONACIÓN SÍ PUEDE. No es una puerta: no comprueba ninguna contraseña ni
    /// admite a nadie que no estuviera ya dentro. Para llegar a ella hay que presentar un token de
    /// administrador válido —emitido por SignupAPI, o sea, con el segundo factor ya pasado— y tener
    /// uno de los tres roles que la autorizan. El token que emite lleva los roles del MIEMBRO
    /// suplantado y el claim <c>impersonatedBy</c> con quién lo pidió.
    ///
    /// Esta lista es de UNO a propósito. Si mañana hay dos, alguien tendrá que escribir aquí por qué
    /// el segundo también es legítimo, que es justo la conversación que esta prueba existe para
    /// forzar.
    /// </remarks>
    [Fact]
    public void ElUnicoEmisorDeTokensDeAdminApi_EsLaImpersonacion()
    {
        var emisores = AdminApiAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(PideElEmisorDeTokens)
            .Select(t => t.FullName!)
            .OrderBy(n => n)
            .ToArray();

        emisores.Should().Equal(
            [typeof(StartImpersonationHandler).FullName!],
            "AdminAPI ya no tiene login: lo único que firma tokens aquí es la impersonación, y " +
            "quien la usa llegó con un token de SignupAPI, que es donde vive el segundo factor.");
    }

    /// <summary>
    /// La impersonación sigue cerrada por rol. Es lo que la separa de una puerta: sin un token de
    /// administrador válido no se llega, y ese token solo lo emite SignupAPI.
    /// </summary>
    [Fact]
    public void LaImpersonacion_ExigeSesionDeAdministradorConRol()
    {
        var controlador = ControllerTypes()
            .Single(t => t.Name == "ImpersonationController");

        var autorizacion = controlador.GetCustomAttribute<AuthorizeAttribute>();

        autorizacion.Should().NotBeNull(
            "sin [Authorize] la impersonación sería una forma anónima de sacar un token con los " +
            "roles de cualquier miembro, que es exactamente el agujero que se acaba de cerrar");

        controlador.GetCustomAttributes<AllowAnonymousAttribute>().Should().BeEmpty();

        autorizacion!.Roles.Should().NotBeNullOrWhiteSpace(
            "sin roles, cualquier usuario autenticado —un miembro cualquiera— podría suplantar a " +
            "otro");

        var roles = autorizacion.Roles!.Split(',', StringSplitOptions.TrimEntries);
        roles.Should().BeEquivalentTo(["SuperAdmin", "Admin", "SupportManager"]);

        // Y que ninguna de sus acciones se escape del candado del controlador.
        controlador.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Should().BeEmpty("una acción anónima dentro de este controlador anula el candado de arriba");
    }


    // ---------------------------------------------------------------------------------------
    //  Ayudantes
    // ---------------------------------------------------------------------------------------

    private static Type[] ControllerTypes() =>
        AdminApiAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .ToArray();

    /// <summary>
    /// Si el tipo recibe el emisor de tokens por constructor o lo guarda en un campo o propiedad.
    /// Se miran las tres formas porque cambiar de una a otra no es un cambio de intención.
    /// </summary>
    private static bool PideElEmisorDeTokens(Type t)
    {
        const BindingFlags todos = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static;

        return t.GetConstructors(todos)
                    .SelectMany(c => c.GetParameters())
                    .Any(p => p.ParameterType == typeof(IJwtService))
            || t.GetFields(todos).Any(f => f.FieldType == typeof(IJwtService))
            || t.GetProperties(todos).Any(p => p.PropertyType == typeof(IJwtService));
    }

    /// <summary>Las rutas declaradas por un controlador: la suya y las de sus acciones.</summary>
    private static IEnumerable<string> RutasDe(Type controlador)
    {
        foreach (var ruta in controlador.GetCustomAttributes<RouteAttribute>().Select(a => a.Template))
            yield return ruta;

        var metodos = controlador.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var metodo in metodos)
        {
            foreach (var ruta in metodo.GetCustomAttributes<RouteAttribute>().Select(a => a.Template))
                yield return ruta;

            foreach (var plantilla in metodo.GetCustomAttributes<HttpMethodAttribute>()
                         .Select(a => a.Template)
                         .Where(p => !string.IsNullOrEmpty(p)))
                yield return plantilla!;
        }
    }
}
