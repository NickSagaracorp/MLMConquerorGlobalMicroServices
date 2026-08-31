using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Server.Configuration;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests;

/// <summary>
/// LAS VIGENCIAS DE SESIÓN, LEÍDAS DE LOS <c>appsettings.json</c> DE VERDAD.
/// </summary>
/// <remarks>
/// POR QUÉ CONTRA LOS ARCHIVOS Y NO CONTRA UNA CONFIGURACIÓN EN MEMORIA. Las pruebas de
/// <c>JwtService</c> comprueban que el servicio LEE bien lo que le den; esto comprueba qué le dan.
/// Son cosas distintas y la que ha fallado históricamente es la segunda: el token de acceso estuvo
/// en 120 minutos sin que ninguna prueba dijera nada, porque ninguna prueba miraba el archivo.
///
/// LA INVARIANTE QUE ESTO EXISTE PARA DEFENDER es <see cref="ElRefrescoDuraMasQueElAcceso"/>. El
/// refresco solo entra en juego DESPUÉS de que el acceso caduque: <c>ApiAuthHandler</c> mira si el
/// acceso murió y solo entonces llama a renovar. Si los dos duran lo mismo —y con un acceso de 15
/// y un refresco de 30 minutos igualarlos es un cambio de un dígito— el refresco caduca en el mismo
/// instante en que empezaría a hacer falta: no hay ventana para usarlo, la renovación falla
/// siempre, y la sesión de todo el mundo muere al caducar el acceso. Peor aún, no se rompe nada
/// visible en desarrollo, donde nadie deja una pestaña quince minutos quieta; se rompe en
/// producción y para todos a la vez.
///
/// LO QUE ESTO NO VIGILA A PROPÓSITO: <c>Auth:TwoFactor:ChallengeLifetimeMinutes</c> (5 min) y las
/// cookies de reto del portal (10 min). Son otro mecanismo, ya están por debajo de estas cifras y
/// no dependen de ellas.
/// </remarks>
public class VigenciasDeSesionTests
{
    /// <summary>Los tres anfitriones que firman o validan tokens de acceso.</summary>
    public static TheoryData<string> LosTresAnfitriones() => new()
    {
        "MLMConquerorGlobalEdition.AdminAPI",
        "MLMConquerorGlobalEdition.BizCenter",
        "MLMConquerorGlobalEdition.SignupAPI"
    };

    /// <summary>
    /// LA PRUEBA QUE IMPIDE IGUALAR LAS DOS VIGENCIAS. Si alguien pone el refresco en 15 —o el
    /// acceso en 30— esto se pone rojo con el motivo escrito.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosTresAnfitriones))]
    public void ElRefrescoDuraMasQueElAcceso(string anfitrion)
    {
        var config = Configuracion(anfitrion);

        var acceso   = TimeSpan.FromMinutes(config.GetValue<int>("Jwt:AccessTokenExpiryMinutes"));
        var refresco = TimeSpan.FromMinutes(config.GetValue<int>("Jwt:RefreshTokenExpiryMinutes"));

        refresco.Should().BeGreaterThan(acceso,
            $"en {anfitrion} el refresco solo se usa DESPUÉS de que caduque el acceso. Igualarlos "
          + "deja al refresco sin ventana: la renovación falla siempre y la sesión de todos los "
          + "usuarios muere al caducar el acceso, trabajen o no. Ver ApiAuthHandler.SendAsync.");
    }

    /// <summary>
    /// El acceso, 15 minutos. Era 120: un token robado servía dos horas aunque la sesión estuviera
    /// revocada, porque nadie lo consulta contra la base hasta que caduca.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosTresAnfitriones))]
    public void ElAccesoDuraQuinceMinutos(string anfitrion) =>
        Configuracion(anfitrion).GetValue<int>("Jwt:AccessTokenExpiryMinutes").Should().Be(15,
            $"{anfitrion} tiene que emitir tokens de acceso de 15 minutos");

    /// <summary>
    /// El refresco, 30 MINUTOS. No es un límite de sesión sino de INACTIVIDAD: cada renovación
    /// vuelve a poner el contador a 30, así que quien está trabajando no se cae nunca y quien deja
    /// el ordenador vuelve a pasar por su segundo factor.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosTresAnfitriones))]
    public void ElRefrescoDuraTreintaMinutos(string anfitrion) =>
        Configuracion(anfitrion).GetValue<int>("Jwt:RefreshTokenExpiryMinutes").Should().Be(30,
            $"{anfitrion} tiene que cerrar por inactividad a los 30 minutos");

    /// <summary>
    /// La clave VIEJA no puede seguir en ningún archivo. No rompería el arranque —nadie la lee ya—,
    /// y eso es justo el problema: se quedaría ahí diciendo "treinta días" para siempre, y el
    /// primero que la creyera al revisar la configuración se llevaría una idea equivocada de cuánto
    /// vive una sesión.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosTresAnfitriones))]
    public void LaClaveViejaEnDiasYaNoAparece(string anfitrion)
    {
        var texto = File.ReadAllText(RutaDeAjustes(anfitrion));

        texto.Should().NotContain("RefreshTokenExpiryDays",
            $"{anfitrion} ya no lee esa clave. JwtService lee Jwt:RefreshTokenExpiryMinutes y la "
          + "interpreta en MINUTOS; dejar ahí la que dice 'Days' es un cartel que miente.");
    }

    /// <summary>
    /// Los enlaces de correo, 15 minutos, y la MISMA cifra que el correo le anuncia al usuario.
    /// </summary>
    /// <remarks>
    /// Solo SignupAPI declara la clave porque es el único que emite y valida estos enlaces; los
    /// otros dos anfitriones caen en <see cref="EmailLinkLifetime.DefaultMinutes"/>, que es el
    /// mismo número. Lo que esta prueba fija es que lo declarado y lo que rige por defecto no
    /// puedan separarse: si alguien cambia el appsettings sin cambiar la constante —o al revés—
    /// AdminAPI y BizCenter se quedarían con una vigencia distinta de la de SignupAPI para el mismo
    /// proveedor de tokens de Identity.
    /// </remarks>
    [Fact]
    public void LosEnlacesDeCorreoDuranQuinceMinutos()
    {
        var config = Configuracion("MLMConquerorGlobalEdition.SignupAPI");

        EmailLinkLifetime.Minutes(config).Should().Be(15,
            "un enlace de recuperación es una credencial de un solo uso que se queda en el buzón");

        config.GetValue<int>(EmailLinkLifetime.ConfigKey).Should().Be(EmailLinkLifetime.DefaultMinutes,
            "lo declarado en SignupAPI y lo que rige por defecto en AdminAPI y BizCenter tienen que "
          + "ser el mismo número: es el mismo proveedor de tokens de Identity");
    }

    /// <summary>
    /// Y que el reto de 2FA siga POR DEBAJO de todo lo anterior. No se toca en este cambio, pero si
    /// alguien lo subiera por encima del acceso tendríamos un reto que sobrevive a la sesión que
    /// autoriza a abrir.
    /// </summary>
    [Fact]
    public void ElRetoDeDosFactoresSigueSiendoElMasCorto()
    {
        var config = Configuracion("MLMConquerorGlobalEdition.SignupAPI");

        // El valor por defecto de ChallengeTokenService cuando la clave no está declarada.
        var reto = config.GetValue("Auth:TwoFactor:ChallengeLifetimeMinutes", 5);

        reto.Should().BeLessThan(config.GetValue<int>("Jwt:AccessTokenExpiryMinutes"),
            "el reto es el permiso para EMPEZAR una sesión; no puede durar más que la sesión");
    }

    private static IConfigurationRoot Configuracion(string anfitrion) =>
        new ConfigurationBuilder()
            .AddJsonFile(RutaDeAjustes(anfitrion), optional: false)
            .Build();

    private static string RutaDeAjustes(string anfitrion)
    {
        var ruta = Path.Combine(RaizDelRepositorio(), anfitrion, "appsettings.json");
        File.Exists(ruta).Should().BeTrue($"no se encontró {ruta}");
        return ruta;
    }

    /// <summary>
    /// Sube desde el directorio de salida de las pruebas hasta la carpeta que contiene el archivo
    /// de solución. Mismo recurso que <c>ConfiguracionEnganosaTests</c>: es la única forma de leer
    /// un appsettings que no se copia a la salida de ESTE proyecto.
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
