using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// Billing leía el claim "member_id", pero JwtService emite "memberId": MemberId salía siempre vacío.
/// </summary>
public class CurrentUserServiceTests
{
    private static CurrentUserService Con(params Claim[] claims) =>
        new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba")) }
        });

    [Fact]
    public void Lee_el_memberId_con_el_nombre_que_emite_JwtService()
    {
        var sut = Con(new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(CallerIdentity.MemberIdClaim, "AMB-1"));

        sut.MemberId.Should().Be("AMB-1");
        sut.UserId.Should().Be("user-1");
    }

    [Fact]
    public void Sin_claim_de_miembro_es_cadena_vacia()
        => Con(new Claim(ClaimTypes.NameIdentifier, "staff-1")).MemberId.Should().BeEmpty();
}
