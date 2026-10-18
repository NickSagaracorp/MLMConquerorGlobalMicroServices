/*
Plan P, Tarea 12. Rellena la clave compacta del binario (DualTeamTree.BinaryPath y Depth) en los nodos
vivos que no la tienen. Requiere la migracion ClaveCompactaDelBinario (columnas, secuencia DualTeamRootOrdinal
y funcion dbo.DualChildKey) y que no queden huecos repetidos (01-reparar-huecos-binario.sql).

  1. Raices sin clave: un ordinal nuevo cada una, en orden (CreationDate, Id). Clave = ordinal + 0x80, Depth 0.
  2. Huerfanos por adyacencia (ParentMemberId que no existe: hijos de una retirada del admin que esperan a que
     su padre vuelva a colocarse): un ordinal VIRTUAL por padre ausente, compartido por sus hijos; cada hijo
     cuelga de esa raiz virtual por su lado (Depth 1). Asi DualTeamNodes los recoloca juntos.
  3. El resto, por niveles en una tabla temporal indexada por nivel (O(N), no un recorrido de la tabla por
     nivel: tras la reparacion hay mas de 8.000), con dbo.DualChildKey, la misma funcion que el codigo.
  4. Escritura por tandas de 50.000 filas, una transaccion por tanda.

Idempotente: solo toca filas sin clave. Aguanta un corte: lo ya escrito es correcto (las claves dependen solo
de la clave del padre) y la siguiente ejecucion sigue con lo que falte. Una segunda ejecucion no cambia nada.

SOLO base local o la base de ensayo del Plan P, con los servicios parados.
Cerrojos: DB_NAME() en lista blanca, -v Confirmo=LOCAL, SERVERPROPERTY('MachineName') = HOST_NAME().

  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-p/03-relleno-clave-binario.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_NAME() NOT IN (N'MLMConquerorGlobalEditionDb', N'MLMConquerorGlobalEditionDb_PlanPEnsayo')
    THROW 51053, N'Este script es solo para MLMConquerorGlobalEditionDb (o su ensayo del Plan P).', 1;
IF N'$(Confirmo)' <> N'LOCAL'
    THROW 51054, N'Falta la confirmacion explicita: ejecutar con -v Confirmo=LOCAL.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51055, N'El servidor no es la maquina local.', 1;
IF COL_LENGTH(N'dbo.DualTeamTree', N'BinaryPath') IS NULL OR OBJECT_ID(N'dbo.DualChildKey') IS NULL
    THROW 51056, N'Falta la migracion ClaveCompactaDelBinario.', 1;
IF EXISTS (SELECT 1 FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL
           GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1)
    THROW 51052, N'Hay huecos repetidos: primero 01-reparar-huecos-binario.sql.', 1;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @inicio datetime2 = SYSDATETIME();

BEGIN TRY
    DECLARE @candado int;
    EXEC @candado = sp_getapplock @Resource = N'dual-tree', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @candado < 0
        THROW 51057, N'Hay escrituras del binario en curso (dual-tree ocupado): parar los servicios.', 1;

    -- 1. Raices sin clave.
    IF OBJECT_ID('tempdb..#r') IS NOT NULL DROP TABLE #r;
    CREATE TABLE #r (MemberId nvarchar(100) COLLATE DATABASE_DEFAULT PRIMARY KEY, Ordinal int NOT NULL);
    INSERT INTO #r (MemberId, Ordinal)
    SELECT d.MemberId, NEXT VALUE FOR dbo.DualTeamRootOrdinal OVER (ORDER BY d.CreationDate, d.Id)
    FROM DualTeamTree d
    WHERE d.IsDeleted = 0 AND d.BinaryPath IS NULL AND d.ParentMemberId IS NULL;
    BEGIN TRAN;
    UPDATE d SET BinaryPath = CAST(r.Ordinal AS binary(4)) + 0x80, Depth = 0
    FROM DualTeamTree d JOIN #r r ON r.MemberId = d.MemberId;
    COMMIT;
    DECLARE @raices int = (SELECT COUNT(*) FROM #r);

    -- 2. Huerfanos por adyacencia: un ordinal virtual por padre ausente.
    IF OBJECT_ID('tempdb..#v') IS NOT NULL DROP TABLE #v;
    CREATE TABLE #v (Padre nvarchar(100) COLLATE DATABASE_DEFAULT PRIMARY KEY, Ordinal int NOT NULL);
    INSERT INTO #v (Padre, Ordinal)
    SELECT x.ParentMemberId, NEXT VALUE FOR dbo.DualTeamRootOrdinal OVER (ORDER BY x.Primero, x.ParentMemberId)
    FROM (SELECT d.ParentMemberId, MIN(d.CreationDate) AS Primero
          FROM DualTeamTree d
          WHERE d.IsDeleted = 0 AND d.BinaryPath IS NULL AND d.ParentMemberId IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = d.ParentMemberId AND p.IsDeleted = 0)
          GROUP BY d.ParentMemberId) x;
    BEGIN TRAN;
    UPDATE d SET BinaryPath = k.ChildKey, Depth = 1
    FROM DualTeamTree d JOIN #v v ON v.Padre = d.ParentMemberId
    CROSS APPLY dbo.DualChildKey(CAST(v.Ordinal AS binary(4)) + 0x80, 0, d.Side) k
    WHERE d.IsDeleted = 0 AND d.BinaryPath IS NULL;
    COMMIT;
    DECLARE @huerfanos int = (SELECT COUNT(*) FROM #v);

    -- 3. El resto por niveles, en una tabla temporal.
    IF OBJECT_ID('tempdb..#p') IS NOT NULL DROP TABLE #p;
    CREATE TABLE #p (
        MemberId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
        ParentMemberId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
        Side int NOT NULL,
        Clave varbinary(1700) NULL,
        Prof int NULL,
        Nivel int NULL);
    INSERT INTO #p (MemberId, ParentMemberId, Side)
    SELECT MemberId, ParentMemberId, Side FROM DualTeamTree WHERE IsDeleted = 0 AND BinaryPath IS NULL;
    CREATE INDEX ix_padre ON #p (ParentMemberId) INCLUDE (Side);
    CREATE INDEX ix_nivel ON #p (Nivel) INCLUDE (Clave, Prof);

    -- Primer nivel: hijos de nodos que ya tienen clave en la tabla.
    UPDATE c SET Clave = k.ChildKey, Prof = d.Depth + 1, Nivel = 1
    FROM #p c
    JOIN DualTeamTree d ON d.MemberId = c.ParentMemberId AND d.IsDeleted = 0 AND d.BinaryPath IS NOT NULL
    CROSS APPLY dbo.DualChildKey(d.BinaryPath, d.Depth, c.Side) k;

    DECLARE @nivel int = 1, @n int = 1;
    WHILE @n > 0
    BEGIN
        UPDATE c SET Clave = k.ChildKey, Prof = p.Prof + 1, Nivel = @nivel + 1
        FROM #p p
        JOIN #p c ON c.ParentMemberId = p.MemberId
        CROSS APPLY dbo.DualChildKey(p.Clave, p.Prof, c.Side) k
        WHERE p.Nivel = @nivel AND c.Clave IS NULL;
        SET @n = @@ROWCOUNT;
        SET @nivel += 1;
        IF @nivel % 1000 = 0
        BEGIN
            DECLARE @msg nvarchar(200) = CONCAT(N'Niveles calculados: ', @nivel, N'  segundos: ', DATEDIFF(second, @inicio, SYSDATETIME()));
            RAISERROR(@msg, 0, 1) WITH NOWAIT;
        END
    END

    -- 4. Escritura por tandas.
    DECLARE @escritas int = 0, @tanda int = 1;
    WHILE @tanda > 0
    BEGIN
        BEGIN TRAN;
        UPDATE TOP (50000) d SET BinaryPath = p.Clave, Depth = p.Prof
        FROM DualTeamTree d JOIN #p p ON p.MemberId = d.MemberId
        WHERE d.IsDeleted = 0 AND d.BinaryPath IS NULL AND p.Clave IS NOT NULL;
        SET @tanda = @@ROWCOUNT;
        COMMIT;
        SET @escritas += @tanda;
    END

    EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';

    SELECT (SELECT COUNT(*) FROM DualTeamTree WHERE IsDeleted = 0) AS Nodos,
           @raices AS RaicesNuevas,
           @huerfanos AS PadresAusentes,
           @escritas AS FilasPorNiveles,
           (SELECT COUNT(*) FROM DualTeamTree WHERE IsDeleted = 0 AND BinaryPath IS NULL) AS SinClave,
           (SELECT MAX(Depth) FROM DualTeamTree WHERE IsDeleted = 0) AS ProfundidadMaxima,
           (SELECT MAX(DATALENGTH(BinaryPath)) FROM DualTeamTree WHERE IsDeleted = 0) AS BytesMaximos,
           @nivel - 1 AS Niveles,
           DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    IF APPLOCK_MODE(N'public', N'dual-tree', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';
    THROW;
END CATCH
