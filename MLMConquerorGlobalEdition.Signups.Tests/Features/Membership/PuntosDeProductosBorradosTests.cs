using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// Plan Q (tercera revision del bloque 2, menor): los puntos de un nivel o de un pedido salen tambien de un producto borrado
/// logicamente (el filtro de consulta de Product lo escondia), como los cuenta 05-realinear-puntos-por-membresia.sql. Si
/// no, al bajar o subir desde un nivel cuyo producto se retiro se restaban 0 puntos viejos y la linea quedaba inflada.
/// </summary>
public class PuntosDeProductosBorradosTests
{
    private static Product Producto(string id, int nivel, int puntos) => new()
    {
        Id = id, Name = id, Description = "", ImageUrl = "", MembershipLevelId = nivel, QualificationPoins = puntos,
        MonthlyFee = 10m, SetupFee = 0m, IsActive = false, IsDeleted = true, CreatedBy = "prueba"
    };

    [Fact]
    public async Task Los_puntos_del_nivel_cuentan_un_producto_borrado()
    {
        await using var db = InMemoryDbHelper.Create();
        db.Products.Add(Producto("prod-viejo", 7, 6));
        await db.SaveChangesAsync();

        (await MembershipPointsDeltas.LevelPointsAsync(db, 7)).Should().Be(6);
    }

    [Fact]
    public async Task Los_puntos_del_pedido_cuentan_un_producto_borrado()
    {
        await using var db = InMemoryDbHelper.Create();
        db.Products.Add(Producto("prod-viejo", 7, 6));
        db.OrderDetails.Add(new OrderDetail { OrderId = "pedido-1", ProductId = "prod-viejo", Quantity = 1, UnitPrice = 10m, CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await MembershipPointsDeltas.OrderPointsAsync(db, "pedido-1")).Should().Be(6);
    }
}
