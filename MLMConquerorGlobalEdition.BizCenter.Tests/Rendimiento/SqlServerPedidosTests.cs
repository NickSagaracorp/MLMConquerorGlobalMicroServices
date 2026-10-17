using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M, Bloque 1: lo que la migración PedidosAcotadosEIndices deja en la base.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPedidosTests
{
    private static Task<int> BytesAsync(AppDbContext db, string tabla, string columna) =>
        db.Database.SqlQuery<int>($"""
            SELECT CAST(c.max_length AS int) AS [Value] FROM sys.columns c
            WHERE c.object_id = OBJECT_ID({tabla}) AND c.name = {columna}
            """).SingleAsync();

    private static Task<int> CuentaAsync(AppDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<int>(sql).SingleAsync();

    [SqlServerFact]
    public async Task Las_columnas_quedan_acotadas()
    {
        await using var db = SqlServerReportes.Db();

        (await BytesAsync(db, "Orders", "OrderNo")).Should().Be(80);
        (await BytesAsync(db, "Orders", "MemberId")).Should().Be(40);
        (await BytesAsync(db, "Orders", "MembershipSubscriptionId")).Should().Be(900);
        (await BytesAsync(db, "OrderDetails", "OrderId")).Should().Be(900);
        (await BytesAsync(db, "OrderDetails", "ProductId")).Should().Be(900);
        (await BytesAsync(db, "PaymentHistories", "OrderId")).Should().Be(900);
        (await BytesAsync(db, "PaymentHistories", "MemberId")).Should().Be(200);
        (await BytesAsync(db, "PaymentHistories", "GatewayTransactionId")).Should().Be(400);
        (await BytesAsync(db, "MembershipSubscriptions", "LastOrderId")).Should().Be(900);
        (await BytesAsync(db, "MemberStatistics", "MemberId")).Should().Be(200);
    }

    [SqlServerFact]
    public async Task Las_sombras_desaparecen_y_las_claves_foraneas_existen()
    {
        await using var db = SqlServerReportes.Db();

        (await CuentaAsync(db, $"""
            SELECT COUNT(*) AS [Value] FROM sys.columns
            WHERE (object_id = OBJECT_ID('OrderDetails') AND name = 'OrdersId')
               OR (object_id = OBJECT_ID('PaymentHistories') AND name = 'OrdersId')
               OR (object_id = OBJECT_ID('MembershipSubscriptions') AND name = 'LastOrderId1')
            """)).Should().Be(0);
        (await CuentaAsync(db, $"""
            SELECT COUNT(*) AS [Value] FROM sys.foreign_keys
            WHERE name IN ('FK_OrderDetails_Orders_OrderId', 'FK_MembershipSubscriptions_Orders_LastOrderId')
            """)).Should().Be(2);
    }

    [SqlServerFact]
    public async Task Los_hijos_del_binario_tienen_el_indice_con_sus_columnas_incluidas()
    {
        await using var db = SqlServerReportes.Db();

        var incluidas = await db.Database.SqlQuery<string>($"""
            SELECT c.name AS [Value]
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('DualTeamTree') AND i.name = 'IX_DualTeamTree_ParentMemberId'
              AND ic.is_included_column = 1
            """).ToListAsync();
        var claves = await db.Database.SqlQuery<string>($"""
            SELECT c.name AS [Value]
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('DualTeamTree') AND i.name = 'IX_DualTeamTree_ParentMemberId'
              AND ic.is_included_column = 0
            """).ToListAsync();

        claves.Should().Equal("ParentMemberId");
        incluidas.Should().BeEquivalentTo("MemberId", "Side", "IsDeleted");
    }

    [SqlServerFact]
    public async Task Numero_de_pedido_y_lineas_hacen_seek()
    {
        await using var db = SqlServerReportes.Db();
        var numero = "NO-EXISTE";
        var pedido = "NO-EXISTE";

        var planNumero = await PlanDeEjecucion.EstimadoAsync(
            db.Orders.Where(o => o.OrderNo == numero).Select(o => o.Id).ToQueryString());
        var planLineas = await PlanDeEjecucion.EstimadoAsync(
            db.OrderDetails.Where(d => d.OrderId == pedido).Select(d => d.ProductId).ToQueryString());

        planNumero.Should().Contain("IX_Orders_OrderNo").And.Contain(PlanDeEjecucion.Seek);
        planLineas.Should().Contain("IX_OrderDetails_OrderId").And.Contain(PlanDeEjecucion.Seek);
    }
}
