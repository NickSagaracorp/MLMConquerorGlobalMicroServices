/*
Plan M, Tarea 17 (D6, M22). Activa READ_COMMITTED_SNAPSHOT en MLMConquerorGlobalEditionDb.

ANTES (obligatorio):
  1. Parar TODOS los servicios que usan la base: los ocho anfitriones (SignupAPI, AdminAPI, BizCenter,
     Billing, CommissionEngine, RankEngine, TicketManagementSystem, SharedAPICenter), los trabajadores
     de Hangfire y cualquier SSMS o sqlcmd abierto contra ella. En local: los del worktree "-run".
  2. tempdb: el version store vive alli. Vigilar su tamano tras activar (consulta de verificacion).
  3. Produccion: NO ejecutar hasta cumplir las condiciones de R6 (huecos del binario reparados e indice
     unico ParentMemberId+Side) y R7 (indice unico del webhook o decision de Nick).

QUE HACE: se niega a seguir si hay otras sesiones en la base. ALTER DATABASE necesita acceso exclusivo
y no puede ir dentro de una transaccion; por eso no es una migracion de EF.

CERROJOS (revision I-2). Los tres terminan en THROW sin tocar nada:
  - la base se llama MLMConquerorGlobalEditionDb;
  - la variable explicita Confirmo: LOCAL (y entonces el servidor debe ser la misma maquina que lo
    ejecuta: SERVERPROPERTY('MachineName') = HOST_NAME()) o PRODUCCION_R6_R7_RESUELTOS (solo cuando R6 y
    R7 esten resueltos; ver el Plan M). Sin -v Confirmo=..., sqlcmd se niega a ejecutar el script.
  - El de la maquina es un cerrojo de comodidad, no de seguridad: HOST_NAME() lo declara el cliente y,
    con SQL Server en Docker, la maquina es el contenedor (revision M10).
Despues del ALTER, THROW si is_read_committed_snapshot_on no quedo en 1.

COMO EJECUTARLO (no lo ejecuta el implementador; lo hace el coordinador o Nick con los servicios parados):
  local:      sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-m/03-activar-rcsi.sql
  produccion: sqlcmd -S <servidor> -d MLMConquerorGlobalEditionDb ... -b -I -v Confirmo=PRODUCCION_R6_R7_RESUELTOS -i Scripts/sql/plan-m/03-activar-rcsi.sql

PARA DESHACER: ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;
(con los servicios parados, igual).
*/
SET NOCOUNT ON;

IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51040, N'Conectate a MLMConquerorGlobalEditionDb (sqlcmd -d MLMConquerorGlobalEditionDb).', 1;

IF N'$(Confirmo)' NOT IN (N'LOCAL', N'PRODUCCION_R6_R7_RESUELTOS')
    THROW 51042, N'Falta la confirmacion explicita: -v Confirmo=LOCAL o -v Confirmo=PRODUCCION_R6_R7_RESUELTOS.', 1;

IF N'$(Confirmo)' = N'LOCAL' AND CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51043, N'Confirmo=LOCAL pero el servidor no es la maquina local.', 1;
GO

SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM sys.databases WHERE name = DB_NAME() AND is_read_committed_snapshot_on = 1)
BEGIN
    PRINT N'READ_COMMITTED_SNAPSHOT ya estaba activado. Nada que hacer.';
    RETURN;
END

IF EXISTS (SELECT 1 FROM sys.dm_exec_sessions WHERE database_id = DB_ID() AND session_id <> @@SPID)
    THROW 51041, N'Hay otras sesiones en la base: para los servicios antes de activar RCSI.', 1;

ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = DB_NAME() AND is_read_committed_snapshot_on = 1)
    THROW 51044, N'ALTER DATABASE termino pero READ_COMMITTED_SNAPSHOT no esta activado.', 1;

-- Verificacion
SELECT name, is_read_committed_snapshot_on, snapshot_isolation_state_desc
FROM sys.databases WHERE name = DB_NAME();

SELECT SUM(version_store_reserved_page_count) * 8 / 1024 AS VersionStoreMB
FROM tempdb.sys.dm_db_file_space_usage;
