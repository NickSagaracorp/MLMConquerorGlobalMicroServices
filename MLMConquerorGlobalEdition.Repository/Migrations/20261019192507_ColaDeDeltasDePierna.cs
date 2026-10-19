using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ColaDeDeltasDePierna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DualLegDeltas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartMemberId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NodeCount = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DualLegDeltas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DualLegDeltas_StartMemberId_Id",
                table: "DualLegDeltas",
                columns: new[] { "StartMemberId", "Id" });

            // Plan Q: 0..@hasta sin tabla (100.000 como maximo; la clave admite 13.567 niveles).
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.Numeros(@hasta int)
RETURNS TABLE AS RETURN
WITH e1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) v(n)),
     e5(n) AS (SELECT 1 FROM e1 a CROSS JOIN e1 b CROSS JOIN e1 c CROSS JOIN e1 d CROSS JOIN e1 e)
SELECT TOP (CASE WHEN @hasta IS NULL OR @hasta < 0 THEN 0 ELSE @hasta + 1 END)
       CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS int) AS N
FROM e5;");

            // Plan Q: las claves de los ascendientes de @k (profundidad 0..D, D = la propia clave) y el lado por el que se
            // baja desde cada uno; en D, el lado del delta (@ladoPropio). Mismo algoritmo que DualBinaryPath.Ancestor y
            // SideBelow (SqlServerAscendientesPorClaveTests). Byte (base 1) del bit j: 5 + j/8; bit dentro del byte: j%8.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.DualAncestorKeys(@k varbinary(1700), @ladoPropio int)
RETURNS TABLE AS RETURN
WITH t AS (
    SELECT DATALENGTH(@k) AS L, CAST(SUBSTRING(@k, DATALENGTH(@k), 1) AS int) AS U),
d AS (
    SELECT (t.L - 5) * 8 + 7 - CASE WHEN t.U & 1 > 0 THEN 0 WHEN t.U & 2 > 0 THEN 1 WHEN t.U & 4 > 0 THEN 2
                                    WHEN t.U & 8 > 0 THEN 3 WHEN t.U & 16 > 0 THEN 4 WHEN t.U & 32 > 0 THEN 5
                                    WHEN t.U & 64 > 0 THEN 6 ELSE 7 END AS D
    FROM t WHERE t.L >= 5),
b AS (
    SELECT n.N AS J, d.D, CAST(SUBSTRING(@k, 5 + n.N / 8, 1) AS int) AS B, n.N % 8 AS M
    FROM d CROSS APPLY dbo.Numeros(d.D) n)
SELECT b.J AS Depth,
       CAST(SUBSTRING(@k, 1, 4 + b.J / 8)
            + CAST(CAST((b.B & (256 - POWER(2, 8 - b.M))) | POWER(2, 7 - b.M) AS binary(1)) AS varbinary(1))
            AS varbinary(1700)) AS AncestorKey,
       CASE WHEN b.J = b.D THEN @ladoPropio
            WHEN b.B & POWER(2, 7 - b.M) <> 0 THEN 1 ELSE 0 END AS Side
FROM b;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.DualAncestorKeys; DROP FUNCTION IF EXISTS dbo.Numeros;");

            migrationBuilder.DropTable(
                name: "DualLegDeltas");
        }
    }
}
