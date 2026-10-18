using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ClaveCompactaDelBinario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "DualTeamRootOrdinal");

            migrationBuilder.AddColumn<byte[]>(
                name: "BinaryPath",
                table: "DualTeamTree",
                type: "varbinary(1700)",
                maxLength: 1700,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Depth",
                table: "DualTeamTree",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeftLegCount",
                table: "DualTeamTree",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RightLegCount",
                table: "DualTeamTree",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "UX_DualTeamTree_BinaryPath",
                table: "DualTeamTree",
                column: "BinaryPath",
                unique: true,
                filter: "[IsDeleted] = 0 AND [BinaryPath] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "MemberId", "Depth", "ParentMemberId", "Side" });

            // Plan P: la clave del hijo a partir de la del padre, el mismo algoritmo que DualBinaryPath.Child.
            // La usan el relleno (Scripts/sql/plan-p) y DualTeamNodes al mover o retirar subarboles.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.DualChildKey(@p varbinary(1700), @depth int, @side int)
RETURNS TABLE AS RETURN
WITH a AS (
    SELECT 4 + @depth / 8 + 1        AS bi,   -- byte (base 1) del terminador del padre = bit del lado
           7 - @depth % 8            AS si,
           4 + (@depth + 1) / 8 + 1  AS bj,   -- byte del terminador del hijo
           7 - (@depth + 1) % 8      AS sj)
SELECT CAST(
    SUBSTRING(@p, 1, a.bi - 1)
    + CASE WHEN a.bi = a.bj
           THEN CAST(CAST(((CAST(SUBSTRING(@p, a.bi, 1) AS int) & ~(CASE WHEN @side = 0 THEN POWER(2, a.si) ELSE 0 END))
                           | POWER(2, a.sj)) AS binary(1)) AS varbinary(1))
           ELSE CAST(CAST((CAST(SUBSTRING(@p, a.bi, 1) AS int) & ~(CASE WHEN @side = 0 THEN POWER(2, a.si) ELSE 0 END))
                          AS binary(1)) AS varbinary(1)) + 0x80
      END AS varbinary(1700)) AS ChildKey
FROM a;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.DualChildKey;");

            migrationBuilder.DropIndex(
                name: "UX_DualTeamTree_BinaryPath",
                table: "DualTeamTree");

            migrationBuilder.DropColumn(
                name: "BinaryPath",
                table: "DualTeamTree");

            migrationBuilder.DropColumn(
                name: "Depth",
                table: "DualTeamTree");

            migrationBuilder.DropColumn(
                name: "LeftLegCount",
                table: "DualTeamTree");

            migrationBuilder.DropColumn(
                name: "RightLegCount",
                table: "DualTeamTree");

            migrationBuilder.DropSequence(
                name: "DualTeamRootOrdinal");
        }
    }
}
