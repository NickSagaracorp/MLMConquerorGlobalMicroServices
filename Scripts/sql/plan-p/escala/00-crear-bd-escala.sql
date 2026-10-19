/*
Plan P, Tarea 18 (Bloque D). Crea la base de escala MLMConquerorGlobalEditionDb_Escala para medir el binario con
400.000 y 1.000.000 de altas sinteticas completas. No escribe en ninguna otra base.

Por que no con `dotnet ef database update` sobre una base vacia: la cadena de migraciones no se puede aplicar
desde cero (20260405130705_HelpdeskModule vuelve a crear AspNetRoles, AspNetUsers y otras tablas que ya creo
20260320004824_AddIdentityAndAuth: error 2714). La base local llego a su estado por otro camino. Asi que:

  1. DBCC CLONEDATABASE de la base local (solo lee: el esquema, sin datos ni estadisticas ni Query Store) a
     MLMConquerorGlobalEditionDb_Escala. Copia exacta de tablas, indices (incluido UX_DualTeamTree_BinaryPath),
     secuencias, funciones (dbo.DualChildKey) y vistas.
  2. Se pasa a lectura y escritura, recuperacion SIMPLE y RCSI (como la local).
  3. Se copian desde la local, en solo lectura, los catalogos (productos, niveles, rangos, roles, paises,
     pasarelas, plantillas, parametros...) y __EFMigrationsHistory, para que `MigrateAsync` de cualquier
     servicio apuntado aqui no tenga nada pendiente. NUNCA datos de miembros, pedidos, pagos ni comisiones,
     ni secretos (ApiCredentials, DataProtectionKeys).
  4. Despues, `dotnet ef database update --connection "...Database=MLMConquerorGlobalEditionDb_Escala..."` no
     debe aplicar nada (comprobacion).

Se ejecuta contra master. Cerrojos: -v Confirmo=ESCALA, SERVERPROPERTY('MachineName') = HOST_NAME() y que la
base de escala NO exista (si existe, se borra antes a mano: ver abajo).

  sqlcmd -S . -d master -E -C -b -I -v Confirmo=ESCALA -i Scripts/sql/plan-p/escala/00-crear-bd-escala.sql

Para borrarla al terminar (solo esa base):
  sqlcmd -S . -d master -E -C -b -Q "ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MLMConquerorGlobalEditionDb_Escala;"
*/
SET NOCOUNT ON;
IF N'$(Confirmo)' <> N'ESCALA'
    THROW 51080, N'Ejecutar con -v Confirmo=ESCALA.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51081, N'Solo en la maquina local.', 1;
IF DB_NAME() <> N'master'
    THROW 51082, N'Ejecutar contra master (-d master).', 1;
IF DB_ID(N'MLMConquerorGlobalEditionDb_Escala') IS NOT NULL
    THROW 51083, N'MLMConquerorGlobalEditionDb_Escala ya existe: borrarla antes (ver la cabecera).', 1;
IF DB_ID(N'MLMConquerorGlobalEditionDb') IS NULL
    THROW 51084, N'No existe la base local de la que se clona el esquema.', 1;
GO
DBCC CLONEDATABASE (MLMConquerorGlobalEditionDb, MLMConquerorGlobalEditionDb_Escala) WITH NO_STATISTICS, NO_QUERYSTORE;
GO
ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET READ_WRITE WITH ROLLBACK IMMEDIATE;
ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET RECOVERY SIMPLE;
ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET AUTO_UPDATE_STATISTICS ON;
ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET AUTO_CREATE_STATISTICS ON;
GO
USE MLMConquerorGlobalEditionDb_Escala;
GO
IF DB_NAME() <> N'MLMConquerorGlobalEditionDb_Escala'
    THROW 51085, N'No se pudo cambiar a la base de escala.', 1;
GO
-- 3. Catalogos, en solo lectura desde la local. Orden indiferente: las FK se desactivan durante la copia y se
--    vuelven a comprobar al final (WITH CHECK).
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @catalogos TABLE (Nombre sysname PRIMARY KEY);
INSERT INTO @catalogos VALUES
 (N'__EFMigrationsHistory'), (N'AspNetRoles'), (N'AspNetRoleClaims'), (N'CannedResponses'), (N'CommissionCategories'),
 (N'CommissionOperationType'), (N'CommissionTypes'), (N'CompanyInfo'), (N'CorporateContests'), (N'CorporateContestTranslations'),
 (N'CorporateEvents'), (N'CorporatePromos'), (N'Countries'), (N'CountryGroupCountries'), (N'CountryGroups'),
 (N'CountryPayoutDefaults'), (N'CountryProducts'), (N'CurrencyPolicies'), (N'DocumentTypes'), (N'EmailTemplateLocalizations'),
 (N'EmailTemplates'), (N'EmailTemplateVariables'), (N'ErrorMessages'), (N'GatewayCatalog'), (N'GatewayFallbackRules'),
 (N'GatewayRoutingRules'), (N'GatewayRoutingRuleSplits'), (N'GlobalParameters'), (N'KbArticles'), (N'KbArticleVersions'),
 (N'MemberIdentificationTypes'), (N'MembershipLevelBenefits'), (N'MembershipLevels'), (N'PaymentGateways'),
 (N'ProductCommissionPromos'), (N'ProductCommissions'), (N'ProductLoyaltySettings'), (N'Products'), (N'RankDefinitions'),
 (N'RankRequirements'), (N'RecurringBillingPlanProducts'), (N'RecurringBillingPlans'), (N'RegionGateways'), (N'Regions'),
 (N'SlaPolicies'), (N'SmsTemplateLocalizations'), (N'SmsTemplates'), (N'StepUpPolicies'), (N'SupportTeams'),
 (N'TicketCategories'), (N'TokenTypeCommissions'), (N'TokenTypeProducts'), (N'TokenTypes');

EXEC sp_MSforeachtable N'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

DECLARE @t sysname, @cols nvarchar(max), @ident bit, @sql nvarchar(max), @n int, @copiadas int = 0;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT k.Nombre FROM @catalogos k WHERE OBJECT_ID(N'dbo.' + QUOTENAME(k.Nombre)) IS NOT NULL ORDER BY k.Nombre;
OPEN c;
FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @cols = STRING_AGG(CAST(QUOTENAME(col.name) AS nvarchar(max)), N', ') WITHIN GROUP (ORDER BY col.column_id)
    FROM sys.columns col
    JOIN sys.types ty ON ty.user_type_id = col.user_type_id
    WHERE col.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@t)) AND col.is_computed = 0 AND ty.name NOT IN (N'timestamp');
    SET @ident = OBJECTPROPERTY(OBJECT_ID(N'dbo.' + QUOTENAME(@t)), N'TableHasIdentity');
    SET @sql = CASE WHEN @ident = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@t) + N' ON; ' ELSE N'' END
             + N'INSERT INTO dbo.' + QUOTENAME(@t) + N' (' + @cols + N') SELECT ' + @cols
             + N' FROM MLMConquerorGlobalEditionDb.dbo.' + QUOTENAME(@t) + N'; SET @n = @@ROWCOUNT; '
             + CASE WHEN @ident = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@t) + N' OFF;' ELSE N'' END;
    EXEC sp_executesql @sql, N'@n int OUTPUT', @n = @n OUTPUT;
    SET @copiadas += 1;
    PRINT CONCAT(@t, N': ', @n);
    FETCH NEXT FROM c INTO @t;
END
CLOSE c; DEALLOCATE c;

EXEC sp_MSforeachtable N'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO
SELECT DB_NAME() AS Base,
       (SELECT recovery_model_desc FROM sys.databases WHERE name = DB_NAME()) AS Recuperacion,
       (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()) AS RCSI,
       (SELECT COUNT(*) FROM __EFMigrationsHistory) AS Migraciones,
       (SELECT COUNT(*) FROM Products) AS Productos,
       (SELECT COUNT(*) FROM MembershipLevels) AS Niveles,
       (SELECT COUNT(*) FROM RankDefinitions) AS Rangos,
       (SELECT COUNT(*) FROM AspNetRoles) AS Roles,
       (SELECT COUNT(*) FROM MemberProfiles) AS Miembros;
