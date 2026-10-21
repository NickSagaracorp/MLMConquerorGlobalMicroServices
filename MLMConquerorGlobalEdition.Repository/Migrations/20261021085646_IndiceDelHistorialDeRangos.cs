using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndiceDelHistorialDeRangos : Migration
    {
        /// <summary>
        /// Plan Q (visualizador del admin). MemberId pasa de nvarchar(max) a nvarchar(450) solo si sigue en max: antes se
        /// comprueba que todo cabe (mejor fallar el despliegue que truncar, como HistorialDeEstados) y se quitan las
        /// estadisticas automaticas de la columna (_WA_Sys_..., SQL Server las vuelve a crear); cualquier otro objeto que
        /// dependa de la columna (un indice o una estadistica creada a mano) para la migracion con un mensaje en vez de
        /// borrarse. El indice se crea despues, fuera de la transaccion de la migracion y ONLINE donde la edicion lo admite
        /// (EngineEdition 3, 5 u 8), como en BusquedaYNivelDeAlta. Cada paso comprueba si ya esta hecho: una migracion
        /// interrumpida a medias se puede volver a ejecutar. Los servicios con el modelo anterior (MemberId sin longitud)
        /// siguen funcionando: mandan nvarchar(4000) o max y comparan igual.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MemberRankHistories') AND name = N'MemberId' AND max_length = -1)
BEGIN
    IF EXISTS (SELECT 1 FROM [MemberRankHistories] WHERE DATALENGTH([MemberId]) / 2 > 450)
        THROW 51004, N'MemberRankHistories.MemberId tiene valores de mas de 450 caracteres: revisar antes de migrar (Plan Q, IndiceDelHistorialDeRangos).', 1;

    IF EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE ic.object_id = OBJECT_ID(N'dbo.MemberRankHistories') AND c.name = N'MemberId')
       OR EXISTS (SELECT 1 FROM sys.stats s
                  JOIN sys.stats_columns sc ON sc.object_id = s.object_id AND sc.stats_id = s.stats_id
                  JOIN sys.columns c ON c.object_id = sc.object_id AND c.column_id = sc.column_id
                  WHERE s.object_id = OBJECT_ID(N'dbo.MemberRankHistories') AND c.name = N'MemberId'
                    AND s.auto_created = 0
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = s.object_id AND i.index_id = s.stats_id))
        THROW 51005, N'MemberRankHistories.MemberId tiene un indice o una estadistica creada a mano: quitarlos antes de migrar (Plan Q, IndiceDelHistorialDeRangos).', 1;

    DECLARE @estadisticas nvarchar(max) = N'';
    SELECT @estadisticas += N'DROP STATISTICS [dbo].[MemberRankHistories].' + QUOTENAME(s.name) + N'; '
    FROM sys.stats s
    JOIN sys.stats_columns sc ON sc.object_id = s.object_id AND sc.stats_id = s.stats_id
    JOIN sys.columns c ON c.object_id = sc.object_id AND c.column_id = sc.column_id
    WHERE s.object_id = OBJECT_ID(N'dbo.MemberRankHistories') AND c.name = N'MemberId' AND s.auto_created = 1;
    EXEC (@estadisticas);

    ALTER TABLE [MemberRankHistories] ALTER COLUMN [MemberId] nvarchar(450) NOT NULL;
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MemberRankHistories') AND name = N'IX_MemberRankHistories_MemberId_AchievedAt')
BEGIN
    DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN N'ON' ELSE N'OFF' END;
    DECLARE @sql nvarchar(max) = N'CREATE INDEX [IX_MemberRankHistories_MemberId_AchievedAt] ON [MemberRankHistories] '
        + N'([MemberId] ASC, [AchievedAt] DESC) INCLUDE ([RankDefinitionId]) WITH (ONLINE = ' + @online + N')';
    EXEC (@sql);
END", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberRankHistories_MemberId_AchievedAt",
                table: "MemberRankHistories");

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "MemberRankHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
