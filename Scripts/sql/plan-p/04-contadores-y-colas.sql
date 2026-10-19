/*
Plan P, Tarea 14. Despues del relleno de la clave (03-relleno-clave-binario.sql):
  1. Recalcula LeftLegCount y RightLegCount de todo el binario desde cero, de abajo arriba y nivel a nivel (por
     Depth), como 02-recalcular-piernas.sql hace con los puntos: la pierna de un nodo = 1 + las dos piernas de su
     hijo de ese lado. Desde aqui los mantiene LegPointMaintenance de forma incremental.
  2. Reconstruye DualTeamLegFrontiers (la cola de derrame de cada patrocinador): para todo nodo con los dos hijos,
     la hoja mas profunda de su pierna preferida (la propia; izquierda si es raiz), con desempate (CreationDate, Id),
     la misma regla que PlacementService.ResolveDeepestAsync. Se calcula de abajo arriba en una pasada (el mejor de
     cada subarbol sale del de sus hijos), sin recorrer cada pierna. La cola es una cache: el motor la valida al
     usarla (sigue en la pierna y con un hueco libre) y la rehace si no.

Escribe por tandas, una transaccion por tanda. Idempotente: una segunda ejecucion no cambia ningun contador y
deja las mismas colas. Toma 'dual-tree' en exclusivo.

SOLO base local o la base de ensayo del Plan P, con los servicios parados, o la base de escala del Bloque D
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

    IF OBJECT_ID('tempdb..#n') IS NOT NULL DROP TABLE #n;
    SELECT CAST(d.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId,
           CAST(d.ParentMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS ParentMemberId,
           d.Side, d.Depth, d.CreationDate, CAST(d.Id AS nvarchar(450)) COLLATE DATABASE_DEFAULT AS Id,
           CAST(0 AS int) AS Izq, CAST(0 AS int) AS Der,
           -- la hoja mas profunda del subarbol: al empezar, el propio nodo
           CAST(d.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS Hoja, d.Depth AS HojaProf,
           d.CreationDate AS HojaAlta, CAST(d.Id AS nvarchar(450)) COLLATE DATABASE_DEFAULT AS HojaId
    INTO #n
    FROM DualTeamTree d
    WHERE d.IsDeleted = 0;
    CREATE UNIQUE CLUSTERED INDEX cx ON #n (Depth, MemberId);
    CREATE INDEX ix_p ON #n (ParentMemberId, Side) INCLUDE (Izq, Der, Hoja, HojaProf, HojaAlta, HojaId);

    -- De la hoja a la raiz: contadores y mejor hoja de cada subarbol.
    DECLARE @prof int = (SELECT MAX(Depth) FROM #n);
    WHILE @prof >= 0
    BEGIN
        UPDATE p SET Izq = ISNULL(l.Nodos, 0), Der = ISNULL(r.Nodos, 0),
                     Hoja = ISNULL(b.Hoja, p.Hoja), HojaProf = ISNULL(b.HojaProf, p.HojaProf),
                     HojaAlta = ISNULL(b.HojaAlta, p.HojaAlta), HojaId = ISNULL(b.HojaId, p.HojaId)
        FROM #n p
        OUTER APPLY (SELECT 1 + c.Izq + c.Der AS Nodos FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 0) l
        OUTER APPLY (SELECT 1 + c.Izq + c.Der AS Nodos FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 1) r
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
        UPDATE TOP (20000) d SET LeftLegCount = n.Izq, RightLegCount = n.Der
        FROM DualTeamTree d JOIN #n n ON n.MemberId = d.MemberId
        WHERE d.IsDeleted = 0 AND (d.LeftLegCount <> n.Izq OR d.RightLegCount <> n.Der);
        SET @hecho = @@ROWCOUNT;
        SET @contadores += @hecho;
        COMMIT;
    END

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

    EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';

    SELECT (SELECT COUNT(*) FROM #n) AS Nodos,
           @contadores AS ContadoresCambiados,
           @borradas AS ColasBorradas,
           @colas AS ColasNuevas,
           DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    IF APPLOCK_MODE(N'public', N'dual-tree', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';
    THROW;
END CATCH
