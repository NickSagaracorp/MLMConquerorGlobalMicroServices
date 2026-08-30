using System.Reflection;
using Microsoft.AspNetCore.Authorization;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests;

/// <summary>
/// LOS DOS ANFITRIONES QUE NO REGISTRAN AUTENTICACIÓN, y la prueba que impide que eso se convierta
/// en una trampa.
/// </summary>
/// <remarks>
/// QUÉ HABÍA. <c>SharedAPICenter</c> y <c>Signups</c> registraban <c>AddAuthentication</c> +
/// <c>AddJwtBearer</c> con una llave HMAC (<c>SymmetricSecurityKey</c> sobre <c>Jwt:Key</c>)
/// mientras el emisor único de la solución firma con RSA. Ningún token real habría validado nunca
/// contra esa llave. Y encima el alcance era CERO endpoints: ni un <c>[Authorize]</c> ni un
/// <c>RequireAuthorization()</c> en ninguno de los dos. SharedAPICenter tenía además el emisor y la
/// audiencia equivocados y un <c>Jwt:Key</c> que decía literalmente
/// <c>YOUR_JWT_KEY_REPLACE_BEFORE_DEPLOY_MIN32CHARS</c>; Signups llevaba el secreto en claro en el
/// repositorio.
///
/// No era un agujero —no se protegía nada, así que no se dejaba de proteger nada—, era DECORADO: la
/// tubería estaba puesta y aparentaba una protección inexistente. Se borró entera en vez de
/// arreglarla, porque arreglarla habría sido poner criptografía correcta al servicio de cero
/// endpoints: el mismo adorno, ahora convincente.
///
/// POR QUÉ ESTA PRUEBA. El riesgo que quedaba no era el bloque muerto sino el SIGUIENTE que llegue:
/// alguien pone un <c>[Authorize]</c> en uno de estos dos anfitriones dando por hecho que hay con
/// qué comprobarlo. En ejecución falla a la vista —sin middleware de autorización, ASP.NET Core
/// lanza al servir ese endpoint—, pero eso solo se descubre al llegar la primera petición. Aquí se
/// descubre al compilar, y con el mensaje que dice qué hay que hacer.
///
/// SI ESTA PRUEBA SE PONE EN ROJO no hay que borrar el atributo: hay que TRAER la autenticación de
/// verdad al anfitrión —el bloque RSA de SignupAPI o AdminAPI, con <c>Jwt:PublicKeyBase64</c>, el
/// emisor y la audiencia del sistema y el evento <c>OnTokenValidated</c> que rechaza los retos de
/// 2FA— y volver a poner <c>UseAuthentication</c>/<c>UseAuthorization</c>.
/// </remarks>
public class ConfiguracionEnganosaTests
{
    public static TheoryData<string> LosDosAnfitrionesSinAutenticacion() => new()
    {
        "MLMConquerorGlobalEdition.SharedAPICenter",
        "MLMConquerorGlobalEdition.Signups"
    };

    [Theory]
    [MemberData(nameof(LosDosAnfitrionesSinAutenticacion))]
    public void NoTienenNingunEndpointProtegido(string ensamblado)
    {
        var tiposConAtributo = Ensamblado(ensamblado).GetTypes()
            .Where(TieneAutorizacion)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        tiposConAtributo.Should().BeEmpty(
            $"{ensamblado} no registra autenticación: no hay AddAuthentication, ni AddJwtBearer, ni " +
            "UseAuthentication. Un [Authorize] aquí no tiene con qué comprobar nada — falla al " +
            "servir la petición. Antes de añadirlo hay que traer el bloque RSA de SignupAPI.");
    }

    /// <summary>
    /// Y que la sección <c>Jwt</c> tampoco vuelva a aparecer en su configuración: un secreto puesto
    /// ahí no protegería nada y volvería a parecer la llave de firma de la casa.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosDosAnfitrionesSinAutenticacion))]
    public void NoTienenSeccionJwtEnSuConfiguracion(string ensamblado)
    {
        var carpeta = Path.Combine(RaizDelRepositorio(), ensamblado);
        var ajustes = Path.Combine(carpeta, "appsettings.json");

        File.Exists(ajustes).Should().BeTrue($"no se encontró {ajustes}");

        // Se busca la CLAVE de la sección, no la palabra: los comentarios que explican por qué se
        // fue la sección mencionan "Jwt" a propósito, y esta prueba no puede prohibir explicarlo.
        File.ReadAllText(ajustes).Should().NotContain("\"Jwt\"",
            $"{ensamblado} no valida tokens; una sección Jwt ahí es un secreto que no firma nada y " +
            "un cartel que dice lo contrario");
    }

    private static bool TieneAutorizacion(Type tipo)
    {
        const BindingFlags todos = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        return tipo.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any()
            || tipo.GetMethods(todos).Any(m =>
                   m.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any());
    }

    private static Assembly Ensamblado(string nombre) =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == nombre)
        ?? Assembly.Load(nombre);

    /// <summary>
    /// Sube desde el directorio de salida de las pruebas hasta la carpeta que contiene el archivo de
    /// solución. Es la única forma de leer un appsettings que no se copia a la salida de ESTE
    /// proyecto.
    /// </summary>
    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null &&
               !File.Exists(Path.Combine(directorio.FullName, "MLMConquerorGlobalEdition.slnx")))
        {
            directorio = directorio.Parent;
        }

        directorio.Should().NotBeNull("la prueba tiene que poder localizar la raíz del repositorio");
        return directorio!.FullName;
    }
}
