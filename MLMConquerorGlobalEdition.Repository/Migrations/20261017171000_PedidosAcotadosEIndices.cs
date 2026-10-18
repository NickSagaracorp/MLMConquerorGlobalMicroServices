using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class PedidosAcotadosEIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan M (R8, M24): ninguna alteración trunca ni crea una clave sobre datos que no la cumplen.
            // Si una comprobación falla, la migración se detiene ANTES de tocar nada.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([OrderNo]) / 2 > 40)
    THROW 51020, N'Orders.OrderNo tiene valores de mas de 40 caracteres (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [Orders] WHERE [OrderNo] IS NOT NULL GROUP BY [OrderNo] HAVING COUNT(*) > 1)
    THROW 51021, N'Orders.OrderNo tiene duplicados: ver Scripts/sql/plan-m/01-orderno-duplicados.sql y R15 del Plan M.', 1;
IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([MemberId]) / 2 > 20 OR DATALENGTH([MembershipSubscriptionId]) / 2 > 450)
    THROW 51022, N'Orders.MemberId o MembershipSubscriptionId no caben (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [OrderDetails] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([ProductId]) / 2 > 450)
    THROW 51023, N'OrderDetails.OrderId o ProductId no caben (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [OrderDetails] d WHERE NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = d.[OrderId]))
    THROW 51024, N'OrderDetails tiene lineas sin pedido: no se puede crear FK_OrderDetails_Orders_OrderId.', 1;
IF EXISTS (SELECT 1 FROM [PaymentHistories] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([MemberId]) / 2 > 100 OR DATALENGTH([GatewayTransactionId]) / 2 > 200)
    THROW 51025, N'PaymentHistories.OrderId, MemberId o GatewayTransactionId no caben (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] WHERE DATALENGTH([LastOrderId]) / 2 > 450)
    THROW 51026, N'MembershipSubscriptions.LastOrderId no cabe (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] s WHERE s.[LastOrderId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = s.[LastOrderId]))
    THROW 51027, N'MembershipSubscriptions.LastOrderId apunta a pedidos que no existen: no se puede crear la FK.', 1;
IF EXISTS (SELECT 1 FROM [MemberStatistics] WHERE DATALENGTH([MemberId]) / 2 > 100)
    THROW 51028, N'MemberStatistics.MemberId no cabe (Plan M, Tarea 5).', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_Orders_LastOrderId1",
                table: "MembershipSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Orders_OrdersId",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentHistories_Orders_OrdersId",
                table: "PaymentHistories");

            migrationBuilder.DropIndex(
                name: "IX_PaymentHistories_OrdersId",
                table: "PaymentHistories");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_OrdersId",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_MembershipSubscriptions_LastOrderId1",
                table: "MembershipSubscriptions");

            // Plan M: la base local no tenía este índice (deriva: lo crea MakeHierarchyPathUnboundedDropPathIndex,
            // pero alguien lo borró a mano). Se suelta solo si existe y se recrea más abajo con INCLUDE.
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_DualTeamTree_ParentMemberId] ON [DualTeamTree];");

            migrationBuilder.DropColumn(
                name: "OrdersId",
                table: "PaymentHistories");

            migrationBuilder.DropColumn(
                name: "OrdersId",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "LastOrderId1",
                table: "MembershipSubscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "PaymentHistories",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "PaymentHistories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "GatewayTransactionId",
                table: "PaymentHistories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrderNo",
                table: "Orders",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MembershipSubscriptionId",
                table: "Orders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ProductId",
                table: "OrderDetails",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "OrderDetails",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "MemberStatistics",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "LastOrderId",
                table: "MembershipSubscriptions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_GatewayTransactionId",
                table: "PaymentHistories",
                column: "GatewayTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_MemberId_TransactionStatus",
                table: "PaymentHistories",
                columns: new[] { "MemberId", "TransactionStatus" })
                .Annotation("SqlServer:Include", new[] { "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_OrderId",
                table: "PaymentHistories",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MemberId_OrderDate",
                table: "Orders",
                columns: new[] { "MemberId", "OrderDate" })
                .Annotation("SqlServer:Include", new[] { "Status", "CreationDate", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MembershipSubscriptionId",
                table: "Orders",
                column: "MembershipSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders",
                column: "OrderDate")
                .Annotation("SqlServer:Include", new[] { "Status", "TotalAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNo",
                table: "Orders",
                column: "OrderNo",
                unique: true,
                filter: "[OrderNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_OrderId",
                table: "OrderDetails",
                column: "OrderId")
                .Annotation("SqlServer:Include", new[] { "ProductId", "Quantity", "UnitPrice" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatistics_MemberId",
                table: "MemberStatistics",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_LastOrderId",
                table: "MembershipSubscriptions",
                column: "LastOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_ParentMemberId",
                table: "DualTeamTree",
                column: "ParentMemberId")
                .Annotation("SqlServer:Include", new[] { "MemberId", "Side", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_Orders_LastOrderId",
                table: "MembershipSubscriptions",
                column: "LastOrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Orders_OrderId",
                table: "OrderDetails",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_Orders_LastOrderId",
                table: "MembershipSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Orders_OrderId",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_PaymentHistories_GatewayTransactionId",
                table: "PaymentHistories");

            migrationBuilder.DropIndex(
                name: "IX_PaymentHistories_MemberId_TransactionStatus",
                table: "PaymentHistories");

            migrationBuilder.DropIndex(
                name: "IX_PaymentHistories_OrderId",
                table: "PaymentHistories");

            migrationBuilder.DropIndex(
                name: "IX_Orders_MemberId_OrderDate",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_MembershipSubscriptionId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNo",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_OrderId",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_MemberStatistics_MemberId",
                table: "MemberStatistics");

            migrationBuilder.DropIndex(
                name: "IX_MembershipSubscriptions_LastOrderId",
                table: "MembershipSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_DualTeamTree_ParentMemberId",
                table: "DualTeamTree");

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "PaymentHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "PaymentHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "GatewayTransactionId",
                table: "PaymentHistories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrdersId",
                table: "PaymentHistories",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrderNo",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MembershipSubscriptionId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ProductId",
                table: "OrderDetails",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "OrderDetails",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<string>(
                name: "OrdersId",
                table: "OrderDetails",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "MemberStatistics",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "LastOrderId",
                table: "MembershipSubscriptions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastOrderId1",
                table: "MembershipSubscriptions",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_OrdersId",
                table: "PaymentHistories",
                column: "OrdersId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_OrdersId",
                table: "OrderDetails",
                column: "OrdersId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_LastOrderId1",
                table: "MembershipSubscriptions",
                column: "LastOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_ParentMemberId",
                table: "DualTeamTree",
                column: "ParentMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_Orders_LastOrderId1",
                table: "MembershipSubscriptions",
                column: "LastOrderId1",
                principalTable: "Orders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Orders_OrdersId",
                table: "OrderDetails",
                column: "OrdersId",
                principalTable: "Orders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentHistories_Orders_OrdersId",
                table: "PaymentHistories",
                column: "OrdersId",
                principalTable: "Orders",
                principalColumn: "Id");
        }
    }
}
