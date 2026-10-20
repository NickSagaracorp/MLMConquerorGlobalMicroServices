using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SelectProducts;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// Plan Q (DQ10): MemberProfiles.SignupMembershipLevelId lo escriben la eleccion de productos del alta y la activacion,
/// con SignupMembershipLevel (regla N2: el nivel mas alto del primer pedido con productos de membresia).
/// </summary>
public class NivelDeAltaGuardadoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 12, 0, 0);

    private static async Task<(Repository.Context.AppDbContext Db, MemberProfile Miembro, Orders Pedido)> AltaAsync()
    {
        var db = InMemoryDbHelper.Create();
        var miembro = new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = "AMB-NIVEL", FirstName = "Nivel", LastName = "Alta", Email = "nivel@prueba.test",
            Country = "US", MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Pending, EnrollDate = Ahora,
            CreatedBy = "seed"
        };
        var pedido = new Orders { Id = "ORD-NIVEL", MemberId = "AMB-NIVEL", Status = OrderStatus.Pending, OrderDate = Ahora, CreatedBy = "seed" };
        db.AddRange(miembro, pedido,
            new Product { Id = "P-LIFE", Name = "Lifestyle", Description = "x", ImageUrl = "x", MembershipLevelId = 1, SetupFee = 50, MonthlyFee = 30, IsActive = true, CreatedBy = "seed" },
            new Product { Id = "P-VIP",  Name = "VIP",       Description = "x", ImageUrl = "x", MembershipLevelId = 2, SetupFee = 50, MonthlyFee = 30, IsActive = true, CreatedBy = "seed" });
        await db.SaveChangesAsync();
        return (db, miembro, pedido);
    }

    [Fact]
    public async Task Elegir_los_productos_del_alta_guarda_el_nivel_mas_alto()
    {
        var (db, _, _) = await AltaAsync();
        await using var _db = db;
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(d => d.Now).Returns(Ahora);

        var r = await new SelectProductsHandler(db, reloj.Object).Handle(
            new SelectProductsCommand("ORD-NIVEL", new SelectProductsRequest { ProductIds = ["P-LIFE", "P-VIP"] }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        (await db.MemberProfiles.AsNoTracking().SingleAsync(m => m.MemberId == "AMB-NIVEL")).SignupMembershipLevelId.Should().Be(2);
    }

    [Fact]
    public async Task La_activacion_guarda_el_nivel_de_alta()
    {
        var (db, miembro, pedido) = await AltaAsync();
        await using var _db = db;
        db.OrderDetails.Add(new OrderDetail { OrderId = pedido.Id, ProductId = "P-VIP", Quantity = 1, CreatedBy = "seed" });
        var suscripcion = new MembershipSubscription
        {
            MemberId = "AMB-NIVEL", MembershipLevelId = 2, SubscriptionStatus = MembershipStatus.Pending, CreatedBy = "seed"
        };
        db.Add(suscripcion);
        await db.SaveChangesAsync();
        var servicio = new SignupActivationService(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
            Mock.Of<IRecurringBillingEnrollmentService>());

        await servicio.ActivateAsync(pedido, miembro, suscripcion, 6, Ahora, "nivel@prueba.test", CancellationToken.None);
        await db.SaveChangesAsync();

        (await db.MemberProfiles.AsNoTracking().SingleAsync(m => m.MemberId == "AMB-NIVEL")).SignupMembershipLevelId.Should().Be(2);
    }
}
