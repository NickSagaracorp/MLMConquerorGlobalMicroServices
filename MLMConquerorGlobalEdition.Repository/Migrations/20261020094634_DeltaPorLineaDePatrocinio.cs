using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class DeltaPorLineaDePatrocinio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "MemberStatisticDeltas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Plan Q: los ascendientes de un Node de inscripcion, el mismo incluido (Distancia 0), sin leer la tabla.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.GenealogyAncestors(@node hierarchyid)
RETURNS TABLE AS RETURN
SELECT n.N AS Distancia, @node.GetAncestor(n.N) AS Node
FROM dbo.Numeros(@node.GetLevel() - 1) n;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.GenealogyAncestors;");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "MemberStatisticDeltas");
        }
    }
}
