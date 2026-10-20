/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: colocacion en el binario: DualTeamTree por profundidad y PlacementLogs.
Lo lanza el orquestador ..\01-generar-altas.ps1 (ver alli el orden, las dependencias y la forma de los datos).
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
IF DB_NAME() <> N'MLMConquerorGlobalEditionDb_Escala'
    THROW 51062, N'Este script es solo para MLMConquerorGlobalEditionDb_Escala.', 1;
IF N'$(Confirmo)' <> N'ESCALA'
    THROW 51063, N'Ejecutar con -v Confirmo=ESCALA.', 1;
-- Revision del bloque 3 del Plan Q: escribe (o lee) DualTeamTree.HierarchyPath, que el Plan Q retira; hasta que la
-- Tarea 32 lo pase a la clave compacta, no se ejecuta contra una base sin la columna.
IF COL_LENGTH('dbo.DualTeamTree', 'HierarchyPath') IS NULL
    THROW 51106, N'Pendiente de la Tarea 32 del Plan Q: DualTeamTree ya no tiene HierarchyPath y este script aun la usa.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51064, N'Solo en la maquina local.', 1;
IF OBJECT_ID('escala.Altas') IS NULL
    THROW 51073, N'Falta la preparacion (a-preparar.sql).', 1;
GO
SET NOCOUNT ON;
IF OBJECT_ID('tempdb..#t') IS NOT NULL DROP TABLE #t;
CREATE TABLE #t (Paso nvarchar(100) NOT NULL, Segundos int NOT NULL, Filas bigint NULL, Inicio datetime2 NULL);
INSERT INTO #t VALUES (N'inicio', 0, NULL, SYSDATETIME());
GO
CREATE OR ALTER PROCEDURE #marca @paso nvarchar(100), @filas bigint = NULL AS
BEGIN
    DECLARE @s int = DATEDIFF(second, (SELECT Inicio FROM #t WHERE Paso = N'inicio'), SYSDATETIME());
    INSERT INTO #t (Paso, Segundos, Filas) VALUES (@paso, @s, @filas);
    DECLARE @msg nvarchar(300) = CONCAT(@s, N' s: ', @paso, CASE WHEN @filas IS NULL THEN N'' ELSE CONCAT(N' (', @filas, N' filas)') END);
    RAISERROR(@msg, 0, 1) WITH NOWAIT;
    CHECKPOINT;
END
GO
SET XACT_ABORT ON;
-- Raices: ESC-0 (del sistema) y ESC-P (provisional: se creo al colocar al primero de su equipo).
INSERT INTO DualTeamTree (Id, MemberId, ParentMemberId, Side, HierarchyPath, LeftLegPoints, RightLegPoints, CreationDate, CreatedBy,
                          LastUpdateDate, LastUpdateBy, IsDeleted, LeftLegCount, RightLegCount)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, NULL, 0, CONCAT(N'/', m.MemberId, N'/'), p.IzqP, p.DerP,
       CASE WHEN m.n = 0 THEN m.Alta ELSE DATEADD(hour, 2, (SELECT Alta FROM escala.Altas WHERE n = m.n + 1)) END,
       CASE WHEN m.n = 0 THEN N'SYSTEM' ELSE N'placement-service' END,
       CASE WHEN m.n = 0 THEN m.Alta ELSE DATEADD(hour, 2, (SELECT Alta FROM escala.Altas WHERE n = m.n + 1)) END,
       CASE WHEN m.n = 0 THEN N'SYSTEM' ELSE N'placement-service' END, 0, p.IzqC, p.DerC
FROM escala.Altas m JOIN escala.Piernas p ON p.n = m.n
WHERE m.BinPadre IS NULL;

-- Primero la columna (BinJ = 0: ESC-M y sus 8.500 descendientes por la izquierda), una fila por sentencia y en orden: la
-- ruta de cada eslabon sale de la del anterior. Despues los subarboles por pasos desde la columna (BinJ = 1, 2...), en
-- lotes de 20.000: todos los nodos de un paso tienen ya a su padre insertado.
DECLARE @n int = 1, @col int = (SELECT MAX(n) FROM escala.Altas WHERE BinJ = 0 AND BinPadre IS NOT NULL AND n < (SELECT N + 2 FROM escala.Par)), @f bigint = 2;
BEGIN TRAN;
WHILE @n <= @col
BEGIN
    INSERT INTO DualTeamTree (Id, MemberId, ParentMemberId, Side, HierarchyPath, LeftLegPoints, RightLegPoints, CreationDate, CreatedBy,
                              LastUpdateDate, LastUpdateBy, IsDeleted, LeftLegCount, RightLegCount)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, pm.MemberId, m.BinLado, CONCAT(pd.HierarchyPath, m.MemberId, N'/'),
           p.IzqP, p.DerP, m.Colocada, N'placement-service', m.Colocada, N'placement-service', 0, p.IzqC, p.DerC
    FROM escala.Altas m
    JOIN escala.Piernas p ON p.n = m.n
    JOIN escala.Altas pm ON pm.n = m.BinPadre
    JOIN DualTeamTree pd ON pd.MemberId = pm.MemberId AND pd.IsDeleted = 0
    WHERE m.n = @n
    OPTION (MAXDOP 1);
    SET @f += @@ROWCOUNT;
    -- Una transaccion cada 250 eslabones: confirmar fila a fila espera al disco (WRITELOG) en cada una.
    IF @n % 250 = 0 BEGIN COMMIT; BEGIN TRAN; END
    IF @n % 1000 = 0
    BEGIN
        DECLARE @msg nvarchar(200) = CONCAT(N'   binario: columna ', @n, N' de ', @col);
        RAISERROR(@msg, 0, 1) WITH NOWAIT;
    END
    SET @n += 1;
END
COMMIT;
CHECKPOINT;
DECLARE @j int = 1, @maxj int = (SELECT MAX(BinJ) FROM escala.Altas), @lo int, @hi int, @tope int;
WHILE @j <= @maxj
BEGIN
    SELECT @lo = MIN(n), @tope = MAX(n) FROM escala.Altas WHERE BinJ = @j AND BinPadre IS NOT NULL;
    WHILE @lo <= @tope
    BEGIN
        SET @hi = @lo + 19999;
        INSERT INTO DualTeamTree (Id, MemberId, ParentMemberId, Side, HierarchyPath, LeftLegPoints, RightLegPoints, CreationDate, CreatedBy,
                                  LastUpdateDate, LastUpdateBy, IsDeleted, LeftLegCount, RightLegCount)
        SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, pm.MemberId, m.BinLado, CONCAT(pd.HierarchyPath, m.MemberId, N'/'),
               p.IzqP, p.DerP, m.Colocada, N'placement-service', m.Colocada, N'placement-service', 0, p.IzqC, p.DerC
        FROM escala.Altas m
        JOIN escala.Piernas p ON p.n = m.n
        JOIN escala.Altas pm ON pm.n = m.BinPadre
        JOIN DualTeamTree pd ON pd.MemberId = pm.MemberId AND pd.IsDeleted = 0
        WHERE m.BinJ = @j AND m.BinPadre IS NOT NULL AND m.n BETWEEN @lo AND @hi
        OPTION (RECOMPILE);
        SET @f += @@ROWCOUNT;
        CHECKPOINT;
        SET @lo = @hi + 1;
    END
    DECLARE @msg2 nvarchar(200) = CONCAT(N'   binario: paso ', @j, N' de ', @maxj, N', ', @f, N' nodos');
    RAISERROR(@msg2, 0, 1) WITH NOWAIT;
    SET @j += 1;
END
EXEC #marca N'7a. DualTeamTree', @f;
GO
INSERT INTO PlacementLogs (MemberId, PlacedUnderMemberId, Side, Action, Reason, UnplacementCount, FirstPlacementDate, CreationDate, CreatedBy)
SELECT m.MemberId, pm.MemberId, m.BinLado, N'Placed', N'Placement engine', 0, m.Colocada, m.Colocada, N'placement-service'
FROM escala.Altas m JOIN escala.Altas pm ON pm.n = m.BinPadre;
DECLARE @f bigint = @@ROWCOUNT;
EXEC #marca N'7b. PlacementLogs', @f;
GO
