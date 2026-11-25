using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ActividadDeEstadosDeCobro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "SubscriptionBillingStates",
                type: "datetime2",
                nullable: false,
                computedColumnSql: "COALESCE([LastAttemptAt], [CreationDate])",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_LastActivityAt",
                table: "SubscriptionBillingStates",
                columns: new[] { "LastActivityAt", "Id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionBillingStates_LastActivityAt",
                table: "SubscriptionBillingStates");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "SubscriptionBillingStates");
        }
    }
}
