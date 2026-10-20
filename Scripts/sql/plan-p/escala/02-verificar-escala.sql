/*
Plan P, Tarea 18 (Bloque D). Verificacion de solo lectura de las altas generadas en MLMConquerorGlobalEditionDb_Escala,
antes de medir. No escribe nada (solo tablas temporales). Falla (THROW) si algo no cuadra.

  1. Binario: 0 huecos repetidos, 0 huerfanos, ruta de texto, clave (dbo.DualChildKey) y Depth coherentes con el padre;
     piernas (puntos y contadores) recalculadas de abajo arriba desde MemberStatistics e iguales a las guardadas;
     DualTeamPoints = piernas.
  2. Inscripcion: 0 huerfanos; Node hijo directo del Node del padre; HierarchyPath y Level coherentes; el patrocinador
     de cada alta existe, es el padre en GenealogyTree y se dio de alta antes.
  3. Cada alta ESC tiene sus filas: usuario y rol, pedido con 2 lineas, suscripcion, facturacion, pago Entrax,
     historial de pago, cuenta atras, estadisticas, comisiones y una fila por ascendiente en las tres tablas.
  4. Indistinguible de un alta real: por cada tabla, las columnas que una alta real de Entrax de la base local
     (AMB-934756, leida en solo lectura) tiene con valor y una alta ESC tiene a NULL, o al reves.

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
-- Revision del bloque 3 del Plan Q: escribe (o lee) DualTeamTree.HierarchyPath, que el Plan Q retira; hasta que la
-- Tarea 32 lo pase a la clave compacta, no se ejecuta contra una base sin la columna.
IF COL_LENGTH('dbo.DualTeamTree', 'HierarchyPath') IS NULL
    THROW 51107, N'Pendiente de la Tarea 32 del Plan Q: DualTeamTree ya no tiene HierarchyPath y este script aun la usa.', 1;
GO
SET NOCOUNT ON;
DECLARE @inicio datetime2 = SYSDATETIME();
IF OBJECT_ID('tempdb..#r') IS NOT NULL DROP TABLE #r;
CREATE TABLE #r (Comprobacion nvarchar(200) NOT NULL, Valor bigint NOT NULL, Esperado bigint NOT NULL);

-- 1. Binario.
DECLARE @completa bit = CASE WHEN (SELECT COUNT(*) FROM DualTeamTree) <= 500000 THEN 1 ELSE 0 END;
INSERT INTO #r SELECT N'binario: huecos repetidos', COUNT(*), 0 FROM (SELECT ParentMemberId, Side FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1) x;
INSERT INTO #r SELECT N'binario: huerfanos', COUNT(*), 0 FROM DualTeamTree c WHERE c.IsDeleted = 0 AND c.ParentMemberId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0);
INSERT INTO #r SELECT N'binario: nodos sin clave', COUNT(*), 0 FROM DualTeamTree WHERE IsDeleted = 0 AND (BinaryPath IS NULL OR Depth IS NULL);
INSERT INTO #r SELECT N'binario: ruta, clave o profundidad incoherente con el padre', COUNT(*), 0
FROM DualTeamTree c JOIN DualTeamTree p ON p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
CROSS APPLY dbo.DualChildKey(p.BinaryPath, p.Depth, c.Side) k
WHERE c.IsDeleted = 0
  AND (c.BinaryPath <> k.ChildKey OR c.Depth <> p.Depth + 1
       -- La ruta de texto: completa hasta 500.000 nodos; por encima (decenas de GB de rutas), longitud y ultimo segmento.
       OR (@completa = 1 AND c.HierarchyPath <> CONCAT(p.HierarchyPath, c.MemberId, N'/'))
       OR (@completa = 0 AND (DATALENGTH(c.HierarchyPath) <> DATALENGTH(p.HierarchyPath) + 2 * (LEN(c.MemberId) + 1)
                              OR RIGHT(c.HierarchyPath, LEN(c.MemberId) + 2) <> CONCAT(N'/', c.MemberId, N'/'))));
INSERT INTO #r SELECT N'binario: raices (ESC-0 y ESC-P)', COUNT(*), 2 FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NULL;

IF OBJECT_ID('tempdb..#v') IS NOT NULL DROP TABLE #v;
SELECT d.MemberId, d.ParentMemberId, d.Side, d.Depth, d.LeftLegPoints, d.RightLegPoints, d.LeftLegCount, d.RightLegCount,
       CAST(ISNULL(s.PersonalPoints, 0) AS decimal(18,4)) AS Propios, s.DualTeamPoints,
       CAST(0 AS decimal(18,4)) AS IzqP, CAST(0 AS decimal(18,4)) AS DerP, 0 AS IzqC, 0 AS DerC
INTO #v
FROM DualTeamTree d
OUTER APPLY (SELECT TOP 1 ms.PersonalPoints, ms.DualTeamPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id) s
WHERE d.IsDeleted = 0;
CREATE UNIQUE CLUSTERED INDEX cx ON #v (Depth, MemberId);
CREATE INDEX ix_p ON #v (ParentMemberId, Side) INCLUDE (Propios, IzqP, DerP, IzqC, DerC);
DECLARE @prof int = (SELECT MAX(Depth) FROM #v);
WHILE @prof >= 0
BEGIN
    UPDATE p SET IzqP = ISNULL(l.P, 0), DerP = ISNULL(r.P, 0), IzqC = ISNULL(l.C, 0), DerC = ISNULL(r.C, 0)
    FROM #v p
    OUTER APPLY (SELECT c.Propios + c.IzqP + c.DerP AS P, 1 + c.IzqC + c.DerC AS C FROM #v c WHERE c.ParentMemberId = p.MemberId AND c.Side = 0) l
    OUTER APPLY (SELECT c.Propios + c.IzqP + c.DerP AS P, 1 + c.IzqC + c.DerC AS C FROM #v c WHERE c.ParentMemberId = p.MemberId AND c.Side = 1) r
    WHERE p.Depth = @prof;
    SET @prof -= 1;
END
INSERT INTO #r SELECT N'binario: piernas (puntos o contadores) distintas del recalculo', COUNT(*), 0
FROM #v WHERE LeftLegPoints <> IzqP OR RightLegPoints <> DerP OR LeftLegCount <> IzqC OR RightLegCount <> DerC;
INSERT INTO #r SELECT N'binario: DualTeamPoints distinto de las piernas', COUNT(*), 0
FROM #v WHERE DualTeamPoints IS NOT NULL AND DualTeamPoints <> CAST(IzqP + DerP AS int);
INSERT INTO #r SELECT N'binario: equipo de ESC-M (contadores)', LeftLegCount + RightLegCount, IzqC + DerC FROM #v WHERE MemberId = N'ESC-M';
DROP TABLE #v;

-- 2. Inscripcion.
INSERT INTO #r SELECT N'inscripcion: huerfanos', COUNT(*), 0 FROM GenealogyTree c WHERE c.ParentMemberId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM GenealogyTree p WHERE p.MemberId = c.ParentMemberId);
INSERT INTO #r SELECT N'inscripcion: Node, ruta o nivel incoherente con el padre', COUNT(*), 0
FROM GenealogyTree c JOIN GenealogyTree p ON p.MemberId = c.ParentMemberId
WHERE c.Node.GetAncestor(1) <> p.Node OR c.HierarchyPath <> CONCAT(p.HierarchyPath, c.MemberId, N'/') OR c.Level <> p.Level + 1;
INSERT INTO #r SELECT N'inscripcion: Node repetido', COUNT(*), 0 FROM (SELECT Node FROM GenealogyTree GROUP BY Node HAVING COUNT(*) > 1) x;
INSERT INTO #r SELECT N'altas: patrocinador inexistente, distinto del padre o posterior', COUNT(*), 0
FROM MemberProfiles m
LEFT JOIN MemberProfiles s ON s.MemberId = m.SponsorMemberId
LEFT JOIN GenealogyTree g ON g.MemberId = m.MemberId
WHERE m.MemberId LIKE N'ESC-%' AND m.SponsorMemberId IS NOT NULL
  AND (s.MemberId IS NULL OR s.EnrollDate >= m.EnrollDate OR g.ParentMemberId IS NULL OR g.ParentMemberId <> m.SponsorMemberId);

-- 3. Filas por alta.
DECLARE @altas bigint = (SELECT COUNT(*) FROM MemberProfiles WHERE MemberId LIKE N'ESC-%' AND SponsorMemberId IS NOT NULL);
DECLARE @asc bigint = (SELECT SUM(CAST(g.Level AS bigint)) FROM GenealogyTree g JOIN MemberProfiles m ON m.MemberId = g.MemberId AND m.SponsorMemberId IS NOT NULL WHERE g.MemberId LIKE N'ESC-%');
INSERT INTO #r SELECT N'altas con patrocinador', @altas, @altas;
INSERT INTO #r SELECT N'AspNetUsers activos y confirmados', COUNT(*), @altas FROM AspNetUsers WHERE MemberProfileId LIKE N'ESC-%' AND IsActive = 1 AND EmailConfirmed = 1;
INSERT INTO #r SELECT N'AspNetUserRoles Ambassador', COUNT(*), @altas FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id = ur.UserId JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name = N'Ambassador' WHERE u.MemberProfileId LIKE N'ESC-%';
INSERT INTO #r SELECT N'Orders completados', COUNT(*), @altas FROM Orders WHERE MemberId LIKE N'ESC-%' AND Status = 4;
INSERT INTO #r SELECT N'OrderDetails', COUNT(*), 2 * @altas FROM OrderDetails od JOIN Orders o ON o.Id = od.OrderId WHERE o.MemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'MembershipSubscriptions activas', COUNT(*), @altas FROM MembershipSubscriptions WHERE MemberId LIKE N'ESC-%' AND SubscriptionStatus = 1;
INSERT INTO #r SELECT N'SubscriptionBillingStates', COUNT(*), @altas FROM SubscriptionBillingStates WHERE MemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'EntraxPayments pagados', COUNT(*), @altas FROM EntraxPayments WHERE MemberId LIKE N'ESC-%' AND Status = 2 AND Consumed = 1;
INSERT INTO #r SELECT N'PaymentHistories Entrax', COUNT(*), @altas FROM PaymentHistories WHERE MemberId LIKE N'ESC-%' AND GatewayName = N'Entrax';
INSERT INTO #r SELECT N'CommissionCountDowns', COUNT(*), @altas FROM CommissionCountDowns c JOIN MemberProfiles m ON m.Id = c.MemberId1 AND m.UserId = c.MemberId WHERE m.MemberId LIKE N'ESC-%' AND m.SponsorMemberId IS NOT NULL;
INSERT INTO #r SELECT N'MemberStatistics (todas las ESC)', COUNT(*), (SELECT COUNT(*) FROM MemberProfiles WHERE MemberId LIKE N'ESC-%') FROM MemberStatistics WHERE MemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'altas sin comision al patrocinador', COUNT(*), 0 FROM Orders o WHERE o.MemberId LIKE N'ESC-%' AND NOT EXISTS (SELECT 1 FROM CommissionEarnings c WHERE c.SourceOrderId = o.Id);
INSERT INTO #r SELECT N'RankEvaluationQueue (1 por ascendiente)', COUNT_BIG(*), @asc FROM RankEvaluationQueue WHERE TriggerMemberId LIKE N'ESC-%' AND TriggerEvent = 1;
INSERT INTO #r SELECT N'MemberStatisticDeltas (1 por ascendiente)', COUNT_BIG(*), @asc FROM MemberStatisticDeltas WHERE SourceMemberId LIKE N'ESC-%';
INSERT INTO #r SELECT N'MemberNotifications (1 por ascendiente)', COUNT_BIG(*), @asc FROM MemberNotifications WHERE EventType = N'DOWNLINE_ENROLLED';
INSERT INTO #r SELECT N'PlacementLogs (nodos con padre)', COUNT(*), (SELECT COUNT(*) FROM DualTeamTree WHERE ParentMemberId IS NOT NULL) FROM PlacementLogs;
INSERT INTO #r SELECT N'MemberStatistics: equipo de inscripcion de ESC-M', EnrollmentTeamSize,
       (SELECT COUNT(*) - 1 FROM GenealogyTree g WHERE g.Node.IsDescendantOf((SELECT Node FROM GenealogyTree WHERE MemberId = N'ESC-M')) = 1)
FROM MemberStatistics WHERE MemberId = N'ESC-M';

SELECT Comprobacion, Valor, Esperado, CASE WHEN Valor = Esperado THEN N'ok' ELSE N'FALLA' END AS Resultado FROM #r;

-- 4. Columnas con valor en un alta real y NULL en una ESC (o al reves). Lee la base local en solo lectura.
IF DB_ID(N'MLMConquerorGlobalEditionDb') IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'', @t sysname, @k nvarchar(400);
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
    OPEN c; FETCH NEXT FROM c INTO @t, @k;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'';
        SELECT @sql += N'INSERT INTO #nulos SELECT ''' + @t + N''', ''' + col.name + N''', '
            + N'(SELECT COUNT(*) FROM MLMConquerorGlobalEditionDb.dbo.' + QUOTENAME(@t) + N' WHERE ' + REPLACE(@k, N'{db}', N'MLMConquerorGlobalEditionDb') + N' AND ' + QUOTENAME(col.name) + N' IS NOT NULL), '
            + N'(SELECT COUNT(*) FROM dbo.' + QUOTENAME(@t) + N' WHERE ' + REPLACE(REPLACE(@k, N'{db}', N'MLMConquerorGlobalEditionDb_Escala'), N'@m', N'@e') + N' AND ' + QUOTENAME(col.name) + N' IS NOT NULL);'
        FROM sys.columns col JOIN sys.types ty ON ty.user_type_id = col.user_type_id
        WHERE col.object_id = OBJECT_ID(N'dbo.' + @t) AND col.is_nullable = 1 AND ty.name NOT IN (N'timestamp');
        EXEC sp_executesql @sql, N'@m nvarchar(50), @e nvarchar(50)', @m = N'AMB-934756', @e = N'ESC-1003';
        FETCH NEXT FROM c INTO @t, @k;
    END
    CLOSE c; DEALLOCATE c;
    SELECT Tabla, Columna, RealConValor, EscConValor FROM #nulos
    WHERE (RealConValor > 0 AND EscConValor = 0) OR (RealConValor = 0 AND EscConValor > 0)
    ORDER BY Tabla, Columna;
END

SELECT DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
IF EXISTS (SELECT 1 FROM #r WHERE Valor <> Esperado)
    THROW 51072, N'La verificacion de la base de escala no cuadra (ver la tabla de comprobaciones).', 1;
