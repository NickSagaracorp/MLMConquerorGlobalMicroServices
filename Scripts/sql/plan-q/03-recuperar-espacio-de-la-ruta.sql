/*
Plan Q, Tarea 13. Libera el espacio de la columna DualTeamTree.HierarchyPath, ya eliminada por RetiraRutaDelBinario.
DBCC CLEANTABLE por lotes de 20.000 filas (cada lote, su transaccion). Idempotente. Ejecutar con los servicios en marcha
o parados. En recuperacion completa el registro crece hasta la siguiente copia del registro: vigilar
log_reuse_wait_desc (sys.databases) y hacer copias del registro durante la ejecucion si hace falta.
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/03-recuperar-espacio-de-la-ruta.sql
*/
SET NOCOUNT ON;
IF N'$(Confirmo)' NOT IN (N'LOCAL', N'ESCALA')
    THROW 51101, N'Ejecutar con -v Confirmo=LOCAL (o ESCALA en la base de escala).', 1;
IF NOT ((N'$(Confirmo)' = N'LOCAL'  AND DB_NAME() = N'MLMConquerorGlobalEditionDb')
     OR (N'$(Confirmo)' = N'ESCALA' AND DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'))
    THROW 51102, N'Confirmo no corresponde a esta base.', 1;
IF COL_LENGTH(N'dbo.DualTeamTree', N'HierarchyPath') IS NOT NULL
    THROW 51103, N'La columna sigue existiendo: aplicar antes la migracion RetiraRutaDelBinario.', 1;

DECLARE @recuperacion nvarchar(60) = (SELECT recovery_model_desc FROM sys.databases WHERE database_id = DB_ID());
DECLARE @registroAntes bigint = (SELECT SUM(size) * 8 / 1024 FROM sys.database_files WHERE type_desc = N'LOG');
DECLARE @antes bigint = (SELECT SUM(used_page_count) * 8 / 1024 FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID(N'dbo.DualTeamTree'));
DECLARE @msg nvarchar(300) = CONCAT(N'Recuperacion ', @recuperacion, N'; registro ', @registroAntes, N' MB; DualTeamTree ', @antes, N' MB');
RAISERROR(@msg, 0, 1) WITH NOWAIT;

DBCC CLEANTABLE (0, N'dbo.DualTeamTree', 20000) WITH NO_INFOMSGS;
ALTER INDEX ALL ON dbo.DualTeamTree REORGANIZE WITH (LOB_COMPACTION = ON);

DECLARE @despues bigint = (SELECT SUM(used_page_count) * 8 / 1024 FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID(N'dbo.DualTeamTree'));
DECLARE @registroDespues bigint = (SELECT SUM(size) * 8 / 1024 FROM sys.database_files WHERE type_desc = N'LOG');
SET @msg = CONCAT(N'DualTeamTree: ', @antes, N' MB -> ', @despues, N' MB; registro: ', @registroAntes, N' MB -> ', @registroDespues, N' MB');
RAISERROR(@msg, 0, 1) WITH NOWAIT;
