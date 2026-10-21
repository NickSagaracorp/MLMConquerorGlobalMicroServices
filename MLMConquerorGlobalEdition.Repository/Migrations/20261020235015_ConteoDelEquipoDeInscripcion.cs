using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ConteoDelEquipoDeInscripcion : Migration
    {
        /// <summary>
        /// Plan Q, revision del bloque 5 (I1): indice estrecho de los nodos vivos por Node, para contar el equipo de
        /// inscripcion. Fuera de la transaccion de la migracion y ONLINE donde la edicion lo admite (EngineEdition 3:
        /// Enterprise, Developer, Evaluation; 5: Azure SQL; 8: Managed Instance), como BusquedaYNivelDeAlta. Si ya existe, no
        /// hace nada.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GenealogyTree') AND name = N'IX_GenealogyTree_Node_Vivo')
BEGIN
    DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN N'ON' ELSE N'OFF' END;
    DECLARE @sql nvarchar(max) = N'CREATE INDEX [IX_GenealogyTree_Node_Vivo] ON [GenealogyTree] ([Node] ASC) WHERE [IsDeleted] = 0 WITH (ONLINE = ' + @online + N')';
    EXEC (@sql);
END", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GenealogyTree_Node_Vivo",
                table: "GenealogyTree");
        }
    }
}
