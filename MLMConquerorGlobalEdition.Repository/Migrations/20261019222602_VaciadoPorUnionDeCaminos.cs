using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class VaciadoPorUnionDeCaminos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "DualLegDeltas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DualLegDeltaDeadLetters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeltaId = table.Column<long>(type: "bigint", nullable: false),
                    StartMemberId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NodeCount = table.Column<int>(type: "int", nullable: false),
                    QualifiedCount = table.Column<int>(type: "int", nullable: false),
                    OwnChange = table.Column<bool>(type: "bit", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiscardedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DualLegDeltaDeadLetters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DualLegDeltaDeadLetters_StartMemberId",
                table: "DualLegDeltaDeadLetters",
                column: "StartMemberId");

            // Revision del bloque 2 (I-3): numeros de @desde a @hasta, sin generar los de antes de @desde.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.NumerosEntre(@desde int, @hasta int)
RETURNS TABLE AS RETURN
WITH e1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) v(n)),
     e5(n) AS (SELECT 1 FROM e1 a CROSS JOIN e1 b CROSS JOIN e1 c CROSS JOIN e1 d CROSS JOIN e1 e)
SELECT TOP (CASE WHEN @desde IS NULL OR @hasta IS NULL OR @desde < 0 OR @hasta < @desde THEN 0 ELSE @hasta - @desde + 1 END)
       CAST(@desde + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS int) AS N
FROM e5;");

            // La profundidad de una clave (la posicion del terminador), como DualBinaryPath.Depth.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.DualKeyDepth(@k varbinary(1700))
RETURNS TABLE AS RETURN
SELECT (t.L - 5) * 8 + 7 - CASE WHEN t.U & 1 > 0 THEN 0 WHEN t.U & 2 > 0 THEN 1 WHEN t.U & 4 > 0 THEN 2
                                WHEN t.U & 8 > 0 THEN 3 WHEN t.U & 16 > 0 THEN 4 WHEN t.U & 32 > 0 THEN 5
                                WHEN t.U & 64 > 0 THEN 6 ELSE 7 END AS D
FROM (SELECT DATALENGTH(@k) AS L, CAST(SUBSTRING(@k, DATALENGTH(@k), 1) AS int) AS U) t
WHERE t.L >= 5;");

            // Las claves del camino de @k de la profundidad @desde a la suya (D), con el lado por el que se baja (en D, el
            // lado del delta, @ladoPropio) e IsSelf (M-1: la fila de la propia clave, J = D). El vaciado expande solo
            // (C, D] de cada arranque: la union de los caminos.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.DualPathKeys(@k varbinary(1700), @ladoPropio int, @desde int)
RETURNS TABLE AS RETURN
WITH b AS (
    SELECT n.N AS J, d.D, CAST(SUBSTRING(@k, 5 + n.N / 8, 1) AS int) AS B, n.N % 8 AS M
    FROM dbo.DualKeyDepth(@k) d
    CROSS APPLY dbo.NumerosEntre(CASE WHEN @desde IS NULL OR @desde < 0 THEN 0 ELSE @desde END, d.D) n)
SELECT b.J AS Depth,
       CAST(SUBSTRING(@k, 1, 4 + b.J / 8)
            + CAST(CAST((b.B & (256 - POWER(2, 8 - b.M))) | POWER(2, 7 - b.M) AS binary(1)) AS varbinary(1))
            AS varbinary(1700)) AS AncestorKey,
       CASE WHEN b.J = b.D THEN @ladoPropio
            WHEN b.B & POWER(2, 7 - b.M) <> 0 THEN 1 ELSE 0 END AS Side,
       CAST(CASE WHEN b.J = b.D THEN 1 ELSE 0 END AS bit) AS IsSelf
FROM b;");

            // M-1: DualAncestorKeys es el camino entero, ahora con IsSelf; un solo algoritmo.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.DualAncestorKeys(@k varbinary(1700), @ladoPropio int)
RETURNS TABLE AS RETURN
SELECT p.Depth, p.AncestorKey, p.Side, p.IsSelf FROM dbo.DualPathKeys(@k, @ladoPropio, 0) p;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La version de ColaDeDeltasDePierna (sin IsSelf).
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
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.DualPathKeys; DROP FUNCTION IF EXISTS dbo.DualKeyDepth; DROP FUNCTION IF EXISTS dbo.NumerosEntre;");

            migrationBuilder.DropTable(
                name: "DualLegDeltaDeadLetters");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "DualLegDeltas");
        }
    }
}
