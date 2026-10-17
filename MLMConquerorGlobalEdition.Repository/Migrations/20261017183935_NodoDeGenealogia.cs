using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.SqlServer.Types;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class NodoDeGenealogia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan M: NodeNullKey copia el Id como varchar(100). Un Id más largo se truncaría y dos filas sin
            // Node podrían chocar en el índice único: la migración se detiene antes de tocar nada.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [GenealogyTree] WHERE DATALENGTH([Id]) / 2 > 100)
    THROW 51033, N'GenealogyTree.Id tiene valores de mas de 100 caracteres: no caben en NodeNullKey (Plan M, Tarea 9).', 1;");

            migrationBuilder.AddColumn<SqlHierarchyId>(
                name: "Node",
                table: "GenealogyTree",
                type: "hierarchyid",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "NodeLevel",
                table: "GenealogyTree",
                type: "smallint",
                nullable: true,
                computedColumnSql: "[Node].GetLevel()",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "NodeNullKey",
                table: "GenealogyTree",
                type: "varchar(100)",
                nullable: true,
                computedColumnSql: "CASE WHEN [Node] IS NULL THEN CAST([Id] AS varchar(100)) END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_Node",
                table: "GenealogyTree",
                columns: new[] { "Node", "NodeNullKey" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "MemberId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_NodeLevel_Node",
                table: "GenealogyTree",
                columns: new[] { "NodeLevel", "Node" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GenealogyTree_Node",
                table: "GenealogyTree");

            migrationBuilder.DropIndex(
                name: "IX_GenealogyTree_NodeLevel_Node",
                table: "GenealogyTree");

            migrationBuilder.DropColumn(
                name: "NodeLevel",
                table: "GenealogyTree");

            migrationBuilder.DropColumn(
                name: "NodeNullKey",
                table: "GenealogyTree");

            migrationBuilder.DropColumn(
                name: "Node",
                table: "GenealogyTree");
        }
    }
}
