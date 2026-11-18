using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedComponents.Services;
using Xunit;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El emisor escribe el miembro en el claim <c>memberId</c> y el usuario en <c>sub</c>, y la cookie
/// del portal los guarda tal cual (se validan con <c>MapInboundClaims = false</c>). El contexto de
/// vista buscaba <c>member_id</c> y el nombre largo de .NET: el miembro salía siempre vacío.
/// Aquí se arma el usuario con los claims de un token real, como lo hace la puerta.
/// </summary>
public class MiembroDelContextoDeVistaTests
{
    [Fact]
    public void ElInicializadorDelPortal_LeeElMiembroYElUsuarioDelToken()
    {
        var contexto = new ViewContextService(new SemillaFija(null));
        new ServerViewContextInitializer(
                new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = UsuarioDelToken() } },
                contexto, isAdminContext: false)
            .Initialize();

        contexto.ViewingMemberId.Should().Be("AMB-000123");
        contexto.ViewerUserId.Should().Be("usuario-1");
    }

    [Fact]
    public void ElContextoDeVista_SeSiembraConElMiembroDelToken()
    {
        var contexto = new ViewContextService(new SemillaFija(UsuarioDelToken()));

        contexto.ViewingMemberId.Should().Be("AMB-000123");
        contexto.ViewerUserId.Should().Be("usuario-1");
        contexto.MemberEmail.Should().Be("socio@ejemplo.com");
    }

    /// <summary>El principal que monta la puerta: los claims del token validado, sin renombrar.</summary>
    private static ClaimsPrincipal UsuarioDelToken()
    {
        var token = TokensDePortal.Firmar(
            [
                new Claim(JwtRegisteredClaimNames.Sub,   "usuario-1"),
                new Claim(JwtRegisteredClaimNames.Email, "socio@ejemplo.com"),
                new Claim("memberId",                    "AMB-000123"),
                new Claim(ClaimTypes.Role,               "Member"),
            ],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(15));

        var jwt = TokensDePortal.Validador.Validate(token)!;
        return new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "Cookies"));
    }

    private sealed class SemillaFija(ClaimsPrincipal? usuario) : IViewContextSeed
    {
        public ClaimsPrincipal? GetUser() => usuario;
        public string? GetPath() => "/";
    }
}
