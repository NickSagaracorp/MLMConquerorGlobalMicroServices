/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: preparacion (arboles, altas, piernas y equipo en tablas de trabajo del esquema escala, que z-cerrar.sql borra).
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
IF EXISTS (SELECT 1 FROM MemberProfiles WHERE MemberId LIKE N'ESC-%') OR SCHEMA_ID(N'escala') IS NOT NULL
    THROW 51065, N'Ya hay altas ESC o tablas de trabajo: borrar la base de escala y volver a 00-crear-bd-escala.sql.', 1;
IF TRY_CAST(N'$(Nodos)' AS int) IS NULL OR CAST(N'$(Nodos)' AS int) < 20000
    THROW 51066, N'Nodos debe ser un entero de al menos 20.000 (-v Nodos=400000).', 1;
GO
CREATE SCHEMA escala;
GO
-- ============================================================================================================
-- 0. Parametros y cronometro.
-- ============================================================================================================
SET NOCOUNT ON;
IF OBJECT_ID('escala.Par') IS NOT NULL DROP TABLE escala.Par;
CREATE TABLE escala.Par (N int NOT NULL, Columna int NOT NULL, TamP int NOT NULL, Inicio datetime2 NOT NULL, Base datetime2 NOT NULL,
                   PasoMs bigint NOT NULL, Hash nvarchar(200) NOT NULL, RolAmbassador nvarchar(450) NOT NULL);
DECLARE @N int = CAST(N'$(Nodos)' AS int);
DECLARE @base datetime2 = DATEADD(month, -24, GETDATE());
INSERT INTO escala.Par
SELECT @N, 8500, 50000, SYSDATETIME(), @base,
       -- paso entre altas: 24 meses repartidos entre todas
       DATEDIFF_BIG(millisecond, @base, GETDATE()) / (@N + 50002),
       N'AQAAAAIAAYagAAAAEDSwkKdz3FkVOp2XD5sTMtHFyMlkcimYfv+ZkVv6A2nlu2acLStf5j/JpexanINnBw==',
       (SELECT Id FROM AspNetRoles WHERE Name = N'Ambassador');
IF (SELECT RolAmbassador FROM escala.Par) IS NULL THROW 51067, N'Falta el rol Ambassador (00 copia los catalogos).', 1;

IF OBJECT_ID('tempdb..#t') IS NOT NULL DROP TABLE #t;
CREATE TABLE #t (Paso nvarchar(100) NOT NULL, Segundos int NOT NULL, Filas bigint NULL);
GO
CREATE OR ALTER PROCEDURE #marca @paso nvarchar(100), @filas bigint = NULL AS
BEGIN
    DECLARE @s int = DATEDIFF(second, (SELECT Inicio FROM escala.Par), SYSDATETIME());
    INSERT INTO #t VALUES (@paso, @s, @filas);
    DECLARE @msg nvarchar(300) = CONCAT(N'[', @s, N' s] ', @paso, CASE WHEN @filas IS NULL THEN N'' ELSE CONCAT(N' (', @filas, N' filas)') END);
    RAISERROR(@msg, 0, 1) WITH NOWAIT;
    CHECKPOINT;
END
GO
-- ============================================================================================================
-- 1. Arbol de inscripcion. escala.Gen: posicion en el arbol, su padre, nivel y ordinal entre hermanos. En anchura con una
--    media de 1,47 inscritos por patrocinador (de 0 a 10: 45 % ninguno, pocos muy productivos), y una rama larga de
--    @cadena niveles colgada del nodo mas hondo (la rama mas larga: unos cientos de niveles).
-- ============================================================================================================
CREATE OR ALTER PROCEDURE #arbol @total int, @tabla nvarchar(10), @cadena int AS
BEGIN
    SET NOCOUNT ON;
    -- @tabla: 'M' (bajo ESC-M) o 'P' (bajo ESC-P). Posicion 0 = la raiz del arbol.
    INSERT INTO escala.Gen (Arbol, Pos, Padre, Nivel, Ord, K) VALUES (@tabla, 0, NULL, 0, 1, 0);
    DECLARE @nivel int = 0, @hechos int = 0, @n int;
    WHILE @hechos < @total - @cadena
    BEGIN
        UPDATE g SET K = CASE WHEN h < 450 THEN 0 WHEN h < 670 THEN 1 WHEN h < 790 THEN 2 WHEN h < 860 THEN 3
                              WHEN h < 910 THEN 4 WHEN h < 940 THEN 5 WHEN h < 960 THEN 6 WHEN h < 975 THEN 7
                              WHEN h < 985 THEN 8 WHEN h < 993 THEN 9 ELSE 10 END
        FROM escala.Gen g
        CROSS APPLY (SELECT ABS(CAST(SUBSTRING(HASHBYTES('MD5', CONCAT(@tabla, g.Pos)), 1, 4) AS int)) % 1000 AS h) x
        WHERE g.Arbol = @tabla AND g.Nivel = @nivel;
        -- Que el arbol no se extinga mientras es pequeno: el primero de cada nivel inscribe al menos a 2.
        IF (SELECT COUNT(*) FROM escala.Gen WHERE Arbol = @tabla AND Nivel = @nivel) < 2000
            UPDATE escala.Gen SET K = CASE WHEN K < 2 THEN 2 ELSE K END
            WHERE Arbol = @tabla AND Pos = (SELECT MIN(Pos) FROM escala.Gen WHERE Arbol = @tabla AND Nivel = @nivel);
        INSERT INTO escala.Gen (Arbol, Pos, Padre, Nivel, Ord, K)
        SELECT TOP (@total - @cadena - @hechos) @tabla, @hechos + ROW_NUMBER() OVER (ORDER BY g.Pos, v.i), g.Pos, @nivel + 1, v.i, 0
        FROM escala.Gen g JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) v(i) ON v.i <= g.K
        WHERE g.Arbol = @tabla AND g.Nivel = @nivel
        ORDER BY g.Pos, v.i;
        SET @n = @@ROWCOUNT;
        IF @n = 0 THROW 51068, N'El arbol de inscripcion se extinguio.', 1;
        SET @hechos += @n;
        SET @nivel += 1;
    END
    -- La rama larga: cada eslabon inscribe al siguiente, desde el nodo mas hondo (sin hijos: es del ultimo nivel).
    DECLARE @padre int = (SELECT MIN(Pos) FROM escala.Gen WHERE Arbol = @tabla AND Nivel = (SELECT MAX(Nivel) FROM escala.Gen WHERE Arbol = @tabla));
    DECLARE @np int = (SELECT Nivel FROM escala.Gen WHERE Arbol = @tabla AND Pos = @padre), @i int = 0;
    WHILE @i < @cadena
    BEGIN
        INSERT INTO escala.Gen (Arbol, Pos, Padre, Nivel, Ord, K) VALUES (@tabla, @hechos + 1, @padre, @np + 1, 1, 0);
        SET @hechos += 1; SET @padre = @hechos; SET @np += 1; SET @i += 1;
    END
END
GO
IF OBJECT_ID('escala.Gen') IS NOT NULL DROP TABLE escala.Gen;
CREATE TABLE escala.Gen (Arbol char(1) NOT NULL, Pos int NOT NULL, Padre int NULL, Nivel int NOT NULL, Ord int NOT NULL, K int NOT NULL,
                   PRIMARY KEY (Arbol, Pos));
CREATE INDEX ix_nivel ON escala.Gen (Arbol, Nivel) INCLUDE (K);
DECLARE @N int = (SELECT N FROM escala.Par), @P int = (SELECT TamP - 1 FROM escala.Par);
EXEC #arbol @N, 'M', 400;
EXEC #arbol @P, 'P', 0;
EXEC #marca N'1. Arbol de inscripcion en anchura';
GO
-- ============================================================================================================
-- 2. escala.Altas: una fila por alta, con su lugar en los dos arboles, su producto y sus ids.
-- ============================================================================================================
IF OBJECT_ID('escala.Altas') IS NOT NULL DROP TABLE escala.Altas;
CREATE TABLE escala.Altas (
    n int NOT NULL PRIMARY KEY,
    MemberId nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL,
    GenPadre int NULL, GenNivel int NOT NULL, GenOrd int NOT NULL,
    BinPadre int NULL, BinLado int NOT NULL DEFAULT 0, BinProf int NULL, BinJ int NOT NULL DEFAULT 0,
    Nivel int NULL, Producto nvarchar(450) COLLATE DATABASE_DEFAULT NULL, Puntos int NOT NULL DEFAULT 0,
    Precio decimal(18,2) NOT NULL DEFAULT 0, Total decimal(18,2) NOT NULL DEFAULT 0, NombreNivel nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
    Alta datetime2 NULL, Activa datetime2 NULL, Colocada datetime2 NULL,
    ProfileId nvarchar(36) COLLATE DATABASE_DEFAULT NOT NULL DEFAULT LOWER(CONVERT(nvarchar(36), NEWID())),
    UserGuid uniqueidentifier NOT NULL DEFAULT NEWID(),
    AspId nvarchar(36) COLLATE DATABASE_DEFAULT NOT NULL DEFAULT LOWER(CONVERT(nvarchar(36), NEWID())),
    OrderId nvarchar(36) COLLATE DATABASE_DEFAULT NOT NULL DEFAULT LOWER(CONVERT(nvarchar(36), NEWID())),
    SubId nvarchar(36) COLLATE DATABASE_DEFAULT NOT NULL DEFAULT LOWER(CONVERT(nvarchar(36), NEWID())),
    PagoExt nvarchar(36) COLLATE DATABASE_DEFAULT NOT NULL DEFAULT LOWER(CONVERT(nvarchar(36), NEWID())),
    Email nvarchar(100) COLLATE DATABASE_DEFAULT NULL, Nombre nvarchar(50) COLLATE DATABASE_DEFAULT NULL, Apellido nvarchar(50) COLLATE DATABASE_DEFAULT NULL,
    OrderNo nvarchar(40) COLLATE DATABASE_DEFAULT NULL);

DECLARE @N int = (SELECT N FROM escala.Par), @P int = (SELECT TamP FROM escala.Par);
-- ESC-0 (raiz del sistema, como ROOT001) y el arbol de ESC-M: n = posicion + 1.
INSERT INTO escala.Altas (n, MemberId, GenPadre, GenNivel, GenOrd) VALUES (0, N'ESC-0', NULL, 0, 1);
INSERT INTO escala.Altas (n, MemberId, GenPadre, GenNivel, GenOrd)
SELECT g.Pos + 1, CASE WHEN g.Pos = 0 THEN N'ESC-M' ELSE CONCAT(N'ESC-', g.Pos + 1) END,
       CASE WHEN g.Pos = 0 THEN 0 ELSE g.Padre + 1 END, g.Nivel + 1, g.Ord
FROM escala.Gen g WHERE g.Arbol = 'M';
-- ESC-P, inscrito por ESC-2 (la cabeza de la columna binaria, nivel 2 de inscripcion: su ultimo hijo), y su arbol:
-- n = N + 2 + posicion. Al colocar a ESC-P bajo su patrocinador derrama a unos 8.500 niveles (riesgo P-R6).
INSERT INTO escala.Altas (n, MemberId, GenPadre, GenNivel, GenOrd)
SELECT @N + 2 + g.Pos, CASE WHEN g.Pos = 0 THEN N'ESC-P' ELSE CONCAT(N'ESC-', @N + 2 + g.Pos) END,
       CASE WHEN g.Pos = 0 THEN 2 ELSE @N + 2 + g.Padre END,
       g.Nivel + 3,
       CASE WHEN g.Pos = 0 THEN (SELECT COUNT(*) FROM escala.Gen x WHERE x.Arbol = 'M' AND x.Padre = 1) + 1 ELSE g.Ord END
FROM escala.Gen g WHERE g.Arbol = 'P';
DROP TABLE escala.Gen;

-- Binario. ESC-0 raiz; ESC-M a su izquierda; columna n = 2..8501 por la izquierda.
DECLARE @col int = (SELECT Columna FROM escala.Par);
UPDATE escala.Altas SET BinPadre = NULL, BinProf = 0 WHERE n = 0;
UPDATE escala.Altas SET BinPadre = 0, BinLado = 0, BinProf = 1 WHERE n = 1;
UPDATE escala.Altas SET BinPadre = n - 1, BinLado = 0, BinProf = n WHERE n BETWEEN 2 AND @col + 1;
-- Subarboles por la derecha de cada nodo de la columna, por niveles, hasta sumar N (n = 8502..N+1).
IF OBJECT_ID('escala.Sb') IS NOT NULL DROP TABLE escala.Sb;
CREATE TABLE escala.Sb (n int PRIMARY KEY, Padre int NOT NULL, Lado int NOT NULL, Prof int NOT NULL, J int NOT NULL, C int NOT NULL, Pos bigint NOT NULL);
DECLARE @sig int = @col + 2, @resto int = @N - @col, @j int = 1, @k int;
INSERT INTO escala.Sb (n, Padre, Lado, Prof, J, C, Pos)
SELECT TOP (@resto) @sig - 1 + ROW_NUMBER() OVER (ORDER BY m.n), m.n, 1, m.BinProf + 1, 1, m.n, 0
FROM escala.Altas m WHERE m.n BETWEEN 2 AND @col + 1 ORDER BY m.n;
SET @k = @@ROWCOUNT; SET @sig += @k; SET @resto -= @k;
WHILE @resto > 0
BEGIN
    INSERT INTO escala.Sb (n, Padre, Lado, Prof, J, C, Pos)
    SELECT TOP (@resto) @sig - 1 + ROW_NUMBER() OVER (ORDER BY s.C, s.Pos, v.l), s.n, v.l, s.Prof + 1, @j + 1, s.C, s.Pos * 2 + v.l
    FROM escala.Sb s CROSS JOIN (VALUES (0), (1)) v(l)
    WHERE s.J = @j
    ORDER BY s.C, s.Pos, v.l;
    SET @k = @@ROWCOUNT; SET @sig += @k; SET @resto -= @k; SET @j += 1;
END
UPDATE m SET BinPadre = s.Padre, BinLado = s.Lado, BinProf = s.Prof, BinJ = s.J FROM escala.Altas m JOIN escala.Sb s ON s.n = m.n;
DROP TABLE escala.Sb;
-- ESC-P: raiz provisional con un arbol completo (indice i = n - (N+2); padre (i-1)/2; lado (i-1)%2).
UPDATE escala.Altas SET BinPadre = NULL, BinLado = 0, BinProf = 0 WHERE n = @N + 2;
UPDATE escala.Altas SET BinPadre = @N + 2 + (n - (@N + 2) - 1) / 2, BinLado = (n - (@N + 2) - 1) % 2
WHERE n > @N + 2;
-- Profundidad dentro del arbol de ESC-P: floor(log2(i + 1)).
UPDATE escala.Altas SET BinProf = CAST(FLOOR(LOG(n - (@N + 2) + 1, 2) + 1e-9) AS int) WHERE n > @N + 2;
-- BinJ: pasos desde la columna (o desde ESC-P); g-binario inserta por BinJ.
UPDATE escala.Altas SET BinJ = BinProf WHERE n > @N + 2;

-- Producto, importes y puntos del catalogo real.
UPDATE m SET Nivel = CASE WHEN m.n % 10 < 3 THEN 2 WHEN m.n % 10 < 8 THEN 3 ELSE 4 END FROM escala.Altas m WHERE m.n > 0;
UPDATE m SET Producto = p.Id, Puntos = p.QualificationPoins, Precio = p.SetupFee + p.MonthlyFee,
             Total = p.SetupFee + p.MonthlyFee + la.SetupFee + la.MonthlyFee, NombreNivel = ml.Name
FROM escala.Altas m
JOIN Products p ON p.MembershipLevelId = m.Nivel AND p.IsActive = 1 AND p.IsDeleted = 0 AND p.Id <> N'00000005-prod-0000-0000-000000000005'
JOIN MembershipLevels ml ON ml.Id = m.Nivel
CROSS JOIN (SELECT SetupFee, MonthlyFee FROM Products WHERE Id = N'00000005-prod-0000-0000-000000000005') la
WHERE m.n > 0;
IF EXISTS (SELECT 1 FROM escala.Altas WHERE n > 0 AND Producto IS NULL) THROW 51069, N'Falta un producto de membresia en el catalogo.', 1;

-- Fechas: alta creciente con n; activacion (pago Entrax) un segundo despues; colocacion dos horas despues.
DECLARE @b datetime2 = (SELECT Base FROM escala.Par), @paso bigint = (SELECT PasoMs FROM escala.Par);
UPDATE escala.Altas SET Alta = DATEADD(millisecond, CAST(((CAST(n AS bigint) + 1) * @paso) % 86400000 AS int),
                             DATEADD(day, CAST(((CAST(n AS bigint) + 1) * @paso) / 86400000 AS int), @b));
UPDATE escala.Altas SET Activa = DATEADD(millisecond, 1100, Alta), Colocada = DATEADD(hour, 2, Alta);

-- Datos personales deterministas.
UPDATE escala.Altas SET Email = CONCAT(N'esc-', n, N'@example.com'),
              Nombre = CHOOSE(n % 20 + 1, N'Ana', N'Luis', N'Maria', N'Jose', N'Carmen', N'Juan', N'Lucia', N'Pedro', N'Elena', N'Diego',
                                         N'Sofia', N'Pablo', N'Laura', N'Jorge', N'Marta', N'Andres', N'Paula', N'Raul', N'Irene', N'Hugo'),
              Apellido = CHOOSE(n % 25 + 1, N'Garcia', N'Lopez', N'Martinez', N'Sanchez', N'Perez', N'Gomez', N'Diaz', N'Ruiz', N'Hernandez', N'Jimenez',
                                           N'Moreno', N'Alvarez', N'Romero', N'Navarro', N'Torres', N'Dominguez', N'Vazquez', N'Ramos', N'Gil', N'Serrano',
                                           N'Blanco', N'Molina', N'Castro', N'Ortiz', N'Rubio');
-- OrderNo como OrderNumberHelper: iniciales del nivel + MMdd-HHmmssfff- + 4 letras (aqui deterministas por n).
IF OBJECT_ID('escala.Ini') IS NOT NULL DROP TABLE escala.Ini;
SELECT ml.Id AS Nivel,
       (SELECT STRING_AGG(UPPER(LEFT(w.value, 1)), N'') WITHIN GROUP (ORDER BY w.ordinal)
        FROM STRING_SPLIT(ml.Name, N' ', 1) w WHERE w.value <> N'') AS Iniciales
INTO escala.Ini FROM MembershipLevels ml;
UPDATE m SET OrderNo = CONCAT(i.Iniciales,
        RIGHT(CONCAT(N'0', DATEPART(month, m.Alta)), 2), RIGHT(CONCAT(N'0', DATEPART(day, m.Alta)), 2), N'-',
        RIGHT(CONCAT(N'0', DATEPART(hour, m.Alta)), 2), RIGHT(CONCAT(N'0', DATEPART(minute, m.Alta)), 2),
        RIGHT(CONCAT(N'0', DATEPART(second, m.Alta)), 2), RIGHT(CONCAT(N'00', DATEPART(millisecond, m.Alta)), 3), N'-',
        CHAR(65 + m.n % 26), CHAR(65 + (m.n / 26) % 26), CHAR(65 + (m.n / 676) % 26), CHAR(65 + (m.n / 17576) % 26))
FROM escala.Altas m JOIN escala.Ini i ON i.Nivel = m.Nivel
WHERE m.n > 0;
CREATE INDEX ix_genpadre ON escala.Altas (GenPadre);
CREATE INDEX ix_gennivel ON escala.Altas (GenNivel);
CREATE INDEX ix_binprof ON escala.Altas (BinProf) INCLUDE (BinPadre, BinLado);
CREATE INDEX ix_binj ON escala.Altas (BinJ, n) INCLUDE (BinPadre, BinLado);
DECLARE @f bigint = (SELECT COUNT_BIG(*) FROM escala.Altas);
EXEC #marca N'2. Altas, arboles, productos y fechas en memoria', @f;
GO
-- ============================================================================================================
-- 3. Estadisticas: piernas del binario (de abajo arriba por profundidad) y equipo de inscripcion (por nivel).
-- ============================================================================================================
IF OBJECT_ID('escala.Piernas') IS NOT NULL DROP TABLE escala.Piernas;
SELECT n, BinPadre, BinLado, BinProf, CAST(Puntos AS decimal(18,4)) AS Propios,
       CAST(0 AS decimal(18,4)) AS IzqP, CAST(0 AS decimal(18,4)) AS DerP, 0 AS IzqC, 0 AS DerC,
       CASE WHEN BinPadre IS NULL THEN 0 ELSE 1 END AS TienePadre,
       -- arbol: 0 el de ESC-0, 1 el de ESC-P (profundidades distintas)
       CASE WHEN n >= (SELECT N + 2 FROM escala.Par) THEN 1 ELSE 0 END AS Arbol
INTO escala.Piernas FROM escala.Altas;
CREATE UNIQUE CLUSTERED INDEX cx ON escala.Piernas (Arbol, BinProf, n);
CREATE INDEX ix_p ON escala.Piernas (BinPadre, BinLado) INCLUDE (Propios, IzqP, DerP, IzqC, DerC);
DECLARE @a int = 0, @prof int;
WHILE @a <= 1
BEGIN
    SET @prof = (SELECT MAX(BinProf) FROM escala.Piernas WHERE Arbol = @a);
    WHILE @prof >= 0
    BEGIN
        UPDATE p SET IzqP = ISNULL(l.P, 0), DerP = ISNULL(r.P, 0), IzqC = ISNULL(l.C, 0), DerC = ISNULL(r.C, 0)
        FROM escala.Piernas p
        OUTER APPLY (SELECT c.Propios + c.IzqP + c.DerP AS P, 1 + c.IzqC + c.DerC AS C FROM escala.Piernas c WHERE c.BinPadre = p.n AND c.BinLado = 0) l
        OUTER APPLY (SELECT c.Propios + c.IzqP + c.DerP AS P, 1 + c.IzqC + c.DerC AS C FROM escala.Piernas c WHERE c.BinPadre = p.n AND c.BinLado = 1) r
        WHERE p.Arbol = @a AND p.BinProf = @prof;
        SET @prof -= 1;
    END
    SET @a += 1;
END
EXEC #marca N'3a. Piernas del binario (puntos y contadores)';
GO
IF OBJECT_ID('escala.Equipo') IS NOT NULL DROP TABLE escala.Equipo;
SELECT n, GenPadre, GenNivel, Puntos, 0 AS Equipo, CAST(0 AS bigint) AS PuntosEquipo, 0 AS Directos INTO escala.Equipo FROM escala.Altas;
CREATE UNIQUE CLUSTERED INDEX cx ON escala.Equipo (GenNivel, n);
CREATE INDEX ix_p ON escala.Equipo (GenPadre) INCLUDE (Equipo, PuntosEquipo, Puntos);
DECLARE @nivel int = (SELECT MAX(GenNivel) FROM escala.Equipo);
WHILE @nivel >= 0
BEGIN
    UPDATE p SET Equipo = ISNULL(h.Equipo, 0), PuntosEquipo = ISNULL(h.Pts, 0), Directos = ISNULL(h.Directos, 0)
    FROM escala.Equipo p
    OUTER APPLY (SELECT SUM(1 + c.Equipo) AS Equipo, SUM(c.Puntos + c.PuntosEquipo) AS Pts, COUNT(*) AS Directos
                 FROM escala.Equipo c WHERE c.GenPadre = p.n) h
    WHERE p.GenNivel = @nivel
    OPTION (RECOMPILE);
    SET @nivel -= 1;
END
EXEC #marca N'3b. Equipo de inscripcion (tamano, puntos, directos)';
GO
-- ============================================================================================================
CREATE UNIQUE INDEX ux_memberid ON escala.Altas (MemberId) INCLUDE (n, GenPadre);
GO
-- Las fases leen las piernas por n (g-binario, d-cobros).
CREATE UNIQUE INDEX ux_n ON escala.Piernas (n) INCLUDE (IzqP, DerP, IzqC, DerC);
CREATE UNIQUE INDEX ux_n ON escala.Equipo (n) INCLUDE (Equipo, PuntosEquipo, Directos);
GO
