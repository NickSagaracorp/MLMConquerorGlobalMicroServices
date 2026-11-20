using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class HuecoUnicoDelBinario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No se crea la clave sobre datos que no la cumplen. Si quedan huecos repetidos, la
            // migracion se detiene sin tocar nada (y un servicio que la aplique al arrancar no arranca).
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [DualTeamTree] WHERE [IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL
           GROUP BY [ParentMemberId], [Side] HAVING COUNT(*) > 1)
    THROW 51048, N'DualTeamTree tiene huecos repetidos: reparar los huecos repetidos del binario antes de migrar.', 1;");

            migrationBuilder.CreateIndex(
                name: "UX_DualTeamTree_ParentMemberId_Side",
                table: "DualTeamTree",
                columns: new[] { "ParentMemberId", "Side" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_DualTeamTree_ParentMemberId_Side",
                table: "DualTeamTree");
        }
    }
}
