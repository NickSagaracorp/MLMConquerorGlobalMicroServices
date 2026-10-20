using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RetiraRutaDelBinario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan Q (Tarea 13): falla cerrada si algun modulo (vista, funcion, procedimiento o desencadenador) aun lee
            // DualTeamTree.HierarchyPath. Los candidatos se filtran por texto y cada uno se confirma con
            // sys.dm_sql_referenced_entities (un modulo que lea GenealogyTree.HierarchyPath y nombre DualTeamTree no
            // cuenta). Si un candidato no se puede analizar, la funcion falla y la migracion se detiene: revisarlo a mano.
            migrationBuilder.Sql(@"
DECLARE @modulo nvarchar(400);
DECLARE candidatos CURSOR LOCAL FAST_FORWARD FOR
    SELECT QUOTENAME(OBJECT_SCHEMA_NAME(m.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(m.object_id))
    FROM sys.sql_modules m
    JOIN sys.objects o ON o.object_id = m.object_id
    WHERE o.is_ms_shipped = 0
      AND m.definition LIKE N'%DualTeamTree%' AND m.definition LIKE N'%HierarchyPath%';
OPEN candidatos;
FETCH NEXT FROM candidatos INTO @modulo;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM sys.dm_sql_referenced_entities(@modulo, N'OBJECT') r
               WHERE r.referenced_entity_name = N'DualTeamTree' AND r.referenced_minor_name = N'HierarchyPath')
    BEGIN
        DECLARE @msg nvarchar(600) = CONCAT(N'El objeto ', @modulo, N' aun lee DualTeamTree.HierarchyPath (Plan Q, Tarea 13).');
        THROW 51100, @msg, 1;
    END
    FETCH NEXT FROM candidatos INTO @modulo;
END
CLOSE candidatos;
DEALLOCATE candidatos;");

            migrationBuilder.DropColumn(
                name: "HierarchyPath",
                table: "DualTeamTree");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La columna vuelve vacia. Volver atras de verdad es restaurar la copia del despliegue (Q-R10).
            migrationBuilder.AddColumn<string>(
                name: "HierarchyPath",
                table: "DualTeamTree",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
