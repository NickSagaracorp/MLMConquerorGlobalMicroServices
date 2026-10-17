using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class CuotaAnual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan K. Guardas antes de tocar nada: mejor fallar el despliegue que dejar datos que
            // contradicen el modelo o truncar. (1) El índice único filtrado de IsAnnualFeePlan fallaría con un
            // mensaje oscuro si hubiera dos planes con ese nombre. (2) Un estado por suscripción en el
            // plan anual quedaría como estado de membresía en el plan de la cuota: la cuota se cobraría
            // dos veces (K2). En local no hay ninguno de los dos casos.
            migrationBuilder.Sql(@"
IF (SELECT COUNT(*) FROM [RecurringBillingPlans] WHERE [Name] = N'Lifestyle Ambassador (Annual)') > 1
    THROW 51010, N'Hay mas de un plan Lifestyle Ambassador (Annual): dejar uno antes de migrar (Plan K, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [SubscriptionBillingStates] s
           JOIN [RecurringBillingPlans] p ON p.[Id] = s.[RecurringBillingPlanId]
           WHERE p.[Name] = N'Lifestyle Ambassador (Annual)')
    THROW 51011, N'Hay estados de facturacion por suscripcion en el plan anual: revisarlos antes de migrar (Plan K, Tarea 5).', 1;
-- Anti-truncado: la columna se vuelve anulable manteniendo nvarchar(100). Si en esta base fuera mas
-- ancha (alguien la amplio a mano), el ALTER la estrecharia: mejor parar.
IF COL_LENGTH(N'SubscriptionBillingStates', N'MembershipSubscriptionId') = -1
   OR COL_LENGTH(N'SubscriptionBillingStates', N'MembershipSubscriptionId') > 200
    THROW 51012, N'SubscriptionBillingStates.MembershipSubscriptionId es mas ancha que nvarchar(100): revisar antes de migrar (Plan K, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [SubscriptionBillingStates] WHERE LEN([MembershipSubscriptionId]) > 100)
    THROW 51013, N'Hay MembershipSubscriptionId de mas de 100 caracteres: se truncarian (Plan K, Tarea 5).', 1;");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionBillingStates_MembershipSubscriptionId",
                table: "SubscriptionBillingStates");

            // Se amplía la nulabilidad, no la longitud: nada se trunca.
            migrationBuilder.AlterColumn<string>(
                name: "MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "SubscriptionBillingStates",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnnualFeePlan",
                table: "RecurringBillingPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // El plan que ya existe es el de la cuota (K2). Es nuestro catálogo, no un dato del legado.
            migrationBuilder.Sql(
                "UPDATE [RecurringBillingPlans] SET [IsAnnualFeePlan] = 1 WHERE [Name] = N'Lifestyle Ambassador (Annual)';");

            migrationBuilder.AddColumn<bool>(
                name: "FeesFromBalance",
                table: "MemberProfiles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AnnualFeePayments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubscriptionBillingStateId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CycleDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PaymentHistoryId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TokenTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    TokenCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualFeePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualFeePayments_SubscriptionBillingStates_SubscriptionBillingStateId",
                        column: x => x.SubscriptionBillingStateId,
                        principalTable: "SubscriptionBillingStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualFeePayments_MemberId_CreationDate",
                table: "AnnualFeePayments",
                columns: new[] { "MemberId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualFeePayments_OrderId",
                table: "AnnualFeePayments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualFeePayments_Status_CreationDate",
                table: "AnnualFeePayments",
                columns: new[] { "Status", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "UX_AnnualFeePayments_LivePerCycle",
                table: "AnnualFeePayments",
                columns: new[] { "SubscriptionBillingStateId", "CycleDueDate" },
                unique: true,
                filter: "[Status] IN (1, 2, 4)");

            migrationBuilder.CreateIndex(
                name: "UX_AnnualFeePayments_Member_IdempotencyKey",
                table: "AnnualFeePayments",
                columns: new[] { "MemberId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                column: "MembershipSubscriptionId",
                unique: true,
                filter: "[MembershipSubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SubscriptionBillingStates_AnnualFeeMember",
                table: "SubscriptionBillingStates",
                column: "MemberId",
                unique: true,
                filter: "[Purpose] = 2");

            migrationBuilder.CreateIndex(
                name: "UX_RecurringBillingPlans_AnnualFee",
                table: "RecurringBillingPlans",
                column: "IsAnnualFeePlan",
                unique: true,
                filter: "[IsAnnualFeePlan] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionBillingStates_Purpose",
                table: "SubscriptionBillingStates",
                sql: "([Purpose] = 1 AND [MembershipSubscriptionId] IS NOT NULL) OR ([Purpose] = 2 AND [MembershipSubscriptionId] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionBillingStates_Purpose",
                table: "SubscriptionBillingStates");

            migrationBuilder.DropTable(name: "AnnualFeePayments");

            migrationBuilder.DropIndex(name: "UX_RecurringBillingPlans_AnnualFee", table: "RecurringBillingPlans");
            migrationBuilder.DropIndex(name: "UX_SubscriptionBillingStates_AnnualFeeMember", table: "SubscriptionBillingStates");
            migrationBuilder.DropIndex(name: "IX_SubscriptionBillingStates_MembershipSubscriptionId", table: "SubscriptionBillingStates");

            // Volver atrás borra los estados de cuota anual (no tienen suscripción y la columna vuelve a
            // ser obligatoria). Se recrean con AnnualFeeStateEnrollmentJob si se vuelve a migrar.
            migrationBuilder.Sql("DELETE FROM [SubscriptionBillingStates] WHERE [Purpose] = 2;");

            migrationBuilder.DropColumn(name: "Purpose", table: "SubscriptionBillingStates");
            migrationBuilder.DropColumn(name: "IsAnnualFeePlan", table: "RecurringBillingPlans");
            migrationBuilder.DropColumn(name: "FeesFromBalance", table: "MemberProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                column: "MembershipSubscriptionId",
                unique: true);
        }
    }
}
