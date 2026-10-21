/*
Plan Q, Bloque 7 (desviacion: crecer en el sitio). Anade UN LOTE de altas completas a una base de escala YA GENERADA
(MLMConquerorGlobalEditionDb_Escala), sin borrarla ni pararla: los servicios pueden estar apuntando a ella. Lo lanza
..\03-anadir-altas.ps1 en bucle hasta el tamano pedido.

Cada alta nueva deja exactamente lo mismo que una del generador (01-generar-altas.ps1, que es lo que deja un alta real del
Plan Q): usuario y rol, perfil con nivel de alta, pedido con dos lineas, suscripcion activa con su aporte, facturacion
recurrente, pago Entrax con sus llamadas e historial, cuenta atras, comisiones al patrocinador, huella, estadisticas,
nodo de inscripcion, nodo binario con su clave, registro de colocacion, cola de rango y avisos hasta 10 ascendientes (mas
la evaluacion del propio) y los tres deltas de estadisticas, todo ya aplicado. Y sube a sus ascendientes lo que subirian las
colas: EnrollmentTeamSize, EnrollmentPoints, EnrollmentQualifiedTeamMembers y QualifiedSponsoredMembers por la linea de
inscripcion; puntos, contadores y calificados de pierna (y DualTeamPoints, DualTeamSize) por la linea binaria.

Forma: la misma que el generador, como si se hubiera generado con mas altas.
  - Inscripcion: continua la anchura del arbol de ESC-M (sin el de ESC-P): por (nivel, n), cada patrocinador inscribe
    hasta K (el mismo hash MD5 de a-preparar.sql sobre su posicion n - 1) y los que ya tienen K no inscriben mas.
  - Binario: continua los subarboles de la columna de ESC-M por niveles: los huecos libres por el n de su padre (que es
    el orden en anchura) y por lado. La columna (ESC-M .. ESC-8501) y ESC-P (raiz provisional de 50.000) no cambian.
  - Fechas: cada alta, entre la de su patrocinador (y la colocacion de su padre) y ahora, repartida por un hash de n.
Solo patrocinan y reciben altas los miembros que ya estaban antes del lote (los de lotes anteriores incluidos).

Concurrencia (los servicios pueden estar trabajando contra la base): el lote se calcula primero (solo lecturas) y se
guarda en el esquema de trabajo escalalote; despues se escribe por fases, cada una en su transaccion corta y anotada en
escalalote.Fase. Sin candados de aplicacion: lo que se suma a los ascendientes es aditivo, igual que lo que suman los
vaciados de las colas, asi que no se pisan (como mucho esperan unos a otros por las filas). La ultima fase (arbol) crea
los nodos y suma a los ascendientes en una transaccion: hasta entonces las altas del lote estan a medias (perfil, pedido y
colas sin nodos); al confirmarla, el lote queda coherente. Si una fase falla (por ejemplo, alguien ocupo un hueco calculado
del binario), al volver a lanzar el script se reanuda ese mismo lote desde la fase que falto. Sin TABLOCK ni indices
desactivados: es mas lento que el generador, pero no bloquea la base.

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
IF TRY_CAST(N'$(Altas)' AS int) IS NULL OR CAST(N'$(Altas)' AS int) NOT BETWEEN 1 AND 100000
    THROW 51141, N'Altas debe ser un entero entre 1 y 100.000 (-v Altas=20000).', 1;
IF NOT EXISTS (SELECT 1 FROM DualTeamTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0)
   OR NOT EXISTS (SELECT 1 FROM DualTeamTree WHERE MemberId = N'ESC-8501' AND IsDeleted = 0)
    THROW 51142, N'No hay una base de escala generada (ESC-M y su columna): primero 01-generar-altas.ps1.', 1;
IF SCHEMA_ID(N'escala') IS NOT NULL
    THROW 51143, N'Hay tablas de trabajo del generador (esquema escala): una generacion a medias.', 1;
GO
-- ============================================================================================================
-- Calculo del lote (solo lecturas). Si hay un lote a medias (esquema escalalote), se reanuda ese y no se calcula otro.
-- ============================================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SCHEMA_ID(N'escalalote') IS NOT NULL
BEGIN
    RAISERROR(N'Hay un lote a medias (esquema escalalote): se reanuda.', 0, 1) WITH NOWAIT;
    RETURN;
END
DECLARE @inicio datetime2 = SYSDATETIME(), @msg nvarchar(400);
DECLARE @altas int = CAST(N'$(Altas)' AS int);
DECLARE @ahora datetime2 = GETDATE();
DECLARE @hash nvarchar(200) = (SELECT TOP 1 PasswordHash FROM AspNetUsers WHERE MemberProfileId = N'ESC-1003');
DECLARE @rol nvarchar(450) = (SELECT Id FROM AspNetRoles WHERE Name = N'Ambassador');
DECLARE @payout int = ISNULL((SELECT TOP 1 DefaultPayoutFrequency FROM CompanyInfo), 2);
DECLARE @nodoM hierarchyid = (SELECT Node FROM GenealogyTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0);
DECLARE @nodoP hierarchyid = (SELECT Node FROM GenealogyTree WHERE MemberId = N'ESC-P' AND IsDeleted = 0);
IF @hash IS NULL OR @rol IS NULL OR @nodoM IS NULL OR @nodoP IS NULL
    THROW 51144, N'Faltan ESC-1003, el rol Ambassador o los nodos de ESC-M y ESC-P.', 1;
-- El primer n libre (ESC-0, ESC-M = 1, ESC-P y los numericos).
DECLARE @n0 int = 1 + (SELECT MAX(TRY_CAST(SUBSTRING(MemberId, 5, 20) AS int)) FROM MemberProfiles WHERE MemberId LIKE N'ESC-[0-9]%');

-- ============================================================================================================
-- 1. Patrocinadores: la anchura del arbol de ESC-M sin el de ESC-P, cada uno hasta su K.
-- ============================================================================================================
DROP TABLE IF EXISTS #hijos;
SELECT CAST(ParentMemberId AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS MemberId, COUNT(*) AS Tiene
INTO #hijos FROM GenealogyTree WHERE IsDeleted = 0 AND ParentMemberId LIKE N'ESC-%' GROUP BY ParentMemberId;
CREATE UNIQUE CLUSTERED INDEX cx ON #hijos (MemberId);

DROP TABLE IF EXISTS #cand;
SELECT CAST(g.MemberId AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS MemberId, g.Node, g.Level, g.HierarchyPath, x.n, ISNULL(h.Tiene, 0) AS Tiene,
       CASE WHEN k.h < 450 THEN 0 WHEN k.h < 670 THEN 1 WHEN k.h < 790 THEN 2 WHEN k.h < 860 THEN 3
            WHEN k.h < 910 THEN 4 WHEN k.h < 940 THEN 5 WHEN k.h < 960 THEN 6 WHEN k.h < 975 THEN 7
            WHEN k.h < 985 THEN 8 WHEN k.h < 993 THEN 9 ELSE 10 END AS K
INTO #cand
FROM GenealogyTree g
CROSS APPLY (SELECT CASE WHEN g.MemberId = N'ESC-M' THEN 1 ELSE TRY_CAST(SUBSTRING(g.MemberId, 5, 20) AS int) END AS n) x
CROSS APPLY (SELECT ABS(CAST(SUBSTRING(HASHBYTES('MD5', CONCAT('M', x.n - 1)), 1, 4) AS int)) % 1000 AS h) k
LEFT JOIN #hijos h ON h.MemberId = g.MemberId
WHERE g.IsDeleted = 0 AND g.Node.IsDescendantOf(@nodoM) = 1 AND g.Node.IsDescendantOf(@nodoP) = 0 AND x.n IS NOT NULL;
DELETE FROM #cand WHERE Tiene >= K;
CREATE CLUSTERED INDEX cx ON #cand (Level, n);

DROP TABLE IF EXISTS #ins;
SELECT TOP (@altas) ROW_NUMBER() OVER (ORDER BY c.Level, c.n, v.i) AS i, c.MemberId AS Patrocinador, c.Node AS NodoPadre,
       c.Level AS NivelPadre, c.HierarchyPath AS RutaPadre, v.i AS Ord
INTO #ins
FROM #cand c JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) v(i) ON v.i > c.Tiene AND v.i <= c.K
ORDER BY c.Level, c.n, v.i;
IF (SELECT COUNT(*) FROM #ins) < @altas THROW 51145, N'No quedan patrocinadores con hueco en la anchura de ESC-M.', 1;
CREATE UNIQUE CLUSTERED INDEX cx ON #ins (i);

-- ============================================================================================================
-- 2. Huecos del binario: los subarboles de la columna de ESC-M (n >= 8502), por el n del padre y el lado.
-- ============================================================================================================
DECLARE @kM varbinary(1700) = (SELECT BinaryPath FROM DualTeamTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0);
DECLARE @k varbinary(1700) = @kM;
DECLARE @kLen int = DATALENGTH(@k);
DECLARE @kUlt int = CAST(SUBSTRING(@k, @kLen, 1) AS int);
DECLARE @kTer int = @kUlt & (-@kUlt);
DECLARE @lo varbinary(1700) = SUBSTRING(@k, 1, @kLen - 1) + CAST(CAST(@kUlt - @kTer AS binary(1)) AS varbinary(1));
DECLARE @hi varbinary(1700) = @lo, @kPos int, @kSuma int, @kVal int;
IF @kTer < 128 SELECT @kPos = @kLen, @kSuma = @kTer * 2 ELSE SELECT @kPos = @kLen - 1, @kSuma = 1;
WHILE @kPos >= 1
BEGIN
    SET @kVal = CAST(SUBSTRING(@hi, @kPos, 1) AS int) + @kSuma;
    SET @hi = SUBSTRING(@hi, 1, @kPos - 1) + CAST(CAST(@kVal % 256 AS binary(1)) AS varbinary(1)) + SUBSTRING(@hi, @kPos + 1, 1700);
    IF @kVal < 256 BREAK;
    SELECT @kSuma = 1, @kPos = @kPos - 1;
END;

DROP TABLE IF EXISTS #ocupado;
SELECT CAST(ParentMemberId AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS MemberId, Side
INTO #ocupado FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId LIKE N'ESC-%';
CREATE CLUSTERED INDEX cx ON #ocupado (MemberId, Side);

DROP TABLE IF EXISTS #hueco;
SELECT TOP (@altas) ROW_NUMBER() OVER (ORDER BY x.n, s.Side) AS i, CAST(d.MemberId AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS Padre,
       s.Side, d.BinaryPath AS ClavePadre, d.Depth AS ProfPadre, d.CreationDate AS ColocadoPadre
INTO #hueco
FROM DualTeamTree d WITH (INDEX(UX_DualTeamTree_BinaryPath))
CROSS APPLY (SELECT TRY_CAST(SUBSTRING(d.MemberId, 5, 20) AS int) AS n) x
CROSS JOIN (VALUES (0), (1)) s(Side)
WHERE d.BinaryPath > @lo AND d.BinaryPath < @hi AND d.IsDeleted = 0 AND x.n >= 8502
  AND NOT EXISTS (SELECT 1 FROM #ocupado o WHERE o.MemberId = d.MemberId AND o.Side = s.Side)
ORDER BY x.n, s.Side;
IF (SELECT COUNT(*) FROM #hueco) < @altas THROW 51146, N'No quedan huecos en los subarboles de ESC-M.', 1;
CREATE UNIQUE CLUSTERED INDEX cx ON #hueco (i);

-- ============================================================================================================
-- 3. Las altas del lote: lugar en los dos arboles, producto, importes, fechas y datos personales (como a-preparar.sql).
-- ============================================================================================================
DROP TABLE IF EXISTS #a;
SELECT i.i, @n0 + i.i - 1 AS n, CONCAT(N'ESC-', @n0 + i.i - 1) AS MemberId,
       i.Patrocinador, i.NodoPadre, i.NivelPadre, i.RutaPadre, i.Ord,
       h.Padre AS PadreBin, h.Side AS Lado, h.ClavePadre, h.ProfPadre, h.ColocadoPadre,
       CASE WHEN (@n0 + i.i - 1) % 10 < 3 THEN 2 WHEN (@n0 + i.i - 1) % 10 < 8 THEN 3 ELSE 4 END AS Nivel,
       CAST(NULL AS nvarchar(450)) AS Producto, 0 AS Puntos, CAST(0 AS decimal(18,2)) AS Precio, CAST(0 AS decimal(18,2)) AS Total,
       CAST(NULL AS nvarchar(200)) AS NombreNivel,
       CAST(NULL AS datetime2) AS Alta, CAST(NULL AS datetime2) AS Activa, CAST(NULL AS datetime2) AS Colocada,
       LOWER(CONVERT(nvarchar(36), NEWID())) AS ProfileId, NEWID() AS UserGuid, LOWER(CONVERT(nvarchar(36), NEWID())) AS AspId,
       LOWER(CONVERT(nvarchar(36), NEWID())) AS OrderId, LOWER(CONVERT(nvarchar(36), NEWID())) AS SubId,
       LOWER(CONVERT(nvarchar(36), NEWID())) AS PagoExt,
       CAST(NULL AS nvarchar(100)) AS Email, CAST(NULL AS nvarchar(50)) AS Nombre, CAST(NULL AS nvarchar(50)) AS Apellido,
       CAST(NULL AS nvarchar(40)) AS OrderNo, CAST(NULL AS varbinary(1700)) AS Clave
INTO #a
FROM #ins i JOIN #hueco h ON h.i = i.i;
CREATE UNIQUE CLUSTERED INDEX cx ON #a (i);

UPDATE m SET Producto = p.Id, Puntos = p.QualificationPoins, Precio = p.SetupFee + p.MonthlyFee,
             Total = p.SetupFee + p.MonthlyFee + la.SetupFee + la.MonthlyFee, NombreNivel = ml.Name
FROM #a m
JOIN Products p ON p.MembershipLevelId = m.Nivel AND p.IsActive = 1 AND p.IsDeleted = 0 AND p.Id <> N'00000005-prod-0000-0000-000000000005'
JOIN MembershipLevels ml ON ml.Id = m.Nivel
CROSS JOIN (SELECT SetupFee, MonthlyFee FROM Products WHERE Id = N'00000005-prod-0000-0000-000000000005') la;
IF EXISTS (SELECT 1 FROM #a WHERE Producto IS NULL) THROW 51069, N'Falta un producto de membresia en el catalogo.', 1;

-- Alta: despues del alta de su patrocinador y de la colocacion de su padre binario, y al menos dos horas antes de ahora.
UPDATE m SET Alta = DATEADD(millisecond, m.n % 1000, DATEADD(second,
        CAST((CAST(ABS(CAST(SUBSTRING(HASHBYTES('MD5', CONCAT('A', m.n)), 1, 4) AS bigint)) % 1000000 AS float) / 1000000.0)
             * DATEDIFF_BIG(second, b.Desde, DATEADD(hour, -2, @ahora)) AS int), b.Desde))
FROM #a m
JOIN MemberProfiles s ON s.MemberId = m.Patrocinador
CROSS APPLY (SELECT DATEADD(second, 1, CASE WHEN s.EnrollDate > m.ColocadoPadre THEN s.EnrollDate ELSE m.ColocadoPadre END) AS Desde) b;
IF EXISTS (SELECT 1 FROM #a WHERE Alta IS NULL OR Alta > DATEADD(second, 1, DATEADD(hour, -2, @ahora)))
    THROW 51147, N'Un patrocinador o un padre binario es de hace menos de dos horas: esperar o lote mas pequeno.', 1;
UPDATE #a SET Activa = DATEADD(millisecond, 1100, Alta), Colocada = DATEADD(hour, 2, Alta),
              Email = CONCAT(N'esc-', n, N'@example.com'),
              Nombre = CHOOSE(n % 20 + 1, N'Ana', N'Luis', N'Maria', N'Jose', N'Carmen', N'Juan', N'Lucia', N'Pedro', N'Elena', N'Diego',
                                         N'Sofia', N'Pablo', N'Laura', N'Jorge', N'Marta', N'Andres', N'Paula', N'Raul', N'Irene', N'Hugo'),
              Apellido = CHOOSE(n % 25 + 1, N'Garcia', N'Lopez', N'Martinez', N'Sanchez', N'Perez', N'Gomez', N'Diaz', N'Ruiz', N'Hernandez', N'Jimenez',
                                           N'Moreno', N'Alvarez', N'Romero', N'Navarro', N'Torres', N'Dominguez', N'Vazquez', N'Ramos', N'Gil', N'Serrano',
                                           N'Blanco', N'Molina', N'Castro', N'Ortiz', N'Rubio');
UPDATE m SET OrderNo = CONCAT(i.Iniciales,
        RIGHT(CONCAT(N'0', DATEPART(month, m.Alta)), 2), RIGHT(CONCAT(N'0', DATEPART(day, m.Alta)), 2), N'-',
        RIGHT(CONCAT(N'0', DATEPART(hour, m.Alta)), 2), RIGHT(CONCAT(N'0', DATEPART(minute, m.Alta)), 2),
        RIGHT(CONCAT(N'0', DATEPART(second, m.Alta)), 2), RIGHT(CONCAT(N'00', DATEPART(millisecond, m.Alta)), 3), N'-',
        CHAR(65 + m.n % 26), CHAR(65 + (m.n / 26) % 26), CHAR(65 + (m.n / 676) % 26), CHAR(65 + (m.n / 17576) % 26))
FROM #a m
JOIN (SELECT ml.Id AS Nivel,
             (SELECT STRING_AGG(UPPER(LEFT(w.value, 1)), N'') WITHIN GROUP (ORDER BY w.ordinal)
              FROM STRING_SPLIT(ml.Name, N' ', 1) w WHERE w.value <> N'') AS Iniciales
      FROM MembershipLevels ml) i ON i.Nivel = m.Nivel;
UPDATE m SET Clave = k.ChildKey FROM #a m CROSS APPLY dbo.DualChildKey(m.ClavePadre, m.ProfPadre, m.Lado) k;

-- Hasta 10 ascendientes de inscripcion de cada alta (el patrocinador es el 1), como deja el alta (DQ5).
DROP TABLE IF EXISTS #pares;
SELECT m.MemberId AS T, CAST(g.MemberId AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS E, d.k AS Distancia, m.Alta, m.Activa, m.Email
INTO #pares
FROM #a m
CROSS APPLY (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) d(k)
JOIN GenealogyTree g ON g.Node = CASE WHEN d.k = 1 THEN m.NodoPadre ELSE m.NodoPadre.GetAncestor(d.k - 1) END AND g.IsDeleted = 0
WHERE d.k <= m.NivelPadre + 1 AND m.NodoPadre.GetLevel() >= d.k;

-- ============================================================================================================
-- 4. Lo que sube a los ascendientes, de abajo arriba (solo los tocados).
-- ============================================================================================================
-- Inscripcion: por Node. T = equipo, P = puntos, QQ = calificados del equipo; Q = patrocinados (solo el patrocinador).
DROP TABLE IF EXISTS #gi;
SELECT NodoPadre AS Node, MAX(Patrocinador) AS MemberId, NodoPadre.GetLevel() AS Nivel,
       COUNT(*) AS T, SUM(Puntos) AS P, COUNT(*) AS QQ, COUNT(*) AS Q
INTO #gi FROM #a GROUP BY NodoPadre;
CREATE UNIQUE CLUSTERED INDEX cx ON #gi (Nivel, Node);
DECLARE @nivel int = (SELECT MAX(Nivel) FROM #gi);
WHILE @nivel > 1
BEGIN
    INSERT INTO #gi (Node, MemberId, Nivel, T, P, QQ, Q)
    SELECT DISTINCT c.Node.GetAncestor(1), g.MemberId, @nivel - 1, 0, 0, 0, 0
    FROM #gi c JOIN GenealogyTree g ON g.Node = c.Node.GetAncestor(1) AND g.IsDeleted = 0
    WHERE c.Nivel = @nivel AND NOT EXISTS (SELECT 1 FROM #gi p WHERE p.Nivel = @nivel - 1 AND p.Node = c.Node.GetAncestor(1));
    UPDATE p SET T = p.T + s.T, P = p.P + s.P, QQ = p.QQ + s.QQ
    FROM #gi p JOIN (SELECT Node.GetAncestor(1) AS Padre, SUM(T) AS T, SUM(P) AS P, SUM(QQ) AS QQ FROM #gi WHERE Nivel = @nivel GROUP BY Node.GetAncestor(1)) s
         ON s.Padre = p.Node
    WHERE p.Nivel = @nivel - 1;
    SET @nivel -= 1;
END

-- Binario: por MemberId y profundidad. Lo de cada nodo sube a la pierna (Side) de su padre.
DROP TABLE IF EXISTS #gb;
SELECT a.PadreBin AS MemberId, a.ProfPadre AS Prof,
       SUM(CASE WHEN a.Lado = 0 THEN a.Puntos ELSE 0 END) AS IP, SUM(CASE WHEN a.Lado = 1 THEN a.Puntos ELSE 0 END) AS DP,
       SUM(CASE WHEN a.Lado = 0 THEN 1 ELSE 0 END) AS IC, SUM(CASE WHEN a.Lado = 1 THEN 1 ELSE 0 END) AS DC,
       CAST(MAX(d.ParentMemberId) AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS Padre, MAX(d.Side) AS Lado
INTO #gb FROM #a a JOIN DualTeamTree d ON d.MemberId = a.PadreBin AND d.IsDeleted = 0
GROUP BY a.PadreBin, a.ProfPadre;
CREATE UNIQUE CLUSTERED INDEX cx ON #gb (Prof, MemberId);
CREATE INDEX ix_m ON #gb (MemberId);
DECLARE @prof int = (SELECT MAX(Prof) FROM #gb);
WHILE @prof > 0
BEGIN
    INSERT INTO #gb (MemberId, Prof, IP, DP, IC, DC, Padre, Lado)
    SELECT d.MemberId, @prof - 1, 0, 0, 0, 0, d.ParentMemberId, d.Side
    FROM (SELECT DISTINCT Padre FROM #gb WHERE Prof = @prof AND Padre IS NOT NULL) c
    JOIN DualTeamTree d ON d.MemberId = c.Padre AND d.IsDeleted = 0
    WHERE NOT EXISTS (SELECT 1 FROM #gb p WHERE p.MemberId = c.Padre);
    UPDATE p SET IP = p.IP + s.I, IC = p.IC + s.IC2, DP = p.DP + s.D, DC = p.DC + s.DC2
    FROM #gb p JOIN (SELECT Padre, SUM(CASE WHEN Lado = 0 THEN IP + DP ELSE 0 END) AS I, SUM(CASE WHEN Lado = 0 THEN IC + DC ELSE 0 END) AS IC2,
                            SUM(CASE WHEN Lado = 1 THEN IP + DP ELSE 0 END) AS D, SUM(CASE WHEN Lado = 1 THEN IC + DC ELSE 0 END) AS DC2
                     FROM #gb WHERE Prof = @prof AND Padre IS NOT NULL GROUP BY Padre) s ON s.Padre = p.MemberId
    WHERE p.Prof = @prof - 1;
    SET @prof -= 1;
END
SET @msg = CONCAT(N'Lote de ', @altas, N' desde ESC-', @n0, N': calculado en ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s (',
                  (SELECT COUNT(*) FROM #gi), N' ascendientes de inscripcion, ', (SELECT COUNT(*) FROM #gb), N' del binario)');
RAISERROR(@msg, 0, 1) WITH NOWAIT;

EXEC (N'CREATE SCHEMA escalalote');
SELECT * INTO escalalote.A FROM #a;
SELECT * INTO escalalote.Pares FROM #pares;
SELECT * INTO escalalote.Gi FROM #gi;
SELECT * INTO escalalote.Gb FROM #gb;
CREATE UNIQUE CLUSTERED INDEX cx ON escalalote.A (i);
CREATE UNIQUE INDEX ux_m ON escalalote.A (MemberId);
SELECT @altas AS Altas, @n0 AS N0, @hash AS Hash, @rol AS Rol, @payout AS Payout INTO escalalote.Par;
CREATE TABLE escalalote.Fase (Nombre nvarchar(50) NOT NULL PRIMARY KEY);
GO
-- ============================================================================================================
-- Escritura por fases. Cada fase es una transaccion corta (sin candados de aplicacion: lo que se suma a los ascendientes
-- es aditivo, como lo que suman las colas, y no se pisa con ellas); al confirmar, la fase consta en escalalote.Fase y no
-- se repite si el lote se reanuda. Mientras faltan fases, las altas del lote estan a medias (perfil sin nodos, por
-- ejemplo); al confirmar la ultima (arbol), el lote queda coherente.
-- ============================================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @inicio datetime2 = SYSDATETIME(), @msg nvarchar(400);
DECLARE @altas int, @n0 int, @hash nvarchar(200), @rol nvarchar(450), @payout int;
SELECT @altas = Altas, @n0 = N0, @hash = Hash, @rol = Rol, @payout = Payout FROM escalalote.Par;

IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'identidad')
BEGIN
BEGIN TRAN;
INSERT INTO AspNetUsers (Id, MemberProfileId, IsActive, LastLoginAt, RefreshToken, RefreshTokenExpiry, CreationDate, CreatedBy,
                         UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp,
                         ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled,
                         AccessFailedCount, PreferredTwoFactorChannel, TwoFactorPhoneConfirmed)
SELECT m.AspId, m.MemberId, 1, NULL, NULL, NULL, m.Alta, m.Email,
       m.Email, UPPER(m.Email), m.Email, UPPER(m.Email), 1, @hash, REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''),
       LOWER(CONVERT(nvarchar(36), NEWID())), NULL, 0, 0, NULL, 1, 0, 0, 0
FROM escalalote.A m;
INSERT INTO AspNetUserRoles (UserId, RoleId) SELECT m.AspId, @rol FROM escalalote.A m;

INSERT INTO MemberProfiles (Id, UserId, MemberId, FirstName, LastName, DateOfBirth, Phone, WhatsApp, Country, State, City, Address,
                            ZipCode, BusinessName, ShowBusinessName, MemberType, Status, EnrollDate, SponsorMemberId, ReplicateSiteSlug,
                            ProfilePhotoUrl, IsNamePublic, IsEmailPublic, IsPhonePublic, CreationDate, CreatedBy, LastUpdateDate,
                            LastUpdateBy, IsDeleted, Email, DefaultLanguage, PayoutFrequency, KeepQualified, FeesFromBalance,
                            SignupMembershipLevelId)
SELECT m.ProfileId, m.UserGuid, m.MemberId, m.Nombre, m.Apellido, DATEADD(day, m.n % 15000, '1960-01-01'),
       CONCAT(N'+1305', RIGHT(CONCAT(N'0000000', m.n), 7)), NULL, N'US', N'FL', N'Miami', CONCAT(m.n, N' Ocean Dr'),
       N'33139', NULL, 0, 0, 1, m.Alta, m.Patrocinador, CONCAT(N'esc', m.n),
       NULL, 0, 0, 0, DATEADD(millisecond, 170, m.Alta), m.Email, m.Activa, m.Email,
       0, m.Email, N'en', @payout, 0, 1, m.Nivel
FROM escalalote.A m;

INSERT INTO escalalote.Fase (Nombre) VALUES (N'identidad');
COMMIT;
SET @msg = CONCAT(N'   fase identidad hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END

IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'pedidos')
BEGIN
BEGIN TRAN;
INSERT INTO Orders (Id, MemberId, MembershipSubscriptionId, TotalAmount, Status, OrderDate, Notes, CreationDate, CreatedBy,
                    LastUpdateDate, LastUpdateBy, IsDeleted, OrderNo)
SELECT m.OrderId, m.MemberId, m.SubId, m.Total, 4, m.Alta, CONCAT(N'Ambassador signup ', NCHAR(8212), N' ', m.NombreNivel),
       DATEADD(millisecond, 170, m.Alta), m.Email, m.Activa, m.Email, 0, m.OrderNo
FROM escalalote.A m;
INSERT INTO OrderDetails (OrderId, ProductId, Quantity, UnitPrice, CreationDate, CreatedBy)
SELECT m.OrderId, x.ProductId, 1, x.Precio, DATEADD(millisecond, 410, m.Alta), m.Email
FROM escalalote.A m
CROSS APPLY (VALUES (m.Producto, m.Precio), (N'00000005-prod-0000-0000-000000000005', m.Total - m.Precio)) x(ProductId, Precio);
-- Plan Q (DQ16): la activa aporta los puntos de su pedido.
INSERT INTO MembershipSubscriptions (Id, MemberId, MembershipLevelId, ChangeReason, SubscriptionStatus, StartDate, EndDate, RenewalDate,
                                     IsFree, IsAutoRenew, LastOrderId, CreationDate, CreatedBy, LastUpdateDate, LastUpdateBy, IsDeleted,
                                     DualTeamContribution, EnrollmentContribution, PersonalContribution)
SELECT m.SubId, m.MemberId, m.Nivel, 0, 1, m.Activa, DATEADD(month, 1, m.Activa), DATEADD(month, 1, m.Activa),
       ml.IsFree, ml.IsAutoRenew, m.OrderId, DATEADD(millisecond, 170, m.Alta), m.Email, m.Activa, m.Email, 0, m.Puntos, m.Puntos, 0
FROM escalalote.A m JOIN MembershipLevels ml ON ml.Id = m.Nivel;
INSERT INTO SubscriptionBillingStates (Id, MembershipSubscriptionId, MemberId, RecurringBillingPlanId, BillingAnchorDate, NextBillingDate,
                                       CurrentAttemptIndex, NextAttemptDate, Status, CreationDate, CreatedBy, LastUpdateDate, IsDeleted, Purpose)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.SubId, m.MemberId, rp.RecurringBillingPlanId, m.Activa, DATEADD(day, 30, m.Activa),
       0, DATEADD(day, 30, m.Activa), 1, m.Activa, m.Email, m.Activa, 0, 1
FROM escalalote.A m
CROSS APPLY (SELECT TOP 1 pp.RecurringBillingPlanId FROM RecurringBillingPlanProducts pp
             JOIN RecurringBillingPlans pl ON pl.Id = pp.RecurringBillingPlanId AND pl.IsActive = 1 AND pl.IsAnnualFeePlan = 0
             WHERE pp.ProductId = m.Producto ORDER BY pp.Id) rp;

INSERT INTO escalalote.Fase (Nombre) VALUES (N'pedidos');
COMMIT;
SET @msg = CONCAT(N'   fase pedidos hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END

IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'cobros')
BEGIN
BEGIN TRAN;
INSERT INTO EntraxPayments (Id, OrderId, MemberId, ExternalOrderId, ExternalPaymentId, Status, AmountUsd, AmountRub, ExchangeRate,
                            PaidAmountUsd, QrLink, ExpiresAt, PaidAt, Consumed, ActivatedAt, LastCheckedAt, CreationDate, CreatedBy,
                            LastUpdateDate, LastUpdateBy, IsDeleted)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.OrderId, m.MemberId, CONCAT(N'SIGNUP-', m.OrderId, N'-1'), m.PagoExt, 2, m.Total,
       ROUND(m.Total * 95.5, 2), 95.5, m.Total, CONCAT(N'https://entrax.invalid/fake/', m.PagoExt), DATEADD(minute, 15, m.Alta),
       m.Activa, 1, m.Activa, m.Activa, DATEADD(millisecond, 800, m.Alta), m.Email, m.Activa, N'Entrax', 0
FROM escalalote.A m;
INSERT INTO EntraxApiCalls (Operation, ExternalOrderId, ExternalPaymentId, HttpStatusCode, Success, ErrorCode, ProviderStatus, DurationMs, CreationDate, CreatedBy)
SELECT x.Op, CONCAT(N'SIGNUP-', m.OrderId, N'-1'), m.PagoExt, x.Http, 1, NULL, x.Estado, x.Ms, DATEADD(millisecond, x.Desfase, m.Alta), ISNULL(x.Por, m.Email)
FROM escalalote.A m
CROSS APPLY (VALUES (N'CreateQr', 200, N'pending', 1, 810, CAST(NULL AS nvarchar(50))),
                    (N'GetPayment', 200, N'paid', 1, 1050, N'Entrax:Poll'),
                    (N'Callback', 0, N'SUCCESS', 0, 1060, N'Entrax:Webhook')) x(Op, Http, Estado, Ms, Desfase, Por);
INSERT INTO PaymentHistories (Id, OrderId, MemberId, Amount, GatewayName, GatewayTransactionId, TransactionStatus, ProcessedAt,
                              CreationDate, CreatedBy, LastUpdateDate, LastUpdateBy, IsDeleted)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.OrderId, m.MemberId, m.Total, N'Entrax', m.PagoExt, 3, m.Activa,
       m.Activa, N'Entrax', m.Activa, N'Entrax', 0
FROM escalalote.A m;
INSERT INTO CommissionCountDowns (Id, MemberId, MemberId1, FastStartBonus1Start, FastStartBonus1End, FastStartBonus2Start, FastStartBonus2End,
                                  FastStartBonus3Start, FastStartBonus3End, CreationDate, CreatedBy, LastUpdateDate, IsDeleted,
                                  FastStartBonus1ExtendedStart, FastStartBonus1ExtendedEnd)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.UserGuid, m.ProfileId, m.Alta, DATEADD(day, 7, m.Alta), DATEADD(day, 7, m.Alta),
       DATEADD(day, 14, m.Alta), DATEADD(day, 14, m.Alta), DATEADD(day, 21, m.Alta), DATEADD(millisecond, 170, m.Alta), m.Email,
       DATEADD(millisecond, 170, m.Alta), 0, m.Alta, DATEADD(day, 14, m.Alta)
FROM escalalote.A m;
INSERT INTO CommissionEarnings (Id, BeneficiaryMemberId, SourceMemberId, SourceOrderId, CommissionTypeId, Amount, Status, EarnedDate,
                                PaymentDate, PeriodDate, IsManualEntry, Notes, CreationDate, CreatedBy, LastUpdateDate, IsDeleted)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.Patrocinador, m.MemberId, m.OrderId, ct.Id, ct.Amount, 1, m.Activa,
       DATEADD(day, ct.PaymentDelayDays, m.Activa), CAST(m.Activa AS date), 0,
       CONCAT(N'Order ', m.OrderNo, N' ', NCHAR(8212), N' ', m.Nombre, N' ', m.Apellido, N' (', m.MemberId, N')'),
       m.Activa, m.Email, m.Activa, 0
FROM escalalote.A m
JOIN (VALUES (2, 1), (2, 33), (3, 2), (3, 34), (4, 2), (4, 3), (4, 34), (4, 38)) t(Nivel, Tipo) ON t.Nivel = m.Nivel
JOIN CommissionTypes ct ON ct.Id = t.Tipo;
INSERT INTO SignupRiskFingerprints (VisitorId, RequestId, Flow, OrderId, MemberId, SponsorReplicateSite, IpAddress, UserAgent, CountryIso2,
                                    IsFlagged, FlagReason, CreationDate, CreatedBy, Cleared)
SELECT CONCAT(N'unknown-', REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')), REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), 0,
       NULL, NULL, s.ReplicateSiteSlug,
       CONCAT(N'10.', m.n / 65536 % 256, N'.', m.n / 256 % 256, N'.', m.n % 256), N'Mozilla/5.0 (escala)', NULL, 0, NULL,
       DATEADD(millisecond, -500, m.Alta), N'anonymous-signup', 0
FROM escalalote.A m JOIN MemberProfiles s ON s.MemberId = m.Patrocinador;

INSERT INTO escalalote.Fase (Nombre) VALUES (N'cobros');
COMMIT;
SET @msg = CONCAT(N'   fase cobros hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END

IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'estadisticas')
BEGIN
BEGIN TRAN;
INSERT INTO MemberStatistics (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize, EnrollmentTeamSize, DualTeamPoints, EnrollmentPoints,
                              QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers, EnrollmentQualifiedTeamMembers, EnrollmentTeamGrowth,
                              DualteamGrowth, EnrollmentTeamPointsGrowth, DualTeamPointsGrowth, CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth,
                              CurrentYearIncomeGrowth, CreationDate, CreatedBy)
SELECT m.MemberId, m.Puntos, 0, 0, 0, 0, m.Puntos, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, m.Activa, m.Email FROM escalalote.A m;

INSERT INTO escalalote.Fase (Nombre) VALUES (N'estadisticas');
COMMIT;
SET @msg = CONCAT(N'   fase estadisticas hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END

IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'cola-de-rango')
BEGIN
BEGIN TRAN;
-- Colas, ya procesadas: la evaluacion de rango (10 ascendientes del alta y el propio de la activacion), los tres deltas de
-- estadisticas y el aviso DOWNLINE_ENROLLED (10 ascendientes).
INSERT INTO RankEvaluationQueue (TriggerMemberId, EvaluateMemberId, TriggerEvent, TriggerDate, IsProcessed, ProcessedAt,
                                 ProcessedBy, ErrorMessage, RetryCount, CreationDate, CreatedBy)
SELECT T, E, 1, Alta, 1, DATEADD(hour, 5, Alta), N'RankEvaluationSweepJob', NULL, 0, DATEADD(millisecond, 170, Alta), Email FROM escalalote.Pares
UNION ALL
SELECT MemberId, MemberId, 1, DATEADD(second, 30, Activa), 1, DATEADD(hour, 5, Alta), N'RankEvaluationSweepJob', NULL, 0,
       DATEADD(second, 30, Activa), N'delta-apply'
FROM escalalote.A;
INSERT INTO escalalote.Fase (Nombre) VALUES (N'cola-de-rango');
COMMIT;
SET @msg = CONCAT(N'   fase cola-de-rango hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END

IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'deltas')
BEGIN
BEGIN TRAN;
INSERT INTO MemberStatisticDeltas (MemberId, Scope, EnrollmentPointsDelta, EnrollmentTeamSizeDelta, QualifiedSponsoredMembersDelta,
                                   EnrollmentQualifiedTeamMembersDelta, IsApplied, AppliedAt, SourceMemberId, CreationDate, CreatedBy)
SELECT x.MemberId, x.Scope, x.P, x.T, x.Q, x.QQ, 1, DATEADD(second, 30, m.Activa), m.MemberId, m.Activa, x.Por
FROM escalalote.A m
CROSS APPLY (VALUES (m.Patrocinador, 1, 0, 1, 1, 0, m.Email),
                    (m.MemberId, 1, m.Puntos, 0, 0, 0, N'member-points'),
                    (m.MemberId, 2, 0, 0, 0, 1, N'qualified-count')) x(MemberId, Scope, P, T, Q, QQ, Por);
INSERT INTO escalalote.Fase (Nombre) VALUES (N'deltas');
COMMIT;
SET @msg = CONCAT(N'   fase deltas hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END

IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'avisos')
BEGIN
BEGIN TRAN;
INSERT INTO MemberNotifications (MemberId, EventType, Title, Body, FirebaseMessageId, IsDelivered, DeliveredAt, IsRead, ReadAt,
                                 MemberProfileId, CreationDate, CreatedBy)
SELECT E, N'DOWNLINE_ENROLLED', N'New Enrollment in Your Team', N'A new ambassador has enrolled in your downline.',
       NULL, 0, NULL, 0, NULL, NULL, DATEADD(millisecond, -800, Alta), N'system'
FROM escalalote.Pares;

INSERT INTO escalalote.Fase (Nombre) VALUES (N'avisos');
COMMIT;
SET @msg = CONCAT(N'   fase avisos hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END

-- El arbol: los nodos y lo que sube a los ascendientes, en una transaccion. Las filas de ESC-M y ESC-0 se leen con su
-- candado de actualizacion para comprobar al final que suben exactamente lo del lote (aunque las colas trabajen a la vez).
IF NOT EXISTS (SELECT 1 FROM escalalote.Fase WHERE Nombre = N'arbol')
BEGIN
DECLARE @equipoM int, @equipo0 int, @puntos0 int;
BEGIN TRAN;
SELECT @equipoM = LeftLegCount + RightLegCount FROM DualTeamTree WITH (UPDLOCK, HOLDLOCK) WHERE MemberId = N'ESC-M' AND IsDeleted = 0;
SELECT TOP 1 @equipo0 = EnrollmentTeamSize, @puntos0 = EnrollmentPoints FROM MemberStatistics WITH (UPDLOCK, HOLDLOCK) WHERE MemberId = N'ESC-0' ORDER BY Id;
INSERT INTO GenealogyTree (Id, MemberId, ParentMemberId, HierarchyPath, Level, CreationDate, CreatedBy, LastUpdateDate, IsDeleted, Node)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, m.Patrocinador, CONCAT(m.RutaPadre, m.MemberId, N'/'), m.NivelPadre + 1,
       DATEADD(millisecond, 170, m.Alta), m.Email, DATEADD(millisecond, 170, m.Alta), 0,
       CAST(CONCAT(m.NodoPadre.ToString(), m.Ord, N'/') AS hierarchyid)
FROM escalalote.A m;
INSERT INTO DualTeamTree (Id, MemberId, ParentMemberId, Side, LeftLegPoints, RightLegPoints, CreationDate, CreatedBy,
                          LastUpdateDate, LastUpdateBy, IsDeleted, LeftLegCount, RightLegCount, BinaryPath, Depth,
                          SelfQualified, LeftLegQualified, RightLegQualified)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.MemberId, m.PadreBin, m.Lado, 0, 0, m.Colocada, N'placement-service',
       m.Colocada, N'placement-service', 0, 0, 0, m.Clave, m.ProfPadre + 1, 1, 0, 0
FROM escalalote.A m;
INSERT INTO PlacementLogs (MemberId, PlacedUnderMemberId, Side, Action, Reason, UnplacementCount, FirstPlacementDate, CreationDate, CreatedBy)
SELECT m.MemberId, m.PadreBin, m.Lado, N'Placed', N'Placement engine', 0, m.Colocada, m.Colocada, N'placement-service' FROM escalalote.A m;

-- Los ascendientes.
UPDATE s SET EnrollmentTeamSize = s.EnrollmentTeamSize + g.T, EnrollmentPoints = s.EnrollmentPoints + g.P,
             EnrollmentQualifiedTeamMembers = s.EnrollmentQualifiedTeamMembers + g.QQ,
             QualifiedSponsoredMembers = s.QualifiedSponsoredMembers + g.Q
FROM MemberStatistics s
JOIN escalalote.Gi g ON g.MemberId = s.MemberId
WHERE s.Id = (SELECT MIN(x.Id) FROM MemberStatistics x WHERE x.MemberId = s.MemberId);
UPDATE d SET LeftLegPoints = d.LeftLegPoints + b.IP, RightLegPoints = d.RightLegPoints + b.DP,
             LeftLegCount = d.LeftLegCount + b.IC, RightLegCount = d.RightLegCount + b.DC,
             LeftLegQualified = d.LeftLegQualified + b.IC, RightLegQualified = d.RightLegQualified + b.DC
FROM DualTeamTree d JOIN escalalote.Gb b ON b.MemberId = d.MemberId
WHERE d.IsDeleted = 0;
UPDATE s SET DualTeamPoints = CAST(d.LeftLegPoints + d.RightLegPoints AS int), DualTeamSize = d.LeftLegCount + d.RightLegCount
FROM MemberStatistics s
JOIN escalalote.Gb b ON b.MemberId = s.MemberId
JOIN DualTeamTree d ON d.MemberId = b.MemberId AND d.IsDeleted = 0
WHERE s.Id = (SELECT MIN(x.Id) FROM MemberStatistics x WHERE x.MemberId = s.MemberId);
IF (SELECT LeftLegCount + RightLegCount FROM DualTeamTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0) <> @equipoM + @altas
   OR (SELECT TOP 1 EnrollmentTeamSize FROM MemberStatistics WHERE MemberId = N'ESC-0' ORDER BY Id) <> @equipo0 + @altas
   OR (SELECT TOP 1 EnrollmentPoints FROM MemberStatistics WHERE MemberId = N'ESC-0' ORDER BY Id) <> @puntos0 + (SELECT SUM(Puntos) FROM escalalote.A)
   OR EXISTS (SELECT 1 FROM DualTeamTree c JOIN escalalote.A m ON m.MemberId = c.MemberId
              JOIN DualTeamTree p ON p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
              CROSS APPLY dbo.DualChildKey(p.BinaryPath, p.Depth, c.Side) k
              WHERE c.BinaryPath <> k.ChildKey OR c.Depth <> p.Depth + 1)
    THROW 51149, N'El lote no cuadra con ESC-M, ESC-0 o las claves: se deshace la fase del arbol.', 1;
INSERT INTO escalalote.Fase (Nombre) VALUES (N'arbol');
COMMIT;
SET @msg = CONCAT(N'   fase arbol hecha a los ', DATEDIFF(second, @inicio, SYSDATETIME()), N' s'); RAISERROR(@msg, 0, 1) WITH NOWAIT;
END
GO
-- Lote terminado: informe y fuera las tablas de trabajo.
SET NOCOUNT ON;
SELECT p.Altas, CONCAT(N'ESC-', p.N0) AS Primera, CONCAT(N'ESC-', p.N0 + p.Altas - 1) AS Ultima,
       (SELECT LeftLegCount + RightLegCount FROM DualTeamTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0) AS EquipoBinarioEscM,
       (SELECT MAX(d.Depth) FROM DualTeamTree d JOIN escalalote.A m ON m.MemberId = d.MemberId) AS ProfundidadMaximaDelLote,
       (SELECT COUNT(*) FROM escalalote.Fase) AS Fases
FROM escalalote.Par p;
IF (SELECT COUNT(*) FROM escalalote.Fase) <> 8
    THROW 51150, N'El lote no termino todas sus fases: volver a lanzar para reanudarlo.', 1;
DROP TABLE escalalote.A, escalalote.Pares, escalalote.Gi, escalalote.Gb, escalalote.Par, escalalote.Fase;
DROP SCHEMA escalalote;
