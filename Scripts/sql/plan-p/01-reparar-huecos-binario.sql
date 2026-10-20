/*
Plan P, Tarea 3. Repara los huecos repetidos del binario (DualTeamTree). Que hace exactamente (decisiones de
Nick, 2026-10-18):
  - En cada hueco (P, lado) con varios hijos vivos se queda el mas antiguo por (CreationDate, Id), K.
  - Los demas (intrusos), por (CreationDate, Id), bajan CON SU SUBARBOL, sin cambiar su forma, dentro de la
    misma pierna (P, lado), que tras quedarse K es el subarbol de K. El primero cuelga de la hoja mas profunda
    del subarbol de K; cada siguiente, de la hoja mas profunda del subarbol del anterior. Una hoja no tiene
    hijos, asi que cuelgan siempre por la izquierda. Empates de profundidad por (CreationDate, Id).
  - Es el derrame de la regla c de DualTeamPlacementRules aplicado como si P fuera el patrocinador y ese lado su
    lado preferido. NO es la cola del patrocinador real de cada intruso ni el hueco libre mas cercano: es lo que
    mantiene la cadena mas profunda (por eso la profundidad pasa de 934 a 8.223 en local).

Como trabaja:
  - Copia los nodos vivos a una tabla de trabajo (#a) y calcula ahi cada movimiento: la tabla real no se
    bloquea mientras se calcula.
  - Un hueco por lote, primero el de padre menos profundo (la profundidad se recalcula en cada lote: los
    movimientos la cambian y hay huecos anidados).
  - SoloInforme=1: solo calcula sobre #a; NO escribe nada en la base y no bloquea DualTeamTree. Informa de
    huecos, intrusos, filas movidas, profundidad resultante, bytes de ruta y tiempo. Falla (51044) si la
    profundidad pasaria de 12.000 (margen bajo el limite de la clave compacta, 13.567).
  - SoloInforme=0: toma el candado 'dual-tree' en exclusivo (el de DualTeamGate) durante todo el script y,
    tras calcular cada hueco, escribe en DualTeamTree las filas que ese hueco cambio, en una transaccion por
    lote. Cada lote confirmado deja el arbol coherente (rutas incluidas): aguanta un corte. Idempotente: al
    volver a ejecutarlo copia el estado real y sigue con los huecos que queden; sin huecos, Lotes = 0.
    Requiere los servicios PARADOS (el codigo anterior al Plan P no toma el candado).
  - Despues: 02-recalcular-piernas.sql (puntos y cola de derrame) y la migracion HuecoUnicoDelBinario.

SOLO base local o la base de ensayo del Plan P. En produccion NO se ejecuta sin decision de Nick.
Cerrojos: DB_NAME() en lista blanca, -v Confirmo=LOCAL, SERVERPROPERTY('MachineName') = HOST_NAME().

  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL SoloInforme=1 -i Scripts/sql/plan-p/01-reparar-huecos-binario.sql
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL SoloInforme=0 -i Scripts/sql/plan-p/01-reparar-huecos-binario.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_NAME() NOT IN (N'MLMConquerorGlobalEditionDb', N'MLMConquerorGlobalEditionDb_PlanPEnsayo')
    THROW 51040, N'Este script es solo para MLMConquerorGlobalEditionDb (o su ensayo del Plan P).', 1;
IF N'$(Confirmo)' <> N'LOCAL'
    THROW 51041, N'Falta la confirmacion explicita: ejecutar con -v Confirmo=LOCAL.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51042, N'El servidor no es la maquina local.', 1;
IF COL_LENGTH(N'dbo.DualTeamTree', N'HierarchyPath') IS NULL
    THROW 51104, N'Retirado por el Plan Q: el binario ya no tiene ruta de texto. La reparacion se hizo en el Plan P.', 1;
IF N'$(SoloInforme)' NOT IN (N'0', N'1')
    THROW 51043, N'SoloInforme debe ser 0 o 1.', 1;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @simular bit = CASE N'$(SoloInforme)' WHEN N'1' THEN 1 ELSE 0 END;
DECLARE @inicio datetime2 = SYSDATETIME();
DECLARE @LimiteProfundidad int = 12000;

BEGIN TRY

IF @simular = 0
BEGIN
    DECLARE @candado int;
    EXEC @candado = sp_getapplock @Resource = N'dual-tree', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @candado < 0
        THROW 51049, N'Hay escrituras del binario en curso (dual-tree ocupado): parar los servicios antes de reparar.', 1;
END

IF EXISTS (SELECT 1 FROM DualTeamTree WHERE IsDeleted = 0 AND (LEN(MemberId) > 100 OR LEN(ParentMemberId) > 100 OR LEN(Id) > 450))
    THROW 51050, N'Hay ids mas largos de lo que admite la tabla de trabajo (100 / 450).', 1;

-- Tabla de trabajo: los nodos vivos. Cambiado marca las filas que el lote en curso modifica.
CREATE TABLE #a (
    MemberId       nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    ParentMemberId nvarchar(100) COLLATE DATABASE_DEFAULT NULL,
    Side           int NOT NULL,
    HierarchyPath  nvarchar(max) COLLATE DATABASE_DEFAULT NOT NULL,
    CreationDate   datetime2 NOT NULL,
    Id             nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL,
    CreatedBy      nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
    Cambiado       bit NOT NULL DEFAULT 0,
    Movido         bit NOT NULL DEFAULT 0);
INSERT INTO #a (MemberId, ParentMemberId, Side, HierarchyPath, CreationDate, Id, CreatedBy)
SELECT MemberId, ParentMemberId, Side, HierarchyPath, CreationDate, Id, LEFT(CreatedBy, 200)
FROM DualTeamTree WHERE IsDeleted = 0;
CREATE INDEX ix_padre ON #a (ParentMemberId, Side) INCLUDE (CreationDate, Id);

-- Los huecos repetidos. Un movimiento nunca crea uno nuevo (el destino es siempre una hoja), asi que la
-- lista se calcula una vez; lo que cambia de un lote a otro es la profundidad de cada padre.
CREATE TABLE #h (
    ParentMemberId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
    Side int NOT NULL,
    Hijos int NOT NULL,
    Hecho bit NOT NULL DEFAULT 0,
    PRIMARY KEY (ParentMemberId, Side));
INSERT INTO #h (ParentMemberId, Side, Hijos)
SELECT ParentMemberId, Side, COUNT(*) FROM #a
WHERE ParentMemberId IS NOT NULL GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1;

-- Antes de mover nada, el arbol tiene que estar sano salvo por los huecos: rutas coherentes con el padre y sin
-- huerfanos. Si no, la reparacion reescribiria rutas a partir de datos rotos.
IF EXISTS (SELECT 1 FROM #a c JOIN #a p ON p.MemberId = c.ParentMemberId WHERE c.HierarchyPath <> p.HierarchyPath + c.MemberId + N'/')
   OR EXISTS (SELECT 1 FROM #a WHERE ParentMemberId IS NULL AND HierarchyPath <> N'/' + MemberId + N'/')
    THROW 51063, N'Hay rutas incoherentes con su padre: revisar antes de reparar (no se ha tocado nada).', 1;
-- Un hijo cuyo padre no tiene nodo es legitimo si el padre se retiro (PlacementLog 'Removed' del admin o 'Unplaced'):
-- espera flotando a que su padre vuelva a colocarse, y 03 le da una raiz virtual. Cualquier otro es un huerfano roto.
IF EXISTS (SELECT 1 FROM #a c
           WHERE c.ParentMemberId IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM #a p WHERE p.MemberId = c.ParentMemberId)
             AND NOT EXISTS (SELECT 1 FROM PlacementLogs l WHERE l.MemberId = c.ParentMemberId AND l.Action IN (N'Removed', N'Unplaced')))
    THROW 51064, N'Hay nodos huerfanos (su padre no existe ni consta retirado): revisar antes de reparar (no se ha tocado nada).', 1;

-- Informe antes
SELECT 'antes' AS Fase,
       (SELECT COUNT(*) FROM #a) AS Nodos,
       (SELECT COUNT(*) FROM #h) AS HuecosRepetidos,
       (SELECT ISNULL(SUM(Hijos), 0) FROM #h) AS FilasEnHuecos,
       (SELECT ISNULL(SUM(Hijos - 1), 0) FROM #h) AS Intrusos,
       (SELECT MAX(LEN(HierarchyPath) - LEN(REPLACE(HierarchyPath, N'/', N''))) - 1 FROM #a) AS ProfundidadMaxima,
       (SELECT SUM(CAST(DATALENGTH(HierarchyPath) AS bigint)) / 1048576 FROM #a) AS MBDeRutas,
       (SELECT COUNT(*) FROM #a c WHERE c.ParentMemberId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM #a p WHERE p.MemberId = c.ParentMemberId)) AS Huerfanos,
       (SELECT COUNT(*) FROM #a WHERE ParentMemberId IS NULL) AS Raices;

-- Intrusos por origen (todas las filas de cada hueco salvo la mas antigua).
;WITH d AS (
    SELECT a.CreatedBy, ROW_NUMBER() OVER (PARTITION BY a.ParentMemberId, a.Side ORDER BY a.CreationDate, a.Id) AS rn
    FROM #a a JOIN #h h ON h.ParentMemberId = a.ParentMemberId AND h.Side = a.Side)
SELECT 'intrusos por origen' AS Fase,
       CASE WHEN CreatedBy LIKE N'AMB-%' THEN N'AMB-… (el propio miembro)' ELSE ISNULL(CreatedBy, N'(nulo)') END AS Origen,
       COUNT(*) AS Intrusos
FROM d WHERE rn > 1
GROUP BY CASE WHEN CreatedBy LIKE N'AMB-%' THEN N'AMB-… (el propio miembro)' ELSE ISNULL(CreatedBy, N'(nulo)') END
ORDER BY Intrusos DESC;

DECLARE @P nvarchar(100), @Side int, @K nvarchar(100), @I nvarchar(100), @T nvarchar(100), @TSide int,
        @RutaVieja nvarchar(max), @RutaNueva nvarchar(max), @Lotes int = 0, @Movidos int = 0,
        @FilasEscritas int = 0, @n int, @ProfLote int;
CREATE TABLE #i (Orden int IDENTITY PRIMARY KEY, MemberId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL);

WHILE 1 = 1
BEGIN
    -- El siguiente hueco: el de padre menos profundo.
    SELECT TOP 1 @P = h.ParentMemberId, @Side = h.Side
    FROM #h h LEFT JOIN #a p ON p.MemberId = h.ParentMemberId
    WHERE h.Hecho = 0
    ORDER BY ISNULL(LEN(p.HierarchyPath) - LEN(REPLACE(p.HierarchyPath, N'/', N'')), 0), h.ParentMemberId, h.Side;
    IF @@ROWCOUNT = 0 BREAK;

    -- K se queda el hueco.
    SELECT TOP 1 @K = MemberId FROM #a WHERE ParentMemberId = @P AND Side = @Side ORDER BY CreationDate, Id;

    -- Primer destino: la hoja mas profunda del subarbol de K.
    ;WITH s AS (
        SELECT MemberId, 0 AS D FROM #a WHERE MemberId = @K
        UNION ALL
        SELECT c.MemberId, s.D + 1 FROM s JOIN #a c ON c.ParentMemberId = s.MemberId)
    SELECT TOP 1 @T = s.MemberId
    FROM s JOIN #a x ON x.MemberId = s.MemberId
    WHERE NOT EXISTS (SELECT 1 FROM #a k WHERE k.ParentMemberId = s.MemberId)
    ORDER BY s.D DESC, x.CreationDate, x.Id
    OPTION (MAXRECURSION 32767);

    TRUNCATE TABLE #i;
    INSERT INTO #i (MemberId)
    SELECT MemberId FROM #a WHERE ParentMemberId = @P AND Side = @Side AND MemberId <> @K ORDER BY CreationDate, Id;

    DECLARE @o int = 1, @ultimo int = (SELECT MAX(Orden) FROM #i);
    WHILE @o <= @ultimo
    BEGIN
        SELECT @I = MemberId FROM #i WHERE Orden = @o;

        SET @TSide = CASE WHEN EXISTS (SELECT 1 FROM #a WHERE ParentMemberId = @T AND Side = 0) THEN 1 ELSE 0 END;
        SELECT @RutaVieja = HierarchyPath FROM #a WHERE MemberId = @I;
        SELECT @RutaNueva = HierarchyPath + @I + N'/' FROM #a WHERE MemberId = @T;

        UPDATE #a SET ParentMemberId = @T, Side = @TSide, Cambiado = 1, Movido = 1 WHERE MemberId = @I;

        IF NOT EXISTS (SELECT 1 FROM #a WHERE ParentMemberId = @I)
        BEGIN
            -- Lo habitual: el intruso es una hoja. Su ruta cambia y el siguiente cuelga de el.
            UPDATE #a SET HierarchyPath = @RutaNueva WHERE MemberId = @I;
            SET @T = @I;
        END
        ELSE
        BEGIN
            -- Rutas del subarbol movido (I incluido): por adyacencia, no por LIKE.
            ;WITH s AS (
                SELECT MemberId FROM #a WHERE MemberId = @I
                UNION ALL
                SELECT c.MemberId FROM s JOIN #a c ON c.ParentMemberId = s.MemberId)
            UPDATE a SET HierarchyPath = @RutaNueva + SUBSTRING(a.HierarchyPath, LEN(@RutaVieja) + 1, 2147483647),
                         Cambiado = 1
            FROM #a a JOIN s ON s.MemberId = a.MemberId
            OPTION (MAXRECURSION 32767);

            -- Nuevo destino: la hoja mas profunda del subarbol de I.
            ;WITH s AS (
                SELECT MemberId, 0 AS D FROM #a WHERE MemberId = @I
                UNION ALL
                SELECT c.MemberId, s.D + 1 FROM s JOIN #a c ON c.ParentMemberId = s.MemberId)
            SELECT TOP 1 @T = s.MemberId
            FROM s JOIN #a x ON x.MemberId = s.MemberId
            WHERE NOT EXISTS (SELECT 1 FROM #a k WHERE k.ParentMemberId = s.MemberId)
            ORDER BY s.D DESC, x.CreationDate, x.Id
            OPTION (MAXRECURSION 32767);
        END

        SET @Movidos += 1;
        SET @o += 1;
    END

    UPDATE #h SET Hecho = 1 WHERE ParentMemberId = @P AND Side = @Side;
    SET @Lotes += 1;

    IF @simular = 0
    BEGIN
        SET @ProfLote = (SELECT MAX(LEN(HierarchyPath) - LEN(REPLACE(HierarchyPath, N'/', N''))) - 1 FROM #a WHERE Cambiado = 1);
        IF @ProfLote > @LimiteProfundidad
            THROW 51044, N'La reparacion dejaria mas de 12.000 niveles (limite de BinaryPath 13.567): decide Nick la regla. Los lotes anteriores quedan confirmados y coherentes.', 1;

        BEGIN TRAN;
        UPDATE d SET ParentMemberId = a.ParentMemberId, Side = a.Side, HierarchyPath = a.HierarchyPath,
                     LastUpdateDate = GETDATE(), LastUpdateBy = N'plan-p-reparacion'
        FROM DualTeamTree d JOIN #a a ON a.MemberId = d.MemberId
        WHERE d.IsDeleted = 0 AND a.Cambiado = 1;
        SET @n = @@ROWCOUNT;
        IF @n <> (SELECT COUNT(*) FROM #a WHERE Cambiado = 1)
            THROW 51051, N'DualTeamTree cambio durante la reparacion (filas que no casan con la copia): parar los servicios y repetir.', 1;
        COMMIT;
        SET @FilasEscritas += @n;
    END
    UPDATE #a SET Cambiado = 0 WHERE Cambiado = 1;

    IF @Lotes % 50 = 0
    BEGIN
        DECLARE @msg nvarchar(200) = CONCAT(N'Lotes: ', @Lotes, N'  intrusos movidos: ', @Movidos, N'  segundos: ', DATEDIFF(second, @inicio, SYSDATETIME()));
        RAISERROR(@msg, 0, 1) WITH NOWAIT;
    END
END

DECLARE @Prof int = (SELECT MAX(LEN(HierarchyPath) - LEN(REPLACE(HierarchyPath, N'/', N''))) - 1 FROM #a);
SELECT 'despues' AS Fase,
       CASE WHEN @simular = 1 THEN 'simulacion (nada escrito)' ELSE 'aplicado' END AS Modo,
       @Lotes AS Lotes,
       @Movidos AS IntrusosMovidos,
       (SELECT COUNT(*) FROM #a WHERE Movido = 1) AS NodosReenganchados,
       (SELECT COUNT(*) FROM #a a JOIN DualTeamTree d ON d.MemberId = a.MemberId AND d.IsDeleted = 0
          WHERE a.HierarchyPath <> d.HierarchyPath OR ISNULL(a.ParentMemberId, N'') <> ISNULL(d.ParentMemberId, N'') OR a.Side <> d.Side) AS FilasPendientesDeEscribir,
       @FilasEscritas AS FilasEscritas,
       @Prof AS ProfundidadMaxima,
       (SELECT SUM(CAST(DATALENGTH(HierarchyPath) AS bigint)) / 1048576 FROM #a) AS MBDeRutas,
       (SELECT MAX(LEN(HierarchyPath)) FROM #a) AS RutaMasLarga,
       (SELECT COUNT(*) FROM (SELECT 1 x FROM #a WHERE ParentMemberId IS NOT NULL
                              GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1) h) AS HuecosRepetidos,
       (SELECT COUNT(*) FROM #a c JOIN #a p ON p.MemberId = c.ParentMemberId
          WHERE c.HierarchyPath <> p.HierarchyPath + c.MemberId + N'/') AS RutasIncoherentes,
       (SELECT COUNT(*) FROM #a c WHERE c.ParentMemberId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM #a p WHERE p.MemberId = c.ParentMemberId)) AS Huerfanos,
       (SELECT COUNT(*) FROM #a WHERE ParentMemberId IS NULL) AS Raices,
       DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;

-- Filas movidas por origen (las que cambiaron de ruta, padre o lado).
SELECT 'filas movidas por origen' AS Fase,
       CASE WHEN a.CreatedBy LIKE N'AMB-%' THEN N'AMB-… (el propio miembro)' ELSE ISNULL(a.CreatedBy, N'(nulo)') END AS Origen,
       COUNT(*) AS Filas
FROM #a a JOIN DualTeamTree d ON d.MemberId = a.MemberId AND d.IsDeleted = 0
WHERE @simular = 1 AND (a.HierarchyPath <> d.HierarchyPath OR a.Side <> d.Side OR ISNULL(a.ParentMemberId, N'') <> ISNULL(d.ParentMemberId, N''))
GROUP BY CASE WHEN a.CreatedBy LIKE N'AMB-%' THEN N'AMB-… (el propio miembro)' ELSE ISNULL(a.CreatedBy, N'(nulo)') END
ORDER BY Filas DESC;

IF @simular = 0
    EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';

IF @simular = 1
BEGIN
    IF @Prof > @LimiteProfundidad
        THROW 51044, N'La reparacion dejaria mas de 12.000 niveles (limite de BinaryPath 13.567): decide Nick la regla.', 1;
    PRINT 'Simulacion: nada se ha guardado.';
END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    IF APPLOCK_MODE(N'public', N'dual-tree', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';
    THROW;
END CATCH
