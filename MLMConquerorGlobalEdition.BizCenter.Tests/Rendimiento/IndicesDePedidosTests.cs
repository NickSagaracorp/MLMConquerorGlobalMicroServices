using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.SharedKernel;
using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M, Bloque 1: longitudes, índices y relaciones de pedidos, pagos, suscripciones, estadísticas
/// y binario, en el modelo. Que existan en la base lo comprueba SqlServerPedidosTests.
/// </summary>
public class IndicesDePedidosTests
{
    // Las columnas incluidas y los filtros son configuración de diseño: no están en el modelo de ejecución.
    private static IModel Modelo()
    {
        using var db = ReportesDb.Nueva();
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Tipo(Type t) => Modelo().FindEntityType(t)!;

    private static int? Longitud(Type t, string propiedad) => Tipo(t).FindProperty(propiedad)!.GetMaxLength();

    private static IIndex Indice(Type t, params string[] columnas) =>
        Tipo(t).GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(columnas));

    [Fact]
    public void Pedidos_acotados()
    {
        Longitud(typeof(OrdersEntity), "OrderNo").Should().Be(OrderNumberHelper.MaxLength);
        Longitud(typeof(OrdersEntity), "MemberId").Should().Be(20, "mide lo mismo que MemberProfiles.MemberId (M2)");
        Longitud(typeof(OrdersEntity), "MembershipSubscriptionId").Should().Be(450);
    }

    [Fact]
    public void Numero_de_pedido_unico()
    {
        var i = Indice(typeof(OrdersEntity), "OrderNo");
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[OrderNo] IS NOT NULL");
    }

    [Fact]
    public void Pedidos_por_miembro_y_fecha_cubren_estado_y_borrado()
        => Indice(typeof(OrdersEntity), "MemberId", "OrderDate").GetIncludeProperties()
            .Should().BeEquivalentTo("Status", "CreationDate", "IsDeleted");

    [Fact]
    public void Pedidos_por_suscripcion_y_por_fecha()
    {
        Indice(typeof(OrdersEntity), "MembershipSubscriptionId").Should().NotBeNull();
        Indice(typeof(OrdersEntity), "OrderDate").GetIncludeProperties().Should().BeEquivalentTo("Status", "TotalAmount");
    }

    [Fact]
    public void Lineas_por_pedido_con_clave_foranea_y_sin_sombra()
    {
        var t = Tipo(typeof(OrderDetail));
        t.FindProperty("OrdersId").Should().BeNull("la FK en sombra vacía desaparece (M6)");
        Longitud(typeof(OrderDetail), "OrderId").Should().Be(450);
        Longitud(typeof(OrderDetail), "ProductId").Should().Be(450);
        var fk = t.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(OrdersEntity));
        fk.Properties.Single().Name.Should().Be("OrderId");
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        Indice(typeof(OrderDetail), "OrderId").GetIncludeProperties().Should().BeEquivalentTo("ProductId", "Quantity", "UnitPrice");
    }

    [Fact]
    public void Pagos_acotados_indexados_y_sin_clave_foranea()
    {
        var t = Tipo(typeof(PaymentHistory));
        t.FindProperty("OrdersId").Should().BeNull();
        t.GetForeignKeys().Should().BeEmpty("el webhook guarda pagos sin pedido conocido (M7)");
        Longitud(typeof(PaymentHistory), "OrderId").Should().Be(450);
        Longitud(typeof(PaymentHistory), "MemberId").Should().Be(100);
        Longitud(typeof(PaymentHistory), "GatewayTransactionId").Should().Be(200);
        Indice(typeof(PaymentHistory), "OrderId").Should().NotBeNull();
        Indice(typeof(PaymentHistory), "MemberId", "TransactionStatus").GetIncludeProperties().Should().BeEquivalentTo("ProcessedAt");
        Indice(typeof(PaymentHistory), "GatewayTransactionId").Should().NotBeNull();
    }

    [Fact]
    public void La_ultima_orden_de_la_suscripcion_es_una_clave_foranea_real()
    {
        var t = Tipo(typeof(MembershipSubscription));
        t.FindProperty("LastOrderId1").Should().BeNull();
        Longitud(typeof(MembershipSubscription), "LastOrderId").Should().Be(450);
        t.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(OrdersEntity))
            .Properties.Single().Name.Should().Be("LastOrderId");
    }

    [Fact]
    public void Estadisticas_por_miembro()
    {
        Longitud(typeof(MemberStatisticEntity), "MemberId").Should().Be(100);
        Indice(typeof(MemberStatisticEntity), "MemberId").Should().NotBeNull();
    }

    [Fact]
    public void Los_hijos_del_binario_se_leen_sin_ir_a_la_tabla()
        => Indice(typeof(DualTeamEntity), "ParentMemberId").GetIncludeProperties()
            .Should().BeEquivalentTo("MemberId", "Side", "IsDeleted");
}
