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
            // Plan M: NodeNullKey copia el Id como nvarchar(100). Un Id más largo se truncaría y dos filas sin
            // Node podrían chocar en el índice único: la migración se detiene antes de tocar nada.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [GenealogyTree] WHERE DATALENGTH([Id]) / 2 > 100)
    THROW 51033, N'GenealogyTree.Id tiene valores de mas de 100 caracteres: no caben en NodeNullKey (Plan M, Tarea 9).', 1;");

            // Revisión M-h: como el de DualTeamTree en PedidosAcotadosEIndices, se suelta solo si existe (una base
            // con deriva podría no tenerlo) y se recrea más abajo con INCLUDE (Node).
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_GenealogyTree_ParentMemberId] ON [GenealogyTree];");

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
                type: "nvarchar(100)",
                nullable: true,
                computedColumnSql: "CASE WHEN [Node] IS NULL THEN CAST([Id] AS nvarchar(100)) END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_Node",
                table: "GenealogyTree",
                columns: new[] { "Node", "NodeNullKey" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "MemberId", "Level", "CreationDate", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_NodeLevel_Node",
                table: "GenealogyTree",
                columns: new[] { "NodeLevel", "Node" });

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_ParentMemberId",
                table: "GenealogyTree",
                column: "ParentMemberId")
                .Annotation("SqlServer:Include", new[] { "Node" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GenealogyTree_Node_OrdinalesEnteros",
                table: "GenealogyTree",
                sql: "[Node] IS NULL OR CHARINDEX('.', [Node].ToString()) = 0");
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

            migrationBuilder.DropIndex(
                name: "IX_GenealogyTree_ParentMemberId",
                table: "GenealogyTree");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GenealogyTree_Node_OrdinalesEnteros",
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

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_ParentMemberId",
                table: "GenealogyTree",
                column: "ParentMemberId");
        }
    }
}
