using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests;

/// <summary>
/// En el perfil de miembro del Admin la cuota se ve pero no se toca (Plan K, K21): un solo GET con el
/// miembro de la ruta y los mismos roles que el resto del perfil. Pagar y la preferencia son del miembro.
/// </summary>
public class AdminMemberAnnualFeeControllerTests
{
    [Fact]
    public async Task Devuelve_el_resumen_del_miembro_de_la_ruta()
    {
        var overview = new Mock<IAnnualFeeOverviewService>();
        overview.Setup(o => o.GetAsync("AMB-7", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnnualFeeOverviewDto { Applies = true, Status = AnnualFeeStatuses.UpToDate });

        var r = await new AdminMemberAnnualFeeController(overview.Object).Get("AMB-7", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<AnnualFeeOverviewDto>>()
         .Which.Data!.Status.Should().Be(AnnualFeeStatuses.UpToDate);
    }

    [Fact]
    public void Solo_lee()
        => typeof(AdminMemberAnnualFeeController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Should().OnlyContain(m => m == "GET");

    [Fact]
    public void Tiene_los_roles_del_perfil_de_miembro()
    {
        var roles = typeof(AdminMemberAnnualFeeController).GetCustomAttribute<AuthorizeAttribute>()!.Roles;
        roles.Should().Be(typeof(AdminMemberProfileController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }
}
