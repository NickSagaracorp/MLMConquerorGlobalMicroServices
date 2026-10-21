/*
Plan Q, Tarea 32. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: el historial de rangos
(MemberRankHistories) que dejaria la evaluacion de rango sobre los datos generados. La cola de rango del generador sale ya
procesada (f-ascendientes.sql), asi que el historial tiene que ser el que esa evaluacion habria escrito; sin el, Mi equipo,
el visualizador del admin y el progreso de rango medirian sobre una tabla vacia.

La regla es la de EvaluateRankHandler con RankQualificationService, por conjuntos:
  - candidatos: los rangos activos (Status = 1) con SortOrder > 0 y con requisitos; de cada uno, su requisito de menor LevelNo;
  - puerta universal: (patrocinados >= RankGate.MinSponsoredMembers y puntos de clientes >= ...WithSponsors) o puntos de
    clientes >= ...WithoutSponsors (GlobalParameters, con los valores por defecto de RankGateParameters). Los puntos de
    clientes son los de los productos del ultimo pedido de las suscripciones activas del propio y de sus patrocinados
    (PersonalCustomerPointsService);
  - binario: piernas truncadas a entero, cada una topada en ROUND(MaxTeamPointsPerBranch * TeamPoints) y la suma en TeamPoints;
  - inscripcion: los EnrollmentPoints de cada hijo directo en GenealogyTree (ramas), cada uno topado en
    ROUND(MaxEnrollmentTeamPointsPerBranch * EnrollmentTeam) y la suma en EnrollmentTeam. ROUND como Math.Round de .NET
    (al par en el medio);
  - PersonalPoints, ExternalMembers (patrocinados de tipo ExternalMember) y SalesVolume (pedidos completados) cuando el
    requisito es > 0.
Como la evaluacion: una fila por cada rango candidato que cumple por encima del mayor que ya tiene (al generar, ninguno),
en orden de SortOrder, con PreviousRankId encadenado y AchievedAt separado un segundo (como el manejador). AchievedAt es la
hora de esta pasada (la del barrido que la evaluo), CreatedBy vacio como el de los trabajos de Hangfire en la base local.
No se generan los avisos de rango alcanzado (trabajos de RankNotificationJobs) ni bonos de rango.

Despues de 04-contadores-y-colas.sql (piernas) y con MemberStatistics completo (d-cobros.sql). Lo lanza ..\01-generar-altas.ps1.
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
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @inicio datetime2 = SYSDATETIME();
DECLARE @hito datetime2 = DATEADD(second, -DATEPART(second, GETDATE()), DATEADD(millisecond, -DATEPART(millisecond, GETDATE()), GETDATE()));

-- Puerta (RankGateParameters).
DECLARE @minPatrocinados int = ISNULL((SELECT TRY_CAST([Value] AS int) FROM GlobalParameters WHERE [Key] = N'RankGate.MinSponsoredMembers'), 3);
DECLARE @minConPatrocinados int = ISNULL((SELECT TRY_CAST([Value] AS int) FROM GlobalParameters WHERE [Key] = N'RankGate.MinPersonalPointsWithSponsors'), 9);
DECLARE @minSinPatrocinados int = ISNULL((SELECT TRY_CAST([Value] AS int) FROM GlobalParameters WHERE [Key] = N'RankGate.MinPersonalPointsWithoutSponsors'), 12);

-- Requisito principal de cada rango candidato, con los topes ya redondeados como Math.Round (al par).
DROP TABLE IF EXISTS #req;
SELECT r.Id AS Rango, r.SortOrder, q.TeamPoints, q.EnrollmentTeam, q.PersonalPoints, q.ExternalMembers, q.SalesVolume,
       CAST(CASE WHEN q.MaxTeamPointsPerBranch > 0 THEN
            CASE WHEN x.Dt - FLOOR(x.Dt) = 0.5 THEN FLOOR(x.Dt) + CAST(FLOOR(x.Dt) AS bigint) % 2 ELSE ROUND(x.Dt, 0) END
            ELSE 0 END AS int) AS TopePierna,
       CAST(CASE WHEN q.MaxEnrollmentTeamPointsPerBranch > 0 THEN
            CASE WHEN x.Et - FLOOR(x.Et) = 0.5 THEN FLOOR(x.Et) + CAST(FLOOR(x.Et) AS bigint) % 2 ELSE ROUND(x.Et, 0) END
            ELSE 0 END AS int) AS TopeRama
INTO #req
FROM RankDefinitions r
CROSS APPLY (SELECT TOP 1 * FROM RankRequirements q WHERE q.RankDefinitionId = r.Id ORDER BY q.LevelNo, q.Id) q
CROSS APPLY (SELECT CAST(q.MaxTeamPointsPerBranch AS float) * q.TeamPoints AS Dt,
                    CAST(q.MaxEnrollmentTeamPointsPerBranch AS float) * q.EnrollmentTeam AS Et) x
WHERE r.Status = 1 AND r.SortOrder > 0;

-- Lo que lee la evaluacion de cada miembro.
DROP TABLE IF EXISTS #m;
SELECT CAST(m.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId,
       ISNULL(s.PersonalPoints, 0) AS Personales,
       CAST(ISNULL(d.LeftLegPoints, 0) AS int) AS Izq, CAST(ISNULL(d.RightLegPoints, 0) AS int) AS Der,
       0 AS Patrocinados, 0 AS Externos, CAST(0 AS bigint) AS PuntosClientes, CAST(0 AS decimal(18,2)) AS Ventas
INTO #m
FROM MemberProfiles m
OUTER APPLY (SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = m.MemberId ORDER BY ms.Id) s
OUTER APPLY (SELECT TOP 1 dt.LeftLegPoints, dt.RightLegPoints FROM DualTeamTree dt WHERE dt.MemberId = m.MemberId AND dt.IsDeleted = 0) d
WHERE m.IsDeleted = 0;
CREATE UNIQUE CLUSTERED INDEX cx ON #m (MemberId);

UPDATE x SET Patrocinados = p.N, Externos = p.Ext
FROM #m x JOIN (SELECT SponsorMemberId, COUNT(*) AS N, SUM(CASE WHEN MemberType = 1 THEN 1 ELSE 0 END) AS Ext
                FROM MemberProfiles WHERE SponsorMemberId IS NOT NULL AND IsDeleted = 0 GROUP BY SponsorMemberId) p
     ON p.SponsorMemberId = x.MemberId;

-- Puntos del pedido de cada suscripcion activa con pedido (un pedido cuenta una vez por miembro).
DROP TABLE IF EXISTS #pp;
SELECT a.MemberId, SUM(CAST(pr.QualificationPoins AS bigint)) AS Puntos
INTO #pp
FROM (SELECT DISTINCT CAST(MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId, LastOrderId
      FROM MembershipSubscriptions WHERE SubscriptionStatus = 1 AND LastOrderId IS NOT NULL) a
JOIN OrderDetails od ON od.OrderId = a.LastOrderId
JOIN Products pr ON pr.Id = od.ProductId
GROUP BY a.MemberId;
CREATE UNIQUE CLUSTERED INDEX cx ON #pp (MemberId);
UPDATE x SET PuntosClientes = ISNULL(yo.Puntos, 0) + ISNULL(h.Puntos, 0)
FROM #m x
LEFT JOIN #pp yo ON yo.MemberId = x.MemberId
LEFT JOIN (SELECT CAST(m.SponsorMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS Patrocinador, SUM(pp.Puntos) AS Puntos
           FROM MemberProfiles m JOIN #pp pp ON pp.MemberId = m.MemberId
           WHERE m.SponsorMemberId IS NOT NULL AND m.IsDeleted = 0 GROUP BY m.SponsorMemberId) h ON h.Patrocinador = x.MemberId;

UPDATE x SET Ventas = v.Total
FROM #m x JOIN (SELECT MemberId, SUM(TotalAmount) AS Total FROM Orders WHERE Status = 4 AND IsDeleted = 0 GROUP BY MemberId) v
     ON v.MemberId = x.MemberId;

-- Ramas de inscripcion: los EnrollmentPoints de cada hijo directo.
DROP TABLE IF EXISTS #ramas;
SELECT CAST(g.ParentMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId, ISNULL(s.EnrollmentPoints, 0) AS Puntos
INTO #ramas
FROM GenealogyTree g
OUTER APPLY (SELECT TOP 1 ms.EnrollmentPoints FROM MemberStatistics ms WHERE ms.MemberId = g.MemberId ORDER BY ms.Id) s
WHERE g.ParentMemberId IS NOT NULL AND g.IsDeleted = 0;
CREATE CLUSTERED INDEX cx ON #ramas (MemberId);

DROP TABLE IF EXISTS #et;
SELECT b.MemberId, r.Rango, SUM(CAST(CASE WHEN r.TopeRama > 0 AND b.Puntos > r.TopeRama THEN r.TopeRama ELSE b.Puntos END AS bigint)) AS Suma
INTO #et
FROM #ramas b CROSS JOIN #req r
WHERE r.EnrollmentTeam > 0
GROUP BY b.MemberId, r.Rango;
CREATE UNIQUE CLUSTERED INDEX cx ON #et (MemberId, Rango);

-- Rangos que cumple cada miembro.
DROP TABLE IF EXISTS #cumple;
SELECT m.MemberId, r.Rango, r.SortOrder
INTO #cumple
FROM #m m
CROSS JOIN #req r
LEFT JOIN #et e ON e.MemberId = m.MemberId AND e.Rango = r.Rango
WHERE ((m.Patrocinados >= @minPatrocinados AND m.PuntosClientes >= @minConPatrocinados) OR m.PuntosClientes >= @minSinPatrocinados)
  AND (r.TeamPoints <= 0 OR
       CASE WHEN r.TopePierna > 0
            THEN CASE WHEN m.Izq > r.TopePierna THEN r.TopePierna ELSE m.Izq END + CASE WHEN m.Der > r.TopePierna THEN r.TopePierna ELSE m.Der END
            ELSE CAST(m.Izq AS bigint) + m.Der END >= r.TeamPoints)
  AND (r.EnrollmentTeam <= 0 OR ISNULL(e.Suma, 0) >= r.EnrollmentTeam)
  AND (r.ExternalMembers <= 0 OR m.Externos >= r.ExternalMembers)
  AND (r.PersonalPoints <= 0 OR m.Personales >= r.PersonalPoints)
  AND (r.SalesVolume <= 0 OR m.Ventas >= r.SalesVolume);

-- Como EvaluateRankHandler: solo los rangos por encima del mayor que ya tiene el miembro, encadenados desde el (la primera
-- generacion parte de una tabla sin historial; 03-anadir-altas.ps1 lo vuelve a ejecutar tras anadir altas).
DROP TABLE IF EXISTS #actual;
SELECT x.MemberId, x.Rango, x.SortOrder
INTO #actual
FROM (SELECT CAST(h.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId, h.RankDefinitionId AS Rango, r.SortOrder,
             ROW_NUMBER() OVER (PARTITION BY h.MemberId ORDER BY r.SortOrder DESC) AS rn
      FROM MemberRankHistories h JOIN RankDefinitions r ON r.Id = h.RankDefinitionId
      WHERE h.IsDeleted = 0) x
WHERE x.rn = 1;
CREATE UNIQUE CLUSTERED INDEX cx ON #actual (MemberId);
DELETE c FROM #cumple c JOIN #actual a ON a.MemberId = c.MemberId WHERE c.SortOrder <= a.SortOrder;

DECLARE @filas bigint;
BEGIN TRAN;
INSERT INTO MemberRankHistories (Id, MemberId, RankDefinitionId, PreviousRankId, AchievedAt, GeneratedCertificateUrl,
                                 CreationDate, CreatedBy, LastUpdateDate, LastUpdateBy, IsDeleted)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), c.MemberId, c.Rango,
       ISNULL(LAG(c.Rango) OVER (PARTITION BY c.MemberId ORDER BY c.SortOrder), a.Rango),
       DATEADD(second, ROW_NUMBER() OVER (PARTITION BY c.MemberId ORDER BY c.SortOrder) - 1, @hito), NULL,
       DATEADD(second, ROW_NUMBER() OVER (PARTITION BY c.MemberId ORDER BY c.SortOrder) - 1, @hito), N'',
       DATEADD(second, ROW_NUMBER() OVER (PARTITION BY c.MemberId ORDER BY c.SortOrder) - 1, @hito), NULL, 0
FROM #cumple c LEFT JOIN #actual a ON a.MemberId = c.MemberId;
SET @filas = @@ROWCOUNT;
COMMIT;
SELECT r.Name AS Rango, COUNT(*) AS Filas
FROM MemberRankHistories h JOIN RankDefinitions r ON r.Id = h.RankDefinitionId
GROUP BY r.Name, r.SortOrder ORDER BY r.SortOrder;
SELECT @filas AS FilasNuevas, (SELECT COUNT(DISTINCT MemberId) FROM #cumple) AS MiembrosQueSuben,
       (SELECT COUNT(DISTINCT MemberId) FROM MemberRankHistories) AS MiembrosConRango,
       (SELECT TOP 1 r.Name FROM MemberRankHistories h JOIN RankDefinitions r ON r.Id = h.RankDefinitionId WHERE h.MemberId = N'ESC-M' ORDER BY r.SortOrder DESC) AS RangoDeEscM,
       DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
