/*
Plan Q (DQ16; revision 2 del bloque 2, C1 y C2). Realinea los puntos del producto con la regla de Nick: los puntos de un
miembro cuentan en toda su linea solo mientras su membresia esta activa. Deja la base en el estado que mantienen despues
MembershipPointsDeltas (desde el interceptor de suscripciones) y las dos colas:

  1. Aportes. Cada suscripcion activa de un miembro guarda lo que aporta (DualTeamContribution = EnrollmentContribution):
     la de mayor nivel (desempate por Id) lleva la suma de lo que ya aportaban sus activas y las demas 0; si esa suma es 0
     (una activa sin puntos), la regla de entrar en Active: los puntos de los productos de su ultimo pedido o, sin pedido,
     los del producto de su nivel (el mayor). Las no activas aportan 0.
  2. PersonalPoints (la primera fila de MemberStatistics del miembro, por Id) = la suma de lo que aportan sus activas;
     0 sin ninguna activa. Un miembro con aporte y sin fila recibe una.
  3. EnrollmentPoints = la suma de lo que aportan las activas del propio y de todo su equipo de inscripcion, sobre el nodo
     VIGENTE de cada miembro (el mas reciente, como GenealogySubtree.CurrentOfAsync), de abajo arriba por profundidad. Un
     miembro con aporte y sin nodo suma a la linea de su patrocinador (como MembershipPointsDeltas). Es la definicion de
     EnrollmentTeamPointsService.RecomputeEnrollmentPointsAsync. Los EnrollmentPointsDelta pendientes en
     MemberStatisticDeltas ya van dentro del recalculo: se ponen a 0 (las filas y sus otros contadores se conservan).
  4. Las piernas del binario NO se tocan aqui: despues, OBLIGATORIO, 02-recalcular-piernas.sql (Plan P), que las
     reconstruye desde los PersonalPoints nuevos y pone a 0 los puntos pendientes de la cola (ya van dentro).

Idempotente: una segunda ejecucion no cambia ninguna fila (todas las cuentas a 0). Toma 'dual-tree' y 'dual-leg-deltas'
en exclusivo durante todo el script. Escribe por tandas de 20.000 filas, una transaccion por tanda.

SOLO base local o la base de escala (MLMConquerorGlobalEditionDb_Escala, con -v Confirmo=ESCALA), con TODOS los servicios
parados, Hangfire incluido: SignupAPI (activa suscripciones y aplica MemberStatisticDeltas, sin candado), BizCenter (vacia
la cola de piernas), AdminAPI, Billing (renueva y retiene suscripciones), RankEngine y CommissionEngine. En produccion lo
ejecuta Nick, como 04 (guion de despliegue).
Cerrojos: DB_NAME() en lista blanca, -v Confirmo=LOCAL (o ESCALA), SERVERPROPERTY('MachineName') = HOST_NAME().

  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/05-realinear-puntos-por-membresia.sql
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-p/02-recalcular-piernas.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_NAME() NOT IN (N'MLMConquerorGlobalEditionDb', N'MLMConquerorGlobalEditionDb_Escala')
    THROW 51130, N'Este script es solo para MLMConquerorGlobalEditionDb o la base de escala del Plan Q.', 1;
IF N'$(Confirmo)' <> CASE WHEN DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala' THEN N'ESCALA' ELSE N'LOCAL' END
    THROW 51131, N'Falta la confirmacion explicita: ejecutar con -v Confirmo=LOCAL (Confirmo=ESCALA en la base de escala).', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51132, N'El servidor no es la maquina local.', 1;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @inicio datetime2 = SYSDATETIME();

BEGIN TRY
    DECLARE @candado int;
    EXEC @candado = sp_getapplock @Resource = N'dual-tree', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @candado < 0
        THROW 51133, N'Hay escrituras del binario en curso (dual-tree ocupado): parar los servicios.', 1;
    DECLARE @cola int;
    EXEC @cola = sp_getapplock @Resource = N'dual-leg-deltas', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @cola < 0
        THROW 51134, N'La cola de piernas se esta vaciando (dual-leg-deltas ocupado): parar los servicios.', 1;

    -- El recalculo de abajo arriba exige que cada nodo vigente cuelgue de la ruta vigente de su padre.
    IF OBJECT_ID('tempdb..#g') IS NOT NULL DROP TABLE #g;
    SELECT CAST(g.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId,
           CAST(g.ParentMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS ParentMemberId,
           g.HierarchyPath,
           LEN(g.HierarchyPath) - LEN(REPLACE(g.HierarchyPath, N'/', N'')) - 1 AS Profundidad,
           CAST(0 AS bigint) AS Base, CAST(0 AS bigint) AS Total
    INTO #g
    FROM (SELECT x.*, ROW_NUMBER() OVER (PARTITION BY x.MemberId ORDER BY x.CreationDate DESC, x.Id DESC) AS Rn
          FROM GenealogyTree x WHERE x.IsDeleted = 0) g
    WHERE g.Rn = 1;
    CREATE UNIQUE CLUSTERED INDEX cx ON #g (MemberId);
    CREATE INDEX ix_p ON #g (Profundidad, ParentMemberId) INCLUDE (Total);
    IF EXISTS (SELECT 1 FROM #g c LEFT JOIN #g p ON p.MemberId = c.ParentMemberId
               WHERE c.ParentMemberId IS NOT NULL AND (p.MemberId IS NULL OR c.HierarchyPath <> p.HierarchyPath + c.MemberId + N'/'))
        THROW 51135, N'Hay nodos de inscripcion vigentes que no cuelgan de la ruta vigente de su padre: repararlos antes.', 1;

    -- ── 1. Aportes ──────────────────────────────────────────────────────────────────────────────────────────────────
    IF OBJECT_ID('tempdb..#a') IS NOT NULL DROP TABLE #a;
    SELECT s.MemberId, SUM(CAST(s.DualTeamContribution AS bigint)) AS Suma,
           (SELECT TOP 1 t.Id FROM MembershipSubscriptions t WHERE t.MemberId = s.MemberId AND t.SubscriptionStatus = 1
            ORDER BY t.MembershipLevelId DESC, t.Id) AS Mayor
    INTO #a
    FROM MembershipSubscriptions s
    WHERE s.SubscriptionStatus = 1
    GROUP BY s.MemberId;
    CREATE UNIQUE CLUSTERED INDEX cx ON #a (MemberId);

    -- La regla de entrar en Active para las activas sin puntos: el ultimo pedido o el producto del nivel.
    IF OBJECT_ID('tempdb..#r') IS NOT NULL DROP TABLE #r;
    SELECT a.MemberId, a.Mayor,
           CAST(CASE WHEN s.LastOrderId IS NOT NULL AND s.LastOrderId <> N''
                     THEN ISNULL((SELECT SUM(p.QualificationPoins) FROM OrderDetails d JOIN Products p ON p.Id = d.ProductId
                                  WHERE d.OrderId = s.LastOrderId), 0)
                     ELSE ISNULL((SELECT MAX(p.QualificationPoins) FROM Products p WHERE p.MembershipLevelId = s.MembershipLevelId), 0)
                END AS bigint) AS Regla
    INTO #r
    FROM #a a JOIN MembershipSubscriptions s ON s.Id = a.Mayor
    WHERE a.Suma = 0;

    DECLARE @aportesACero int, @aportesFijados int, @porRegla int = (SELECT COUNT(*) FROM #r WHERE Regla <> 0);
    BEGIN TRAN;
    UPDATE s SET DualTeamContribution = 0, EnrollmentContribution = 0
    FROM MembershipSubscriptions s
    WHERE (s.DualTeamContribution <> 0 OR s.EnrollmentContribution <> 0)
      AND NOT EXISTS (SELECT 1 FROM #a a WHERE a.Mayor = s.Id);
    SET @aportesACero = @@ROWCOUNT;
    UPDATE s SET DualTeamContribution = v.Aporte, EnrollmentContribution = v.Aporte
    FROM MembershipSubscriptions s
    JOIN (SELECT a.Mayor, CAST(CASE WHEN a.Suma <> 0 THEN a.Suma ELSE ISNULL(r.Regla, 0) END AS int) AS Aporte
          FROM #a a LEFT JOIN #r r ON r.MemberId = a.MemberId) v ON v.Mayor = s.Id
    WHERE s.DualTeamContribution <> v.Aporte OR s.EnrollmentContribution <> v.Aporte;
    SET @aportesFijados = @@ROWCOUNT;
    COMMIT;

    -- ── 2. PersonalPoints ───────────────────────────────────────────────────────────────────────────────────────────
    IF OBJECT_ID('tempdb..#t') IS NOT NULL DROP TABLE #t;
    SELECT s.MemberId, SUM(CAST(s.DualTeamContribution AS bigint)) AS T
    INTO #t
    FROM MembershipSubscriptions s WHERE s.SubscriptionStatus = 1
    GROUP BY s.MemberId;
    CREATE UNIQUE CLUSTERED INDEX cx ON #t (MemberId);

    IF OBJECT_ID('tempdb..#pp') IS NOT NULL DROP TABLE #pp;
    SELECT f.Id, f.MemberId, f.PersonalPoints AS Antes, CAST(ISNULL(t.T, 0) AS int) AS Despues
    INTO #pp
    FROM (SELECT ms.Id, ms.MemberId, ms.PersonalPoints,
                 ROW_NUMBER() OVER (PARTITION BY ms.MemberId ORDER BY ms.Id) AS Rn
          FROM MemberStatistics ms) f
    LEFT JOIN #t t ON t.MemberId = f.MemberId
    WHERE f.Rn = 1 AND f.PersonalPoints <> ISNULL(t.T, 0);
    CREATE UNIQUE CLUSTERED INDEX cx ON #pp (Id);

    DECLARE @ppCambiados int = (SELECT COUNT(*) FROM #pp),
            @ppSinActiva int = (SELECT COUNT(*) FROM #pp WHERE Despues = 0),
            @puntosQuitados bigint = ISNULL((SELECT SUM(CAST(Antes - Despues AS bigint)) FROM #pp WHERE Antes > Despues), 0),
            @puntosPuestos bigint = ISNULL((SELECT SUM(CAST(Despues - Antes AS bigint)) FROM #pp WHERE Despues > Antes), 0);
    DECLARE @hecho int = 1;
    WHILE @hecho > 0
    BEGIN
        BEGIN TRAN;
        UPDATE TOP (20000) ms SET PersonalPoints = p.Despues
        FROM MemberStatistics ms JOIN #pp p ON p.Id = ms.Id
        WHERE ms.PersonalPoints <> p.Despues;
        SET @hecho = @@ROWCOUNT;
        COMMIT;
    END

    BEGIN TRAN;
    INSERT INTO MemberStatistics (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize, EnrollmentTeamSize,
                                  DualTeamPoints, EnrollmentPoints, QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers,
                                  EnrollmentTeamGrowth, DualteamGrowth, EnrollmentTeamPointsGrowth, DualTeamPointsGrowth,
                                  CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth, CurrentYearIncomeGrowth,
                                  CreationDate, CreatedBy)
    SELECT t.MemberId, CAST(t.T AS int), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, GETDATE(), N'plan-q-realinear'
    FROM #t t
    WHERE t.T <> 0 AND NOT EXISTS (SELECT 1 FROM MemberStatistics ms WHERE ms.MemberId = t.MemberId);
    DECLARE @filasNuevas int = @@ROWCOUNT;
    COMMIT;

    -- ── 3. EnrollmentPoints ─────────────────────────────────────────────────────────────────────────────────────────
    UPDATE g SET Base = t.T FROM #g g JOIN #t t ON t.MemberId = g.MemberId;
    -- Un miembro con aporte y sin nodo suma a la linea de su patrocinador (MembershipPointsDeltas sube por la suya).
    UPDATE g SET Base = g.Base + x.S
    FROM #g g
    JOIN (SELECT CAST(m.SponsorMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS SponsorMemberId, SUM(t.T) AS S
          FROM #t t JOIN MemberProfiles m ON m.MemberId = t.MemberId
          WHERE NOT EXISTS (SELECT 1 FROM #g n WHERE n.MemberId = t.MemberId) AND m.SponsorMemberId IS NOT NULL
          GROUP BY m.SponsorMemberId) x ON x.SponsorMemberId = g.MemberId;
    DECLARE @sinNodoConAporte int = (SELECT COUNT(*) FROM #t t WHERE t.T <> 0 AND NOT EXISTS (SELECT 1 FROM #g n WHERE n.MemberId = t.MemberId));
    UPDATE #g SET Total = Base;
    DECLARE @prof int = (SELECT MAX(Profundidad) FROM #g);
    WHILE @prof >= 1
    BEGIN
        UPDATE p SET Total = p.Total + c.S
        FROM #g p
        JOIN (SELECT ParentMemberId, SUM(Total) AS S FROM #g WHERE Profundidad = @prof GROUP BY ParentMemberId) c
          ON c.ParentMemberId = p.MemberId;
        SET @prof -= 1;
    END

    IF OBJECT_ID('tempdb..#ep') IS NOT NULL DROP TABLE #ep;
    SELECT f.Id, f.EnrollmentPoints AS Antes,
           CAST(COALESCE(g.Total, t.T, 0) AS int) AS Despues
    INTO #ep
    FROM (SELECT ms.Id, ms.MemberId, ms.EnrollmentPoints,
                 ROW_NUMBER() OVER (PARTITION BY ms.MemberId ORDER BY ms.Id) AS Rn
          FROM MemberStatistics ms) f
    LEFT JOIN #g g ON g.MemberId = f.MemberId
    LEFT JOIN #t t ON t.MemberId = f.MemberId
    WHERE f.Rn = 1 AND f.EnrollmentPoints <> CAST(COALESCE(g.Total, t.T, 0) AS int);
    CREATE UNIQUE CLUSTERED INDEX cx ON #ep (Id);
    DECLARE @epCambiados int = (SELECT COUNT(*) FROM #ep);

    BEGIN TRAN;
    UPDATE MemberStatisticDeltas SET EnrollmentPointsDelta = 0 WHERE IsApplied = 0 AND EnrollmentPointsDelta <> 0;
    DECLARE @epPendientesNeutralizados int = @@ROWCOUNT;
    COMMIT;

    SET @hecho = 1;
    WHILE @hecho > 0
    BEGIN
        BEGIN TRAN;
        UPDATE TOP (20000) ms SET EnrollmentPoints = e.Despues
        FROM MemberStatistics ms JOIN #ep e ON e.Id = ms.Id
        WHERE ms.EnrollmentPoints <> e.Despues;
        SET @hecho = @@ROWCOUNT;
        COMMIT;
    END

    EXEC sp_releaseapplock @Resource = N'dual-leg-deltas', @LockOwner = N'Session';
    EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';

    SELECT @aportesACero AS AportesPuestosACero,
           @aportesFijados AS AportesFijados,
           @porRegla AS ActivasSinPuntosPorRegla,
           @ppCambiados AS PersonalPointsCambiados,
           @ppSinActiva AS PersonalPointsACeroSinActiva,
           @puntosQuitados AS PuntosQuitados,
           @puntosPuestos AS PuntosPuestos,
           @filasNuevas AS FilasDeEstadisticasNuevas,
           @sinNodoConAporte AS ActivosSinNodoDeInscripcion,
           @epCambiados AS EnrollmentPointsCambiados,
           @epPendientesNeutralizados AS EnrollmentPendientesNeutralizados,
           DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos,
           N'Siguiente paso obligatorio: 02-recalcular-piernas.sql' AS Siguiente;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    IF APPLOCK_MODE(N'public', N'dual-leg-deltas', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-leg-deltas', @LockOwner = N'Session';
    IF APPLOCK_MODE(N'public', N'dual-tree', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';
    THROW;
END CATCH
