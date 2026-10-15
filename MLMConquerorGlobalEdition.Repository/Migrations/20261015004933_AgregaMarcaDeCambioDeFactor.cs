using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Columna AspNetUsers.SecurityFactorsChangedAt: cuándo cambió por última vez un canal por el que
    /// la cuenta recibe códigos. Nullable y sin valor inicial a propósito: las cuentas que ya existen
    /// no tienen un cambio reciente que esperar, y rellenarla con la fecha de la migración dejaría a
    /// todos los miembros un día sin poder cambiar su método de cobro.
    /// </summary>
    public partial class AgregaMarcaDeCambioDeFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SecurityFactorsChangedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityFactorsChangedAt",
                table: "AspNetUsers");
        }
    }
}
