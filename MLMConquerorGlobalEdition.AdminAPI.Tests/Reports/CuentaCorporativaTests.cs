using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>El admin marca o desmarca la cuenta corporativa; el cambio queda en GeneralAuditTracking.</summary>
public class CuentaCorporativaTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 0, 0);

    private static AppDbContext Base()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = "AMB-1", FirstName = "MWR", LastName = "Corp.",
            Email = "info@example.com", Country = "US", CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.SaveChanges();
        return db;
    }

    private static AdminMemberCorporateAccountController Controlador(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var usuario = new Mock<ICurrentUserService>();
        usuario.SetupGet(u => u.UserId).Returns("admin-1");
        return new AdminMemberCorporateAccountController(db, reloj.Object, usuario.Object);
    }

    [Fact]
    public async Task Marcar_guarda_el_indicador_y_audita_el_cambio()
    {
        using var db = Base();

        var r = await Controlador(db).Set("AMB-1", new AdminMemberCorporateAccountController.SetRequest(true), default);

        r.Should().BeOfType<OkObjectResult>();
        (await db.MemberProfiles.SingleAsync()).IsCorporateAccount.Should().BeTrue();
        var audit = await db.AuditTracking.SingleAsync();
        audit.EntityName.Should().Be("MemberProfile");
        audit.EntityId.Should().Be("AMB-1");
        audit.Action.Should().Be("CorporateAccountChanged");
        audit.OldValues.Should().Be("{\"IsCorporateAccount\":false}");
        audit.NewValues.Should().Be("{\"IsCorporateAccount\":true}");
        audit.ChangedBy.Should().Be("admin-1");
        audit.ChangedAt.Should().Be(Ahora);
    }

    [Fact]
    public async Task Sin_cambio_no_audita()
    {
        using var db = Base();

        await Controlador(db).Set("AMB-1", new AdminMemberCorporateAccountController.SetRequest(false), default);

        (await db.AuditTracking.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Miembro_inexistente_da_404()
    {
        using var db = Base();

        var r = await Controlador(db).Set("AMB-9", new AdminMemberCorporateAccountController.SetRequest(true), default);

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
    {
        typeof(AdminMemberCorporateAccountController).GetCustomAttribute<AuthorizeAttribute>()!
            .Roles.Should().Be("SuperAdmin,Admin");
    }
}
