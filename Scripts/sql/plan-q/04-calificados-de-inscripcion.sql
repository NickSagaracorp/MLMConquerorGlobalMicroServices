/*
Plan Q, Tarea 19b (DQ13). Recalcula MemberStatistics.EnrollmentQualifiedTeamMembers (suscripciones activas del equipo de
inscripcion, sin el propio) desde cero, de abajo arriba por Level: lo de un nodo = sus activas + lo de sus hijos. Antes,
pone a cero en la cola de estadisticas lo pendiente de calificados (ya va dentro del recalculo). Toma
'member-statistic-deltas' en exclusivo (el trabajo de deltas lo pide en cada tanda). Escribe por tandas de 20.000.
Idempotente: una segunda ejecucion cambia 0 filas. Necesita un solo nodo vivo por miembro (Tarea 21, DQ9).
Con los servicios parados: un cambio de suscripcion durante el recalculo quedaria fuera del contador.
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/04-calificados-de-inscripcion.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(Confirmo)' NOT IN (N'LOCAL', N'ESCALA')
    THROW 51116, N'Ejecutar con -v Confirmo=LOCAL (o ESCALA en la base de escala).', 1;
IF NOT ((N'$(Confirmo)' = N'LOCAL'  AND DB_NAME() = N'MLMConquerorGlobalEditionDb')
     OR (N'$(Confirmo)' = N'ESCALA' AND DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'))
    THROW 51117, N'Confirmo no corresponde a esta base.', 1;
IF EXISTS (SELECT 1 FROM GenealogyTree WHERE IsDeleted = 0 GROUP BY MemberId HAVING COUNT(*) > 1)
    THROW 51118, N'Hay miembros con mas de un nodo vivo: primero Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql.', 1;

DECLARE @inicio datetime2 = SYSDATETIME();
BEGIN TRY
    DECLARE @candado int;
    EXEC @candado = sp_getapplock @Resource = N'member-statistic-deltas', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @candado < 0
        THROW 51119, N'El trabajo de deltas esta aplicando una tanda (member-statistic-deltas ocupado): parar los servicios.', 1;

    BEGIN TRAN;
    UPDATE MemberStatisticDeltas SET EnrollmentQualifiedTeamMembersDelta = 0
    WHERE IsApplied = 0 AND EnrollmentQualifiedTeamMembersDelta <> 0;
    UPDATE MemberStatisticDeltas SET IsApplied = 1, AppliedAt = GETDATE()
    WHERE IsApplied = 0 AND Scope = 2;   -- AncestorsOf: no llevan otra cosa
    COMMIT;

    DROP TABLE IF EXISTS #g;
    SELECT CAST(g.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId,
           CAST(g.ParentMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS ParentMemberId,
           g.Level, CAST(0 AS int) AS Yo, CAST(0 AS int) AS Sub
    INTO #g
    FROM GenealogyTree g
    WHERE g.IsDeleted = 0;
    CREATE UNIQUE CLUSTERED INDEX cx ON #g (Level, MemberId);
    CREATE INDEX ix_p ON #g (ParentMemberId) INCLUDE (Sub);

    UPDATE g SET Yo = a.Activas
    FROM #g g
    JOIN (SELECT CAST(MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId, COUNT(*) AS Activas
          FROM MembershipSubscriptions WHERE SubscriptionStatus = 1 GROUP BY MemberId) a ON a.MemberId = g.MemberId;

    DECLARE @nivel int = (SELECT MAX(Level) FROM #g);
    WHILE @nivel >= 0
    BEGIN
        UPDATE p SET Sub = p.Yo + ISNULL(h.S, 0)
        FROM #g p
        OUTER APPLY (SELECT SUM(c.Sub) AS S FROM #g c WHERE c.ParentMemberId = p.MemberId) h
        WHERE p.Level = @nivel;
        SET @nivel -= 1;
    END

    DECLARE @hecho int = 1, @cambiados int = 0;
    WHILE @hecho > 0
    BEGIN
        BEGIN TRAN;
        UPDATE TOP (20000) s SET EnrollmentQualifiedTeamMembers = g.Sub - g.Yo
        FROM MemberStatistics s JOIN #g g ON g.MemberId = s.MemberId
        WHERE s.EnrollmentQualifiedTeamMembers <> g.Sub - g.Yo;
        SET @hecho = @@ROWCOUNT;
        SET @cambiados += @hecho;
        COMMIT;
    END

    -- Quien tiene equipo con activas y aun no tiene fila de estadisticas: se crea, como haria el MERGE del trabajo.
    INSERT INTO MemberStatistics (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize, EnrollmentTeamSize,
        DualTeamPoints, EnrollmentPoints, QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers,
        EnrollmentQualifiedTeamMembers, EnrollmentTeamGrowth, DualteamGrowth, EnrollmentTeamPointsGrowth, DualTeamPointsGrowth,
        CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth, CurrentYearIncomeGrowth, CreationDate, CreatedBy)
    SELECT g.MemberId, 0, 0, 0, 0, 0, 0, 0, 0, g.Sub - g.Yo, 0, 0, 0, 0, 0, 0, 0, GETDATE(), N'plan-q-calificados'
    FROM #g g
    WHERE g.Sub - g.Yo <> 0 AND NOT EXISTS (SELECT 1 FROM MemberStatistics s WHERE s.MemberId = g.MemberId);
    DECLARE @creados int = @@ROWCOUNT;

    EXEC sp_releaseapplock @Resource = N'member-statistic-deltas', @LockOwner = N'Session';

    -- Verificacion: las cinco raices mayores, contador frente al recuento al vuelo por el rango del Node.
    SELECT TOP 5 r.MemberId, s.EnrollmentQualifiedTeamMembers AS Contador,
           (SELECT COUNT(*) FROM GenealogyTree g
            JOIN MembershipSubscriptions m ON m.MemberId = g.MemberId AND m.SubscriptionStatus = 1
            WHERE g.IsDeleted = 0 AND g.Node.IsDescendantOf(r.Node) = 1 AND g.MemberId <> r.MemberId) AS AlVuelo
    FROM GenealogyTree r
    JOIN MemberStatistics s ON s.MemberId = r.MemberId
    WHERE r.IsDeleted = 0 AND r.ParentMemberId IS NULL
    ORDER BY s.EnrollmentTeamSize DESC;

    SELECT (SELECT COUNT(*) FROM #g) AS Nodos, @cambiados AS Cambiados, @creados AS Creados,
           DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    IF APPLOCK_MODE(N'public', N'member-statistic-deltas', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'member-statistic-deltas', @LockOwner = N'Session';
    THROW;
END CATCH
