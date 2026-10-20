using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UnNodoPorMiembro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan Q (DQ9): falla cerrada si hay repetidos; 01-nodos-de-inscripcion-repetidos.sql los deja en uno.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM GenealogyTree WHERE IsDeleted = 0 GROUP BY MemberId HAVING COUNT(*) > 1)
    THROW 51108, N'GenealogyTree tiene miembros con mas de un nodo vivo: ejecutar antes Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql (Plan Q, Tarea 21).', 1;");

            migrationBuilder.CreateIndex(
                name: "UX_GenealogyTree_MemberId_Vivo",
                table: "GenealogyTree",
                column: "MemberId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_GenealogyTree_MemberId_Vivo",
                table: "GenealogyTree");
        }
    }
}
