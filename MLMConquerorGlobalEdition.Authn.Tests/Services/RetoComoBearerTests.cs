using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

/// <summary>
/// La regresión del bypass completo del segundo factor.
///
/// EL AGUJERO: el ChallengeToken se emite en el login <b>antes</b> de verificar ningún código, y
/// llevaba el mismo emisor, la misma audiencia y la misma llave que los tokens de acceso, más un
/// <c>sub</c>. Nada lo distinguía. Con solo el correo y la contraseña de la víctima:
///
///     POST /api/v1/auth/login              → 200 { challengeToken: "eyJ..." }
///     POST /api/v1/auth/two-factor/disable
///          Authorization: Bearer eyJ...    → 2FA desactivado
///
/// Sin tocar su correo, su SMS ni su autenticador. Y no era solo desactivar: <b>todos</b> los
/// endpoints <c>[Authorize]</c> quedaban abiertos — cambiar contraseña, exportar datos
/// personales, gestionar teléfono, cambiar el canal del 2FA. El claim <c>purpose</c> existía,
/// pero solo lo miraba <c>ChallengeTokenService.Validate</c>, que es el camino de verificación
/// del 2FA; el middleware de las APIs nunca lo miraba.
///
/// POR QUÉ UN ANFITRIÓN DE VERDAD: la prueba tiene que ser el 401 que devuelve la tubería, no una
/// aserción sobre <c>ValidateToken()</c> — eso probaría la librería de Microsoft, no que la
/// petición rebote. Aquí se levanta un anfitrión en memoria con <b>la misma configuración de
/// bearer que los nueve servicios</b> (audiencia <c>Jwt:Audience</c> y el rechazo del claim de
/// propósito) y se ejerce el ataque por HTTP contra rutas <c>[Authorize]</c> reales.
///
/// CÓMO SE COMPROBÓ QUE ESTAS PRUEBAS SIRVEN: revirtiendo el arreglo (devolviendo a
/// <c>ChallengeTokenService</c> la audiencia de acceso), los quince casos de
/// <see cref="UnRetoNoAbreNingunEndpointAutorizado"/> se ponen rojos con 200 en vez de 401.
/// </summary>
public class RetoComoBearerTests : IAsyncLifetime
{
    private const string Issuer         = "MLMConqueror";
    private const string AccessAudience = "MLMConquerorUsers";

    private const string UserId = "usr-victima";
    private const string Email  = "victima@example.com";

    /// <summary>
    /// Reloj en la hora real, no una fecha fija. Aquí la vigencia la mide el middleware con el
    /// reloj de la máquina: con una fecha de laboratorio el reto llegaría caducado y todas las
    /// pruebas del ataque darían 401 <b>por la razón equivocada</b> — seguirían verdes con el
    /// agujero abierto. El reto tiene que estar vivo para que el 401 signifique lo que dice.
    /// </summary>
    private static readonly DateTime Ahora = DateTime.UtcNow;

    /// <summary>
    /// Las rutas del ataque. Son las de SignupAPI que llevan <c>[Authorize]</c> pelado: el
    /// informe original solo nombraba <c>disable</c>, pero el agujero las abría todas, así que
    /// la regresión las cubre todas y no solo la que se demostró.
    /// </summary>
    private static readonly string[] Rutas =
    [
        "/api/v1/auth/two-factor/disable",
        "/api/v1/auth/change-password",
        "/api/v1/auth/personal-data",
        "/api/v1/auth/phone",
        "/api/v1/auth/two-factor/channel",
        "/api/v1/auth/account-status",
    ];

    public static TheoryData<string> EndpointsAutorizados()
    {
        var data = new TheoryData<string>();
        foreach (var ruta in Rutas) data.Add(ruta);
        return data;
    }

    /// <summary>Los tres propósitos que emite el servicio de retos.</summary>
    public static TheoryData<TwoFactorPurpose, string?> LosTresPropositos() => new()
    {
        { TwoFactorPurpose.Login,      null },
        { TwoFactorPurpose.Enrollment, null },
        { TwoFactorPurpose.StepUp,     "payout.withdraw" },
    };

    public static TheoryData<string, TwoFactorPurpose, string?> CadaPropositoContraCadaEndpoint()
    {
        var data = new TheoryData<string, TwoFactorPurpose, string?>();
        foreach (var ruta in Rutas)
        {
            data.Add(ruta, TwoFactorPurpose.Login,      null);
            data.Add(ruta, TwoFactorPurpose.Enrollment, null);
            data.Add(ruta, TwoFactorPurpose.StepUp,     "payout.withdraw");
        }
        return data;
    }

    private IHost                _host      = null!;
    private HttpClient           _client    = null!;
    private ChallengeTokenService _retos    = null!;
    private RsaSecurityKey       _firma     = null!;

    public async Task InitializeAsync()
    {
        using var rsa = RSA.Create(2048);
        var privada = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        var publica = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyBase64"] = privada,
                ["Jwt:PublicKeyBase64"]  = publica,
                ["Jwt:Issuer"]           = Issuer,
                ["Jwt:Audience"]         = AccessAudience,
            })
            .Build();

        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(c => c.Now).Returns(Ahora);
        reloj.Setup(c => c.UtcNow).Returns(Ahora);

        _retos = new ChallengeTokenService(config, reloj.Object);

        var rsaFirma = RSA.Create();
        rsaFirma.ImportPkcs8PrivateKey(Convert.FromBase64String(privada), out _);
        _firma = new RsaSecurityKey(rsaFirma);

        var rsaValidacion = RSA.Create();
        rsaValidacion.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publica), out _);
        var llaveValidacion = new RsaSecurityKey(rsaValidacion);

        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            // Copia fiel de la configuración de los nueve anfitriones. Si esto se
                            // separara de lo que hay en los Program.cs, la prueba dejaría de
                            // demostrar nada sobre ellos.
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer           = true,
                                ValidateAudience         = true,
                                ValidateLifetime         = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer              = Issuer,
                                ValidAudience            = AccessAudience,
                                IssuerSigningKey         = llaveValidacion,
                                ClockSkew                = TimeSpan.Zero
                            };

                            options.Events = new JwtBearerEvents
                            {
                                OnTokenValidated = ctx =>
                                {
                                    if (ChallengeAudience.CarriesPurpose(ctx.Principal!.Claims))
                                        ctx.Fail("Un reto de 2FA no autoriza: falta completar el segundo factor.");
                                    return Task.CompletedTask;
                                }
                            };
                        });
                    services.AddAuthorization();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        foreach (var ruta in Rutas)
                            endpoints.Map(ruta, () => Results.Ok("hecho")).RequireAuthorization();

                        // Con rol, para comprobar —no dar por supuesto— que el reto tampoco
                        // llega aquí, y que un acceso sin rol se queda en 403 y no en 200.
                        endpoints.Map("/api/v1/admin/solo-superadmin", () => Results.Ok("hecho"))
                                 .RequireAuthorization(p => p.RequireRole("SuperAdmin"));
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    // ── el ataque ────────────────────────────────────────────────────────────────

    /// <summary>
    /// EL CORAZÓN DE TODO ESTO: un reto presentado como Bearer no abre ningún endpoint
    /// <c>[Authorize]</c>, sea cual sea su propósito y sea cual sea el endpoint.
    /// </summary>
    [Theory]
    [MemberData(nameof(CadaPropositoContraCadaEndpoint))]
    public async Task UnRetoNoAbreNingunEndpointAutorizado(
        string ruta, TwoFactorPurpose proposito, string? operacion)
    {
        var reto = EmitirReto(proposito, operacion);

        var respuesta = await Llamar(ruta, reto);

        respuesta.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "un token que no ha completado el segundo factor no debe autorizar nada; " +
            $"con el arreglo revertido esta llamada a {ruta} devolvía 200.");
    }

    [Theory]
    [MemberData(nameof(LosTresPropositos))]
    public async Task UnRetoTampocoAbreLoQueEstaCerradoPorRol(
        TwoFactorPurpose proposito, string? operacion)
    {
        var respuesta = await Llamar("/api/v1/admin/solo-superadmin", EmitirReto(proposito, operacion));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── y que el flujo legítimo sigue funcionando ────────────────────────────────

    [Theory]
    [MemberData(nameof(EndpointsAutorizados))]
    public async Task UnTokenDeAccesoDeVerdadSiAbreLosEndpoints(string ruta)
    {
        var respuesta = await Llamar(ruta, EmitirAcceso());

        respuesta.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "cerrar el agujero no puede romper el 2FA: el token que sale de canjear el código " +
            "tiene que seguir abriendo lo de siempre.");
    }

    [Fact]
    public async Task UnTokenDeAccesoConElRolAbreLoCerradoPorRol()
    {
        var respuesta = await Llamar("/api/v1/admin/solo-superadmin", EmitirAcceso("SuperAdmin"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnTokenDeAccesoSinElRolSeQuedaEn403()
    {
        // La auditoría dijo que los [Authorize(Roles=...)] estaban a salvo porque el reto no
        // lleva claims de rol. Esto lo comprueba por el otro lado: sin rol es 403, no 200 — o
        // sea que el rol se está exigiendo de verdad.
        var respuesta = await Llamar("/api/v1/admin/solo-superadmin", EmitirAcceso());

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── los dos cinturones, por separado ─────────────────────────────────────────

    [Fact]
    public void ElRetoNoSeEmiteConLaAudienciaDeAcceso()
    {
        // El primer cinturón, mirado en el token mismo: si esta aserción cae, el reto vuelve a
        // ser indistinguible de un token de acceso para cualquier anfitrión.
        var reto = new JwtSecurityTokenHandler().ReadJwtToken(EmitirReto(TwoFactorPurpose.Login, null));

        reto.Audiences.Should().NotContain(AccessAudience);
        reto.Audiences.Should().ContainSingle().Which
            .Should().Be(ChallengeAudience.For(AccessAudience))
            .And.EndWith(ChallengeAudience.Suffix);
    }

    [Fact]
    public async Task ElSegundoCinturonAguantaSoloConLaAudienciaDeAcceso()
    {
        // Aquí se falsifica el peor caso: un token firmado con la llave buena, con el emisor
        // bueno y con la audiencia DE ACCESO, pero con el claim de propósito dentro. La
        // audiencia ya no lo para; lo tiene que parar el segundo cinturón.
        //
        // Es el escenario de que alguien afloje la validación de audiencia en un servicio, o de
        // que un reto viejo siga vivo tras un despliegue a medias.
        var falsificado = FirmarToken(AccessAudience,
        [
            new Claim(JwtRegisteredClaimNames.Sub,   UserId),
            new Claim(JwtRegisteredClaimNames.Email, Email),
            new Claim(ChallengeAudience.PurposeClaim, "login"),
        ]);

        var respuesta = await Llamar("/api/v1/auth/two-factor/disable", falsificado);

        respuesta.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "el claim de propósito basta por sí solo para rechazar, aunque la audiencia cuele.");
    }

    // ── y que el servicio de retos sigue aceptando los suyos ─────────────────────

    [Theory]
    [MemberData(nameof(LosTresPropositos))]
    public void ValidateSigueAceptandoSusPropiosRetos(TwoFactorPurpose proposito, string? operacion)
    {
        // Mover la audiencia no puede romper el camino de verificación del 2FA: es el que
        // convierte el reto en sesión, y si se rompiera nadie podría iniciar sesión.
        var resultado = _retos.Validate(EmitirReto(proposito, operacion), proposito, operacion);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UserId.Should().Be(UserId);
        resultado.Value.Purpose.Should().Be(proposito);
    }

    // ── ayudantes ────────────────────────────────────────────────────────────────

    private string EmitirReto(TwoFactorPurpose proposito, string? operacion) =>
        _retos.Issue(
            userId:       UserId,
            email:        Email,
            purpose:      proposito,
            channel:      TwoFactorChannel.Authenticator,
            codeHash:     null,
            operationKey: operacion);

    /// <summary>
    /// Un token de acceso como el que emite <c>SignupAPI.Services.JwtService</c>: mismos emisor,
    /// audiencia y llave, y sin claim de propósito.
    /// </summary>
    private string EmitirAcceso(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   UserId),
            new(JwtRegisteredClaimNames.Email, Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("memberId", "MEM-1"),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        return FirmarToken(AccessAudience, claims);
    }

    private string FirmarToken(string audiencia, IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           audiencia,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(_firma, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Task<HttpResponseMessage> Llamar(string ruta, string token)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(peticion);
    }
}
