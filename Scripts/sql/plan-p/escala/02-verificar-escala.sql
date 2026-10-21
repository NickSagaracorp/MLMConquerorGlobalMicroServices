/*
Plan P, Tarea 18 (Bloque D); Plan Q, Tarea 32. Verificacion de solo lectura de las altas generadas en
MLMConquerorGlobalEditionDb_Escala, antes de medir. No escribe nada (solo tablas temporales). Falla (THROW) si algo no cuadra.

  1. Binario: 0 huecos repetidos, 0 huerfanos, clave (dbo.DualChildKey) y Depth coherentes con el padre (sin ruta de texto:
     el Plan Q la retiro); piernas (puntos, contadores y calificados) recalculadas de abajo arriba desde MemberStatistics y
     las suscripciones activas e iguales a las guardadas; SelfQualified = activas del propio; DualTeamPoints = piernas y
     DualTeamSize = contadores; en ESC-M, LeftLegQualified + RightLegQualified = el recuento al vuelo de las activas de su
     rango por la clave (DQ13). Cola de piernas: 0 filas de pierna (OwnChange = 0), tantos propios como miembros con activas
     sin nodo binario vivo, 0 apartados.
  2. Inscripcion: 0 huerfanos; Node hijo directo del Node del padre; HierarchyPath y Level coherentes; 0 miembros con dos nodos
     vivos (DQ9); el patrocinador de cada alta existe, es el padre en GenealogyTree y se dio de alta antes; en ESC-M,
     EnrollmentTeamSize, EnrollmentPoints y EnrollmentQualifiedTeamMembers iguales al recuento al vuelo de su Node (DQ13, DQ16).
  3. Cada alta ESC tiene sus filas: usuario y rol, pedido con 2 lineas, suscripcion activa con su aporte (DQ16), facturacion,
     pago Entrax, historial de pago, cuenta atras, estadisticas (PersonalPoints = aportes de sus activas), nivel de alta
     guardado (DQ10), comisiones; cola de rango y avisos hasta 10 ascendientes (DQ5), tres deltas de estadisticas por alta
     (Bloque 4, 19b, DQ16), todo aplicado; historial de rangos sin rangos repetidos y con PreviousRankId encadenado.
  4. Indistinguible de un alta real: por cada tabla, las columnas que una alta real de Entrax de la base local
     (AMB-934756, leida en solo lectura) tiene con valor y una alta ESC tiene a NULL, o al reves.

El 10 de la cola de rango y los avisos es GenealogyUpline.EnrollmentLevels (no se puede leer desde SQL): si Q1 lo cambia, se
cambia aqui (@niveles) y en altas\e-inscripcion.sql.

  sqlcmd -S . -d MLMConquerorGlobalEditionDb_Escala -E -C -b -I -v Confirmo=ESCALA -i Scripts/sql/plan-p/escala/02-verificar-escala.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
IF DB_NAME() <> N'MLMConquerorGlobalEditionDb_Escala'
    THROW 51070, N'Este script es solo para MLMConquerorGlobalEditionDb_Escala.', 1;
IF N'$(Confirmo)' <> N'ESCALA'
    THROW 51071, N'Ejecutar con -v Confirmo=ESCALA.', 1;
GO
SET NOCOUNT ON;
DECLARE @inicio datetime2 = SYSDATETIME(), @niveles int = 10;
IF OBJECT_ID('tempdb..#r') IS NOT NULL DROP TABLE #r;
CREATE TABLE #r (Comprobacion nvarchar(200) NOT NULL, Valor bigint NOT NULL, Esperado bigint NOT NULL);

-- Activas por miembro (DQ13: suscripciones, no miembros, sin mirar IsDeleted) y la suma de sus aportes (DQ16).
IF OBJECT_ID('tempdb..#act') IS NOT NULL DROP TABLE #act;
SELECT CAST(MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId, COUNT(*) AS Activas,
       SUM(CAST(EnrollmentContribution AS bigint)) AS Aporte
INTO #act FROM MembershipSubscriptions WHERE SubscriptionStatus = 1 GROUP BY MemberId;
CREATE UNIQUE CLUSTERED INDEX cx ON #act (MemberId);

-- 1. Binario.
INSERT INTO #r SELECT N'binario: huecos repetidos', COUNT(*), 0 FROM (SELECT ParentMemberId, Side FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1) x;
INSERT INTO #r SELECT N'binario: huerfanos', COUNT(*), 0 FROM DualTeamTree c WHERE c.IsDeleted = 0 AND c.ParentMemberId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0);
INSERT INTO #r SELECT N'binario: nodos sin clave', COUNT(*), 0 FROM DualTeamTree WHERE IsDeleted = 0 AND (BinaryPath IS NULL OR Depth IS NULL);
INSERT INTO #r SELECT N'binario: clave o profundidad incoherente con el padre', COUNT(*), 0
FROM DualTeamTree c JOIN DualTeamTree p ON p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
CROSS APPLY dbo.DualChildKey(p.BinaryPath, p.Depth, c.Side) k
WHERE c.IsDeleted = 0 AND (c.BinaryPath <> k.ChildKey OR c.Depth <> p.Depth + 1);
INSERT INTO #r SELECT N'binario: raices (ESC-0 y ESC-P)', COUNT(*), 2 FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NULL;

IF OBJECT_ID('tempdb..#v') IS NOT NULL DROP TABLE #v;
SELECT d.MemberId, d.ParentMemberId, d.Side, d.Depth, d.LeftLegPoints, d.RightLegPoints, d.LeftLegCount, d.RightLegCount,
       d.SelfQualified, d.LeftLegQualified, d.RightLegQualified,
       CAST(ISNULL(s.PersonalPoints, 0) AS decimal(18,4)) AS Propios, s.DualTeamPoints, s.DualTeamSize, ISNULL(a.Activas, 0) AS Yo,
       CAST(0 AS decimal(18,4)) AS IzqP, CAST(0 AS decimal(18,4)) AS DerP, 0 AS IzqC, 0 AS DerC, 0 AS IzqQ, 0 AS DerQ
INTO #v
FROM DualTeamTree d
OUTER APPLY (SELECT TOP 1 ms.PersonalPoints, ms.DualTeamPoints, ms.DualTeamSize FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id) s
LEFT JOIN #act a ON a.MemberId = d.MemberId
WHERE d.IsDeleted = 0;
CREATE UNIQUE CLUSTERED INDEX cx ON #v (Depth, MemberId);
CREATE INDEX ix_p ON #v (ParentMemberId, Side) INCLUDE (Propios, IzqP, DerP, IzqC, DerC, Yo, IzqQ, DerQ);
DECLARE @prof int = (SELECT MAX(Depth) FROM #v);
WHILE @prof >= 0
BEGIN
    UPDATE p SET IzqP = ISNULL(l.P, 0), DerP = ISNULL(r.P, 0), IzqC = ISNULL(l.C, 0), DerC = ISNULL(r.C, 0),
                 IzqQ = ISNULL(l.Q, 0), DerQ = ISNULL(r.Q, 0)
    FROM #v p
    OUTER APPLY (SELECT c.Propios + c.IzqP + c.DerP AS P, 1 + c.IzqC + c.DerC AS C, c.Yo + c.IzqQ + c.DerQ AS Q FROM #v c WHERE c.ParentMemberId = p.MemberId AND c.Side = 0) l
    OUTER APPLY (SELECT c.Propios + c.IzqP + c.DerP AS P, 1 + c.IzqC + c.DerC AS C, c.Yo + c.IzqQ + c.DerQ AS Q FROM #v c WHERE c.ParentMemberId = p.MemberId AND c.Side = 1) r
    WHERE p.Depth = @prof;
    SET @prof -= 1;
END
INSERT INTO #r SELECT N'binario: piernas (puntos o contadores) distintas del recalculo', COUNT(*), 0
FROM #v WHERE LeftLegPoints <> IzqP OR RightLegPoints <> DerP OR LeftLegCount <> IzqC OR RightLegCount <> DerC;
INSERT INTO #r SELECT N'binario: calificados (propios o de pierna) distintos del recalculo', COUNT(*), 0
FROM #v WHERE SelfQualified <> Yo OR LeftLegQualified <> IzqQ OR RightLegQualified <> DerQ;
INSERT INTO #r SELECT N'binario: DualTeamPoints distinto de las piernas', COUNT(*), 0
FROM #v WHERE DualTeamPoints IS NOT NULL AND DualTeamPoints <> CAST(IzqP + DerP AS int);
INSERT INTO #r SELECT N'binario: DualTeamSize distinto de los contadores', COUNT(*), 0
FROM #v WHERE DualTeamSize IS NOT NULL AND DualTeamSize <> IzqC + DerC;
INSERT INTO #r SELECT N'binario: equipo de ESC-M (contadores)', LeftLegCount + RightLegCount, IzqC + DerC FROM #v WHERE MemberId = N'ESC-M';
DROP TABLE #v;

-- ESC-M: calificados del binario frente al recuento al vuelo de las activas de su rango por la clave (las cotas como
-- Scripts/sql/dual-subtree-bounds.sql y DualBinaryPath.SubtreeBounds).
DECLARE @k varbinary(1700) = (SELECT BinaryPath FROM DualTeamTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0);
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
INSERT INTO #r SELECT N'binario: calificados de ESC-M (al vuelo, por la clave)', d.LeftLegQualified + d.RightLegQualified,
       (SELECT COUNT(*) FROM DualTeamTree t WITH (INDEX(UX_DualTeamTree_BinaryPath))
        JOIN MembershipSubscriptions s ON s.MemberId = t.MemberId AND s.SubscriptionStatus = 1
        WHERE t.BinaryPath > @lo AND t.BinaryPath < @hi AND t.IsDeleted = 0 AND t.MemberId <> N'ESC-M')
FROM DualTeamTree d WHERE d.MemberId = N'ESC-M' AND d.IsDeleted = 0;

INSERT INTO #r SELECT N'cola de piernas: filas de pierna (OwnChange = 0)', COUNT(*), 0 FROM DualLegDeltas WHERE OwnChange = 0;
INSERT INTO #r SELECT N'cola de piernas: propios (miembros con activas sin nodo binario)', COUNT(DISTINCT StartMemberId),
       (SELECT COUNT(*) FROM #act a WHERE NOT EXISTS (SELECT 1 FROM DualTeamTree d WHERE d.MemberId = a.MemberId AND d.IsDeleted = 0))
FROM DualLegDeltas WHERE OwnChange = 1;
INSERT INTO #r SELECT N'cola de piernas: apartados', COUNT(*), 0 FROM DualLegDeltaDeadLetters;

-- 2. Inscripcion.
INSERT INTO #r SELECT N'inscripcion: huerfanos', COUNT(*), 0 FROM GenealogyTree c WHERE c.ParentMemberId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM GenealogyTree p WHERE p.MemberId = c.ParentMemberId);
INSERT INTO #r SELECT N'inscripcion: Node, ruta o nivel incoherente con el padre', COUNT(*), 0
FROM GenealogyTree c JOIN GenealogyTree p ON p.MemberId = c.ParentMemberId
WHERE c.Node.GetAncestor(1) <> p.Node OR c.HierarchyPath <> CONCAT(p.HierarchyPath, c.MemberId, N'/') OR c.Level <> p.Level + 1;
INSERT INTO #r SELECT N'inscripcion: Node repetido', COUNT(*), 0 FROM (SELECT Node FROM GenealogyTree GROUP BY Node HAVING COUNT(*) > 1) x;
INSERT INTO #r SELECT N'inscripcion: miembros con dos nodos vivos', COUNT(*), 0 FROM (SELECT MemberId FROM GenealogyTree WHERE IsDeleted = 0 GROUP BY MemberId HAVING COUNT(*) > 1) x;
INSERT INTO #r SELECT N'altas: patrocinador inexistente, distinto del padre o posterior', COUNT(*), 0
FROM MemberProfiles m
LEFT JOIN MemberProfiles s ON s.MemberId = m.SponsorMemberId
LEFT JOIN GenealogyTree g ON g.MemberId = m.MemberId
WHERE m.MemberId LIKE N'ESC-%' AND m.SponsorMemberId IS NOT NULL
  AND (s.MemberId IS NULL OR s.EnrollDate >= m.EnrollDate OR g.ParentMemberId IS NULL OR g.ParentMemberId <> m.SponsorMemberId);
DECLARE @nodoM hierarchyid = (SELECT Node FROM GenealogyTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0);
IF OBJECT_ID('tempdb..#subM') IS NOT NULL DROP TABLE #subM;
SELECT CAST(g.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId, ISNULL(a.Activas, 0) AS Activas, ISNULL(a.Aporte, 0) AS Aporte
INTO #subM FROM GenealogyTree g LEFT JOIN #act a ON a.MemberId = g.MemberId
WHERE g.IsDeleted = 0 AND g.Node.IsDescendantOf(@nodoM) = 1;
INSERT INTO #r SELECT N'MemberStatistics: equipo de inscripcion de ESC-M', EnrollmentTeamSize, (SELECT COUNT(*) - 1 FROM #subM)
FROM MemberStatistics WHERE MemberId = N'ESC-M';
INSERT INTO #r SELECT N'MemberStatistics: EnrollmentPoints de ESC-M (aportes del subarbol, el propio incluido)', EnrollmentPoints, (SELECT SUM(Aporte) FROM #subM)
FROM MemberStatistics WHERE MemberId = N'ESC-M';
INSERT INTO #r SELECT N'MemberStatistics: EnrollmentQualifiedTeamMembers de ESC-M (al vuelo)', EnrollmentQualifiedTeamMembers,
       (SELECT SUM(Activas) FROM #subM WHERE MemberId <> N'ESC-M')
FROM MemberStatistics WHERE MemberId = N'ESC-M';
DROP TABLE #subM;

-- 3. Filas por alta.
DECLARE @altas bigint = (SELECT COUNT(*) FROM MemberProfiles WHERE MemberId LIKE N'ESC-%' AND SponsorMemberId IS NOT NULL);
DECLARE @asc bigint = (SELECT SUM(CAST(CASE WHEN g.Level > @niveles THEN @niveles ELSE g.Level END AS bigint))
                       FROM GenealogyTree g JOIN MemberProfiles m ON m.MemberId = g.MemberId AND m.SponsorMemberId IS NOT NULL
                       WHERE g.MemberId LIKE N'ESC-%' AND g.IsDeleted = 0);
INSERT INTO #r SELECT N'altas con patrocinador', @altas, @altas;
INSERT INTO #r SELECT N'AspNetUsers activos y confirmados', COUNT(*), @altas FROM AspNetUsers WHERE MemberProfileId LIKE N'ESC-%' AND IsActive = 1 AND EmailConfirmed = 1;
INSERT INTO #r SELECT N'AspNetUserRoles Ambassador', COUNT(*), @altas FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id = ur.UserId JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name = N'Ambassador' WHERE u.MemberProfileId LIKE N'ESC-%';
INSERT INTO #r SELECT N'Orders completados', COUNT(*), @altas FROM Orders WHERE MemberId LIKE N'ESC-%' AND Status = 4;
INSERT INTO #r SELECT N'OrderDetails', COUNT(*), 2 * @altas FROM OrderDetails od JOIN Orders o ON o.Id = od.OrderId WHERE o.MemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'MembershipSubscriptions activas', COUNT(*), @altas FROM MembershipSubscriptions WHERE MemberId LIKE N'ESC-%' AND SubscriptionStatus = 1;
INSERT INTO #r SELECT N'suscripciones: aporte distinto de los puntos de su pedido (activas) o de 0 (las demas)', COUNT(*), 0
FROM MembershipSubscriptions s
OUTER APPLY (SELECT SUM(p.QualificationPoins) AS Puntos FROM OrderDetails od JOIN Products p ON p.Id = od.ProductId WHERE od.OrderId = s.LastOrderId) o
WHERE s.DualTeamContribution <> s.EnrollmentContribution
   OR (s.SubscriptionStatus = 1 AND s.EnrollmentContribution <> ISNULL(o.Puntos, 0))
   OR (s.SubscriptionStatus <> 1 AND s.EnrollmentContribution <> 0);
INSERT INTO #r SELECT N'MemberStatistics: PersonalPoints distinto de los aportes de sus activas', COUNT(*), 0
FROM MemberStatistics ms LEFT JOIN #act a ON a.MemberId = ms.MemberId
WHERE ms.PersonalPoints <> ISNULL(a.Aporte, 0);
INSERT INTO #r SELECT N'perfiles con alta y sin SignupMembershipLevelId', COUNT(*), 0 FROM MemberProfiles WHERE SponsorMemberId IS NOT NULL AND SignupMembershipLevelId IS NULL;
INSERT INTO #r SELECT N'SubscriptionBillingStates', COUNT(*), @altas FROM SubscriptionBillingStates WHERE MemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'EntraxPayments pagados', COUNT(*), @altas FROM EntraxPayments WHERE MemberId LIKE N'ESC-%' AND Status = 2 AND Consumed = 1;
INSERT INTO #r SELECT N'PaymentHistories Entrax', COUNT(*), @altas FROM PaymentHistories WHERE MemberId LIKE N'ESC-%' AND GatewayName = N'Entrax';
INSERT INTO #r SELECT N'CommissionCountDowns', COUNT(*), @altas FROM CommissionCountDowns c JOIN MemberProfiles m ON m.Id = c.MemberId1 AND m.UserId = c.MemberId WHERE m.MemberId LIKE N'ESC-%' AND m.SponsorMemberId IS NOT NULL;
INSERT INTO #r SELECT N'MemberStatistics (todas las ESC)', COUNT(*), (SELECT COUNT(*) FROM MemberProfiles WHERE MemberId LIKE N'ESC-%') FROM MemberStatistics WHERE MemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'altas sin comision al patrocinador', COUNT(*), 0 FROM Orders o WHERE o.MemberId LIKE N'ESC-%' AND NOT EXISTS (SELECT 1 FROM CommissionEarnings c WHERE c.SourceOrderId = o.Id);
INSERT INTO #r SELECT N'RankEvaluationQueue (hasta 10 ascendientes y el propio por alta)', COUNT_BIG(*), @asc + @altas FROM RankEvaluationQueue WHERE TriggerMemberId LIKE N'ESC-%' AND TriggerEvent = 1;
INSERT INTO #r SELECT N'RankEvaluationQueue pendientes', COUNT_BIG(*), 0 FROM RankEvaluationQueue WHERE IsProcessed = 0;
INSERT INTO #r SELECT N'MemberStatisticDeltas (3 por alta)', COUNT_BIG(*), 3 * @altas FROM MemberStatisticDeltas WHERE SourceMemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'MemberStatisticDeltas pendientes', COUNT_BIG(*), 0 FROM MemberStatisticDeltas WHERE IsApplied = 0;
INSERT INTO #r SELECT N'MemberNotifications (hasta 10 ascendientes por alta)', COUNT_BIG(*), @asc FROM MemberNotifications WHERE EventType = N'DOWNLINE_ENROLLED';
INSERT INTO #r SELECT N'PlacementLogs (nodos con padre)', COUNT(*), (SELECT COUNT(*) FROM DualTeamTree WHERE ParentMemberId IS NOT NULL) FROM PlacementLogs;
INSERT INTO #r SELECT N'MemberRankHistories: rango repetido en un miembro', COUNT(*), 0
FROM (SELECT MemberId, RankDefinitionId FROM MemberRankHistories GROUP BY MemberId, RankDefinitionId HAVING COUNT(*) > 1) x;
INSERT INTO #r SELECT N'MemberRankHistories: PreviousRankId no es el rango anterior del miembro', COUNT(*), 0
FROM (SELECT h.PreviousRankId, LAG(h.RankDefinitionId) OVER (PARTITION BY h.MemberId ORDER BY r.SortOrder) AS Anterior
      FROM MemberRankHistories h JOIN RankDefinitions r ON r.Id = h.RankDefinitionId) x
WHERE ISNULL(x.PreviousRankId, -1) <> ISNULL(x.Anterior, -1);
INSERT INTO #r SELECT N'MemberRankHistories: ESC-M tiene rango', CASE WHEN EXISTS (SELECT 1 FROM MemberRankHistories WHERE MemberId = N'ESC-M') THEN 1 ELSE 0 END, 1;

SELECT Comprobacion, Valor, Esperado, CASE WHEN Valor = Esperado THEN N'ok' ELSE N'FALLA' END AS Resultado FROM #r;

-- 4. Columnas con valor en un alta real y NULL en una ESC (o al reves). Lee la base local en solo lectura.
IF DB_ID(N'MLMConquerorGlobalEditionDb') IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'', @t sysname, @filtro nvarchar(400);
    DECLARE @tablas TABLE (Tabla sysname, Filtro nvarchar(400));
    INSERT INTO @tablas VALUES
      (N'MemberProfiles', N'MemberId = @m'), (N'GenealogyTree', N'MemberId = @m'), (N'Orders', N'MemberId = @m'),
      (N'MembershipSubscriptions', N'MemberId = @m'), (N'SubscriptionBillingStates', N'MemberId = @m'),
      (N'EntraxPayments', N'MemberId = @m'), (N'PaymentHistories', N'MemberId = @m'), (N'MemberStatistics', N'MemberId = @m'),
      (N'CommissionEarnings', N'SourceMemberId = @m'), (N'RankEvaluationQueue', N'TriggerMemberId = @m'),
      (N'MemberStatisticDeltas', N'SourceMemberId = @m'), (N'AspNetUsers', N'MemberProfileId = @m'),
      (N'OrderDetails', N'OrderId IN (SELECT Id FROM {db}.dbo.Orders WHERE MemberId = @m)'),
      (N'CommissionCountDowns', N'MemberId1 IN (SELECT Id FROM {db}.dbo.MemberProfiles WHERE MemberId = @m)');
    IF OBJECT_ID('tempdb..#nulos') IS NOT NULL DROP TABLE #nulos;
    CREATE TABLE #nulos (Tabla sysname, Columna sysname, RealConValor int, EscConValor int);
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Tabla, Filtro FROM @tablas;
    OPEN c; FETCH NEXT FROM c INTO @t, @filtro;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'';
        SELECT @sql += N'INSERT INTO #nulos SELECT ''' + @t + N''', ''' + col.name + N''', '
            + N'(SELECT COUNT(*) FROM MLMConquerorGlobalEditionDb.dbo.' + QUOTENAME(@t) + N' WHERE ' + REPLACE(@filtro, N'{db}', N'MLMConquerorGlobalEditionDb') + N' AND ' + QUOTENAME(col.name) + N' IS NOT NULL), '
            + N'(SELECT COUNT(*) FROM dbo.' + QUOTENAME(@t) + N' WHERE ' + REPLACE(REPLACE(@filtro, N'{db}', N'MLMConquerorGlobalEditionDb_Escala'), N'@m', N'@e') + N' AND ' + QUOTENAME(col.name) + N' IS NOT NULL);'
        FROM sys.columns col JOIN sys.types ty ON ty.user_type_id = col.user_type_id
        WHERE col.object_id = OBJECT_ID(N'dbo.' + @t) AND col.is_nullable = 1 AND ty.name NOT IN (N'timestamp');
        EXEC sp_executesql @sql, N'@m nvarchar(50), @e nvarchar(50)', @m = N'AMB-934756', @e = N'ESC-1003';
        FETCH NEXT FROM c INTO @t, @filtro;
    END
    CLOSE c; DEALLOCATE c;
    SELECT Tabla, Columna, RealConValor, EscConValor FROM #nulos
    WHERE (RealConValor > 0 AND EscConValor = 0) OR (RealConValor = 0 AND EscConValor > 0)
    ORDER BY Tabla, Columna;
END

SELECT DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
IF EXISTS (SELECT 1 FROM #r WHERE Valor <> Esperado)
    THROW 51072, N'La verificacion de la base de escala no cuadra (ver la tabla de comprobaciones).', 1;
