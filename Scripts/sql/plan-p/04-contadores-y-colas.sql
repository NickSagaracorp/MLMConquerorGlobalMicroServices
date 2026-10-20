/*
Plan P, Tarea 14. Despues del relleno de la clave (03-relleno-clave-binario.sql):
  1. Recalcula LeftLegCount y RightLegCount de todo el binario desde cero, de abajo arriba y nivel a nivel (por
     Depth), como 02-recalcular-piernas.sql hace con los puntos: la pierna de un nodo = 1 + las dos piernas de su
     hijo de ese lado. Desde aqui los mantiene LegPointMaintenance de forma incremental.
  1b. Plan Q (DQ13 y DQ15): en la misma pasada, los calificados del binario (SelfQualified = suscripciones activas del
     propio miembro; LeftLegQualified/RightLegQualified = las de cada pierna) y MemberStatistics.DualTeamSize
     (= LeftLegCount + RightLegCount). Neutraliza la cola de calificados (dual-leg-deltas en exclusivo) y vuelve a
     encolar, como propios, las activas de los miembros sin nodo vivo.
  2. Reconstruye DualTeamLegFrontiers (la cola de derrame de cada patrocinador): para todo nodo con los dos hijos,
     la hoja mas profunda de su pierna preferida (la propia; izquierda si es raiz), con desempate (CreationDate, Id),
     la misma regla que PlacementService.ResolveDeepestAsync. Se calcula de abajo arriba en una pasada (el mejor de
     cada subarbol sale del de sus hijos), sin recorrer cada pierna. La cola es una cache: el motor la valida al
     usarla (sigue en la pierna y con un hueco libre) y la rehace si no.

Escribe por tandas, una transaccion por tanda. Idempotente: una segunda ejecucion no cambia ningun contador y
deja las mismas colas. Toma 'dual-tree' en exclusivo.

  1c. Plan Q (revision del bloque 2, I-2): neutraliza tambien los CONTADORES pendientes (NodeCount) de la cola: los
     recalcula desde cero y aplicarlos despues los contaria dos veces. Los puntos pendientes se conservan (los recalcula
     02-recalcular-piernas.sql, que neutraliza los suyos).

SOLO base local o la base de ensayo del Plan P, con TODOS los servicios parados (Hangfire incluido: BizCenter, SignupAPI,
AdminAPI, Billing, RankEngine y CommissionEngine; cualquiera de ellos coloca, retira, activa suscripciones o vacia la
cola de piernas), o la base de escala del Bloque D
(MLMConquerorGlobalEditionDb_Escala), que exige -v Confirmo=ESCALA; LOCAL solo vale en las otras dos. No se usa
-v Base=ESCALA porque sqlcmd falla si una variable no se define y romperia la invocacion de siempre.
Cerrojos: DB_NAME() en lista blanca, -v Confirmo=LOCAL, SERVERPROPERTY('MachineName') = HOST_NAME().

  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-p/04-contadores-y-colas.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_NAME() NOT IN (N'MLMConquerorGlobalEditionDb', N'MLMConquerorGlobalEditionDb_PlanPEnsayo', N'MLMConquerorGlobalEditionDb_Escala')
    THROW 51058, N'Este script es solo para MLMConquerorGlobalEditionDb (o su ensayo o la base de escala del Plan P).', 1;
IF N'$(Confirmo)' <> CASE WHEN DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala' THEN N'ESCALA' ELSE N'LOCAL' END
    THROW 51059, N'Falta la confirmacion explicita: ejecutar con -v Confirmo=LOCAL (Confirmo=ESCALA en la base de escala).', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51060, N'El servidor no es la maquina local.', 1;
IF EXISTS (SELECT 1 FROM DualTeamTree WHERE IsDeleted = 0 AND (BinaryPath IS NULL OR Depth IS NULL))
    THROW 51061, N'Hay nodos sin clave: primero 03-relleno-clave-binario.sql.', 1;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @inicio datetime2 = SYSDATETIME();

BEGIN TRY
    DECLARE @candado int;
    EXEC @candado = sp_getapplock @Resource = N'dual-tree', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @candado < 0
        THROW 51062, N'Hay escrituras del binario en curso (dual-tree ocupado): parar los servicios.', 1;

    -- Plan Q (DQ13): este script recalcula los CALIFICADOS desde cero. Lo pendiente de calificados y de contadores ya va
    -- dentro del recalculo: se pone a cero (en los de pierna y en los propios); al final se vuelven a encolar, como propios,
    -- las activas de los miembros sin nodo vivo (esperan a su colocacion).
    DECLARE @cola int;
    EXEC @cola = sp_getapplock @Resource = N'dual-leg-deltas', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @cola < 0
        THROW 51114, N'La cola de piernas se esta vaciando (dual-leg-deltas ocupado): parar los servicios.', 1;
    IF OBJECT_ID(N'dbo.DualLegDeltas') IS NOT NULL
    BEGIN
        BEGIN TRAN;
        -- Plan Q (DQ16): los propios llevan tambien PUNTOS pendientes (MembershipPointsDeltas) que este script no
        -- recalcula: se pone a cero su parte de calificados y se borran solo las filas que quedan a cero.
        UPDATE DualLegDeltas SET QualifiedCount = 0 WHERE QualifiedCount <> 0;
        -- Revision del bloque 2 (I-2): los contadores de pierna tambien se recalculan aqui.
        UPDATE DualLegDeltas SET NodeCount = 0 WHERE NodeCount <> 0;
        DELETE FROM DualLegDeltas WHERE Points = 0 AND NodeCount = 0 AND QualifiedCount = 0;
        COMMIT;
    END

    IF OBJECT_ID('tempdb..#n') IS NOT NULL DROP TABLE #n;
    SELECT CAST(d.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId,
           CAST(d.ParentMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS ParentMemberId,
           d.Side, d.Depth, d.CreationDate, CAST(d.Id AS nvarchar(450)) COLLATE DATABASE_DEFAULT AS Id,
           CAST(0 AS int) AS Izq, CAST(0 AS int) AS Der,
           -- Plan Q (DQ13): activas propias y de cada pierna
           CAST(0 AS int) AS Yo, CAST(0 AS int) AS IzqQ, CAST(0 AS int) AS DerQ,
           -- la hoja mas profunda del subarbol: al empezar, el propio nodo
           CAST(d.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS Hoja, d.Depth AS HojaProf,
           d.CreationDate AS HojaAlta, CAST(d.Id AS nvarchar(450)) COLLATE DATABASE_DEFAULT AS HojaId
    INTO #n
    FROM DualTeamTree d
    WHERE d.IsDeleted = 0;
    CREATE UNIQUE CLUSTERED INDEX cx ON #n (Depth, MemberId);
    CREATE INDEX ix_p ON #n (ParentMemberId, Side) INCLUDE (Izq, Der, Yo, IzqQ, DerQ, Hoja, HojaProf, HojaAlta, HojaId);

    -- Plan Q (DQ13): suscripciones activas (no miembros) y sin mirar IsDeleted, como el recuento al vuelo (Q-R21).
    UPDATE n SET Yo = a.Activas
    FROM #n n
    JOIN (SELECT CAST(MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId, COUNT(*) AS Activas
          FROM MembershipSubscriptions WHERE SubscriptionStatus = 1 GROUP BY MemberId) a ON a.MemberId = n.MemberId;

    -- De la hoja a la raiz: contadores y mejor hoja de cada subarbol.
    DECLARE @prof int = (SELECT MAX(Depth) FROM #n);
    WHILE @prof >= 0
    BEGIN
        UPDATE p SET Izq = ISNULL(l.Nodos, 0), Der = ISNULL(r.Nodos, 0),
                     IzqQ = ISNULL(l.Q, 0), DerQ = ISNULL(r.Q, 0),
                     Hoja = ISNULL(b.Hoja, p.Hoja), HojaProf = ISNULL(b.HojaProf, p.HojaProf),
                     HojaAlta = ISNULL(b.HojaAlta, p.HojaAlta), HojaId = ISNULL(b.HojaId, p.HojaId)
        FROM #n p
        OUTER APPLY (SELECT 1 + c.Izq + c.Der AS Nodos, c.Yo + c.IzqQ + c.DerQ AS Q FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 0) l
        OUTER APPLY (SELECT 1 + c.Izq + c.Der AS Nodos, c.Yo + c.IzqQ + c.DerQ AS Q FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 1) r
        OUTER APPLY (SELECT TOP 1 c.Hoja, c.HojaProf, c.HojaAlta, c.HojaId FROM #n c
                     WHERE c.ParentMemberId = p.MemberId
                     ORDER BY c.HojaProf DESC, c.HojaAlta, c.HojaId) b
        WHERE p.Depth = @prof;
        SET @prof -= 1;
    END

    DECLARE @hecho int = 1, @contadores int = 0;
    WHILE @hecho > 0
    BEGIN
        BEGIN TRAN;
        UPDATE TOP (20000) d
        SET LeftLegCount = n.Izq, RightLegCount = n.Der,
            SelfQualified = n.Yo, LeftLegQualified = n.IzqQ, RightLegQualified = n.DerQ
        FROM DualTeamTree d JOIN #n n ON n.MemberId = d.MemberId
        WHERE d.IsDeleted = 0 AND (d.LeftLegCount <> n.Izq OR d.RightLegCount <> n.Der OR d.SelfQualified <> n.Yo
                                   OR d.LeftLegQualified <> n.IzqQ OR d.RightLegQualified <> n.DerQ);
        SET @hecho = @@ROWCOUNT;
        SET @contadores += @hecho;
        COMMIT;
    END

    -- Plan Q (DQ15): la copia del tamano del equipo binario en las estadisticas.
    DECLARE @tamanos int = 0;
    SET @hecho = 1;
    WHILE @hecho > 0
    BEGIN
        BEGIN TRAN;
        UPDATE TOP (20000) s SET DualTeamSize = n.Izq + n.Der
        FROM MemberStatistics s JOIN #n n ON n.MemberId = s.MemberId
        WHERE s.DualTeamSize <> n.Izq + n.Der;
        SET @hecho = @@ROWCOUNT;
        SET @tamanos += @hecho;
        COMMIT;
    END

    -- Plan Q (DQ13): las activas de los miembros sin nodo vivo esperan en la cola, como propios, a su colocacion.
    INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, QualifiedCount, OwnChange, CreationDate, CreatedBy)
    SELECT s.MemberId, 0, 0, 0, COUNT(*), 1, GETDATE(), N'plan-q-calificados'
    FROM MembershipSubscriptions s
    WHERE s.SubscriptionStatus = 1
      AND NOT EXISTS (SELECT 1 FROM DualTeamTree d WHERE d.MemberId = s.MemberId AND d.IsDeleted = 0)
    GROUP BY s.MemberId;
    DECLARE @aparcados int = @@ROWCOUNT;

    -- Colas: para cada nodo con los dos hijos, la mejor hoja de su hijo del lado preferido.
    IF OBJECT_ID('tempdb..#c') IS NOT NULL DROP TABLE #c;
    SELECT p.MemberId AS SponsorMemberId,
           CASE WHEN p.ParentMemberId IS NULL THEN 0 ELSE p.Side END AS PreferredSide,
           c.Hoja AS DeepestMemberId, c.HojaProf AS DeepestDepth
    INTO #c
    FROM #n p
    JOIN #n c ON c.ParentMemberId = p.MemberId AND c.Side = CASE WHEN p.ParentMemberId IS NULL THEN 0 ELSE p.Side END
    WHERE EXISTS (SELECT 1 FROM #n x WHERE x.ParentMemberId = p.MemberId AND x.Side = 0)
      AND EXISTS (SELECT 1 FROM #n x WHERE x.ParentMemberId = p.MemberId AND x.Side = 1);

    BEGIN TRAN;
    DELETE FROM DualTeamLegFrontiers;
    DECLARE @borradas int = @@ROWCOUNT;
    INSERT INTO DualTeamLegFrontiers (Id, SponsorMemberId, PreferredSide, DeepestMemberId, DeepestDepth,
                                      CreationDate, CreatedBy, LastUpdateDate, LastUpdateBy, IsDeleted)
    SELECT CONVERT(nvarchar(36), NEWID()), SponsorMemberId, PreferredSide, DeepestMemberId, DeepestDepth,
           GETDATE(), N'plan-p-colas', GETDATE(), N'plan-p-colas', 0
    FROM #c;
    DECLARE @colas int = @@ROWCOUNT;
    COMMIT;

    -- Plan Q (DQ13): verificacion. Las cinco raices con mas nodos: sus calificados frente al recuento al vuelo de las
    -- activas de su rango por la clave (la misma consulta que DualSubtree.MemberIdsUnder).
    DECLARE @distintos int = 0;
    SELECT @distintos = COUNT(*)
    FROM (SELECT TOP 5 d.MemberId, d.BinaryPath, d.LeftLegQualified + d.RightLegQualified AS Guardado
          FROM DualTeamTree d WHERE d.IsDeleted = 0 AND d.ParentMemberId IS NULL
          ORDER BY d.LeftLegCount + d.RightLegCount DESC, d.Id) r
    CROSS APPLY (SELECT SUBSTRING(r.BinaryPath, 1, 4) AS Lo,
                        CAST(CAST(SUBSTRING(r.BinaryPath, 1, 4) AS int) + 1 AS binary(4)) AS Hi) b
    CROSS APPLY (SELECT COUNT(*) AS AlVuelo
                 FROM DualTeamTree t WITH (INDEX(UX_DualTeamTree_BinaryPath))
                 JOIN MembershipSubscriptions s ON s.MemberId = t.MemberId AND s.SubscriptionStatus = 1
                 WHERE t.BinaryPath > b.Lo AND t.BinaryPath < b.Hi AND t.IsDeleted = 0 AND t.MemberId <> r.MemberId) v
    WHERE r.Guardado <> v.AlVuelo;

    EXEC sp_releaseapplock @Resource = N'dual-leg-deltas', @LockOwner = N'Session';
    EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';

    SELECT (SELECT COUNT(*) FROM #n) AS Nodos,
           @contadores AS ContadoresCambiados,
           @tamanos AS TamanosCambiados,
           @borradas AS ColasBorradas,
           @colas AS ColasNuevas,
           @distintos AS CalificadosDistintos,
           @aparcados AS PropiosAparcados,
           DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    IF APPLOCK_MODE(N'public', N'dual-leg-deltas', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-leg-deltas', @LockOwner = N'Session';
    IF APPLOCK_MODE(N'public', N'dual-tree', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';
    THROW;
END CATCH
