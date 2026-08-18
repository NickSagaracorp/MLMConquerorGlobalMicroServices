using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletProviderAccountCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderAccountCreatedAt",
                table: "Wallets",
                type: "datetime2",
                nullable: true);

            // SIN BACKFILL, a proposito.
            //
            // Seria tentador marcar como "cuenta abierta" a todas las wallets que ya estan
            // Approved, pero ese estado NO prueba que exista una cuenta del lado del
            // proveedor: hasta ahora el alta era un stub simulado que devolvia Approved sin
            // llamar a nadie. Marcarlas dejaria a esos miembros fuera del job de alta
            // diferida para siempre, y descubririan el problema el dia que intenten cobrar.
            //
            // Dejandolas en NULL, el job las evalua. Y no hay riesgo de pagar una cuenta dos
            // veces: el registrador hace ValidateAccount ANTES de crear, asi que a quien ya
            // tenga cuenta (por ejemplo los migrados de MWRLife) sencillamente no le abre otra.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderAccountCreatedAt",
                table: "Wallets");
        }
    }
}
