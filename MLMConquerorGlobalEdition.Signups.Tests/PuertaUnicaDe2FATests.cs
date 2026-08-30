using System.Reflection;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.RefreshToken;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.VerifyTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;

namespace MLMConquerorGlobalEdition.Signups.Tests;

/// <summary>
/// El guardián de la PUERTA ÚNICA por el lado de SignupAPI: el censo de quién puede firmar un token
/// de acceso en toda la solución.
///
/// AdminAPI tiene su propia mitad de esta prueba (<c>PuertaUnicaDe2FATests</c>, en AdminAPI.Tests),
/// que vigila que allí no vuelva a aparecer un login. Esta vigila lo de aquí: que la lista de
/// emisores no crezca sin que nadie se entere.
///
/// POR QUÉ UNA LISTA CERRADA Y NO UNA REGLA. No hay una regla que distinga un emisor legítimo de un
/// agujero: los cinco de abajo firman tokens y los cinco están bien, cada uno por un motivo
/// distinto y ninguno deducible del código. La única forma de que el sexto no entre de tapadillo es
/// que añadirlo obligue a tocar esta lista y a escribir aquí por qué.
///
/// SI ESTA PRUEBA SE PONE EN ROJO CON UN NOMBRE NUEVO, la pregunta es una: ¿puede alguien llegar
/// hasta ahí sin haber pasado el segundo factor cuando su cuenta lo tiene puesto? Si la respuesta es
/// que sí, eso es el mismo agujero que se cerró en AdminAPI.
/// </summary>
public class PuertaUnicaDe2FATests
{
    private static Assembly SignupApiAssembly => typeof(LoginHandler).Assembly;

    [Fact]
    public void LaPruebaMiraElEnsambladoDeSignupApi()
    {
        SignupApiAssembly.GetName().Name.Should().Be("MLMConquerorGlobalEdition.SignupAPI");
        SignupApiAssembly.GetTypes().Should().NotBeEmpty();
    }

    /// <summary>
    /// El censo. Cinco handlers y el controlador que los expone — y nadie más.
    /// </summary>
    /// <remarks>
    /// POR QUÉ CADA UNO ES LEGÍTIMO:
    ///
    /// <list type="bullet">
    /// <item><b>LoginHandler</b> — es LA puerta. Solo llega a firmar por la rama en la que
    /// <c>TwoFactorEnabled</c> es falso; con el segundo factor puesto se va antes devolviendo un
    /// reto y ni un token.</item>
    ///
    /// <item><b>VerifyTwoFactorHandler</b> — firma justo DESPUÉS de que la librería Authn haya
    /// comprobado el código contra el reto firmado. Es el sitio donde el segundo factor se
    /// convierte en sesión.</item>
    ///
    /// <item><b>ConfirmEnrollmentHandler</b> — cierra el enrolamiento obligatorio con el primer
    /// código de la aplicación autenticadora. El usuario demostró los dos factores en la misma
    /// sesión: contraseña en el login que dio el token de enrolamiento, TOTP aquí.</item>
    ///
    /// <item><b>RefreshTokenHandler</b> — no autentica a nadie: canjea un refresh token que ya se
    /// emitió, y ese solo sale de uno de los tres de arriba. Renovar no puede volver a exigir el
    /// segundo factor sin que la sesión muera cada quince minutos.</item>
    ///
    /// <item><b>CompleteSignupHandler</b> — cierra un alta y deja dentro a la cuenta que acaba de
    /// nacer. Nace sin segundo factor configurado, así que no hay ninguno que saltarse; su token
    /// lleva solo <c>Member</c> o <c>Ambassador</c>, nunca un rol del panel.</item>
    ///
    /// <item><b>AuthController</b> — no firma: pide <c>IJwtService</c> solo para leer
    /// <c>RefreshTokenExpiry</c> al poner la cookie.</item>
    /// </list>
    /// </remarks>
    [Fact]
    public void ElCensoDeEmisoresDeTokensDeSignupApi_NoHaCrecido()
    {
        var esperados = new[]
        {
            typeof(LoginHandler).FullName!,
            typeof(VerifyTwoFactorHandler).FullName!,
            typeof(ConfirmEnrollmentHandler).FullName!,
            typeof(RefreshTokenHandler).FullName!,
            typeof(CompleteSignupHandler).FullName!,
            "MLMConquerorGlobalEdition.SignupAPI.Controllers.AuthController",
        }.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        var reales = SignupApiAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(PideElEmisorDeTokens)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        reales.Should().Equal(esperados,
            "cada tipo que pide IJwtService puede firmar un token de acceso con los roles que " +
            "quiera. Si aparece uno nuevo, hay que responder si se puede llegar hasta él sin haber " +
            "pasado el segundo factor — y, si se puede, es el mismo agujero que se cerró en AdminAPI.");
    }

    /// <summary>
    /// El emisor de tokens es UNO SOLO en toda la solución, y vive en la biblioteca de
    /// autenticación. Había dos copias —AdminAPI y SignupAPI— prácticamente idénticas, y la de
    /// AdminAPI ya había perdido por el camino el claim <c>default_language</c> sin que nada se
    /// pusiera rojo.
    /// </summary>
    [Fact]
    public void ElEmisorDeTokens_ViveUnaSolaVezYEnLaBibliotecaDeAutenticacion()
    {
        var implementaciones = new[]
            {
                SignupApiAssembly,
                typeof(MLMConquerorGlobalEdition.Authn.Services.JwtService).Assembly,
                typeof(MLMConquerorGlobalEdition.SharedKernel.Interfaces.IJwtService).Assembly,
            }
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(IJwtService).IsAssignableFrom(t))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        implementaciones.Should().Equal(
            [typeof(MLMConquerorGlobalEdition.Authn.Services.JwtService).FullName!],
            "una segunda implementación de IJwtService es una segunda política de firma: otra " +
            "caducidad, otros claims, otra llave. Si hace falta variar algo, se varía dentro de la " +
            "única que hay.");
    }

    /// <summary>
    /// El emisor no arrastra ASP.NET Core. Es lo que permite que viva en Authn —al lado del reto de
    /// 2FA, con el que comparte llave RSA y emisor— en vez de repetido en cada API.
    /// </summary>
    [Fact]
    public void LaBibliotecaDeAutenticacion_SigueSinDependerDeAspNetCore()
    {
        var referencias = typeof(MLMConquerorGlobalEdition.Authn.Services.JwtService).Assembly
            .GetReferencedAssemblies()
            .Select(r => r.Name ?? string.Empty)
            .Where(n => n.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        referencias.Should().BeEmpty(
            "Authn es una biblioteca, no un anfitrión. Si necesita alojamiento web, deja de poder " +
            "usarse desde donde no lo hay.");
    }

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
}
