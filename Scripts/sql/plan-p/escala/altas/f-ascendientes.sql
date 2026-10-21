/*
Plan P, Bloque D (Plan Q, Tarea 32). Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: las filas que un alta escribe por
ascendiente de inscripcion (hasta 10 niveles) y sus deltas, desde escala.Pares (e-inscripcion.sql). Una tabla por ejecucion (-v Tabla=Cola, Deltas o
Avisos), para cargar las tres a la vez en sesiones distintas. Cada una: se desactivan sus indices no agrupados, un solo
INSERT con TABLOCK sobre la tabla vacia (registro minimo en recuperacion SIMPLE) y se reconstruyen.
Lo lanza el orquestador ..\01-generar-altas.ps1.
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
IF OBJECT_ID('escala.Pares') IS NULL
    THROW 51074, N'Falta escala.Pares (e-inscripcion.sql).', 1;
IF N'$(Tabla)' NOT IN (N'Cola', N'Deltas', N'Avisos')
    THROW 51075, N'Tabla debe ser Cola, Deltas o Avisos.', 1;
GO
SET NOCOUNT ON;
DECLARE @inicio datetime2 = SYSDATETIME(), @f bigint;
IF N'$(Tabla)' = N'Cola'
BEGIN
    ALTER INDEX IX_RankEvaluationQueue_EvaluateMemberId ON RankEvaluationQueue DISABLE;
    ALTER INDEX IX_RankEvaluationQueue_IsProcessed_TriggerDate ON RankEvaluationQueue DISABLE;
    ALTER INDEX IX_RankEvaluationQueue_TriggerMemberId ON RankEvaluationQueue DISABLE;
    -- El alta (SignupAmbassadorHandler): la linea del patrocinador hasta 10 niveles (escala.Pares ya viene acotada).
    -- La activacion (ApplyMemberStatisticDeltasJob, fila de puntos sobre el propio): el propio y sus 10 ascendientes; los
    -- 10 ya tienen su evaluacion pendiente del alta (no se repiten), asi que solo anade la del propio.
    INSERT INTO RankEvaluationQueue WITH (TABLOCK) (TriggerMemberId, EvaluateMemberId, TriggerEvent, TriggerDate, IsProcessed, ProcessedAt,
                                                    ProcessedBy, ErrorMessage, RetryCount, CreationDate, CreatedBy)
    SELECT T, E, 1, Alta, 1, DATEADD(hour, 5, Alta), N'RankEvaluationSweepJob', NULL, 0, DATEADD(millisecond, 170, Alta), Email
    FROM escala.Pares
    UNION ALL
    SELECT T, T, 1, DATEADD(second, 30, Activa), 1, DATEADD(hour, 5, Alta), N'RankEvaluationSweepJob', NULL, 0,
           DATEADD(second, 30, Activa), N'delta-apply'
    FROM escala.Pares WHERE Directo = 1;
    SET @f = @@ROWCOUNT;
    ALTER INDEX ALL ON RankEvaluationQueue REBUILD WITH (SORT_IN_TEMPDB = ON);
END
IF N'$(Tabla)' = N'Deltas'
BEGIN
    ALTER INDEX IX_MemberStatisticDeltas_IsApplied_CreationDate ON MemberStatisticDeltas DISABLE;
    ALTER INDEX IX_MemberStatisticDeltas_MemberId_IsApplied ON MemberStatisticDeltas DISABLE;
    -- Plan Q (Bloque 4, 19b y DQ16): tres filas por alta, ya aplicadas, y ninguna por ascendiente:
    --   1. la del alta (SignupActivationService): SponsorLine sobre el patrocinador, equipo +1 y patrocinados +1;
    --   2. la de puntos (MembershipPointsDeltas, al pasar la suscripcion a Active): SponsorLine sobre el propio con sus puntos;
    --   3. la de calificados (QualifiedTeamDeltas): AncestorsOf sobre el propio, +1 a los calificados de su linea.
    INSERT INTO MemberStatisticDeltas WITH (TABLOCK) (MemberId, Scope, EnrollmentPointsDelta, EnrollmentTeamSizeDelta,
                                                      QualifiedSponsoredMembersDelta, EnrollmentQualifiedTeamMembersDelta,
                                                      IsApplied, AppliedAt, SourceMemberId, CreationDate, CreatedBy)
    SELECT x.MemberId, x.Scope, x.P, x.T, x.Q, x.QQ, 1, DATEADD(second, 30, p.Activa), p.T, p.Activa, x.Por
    FROM escala.Pares p
    CROSS APPLY (VALUES (p.E, 1, 0, 1, 1, 0, p.Email),
                        (p.T, 1, p.Puntos, 0, 0, 0, N'member-points'),
                        (p.T, 2, 0, 0, 0, 1, N'qualified-count')) x(MemberId, Scope, P, T, Q, QQ, Por)
    WHERE p.Directo = 1;
    SET @f = @@ROWCOUNT;
    ALTER INDEX ALL ON MemberStatisticDeltas REBUILD WITH (SORT_IN_TEMPDB = ON);
END
IF N'$(Tabla)' = N'Avisos'
BEGIN
    ALTER INDEX IX_MemberNotifications_MemberProfileId ON MemberNotifications DISABLE;
    -- El aviso DOWNLINE_ENROLLED del alta: la linea del patrocinador hasta 10 niveles.
    INSERT INTO MemberNotifications WITH (TABLOCK) (MemberId, EventType, Title, Body, FirebaseMessageId, IsDelivered, DeliveredAt, IsRead, ReadAt,
                                                    MemberProfileId, CreationDate, CreatedBy)
    SELECT E, N'DOWNLINE_ENROLLED', N'New Enrollment in Your Team', N'A new ambassador has enrolled in your downline.',
           NULL, 0, NULL, 0, NULL, NULL, DATEADD(millisecond, -800, Alta), N'system'
    FROM escala.Pares;
    SET @f = @@ROWCOUNT;
    ALTER INDEX ALL ON MemberNotifications REBUILD WITH (SORT_IN_TEMPDB = ON);
END
CHECKPOINT;
DECLARE @msg nvarchar(300) = CONCAT(DATEDIFF(second, @inicio, SYSDATETIME()), N' s: f. $(Tabla) (', @f, N' filas)');
RAISERROR(@msg, 0, 1) WITH NOWAIT;
