/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: colocacion en el binario: DualTeamTree (sin ruta de texto; Plan Q, Tarea 32) y PlacementLogs.
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
-- Plan Q (Bloque 3, Tarea 32): sin la ruta de texto (columna retirada). Las filas llevan el padre por MemberId y el lado; la clave compacta
-- (BinaryPath) y Depth las calcula despues ..\..\03-relleno-clave-binario.sql por niveles, como en el Plan P. Sin la ruta de
-- texto ya no hace falta insertar la columna eslabon a eslabon (cada ruta salia de la del padre): todo va por lotes.
-- Raices: ESC-0 (del sistema) y ESC-P (provisional: se creo al colocar al primero de su equipo).
INSERT INTO DualTeamTree (Id, MemberId, ParentMemberId, Side, LeftLegPoints, RightLegPoints, CreationDate, CreatedBy,
                          LastUpdateDate, LastUpdateBy, IsDeleted, LeftLegCount, RightLegCount)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, NULL, 0, p.IzqP, p.DerP,
       CASE WHEN m.n = 0 THEN m.Alta ELSE DATEADD(hour, 2, (SELECT Alta FROM escala.Altas WHERE n = m.n + 1)) END,
       CASE WHEN m.n = 0 THEN N'SYSTEM' ELSE N'placement-service' END,
       CASE WHEN m.n = 0 THEN m.Alta ELSE DATEADD(hour, 2, (SELECT Alta FROM escala.Altas WHERE n = m.n + 1)) END,
       CASE WHEN m.n = 0 THEN N'SYSTEM' ELSE N'placement-service' END, 0, p.IzqC, p.DerC
FROM escala.Altas m JOIN escala.Piernas p ON p.n = m.n
WHERE m.BinPadre IS NULL;

-- El resto, por lotes de 100.000 en orden de n.
DECLARE @lo int = 0, @tope int = (SELECT MAX(n) FROM escala.Altas), @f bigint = 2;
WHILE @lo <= @tope
BEGIN
    INSERT INTO DualTeamTree (Id, MemberId, ParentMemberId, Side, LeftLegPoints, RightLegPoints, CreationDate, CreatedBy,
                              LastUpdateDate, LastUpdateBy, IsDeleted, LeftLegCount, RightLegCount)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, pm.MemberId, m.BinLado,
           p.IzqP, p.DerP, m.Colocada, N'placement-service', m.Colocada, N'placement-service', 0, p.IzqC, p.DerC
    FROM escala.Altas m
    JOIN escala.Piernas p ON p.n = m.n
    JOIN escala.Altas pm ON pm.n = m.BinPadre
    WHERE m.n BETWEEN @lo AND @lo + 99999
    OPTION (RECOMPILE);
    SET @f += @@ROWCOUNT;
    CHECKPOINT;
    SET @lo += 100000;
END
EXEC #marca N'7a. DualTeamTree', @f;
GO
INSERT INTO PlacementLogs (MemberId, PlacedUnderMemberId, Side, Action, Reason, UnplacementCount, FirstPlacementDate, CreationDate, CreatedBy)
SELECT m.MemberId, pm.MemberId, m.BinLado, N'Placed', N'Placement engine', 0, m.Colocada, m.Colocada, N'placement-service'
FROM escala.Altas m JOIN escala.Altas pm ON pm.n = m.BinPadre;
DECLARE @f bigint = @@ROWCOUNT;
EXEC #marca N'7b. PlacementLogs', @f;
GO
