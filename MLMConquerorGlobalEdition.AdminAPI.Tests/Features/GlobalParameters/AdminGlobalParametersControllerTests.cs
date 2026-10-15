using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.GlobalParameters;

/// <summary>
/// El CRUD de parámetros globales: solo SuperAdmin y Admin, rastro de quién y cuándo, la clave no
/// cambia una vez creada, y las direcciones de depósito cripto se validan porque cambiarlas desvía
/// dinero. Cada cambio queda en el historial y el de una dirección avisa a los SuperAdmin activos.
/// </summary>
public class AdminGlobalParametersControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private const string BtcKey = "Crypto.DepositAddress.BTC";
    private const string BtcOld = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh";
    private const string BtcNew = "bc1qar0srrr7xfkvy5l643lydnw9re59gtzzwf5mdq";

    private static AdminGlobalParametersController Build(AppDbContext db, Mock<IEmailService>? email = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.Email).Returns("admin@x.com");
        user.Setup(u => u.UserId).Returns("admin-1");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);

        email ??= new Mock<IEmailService>();
        var notifier = new CryptoDepositAlertNotifier(db, email.Object, clock.Object,
            NullLogger<CryptoDepositAlertNotifier>.Instance);

        return new AdminGlobalParametersController(db, user.Object, clock.Object, notifier,
            NullLogger<AdminGlobalParametersController>.Instance);
    }

    private static void SeedAdmins(AppDbContext db)
    {
        db.Roles.AddRange(
            new IdentityRole { Id = "r-super", Name = AppRoles.SuperAdmin, NormalizedName = AppRoles.SuperAdmin.ToUpperInvariant() },
            new IdentityRole { Id = "r-admin", Name = AppRoles.Admin,      NormalizedName = AppRoles.Admin.ToUpperInvariant() });

        ApplicationUser U(string id, string email, bool active) =>
            new() { Id = id, UserName = email, Email = email, IsActive = active, CreatedBy = "seed", CreationDate = Now };

        db.Users.AddRange(
            U("u1", "super1@x.com", true),
            U("u2", "super2@x.com", true),
            U("u3", "super-baja@x.com", false),
            U("u4", "solo-admin@x.com", true));
        db.UserRoles.AddRange(
            new IdentityUserRole<string> { UserId = "u1", RoleId = "r-super" },
            new IdentityUserRole<string> { UserId = "u2", RoleId = "r-super" },
            new IdentityUserRole<string> { UserId = "u3", RoleId = "r-super" },
            new IdentityUserRole<string> { UserId = "u4", RoleId = "r-admin" });
    }

    private static async Task<GlobalParameter> SeedBtc(AppDbContext db)
    {
        var p = new GlobalParameter { Key = BtcKey, Value = BtcOld, CreatedBy = "seed", CreationDate = Now.AddDays(-5) };
        db.GlobalParameters.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
        => typeof(AdminGlobalParametersController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.SuperAdmin + "," + AppRoles.Admin);

    [Fact]
    public async Task Crear_deja_el_rastro_de_quien_y_cuando()
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Build(db).Create(new CreateGlobalParameterRequest
        {
            Key = BtcKey, Value = " " + BtcOld + " ", Description = "Cold wallet"
        }, default);

        r.Should().BeOfType<CreatedAtActionResult>();
        var p = db.GlobalParameters.Single();
        p.Value.Should().Be(BtcOld);
        p.CreatedBy.Should().Be("admin@x.com");
        // AuditInterceptor sella CreationDate con la hora de la máquina al guardar; el rastro de
        // "cuándo" lo da él, así que se comprueba que esté sellado y sea de ahora.
        p.CreationDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Una_clave_repetida_es_409()
    {
        await using var db = InMemoryDbHelper.Create();
        db.GlobalParameters.Add(new GlobalParameter { Key = "Foo", Value = "1", CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        var r = await Build(db).Create(new CreateGlobalParameterRequest { Key = "Foo", Value = "2" }, default);

        r.Should().BeOfType<ConflictObjectResult>();
    }

    [Theory]
    [InlineData("Crypto.DepositAddress.BTC", "no es una direccion")]
    [InlineData("Crypto.DepositAddress.DOGE", "DH5yaieqoZN36fDVciNyRueRGvGLR3mr7L")]
    public async Task Una_direccion_cripto_invalida_o_de_moneda_no_admitida_es_400(string key, string value)
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Build(db).Create(new CreateGlobalParameterRequest { Key = key, Value = value }, default);

        r.Should().BeOfType<BadRequestObjectResult>();
        db.GlobalParameters.Should().BeEmpty();
    }

    [Fact]
    public async Task Actualizar_cambia_el_valor_y_el_rastro_pero_no_la_clave()
    {
        await using var db = InMemoryDbHelper.Create();
        var p = new GlobalParameter { Key = "DailyResidualConsolidationMinimum", Value = "100", CreatedBy = "seed", CreationDate = Now.AddDays(-5) };
        db.GlobalParameters.Add(p);
        await db.SaveChangesAsync();

        var r = await Build(db).Update(p.Id, new UpdateGlobalParameterRequest { Value = "150", Description = "subido" }, default);

        r.Should().BeOfType<OkObjectResult>();
        var saved = db.GlobalParameters.Single();
        saved.Key.Should().Be("DailyResidualConsolidationMinimum");
        saved.Value.Should().Be("150");
        saved.LastUpdateBy.Should().Be("admin@x.com");
        saved.LastUpdateDate.Should().NotBeNull();
        saved.LastUpdateDate!.Value.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Listar_filtra_por_prefijo()
    {
        await using var db = InMemoryDbHelper.Create();
        db.GlobalParameters.AddRange(
            new GlobalParameter { Key = "Crypto.DepositAddress.BTC", Value = "x", CreatedBy = "seed", CreationDate = Now },
            new GlobalParameter { Key = "Foo", Value = "1", CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        var r = await Build(db).List("Crypto.", default);

        ((OkObjectResult)r).Value.Should().BeOfType<ApiResponse<IEnumerable<GlobalParameterDto>>>()
            .Which.Data!.Select(d => d.Key).Should().Equal("Crypto.DepositAddress.BTC");
    }

    // ── Auditoría y aviso (decisión de cierre 2 del Plan F) ──────────────────────

    [Fact]
    public async Task Cada_cambio_queda_en_el_historial_con_el_valor_viejo_y_el_nuevo()
    {
        await using var db = InMemoryDbHelper.Create();
        var p = await SeedBtc(db);

        await Build(db).Update(p.Id, new UpdateGlobalParameterRequest { Value = BtcNew }, default);

        var audit = db.AuditTracking.Single();
        audit.EntityName.Should().Be(nameof(GlobalParameter));
        audit.EntityId.Should().Be(p.Id.ToString());
        audit.Action.Should().Be("Update");
        audit.OldValues.Should().Contain(BtcOld);
        audit.NewValues.Should().Contain(BtcNew);
        audit.ChangedBy.Should().Be("admin-1");
        audit.ChangedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Crear_tambien_queda_en_el_historial()
    {
        await using var db = InMemoryDbHelper.Create();

        await Build(db).Create(new CreateGlobalParameterRequest { Key = "Foo", Value = "1" }, default);

        var audit = db.AuditTracking.Single();
        audit.Action.Should().Be("Create");
        audit.OldValues.Should().BeNull();
        audit.EntityId.Should().Be(db.GlobalParameters.Single().Id.ToString());
    }

    [Fact]
    public async Task Cambiar_una_direccion_avisa_por_correo_a_los_SuperAdmin_activos()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedAdmins(db);
        var p = await SeedBtc(db);
        var email = new Mock<IEmailService>();

        await Build(db, email).Update(p.Id, new UpdateGlobalParameterRequest { Value = BtcNew }, default);

        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            NotificationEvents.CryptoDepositAddressChanged,
            It.Is<Dictionary<string, string>>(v =>
                v["Currency"] == "BTC" && v["OldAddress"] == BtcOld && v["NewAddress"] == BtcNew
                && v["ChangedBy"] == "admin@x.com" && v["ChangedAt"] == "2026-10-15 10:00"),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        email.Verify(e => e.SendAsync("super1@x.com", It.IsAny<string>(), "en", It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        email.Verify(e => e.SendAsync("super2@x.com", It.IsAny<string>(), "en", It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Crear_una_direccion_tambien_avisa()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedAdmins(db);
        await db.SaveChangesAsync();
        var email = new Mock<IEmailService>();

        await Build(db, email).Create(new CreateGlobalParameterRequest { Key = BtcKey, Value = BtcNew }, default);

        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            NotificationEvents.CryptoDepositAddressChanged,
            It.Is<Dictionary<string, string>>(v => v["OldAddress"] == "" && v["NewAddress"] == BtcNew),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Otro_parametro_o_la_misma_direccion_no_avisan()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedAdmins(db);
        var p = await SeedBtc(db);
        var email = new Mock<IEmailService>();
        var controller = Build(db, email);

        // Solo cambia la descripción: el dinero sigue yendo al mismo sitio.
        await controller.Update(p.Id, new UpdateGlobalParameterRequest { Value = BtcOld, Description = "fría" }, default);
        await controller.Create(new CreateGlobalParameterRequest { Key = "Foo", Value = "1" }, default);

        email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_fallo_del_correo_no_deshace_el_cambio()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedAdmins(db);
        var p = await SeedBtc(db);
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SES caído"));

        var r = await Build(db, email).Update(p.Id, new UpdateGlobalParameterRequest { Value = BtcNew }, default);

        r.Should().BeOfType<OkObjectResult>();
        db.GlobalParameters.Single().Value.Should().Be(BtcNew);
        db.AuditTracking.Should().ContainSingle();
        // Un buzón que falla no impide avisar a los demás.
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
