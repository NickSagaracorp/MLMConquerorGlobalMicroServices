using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Filters;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Controllers;

/// <summary>
/// Plan P, Tarea 8 (decision de Nick, 2026-10-18): DevSeedController solo existe en Development y, tambien
/// alli, exige SuperAdmin. La unica excepcion es el arranque inicial del superadmin, y solo mientras no exista
/// ninguno: en cuanto hay uno, ese camino queda cerrado para quien no sea SuperAdmin.
/// </summary>
public class DevSeedSoloEnDesarrolloTests
{
    private static ResourceExecutingContext Contexto(string entorno)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(entorno);
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(env.Object).BuildServiceProvider()
        };
        var accion = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new ResourceExecutingContext(accion, new List<IFilterMetadata>(), new List<IValueProviderFactory>());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Fuera_de_Development_el_filtro_responde_404(string entorno)
    {
        var ctx = Contexto(entorno);
        new DevelopmentOnlyAttribute().OnResourceExecuting(ctx);
        ctx.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void En_Development_el_filtro_deja_pasar()
    {
        var ctx = Contexto(Environments.Development);
        new DevelopmentOnlyAttribute().OnResourceExecuting(ctx);
        ctx.Result.Should().BeNull();
    }

    [Fact]
    public void El_controlador_es_solo_de_Development_y_exige_SuperAdmin()
    {
        var t = typeof(DevSeedController);
        t.GetCustomAttribute<DevelopmentOnlyAttribute>().Should().NotBeNull();
        t.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin");
        t.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }

    [Fact]
    public void Solo_el_arranque_del_superadmin_admite_anonimos()
    {
        var anonimas = typeof(DevSeedController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => m.Name);
        anonimas.Should().Equal(nameof(DevSeedController.SeedSuperAdmin));
    }

    // ── Arranque del superadmin ─────────────────────────────────────────────

    private static (DevSeedController Controller, Mock<UserManager<ApplicationUser>> Users) Controlador(
        int superAdminsExistentes, ClaimsPrincipal usuario)
    {
        var users = UserManagerHelper.Create();
        users.Setup(u => u.GetUsersInRoleAsync("SuperAdmin"))
             .ReturnsAsync(Enumerable.Range(0, superAdminsExistentes).Select(i => new ApplicationUser { Email = $"sa{i}@x.test" }).ToList());
        users.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var roles = new Mock<RoleManager<IdentityRole>>(Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);
        roles.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(new DateTime(2026, 10, 18, 10, 0, 0));

        var c = new DevSeedController(users.Object, roles.Object, env.Object, reloj.Object,
            InMemoryDbHelper.Create(), Mock.Of<IPlacementService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario } }
        };
        return (c, users);
    }

    private static ClaimsPrincipal Anonimo() => new(new ClaimsIdentity());

    private static ClaimsPrincipal Con(string rol) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "x"), new Claim(ClaimTypes.Role, rol) }, "Bearer"));

    [Fact]
    public async Task Sin_ningun_superadmin_el_arranque_anonimo_crea_el_primero()
    {
        var (c, users) = Controlador(0, Anonimo());
        var r = await c.SeedSuperAdmin(new DevSeedController.SeedRequest("sa@x.test", "Secreta!1"), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
        users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Con_un_superadmin_el_arranque_anonimo_queda_cerrado()
    {
        var (c, users) = Controlador(1, Anonimo());
        var r = await c.SeedSuperAdmin(new DevSeedController.SeedRequest("otro@x.test", "Secreta!1"), CancellationToken.None);
        r.Result.Should().BeOfType<UnauthorizedResult>();
        users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        users.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Con_un_superadmin_otro_rol_recibe_403()
    {
        var (c, users) = Controlador(1, Con("Admin"));
        var r = await c.SeedSuperAdmin(new DevSeedController.SeedRequest("otro@x.test", "Secreta!1"), CancellationToken.None);
        r.Result.Should().BeOfType<ForbidResult>();
        users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Con_un_superadmin_otro_superadmin_si_puede_usarlo()
    {
        var (c, users) = Controlador(1, Con("SuperAdmin"));
        var r = await c.SeedSuperAdmin(new DevSeedController.SeedRequest("otro@x.test", "Secreta!1"), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
        users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Once);
    }
}
