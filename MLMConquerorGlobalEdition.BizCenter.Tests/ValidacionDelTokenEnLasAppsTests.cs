using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.ClientCore.AccessTokens;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using Xunit;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Las MAUI arman la identidad de la interfaz —roles incluidos— con el token de acceso. Antes lo
/// leían sin comprobar la firma; ahora lo valida <see cref="ClientAccessTokenValidator"/> contra las
/// llaves que publica SignupAPI. Aquí se fija qué entra, qué no y cómo llega una llave nueva.
/// </summary>
public class ValidacionDelTokenEnLasAppsTests
{
    private static readonly AccessTokenTrust Confianza = AccessTokenTrust.Default;

    // ─── Lo que entra y lo que no ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnTokenFirmadoPorNuestroEmisor_Vale()
    {
        using var llave = RSA.Create(2048);
        var validador   = Validador(new ApiDeLlaves(llave), out _);

        var check = await validador.CheckAsync(Token(llave));

        check.IsValid.Should().BeTrue();
        check.Token!.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Member",
            "los claims salen tal como los escribió el emisor, igual que con ReadJwtToken");
    }

    [Fact]
    public async Task UnTokenAlterado_NoVale()
    {
        using var llave = RSA.Create(2048);
        var validador   = Validador(new ApiDeLlaves(llave), out _);

        // Se cambia el rol en la carga útil y se deja la firma original.
        var partes  = Token(llave).Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(partes[1]))
            .Replace("\"Member\"", "\"SuperAdmin\"");
        var alterado = $"{partes[0]}.{Base64UrlEncoder.Encode(payload)}.{partes[2]}";

        var check = await validador.CheckAsync(alterado);

        check.IsValid.Should().BeFalse();
        check.Token.Should().BeNull();
    }

    [Fact]
    public async Task UnTokenSinFirma_NoVale()
    {
        using var llave = RSA.Create(2048);
        var validador   = Validador(new ApiDeLlaves(llave), out _);

        var sinFirma = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Confianza.Issuer, Confianza.Audience, [new Claim("role", "SuperAdmin")],
            expires: DateTime.UtcNow.AddMinutes(10)));

        (await validador.CheckAsync(sinFirma)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UnTokenDeOtraLlave_NoVale()
    {
        using var nuestra = RSA.Create(2048);
        using var ajena   = RSA.Create(2048);
        var validador     = Validador(new ApiDeLlaves(nuestra), out _);

        var check = await validador.CheckAsync(Token(ajena));

        check.IsValid.Should().BeFalse();
        check.Status.Should().Be(AccessTokenStatus.UnknownSigner);
    }

    [Fact]
    public async Task UnTokenCaducado_SeDistingueParaBorrarLaSesion()
    {
        using var llave = RSA.Create(2048);
        var validador   = Validador(new ApiDeLlaves(llave), out _);

        var check = await validador.CheckAsync(
            Token(llave, notBefore: DateTime.UtcNow.AddHours(-2), caduca: DateTime.UtcNow.AddHours(-1)));

        check.Status.Should().Be(AccessTokenStatus.Expired);
        check.Token.Should().BeNull();
    }

    [Fact]
    public async Task UnTokenDeOtraAudiencia_NoVale()
    {
        using var llave = RSA.Create(2048);
        var validador   = Validador(new ApiDeLlaves(llave), out _);

        (await validador.CheckAsync(Token(llave, audiencia: "otra"))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UnRetoDe2FA_NoValeComoSesion()
    {
        using var llave = RSA.Create(2048);
        var validador   = Validador(new ApiDeLlaves(llave), out _);

        var reto = Token(llave, audiencia: ChallengeAudience.For(Confianza.Audience),
            extra: new Claim(ChallengeAudience.PurposeClaim, "login"));
        var conProposito = Token(llave, extra: new Claim(ChallengeAudience.PurposeClaim, "login"));

        (await validador.CheckAsync(reto)).IsValid.Should().BeFalse();
        (await validador.CheckAsync(conProposito)).IsValid.Should().BeFalse(
            "el claim de propósito es el segundo cinturón aunque la audiencia sea la de acceso");
    }

    // ─── Las llaves ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConUnaLlaveNueva_SeVuelvenAPedirLasLlavesYElTokenVale()
    {
        using var vieja = RSA.Create(2048);
        using var nueva = RSA.Create(2048);
        var api         = new ApiDeLlaves(vieja);
        var validador   = Validador(api, out var reloj);

        (await validador.CheckAsync(Token(vieja))).IsValid.Should().BeTrue();

        // La API rota la llave. Pasa más del intervalo mínimo entre descargas.
        api.Llaves = [nueva];
        reloj.Advance(JwksKeySource.MinRefreshInterval + TimeSpan.FromSeconds(1));

        (await validador.CheckAsync(Token(nueva))).IsValid.Should().BeTrue();
        api.Descargas.Should().Be(2);
    }

    [Fact]
    public async Task TokensBasuraSeguidos_NoProvocanUnaDescargaPorToken()
    {
        using var nuestra = RSA.Create(2048);
        using var ajena   = RSA.Create(2048);
        var api           = new ApiDeLlaves(nuestra);
        var validador     = Validador(api, out _);

        for (var i = 0; i < 5; i++)
            (await validador.CheckAsync(Token(ajena))).IsValid.Should().BeFalse();

        api.Descargas.Should().Be(1, "dentro del intervalo mínimo no se vuelve a pedir el documento");
    }

    [Fact]
    public async Task SinRed_ValidaConLaCopiaGuardada()
    {
        using var llave = RSA.Create(2048);

        // Primera sesión: descarga y guarda la copia.
        string? guardado = null;
        var cache = new DelegateJwksCache(() => guardado, json => guardado = json);
        await Validador(new ApiDeLlaves(llave), out _, cache).CheckAsync(Token(llave));
        guardado.Should().NotBeNull();

        // Se reinicia la app sin red.
        var sinRed = new ApiDeLlaves(llave) { Caida = true };
        var check  = await Validador(sinRed, out _, cache).CheckAsync(Token(llave));

        check.IsValid.Should().BeTrue();
        sinRed.Descargas.Should().Be(0);
    }

    [Fact]
    public async Task SinRedYSinCopia_NoDaPorBuenoNada()
    {
        using var llave = RSA.Create(2048);
        var validador   = Validador(new ApiDeLlaves(llave) { Caida = true }, out _);

        (await validador.CheckAsync(Token(llave))).IsValid.Should().BeFalse();
    }

    // ─── El portal: reto del 2FA y caducidad ─────────────────────────────────────────────────────

    [Fact]
    public void Portal_LeeElRetoFirmadoAunqueHayaCaducado_YRechazaElQueNoFirmamos()
    {
        var reto = Firmar(TokensDePortal.Audiencia + ChallengeAudience.Suffix,
            caduca: DateTime.UtcNow.AddMinutes(-1),
            new Claim(ChallengeAudience.PurposeClaim, "login"), new Claim("channel", "Email"));

        TokensDePortal.Validador.ValidateChallengeForDisplay(reto)
            .Should().NotBeNull("un reto caducado tiene que poder pintar su pantalla para ofrecer el reenvío");

        var sinFirma = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            TokensDePortal.Emisor, TokensDePortal.Audiencia + ChallengeAudience.Suffix,
            [new Claim(ChallengeAudience.PurposeClaim, "login")], expires: DateTime.UtcNow.AddMinutes(5)));
        TokensDePortal.Validador.ValidateChallengeForDisplay(sinFirma).Should().BeNull();
    }

    [Fact]
    public void Portal_UnTokenDeAccesoNoPasaPorReto()
    {
        var acceso = Firmar(TokensDePortal.Audiencia, DateTime.UtcNow.AddMinutes(10), new Claim("sub", "x"));

        TokensDePortal.Validador.ValidateChallengeForDisplay(acceso).Should().BeNull();
    }

    [Fact]
    public void Portal_UnTokenSinNuestraFirmaCuentaComoCaducado()
    {
        var sinFirma = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            TokensDePortal.Emisor, TokensDePortal.Audiencia, [new Claim("sub", "x")],
            expires: DateTime.UtcNow.AddMinutes(10)));
        var firmado = Firmar(TokensDePortal.Audiencia, DateTime.UtcNow.AddMinutes(10), new Claim("sub", "x"));

        SessionExpiry.IsExpired(sinFirma, TokensDePortal.Validador).Should().BeTrue();
        SessionExpiry.IsExpired(firmado, TokensDePortal.Validador).Should().BeFalse();
    }

    // ─── Ayudas ──────────────────────────────────────────────────────────────────────────────────

    private static ClientAccessTokenValidator Validador(
        ApiDeLlaves api, out RelojFalso reloj, IJwksCache? cache = null)
    {
        reloj = new RelojFalso();
        var fuente = new JwksKeySource(
            new Fabrica(api), cache ?? new NoJwksCache(), NullLogger<JwksKeySource>.Instance, reloj);
        return new ClientAccessTokenValidator(fuente, Confianza);
    }

    private static string Token(
        RSA llave, string? audiencia = null, DateTime? notBefore = null, DateTime? caduca = null,
        Claim? extra = null)
    {
        var claims = new List<Claim> { new("sub", "usuario"), new(ClaimTypes.Role, "Member") };
        if (extra is not null) claims.Add(extra);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer:             Confianza.Issuer,
            audience:           audiencia ?? Confianza.Audience,
            claims:             claims,
            notBefore:          notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires:            caduca ?? DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(llave), SecurityAlgorithms.RsaSha256)));
    }

    private static string Firmar(string audiencia, DateTime caduca, params Claim[] claims) =>
        TokensDePortal.FirmarPara(audiencia, claims, DateTime.UtcNow.AddMinutes(-10), caduca);

    /// <summary>SignupAPI publicando sus llaves en /.well-known/jwks.json.</summary>
    private sealed class ApiDeLlaves(params RSA[] llaves) : HttpMessageHandler
    {
        public RSA[] Llaves    { get; set; } = llaves;
        public bool  Caida     { get; set; }
        public int   Descargas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Caida) throw new HttpRequestException("sin red");

            request.RequestUri!.AbsolutePath.Should().Be("/" + JwksKeySource.DocumentPath);
            Descargas++;

            var keys = Llaves.Select(r =>
            {
                var p = r.ExportParameters(false);
                return $"{{\"kty\":\"RSA\",\"use\":\"sig\",\"alg\":\"RS256\",\"n\":\"{Base64UrlEncoder.Encode(p.Modulus)}\",\"e\":\"{Base64UrlEncoder.Encode(p.Exponent)}\"}}";
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"keys\":[{string.Join(",", keys)}]}}")
            });
        }
    }

    private sealed class RelojFalso : TimeProvider
    {
        private DateTimeOffset _ahora = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _ahora;
        public void Advance(TimeSpan tiempo) => _ahora += tiempo;
    }

    private sealed class Fabrica(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://auth.pruebas/") };
    }
}
