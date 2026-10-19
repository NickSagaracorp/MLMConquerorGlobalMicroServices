/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: GenealogyTree por niveles y la tabla de trabajo escala.Pares (alta, ascendiente) para f-ascendientes.sql.
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
-- GenealogyTree por niveles: Node = Node del padre + ordinal; HierarchyPath = ruta del padre + MemberId + '/'.
SET XACT_ABORT ON;
INSERT INTO GenealogyTree (Id, MemberId, ParentMemberId, HierarchyPath, Level, CreationDate, CreatedBy, LastUpdateDate, IsDeleted, Node)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, NULL, CONCAT(N'/', m.MemberId, N'/'), 0,
       DATEADD(millisecond, 170, m.Alta), N'SYSTEM', DATEADD(millisecond, 170, m.Alta), 0,
       hierarchyid::GetRoot().GetDescendant((SELECT MAX(Node) FROM GenealogyTree WHERE Node.GetLevel() = 1), NULL)
FROM escala.Altas m WHERE m.n = 0;
DECLARE @nivel int = 1, @max int = (SELECT MAX(GenNivel) FROM escala.Altas), @filas bigint = 1;
WHILE @nivel <= @max
BEGIN
    INSERT INTO GenealogyTree (Id, MemberId, ParentMemberId, HierarchyPath, Level, CreationDate, CreatedBy, LastUpdateDate, IsDeleted, Node)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, p.MemberId, CONCAT(gp.HierarchyPath, m.MemberId, N'/'), gp.Level + 1,
           DATEADD(millisecond, 170, m.Alta), m.Email, DATEADD(millisecond, 170, m.Alta), 0,
           CAST(CONCAT(gp.Node.ToString(), m.GenOrd, N'/') AS hierarchyid)
    FROM escala.Altas m
    JOIN escala.Altas p ON p.n = m.GenPadre
    JOIN GenealogyTree gp ON gp.MemberId = p.MemberId
    WHERE m.GenNivel = @nivel
    OPTION (RECOMPILE);
    SET @filas += @@ROWCOUNT;
    SET @nivel += 1;
END
EXEC #marca N'4b. GenealogyTree', @filas;
GO
-- Pares (alta, ascendiente) para las tres tablas de f-ascendientes.sql: los ascendientes de un alta son los segmentos de
-- su HierarchyPath de inscripcion salvo ella misma (el patrocinador y todos los suyos hasta la raiz). Directo = patrocinador.
SELECT g.MemberId AS T, CAST(s.value AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS E,
       CAST(CASE WHEN s.value = p.MemberId THEN 1 ELSE 0 END AS bit) AS Directo,
       a.Alta, a.Activa, a.Email, a.Puntos
INTO escala.Pares
FROM GenealogyTree g
JOIN escala.Altas a ON a.MemberId = g.MemberId
JOIN escala.Altas p ON p.n = a.GenPadre
CROSS APPLY STRING_SPLIT(g.HierarchyPath, N'/') s
WHERE s.value <> N'' AND s.value <> g.MemberId;
DECLARE @f bigint = @@ROWCOUNT;
EXEC #marca N'e2. Pares (alta, ascendiente de inscripcion)', @f;
GO