/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: informe y borrado de las tablas de
trabajo (esquema escala). Despues la base solo tiene las tablas del modelo. Lo lanza ..\01-generar-altas.ps1.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
IF DB_NAME() <> N'MLMConquerorGlobalEditionDb_Escala'
    THROW 51062, N'Este script es solo para MLMConquerorGlobalEditionDb_Escala.', 1;
IF N'$(Confirmo)' <> N'ESCALA'
    THROW 51063, N'Ejecutar con -v Confirmo=ESCALA.', 1;
GO
SELECT (SELECT N FROM escala.Par) AS N,
       (SELECT COUNT(*) FROM MemberProfiles WHERE MemberId LIKE N'ESC-%') AS Altas,
       (SELECT MAX(GenNivel) FROM escala.Altas) AS NivelesInscripcion,
       (SELECT MAX(BinProf) FROM escala.Altas) AS ProfundidadBinario,
       (SELECT COUNT(*) FROM DualTeamTree) AS NodosBinario,
       (SELECT LeftLegCount + RightLegCount FROM DualTeamTree WHERE MemberId = N'ESC-M') AS EquipoBinarioEscM,
       (SELECT Equipo FROM escala.Equipo WHERE n = 1) AS EquipoInscripcionEscM,
       (SELECT COUNT_BIG(*) FROM escala.Pares) AS ParesAscendientes;
DROP TABLE IF EXISTS escala.Pares, escala.Equipo, escala.Piernas, escala.Altas, escala.Ini, escala.Par, escala.Gen, escala.Sb;
DROP SCHEMA escala;
CHECKPOINT;
SELECT name, size / 128 AS Mb FROM sys.database_files;
