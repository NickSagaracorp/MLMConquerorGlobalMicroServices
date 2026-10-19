/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: EntraxPayments, EntraxApiCalls, PaymentHistories, CommissionCountDowns, CommissionEarnings, SignupRiskFingerprints y MemberStatistics (despues de c).
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
IF OBJECT_ID('escala.Altas') IS NULL
    THROW 51073, N'Falta la preparacion (a-preparar.sql).', 1;
GO
SET NOCOUNT ON;
IF OBJECT_ID('tempdb..#t') IS NOT NULL DROP TABLE #t;
CREATE TABLE #t (Paso nvarchar(100) NOT NULL, Segundos int NOT NULL, Filas bigint NULL, Inicio datetime2 NULL);
INSERT INTO #t VALUES (N'inicio', 0, NULL, SYSDATETIME());
GO
CREATE OR ALTER PROCEDURE #marca @paso nvarchar(100), @filas bigint = NULL AS
BEGIN
    DECLARE @s int = DATEDIFF(second, (SELECT Inicio FROM #t WHERE Paso = N'inicio'), SYSDATETIME());
    INSERT INTO #t (Paso, Segundos, Filas) VALUES (@paso, @s, @filas);
    DECLARE @msg nvarchar(300) = CONCAT(@s, N' s: ', @paso, CASE WHEN @filas IS NULL THEN N'' ELSE CONCAT(N' (', @filas, N' filas)') END);
    RAISERROR(@msg, 0, 1) WITH NOWAIT;
    CHECKPOINT;
END
GO
SET XACT_ABORT ON;
DECLARE @lo int = 1, @tope int = (SELECT MAX(n) FROM escala.Altas), @lote int = 100000;
WHILE @lo <= @tope
BEGIN
    BEGIN TRAN;
    INSERT INTO EntraxPayments (Id, OrderId, MemberId, ExternalOrderId, ExternalPaymentId, Status, AmountUsd, AmountRub, ExchangeRate,
                                PaidAmountUsd, QrLink, ExpiresAt, PaidAt, Consumed, ActivatedAt, LastCheckedAt, CreationDate, CreatedBy,
                                LastUpdateDate, LastUpdateBy, IsDeleted)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.OrderId, m.MemberId, CONCAT(N'SIGNUP-', m.OrderId, N'-1'), m.PagoExt, 2, m.Total,
           ROUND(m.Total * 95.5, 2), 95.5, m.Total, CONCAT(N'https://entrax.invalid/fake/', m.PagoExt), DATEADD(minute, 15, m.Alta),
           m.Activa, 1, m.Activa, m.Activa, DATEADD(millisecond, 800, m.Alta), m.Email, m.Activa, N'Entrax', 0
    FROM escala.Altas m WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    INSERT INTO EntraxApiCalls (Operation, ExternalOrderId, ExternalPaymentId, HttpStatusCode, Success, ErrorCode, ProviderStatus, DurationMs, CreationDate, CreatedBy)
    SELECT x.Op, CONCAT(N'SIGNUP-', m.OrderId, N'-1'), m.PagoExt, x.Http, 1, NULL, x.Estado, x.Ms, DATEADD(millisecond, x.Desfase, m.Alta), ISNULL(x.Por, m.Email)
    FROM escala.Altas m
    CROSS APPLY (VALUES (N'CreateQr', 200, N'pending', 1, 810, CAST(NULL AS nvarchar(50))),
                        (N'GetPayment', 200, N'paid', 1, 1050, N'Entrax:Poll'),
                        (N'Callback', 0, N'SUCCESS', 0, 1060, N'Entrax:Webhook')) x(Op, Http, Estado, Ms, Desfase, Por)
    WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    INSERT INTO PaymentHistories (Id, OrderId, MemberId, Amount, GatewayName, GatewayTransactionId, TransactionStatus, ProcessedAt,
                                  CreationDate, CreatedBy, LastUpdateDate, LastUpdateBy, IsDeleted)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.OrderId, m.MemberId, m.Total, N'Entrax', m.PagoExt, 3, m.Activa,
           m.Activa, N'Entrax', m.Activa, N'Entrax', 0
    FROM escala.Altas m WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    INSERT INTO CommissionCountDowns (Id, MemberId, MemberId1, FastStartBonus1Start, FastStartBonus1End, FastStartBonus2Start, FastStartBonus2End,
                                      FastStartBonus3Start, FastStartBonus3End, CreationDate, CreatedBy, LastUpdateDate, IsDeleted,
                                      FastStartBonus1ExtendedStart, FastStartBonus1ExtendedEnd)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.UserGuid, m.ProfileId, m.Alta, DATEADD(day, 7, m.Alta), DATEADD(day, 7, m.Alta),
           DATEADD(day, 14, m.Alta), DATEADD(day, 14, m.Alta), DATEADD(day, 21, m.Alta), DATEADD(millisecond, 170, m.Alta), m.Email,
           DATEADD(millisecond, 170, m.Alta), 0, m.Alta, DATEADD(day, 14, m.Alta)
    FROM escala.Altas m WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    -- Bono de patrocinador (categoria 1) y Builder Bonus (6 y 7) al patrocinador directo, como en las altas reales
    -- de la base local (Elite: tipos 2 y 34). Sin rangos en la linea, los diferenciales no van a nadie mas.
    INSERT INTO CommissionEarnings (Id, BeneficiaryMemberId, SourceMemberId, SourceOrderId, CommissionTypeId, Amount, Status, EarnedDate,
                                    PaymentDate, PeriodDate, IsManualEntry, Notes, CreationDate, CreatedBy, LastUpdateDate, IsDeleted)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), s.MemberId, m.MemberId, m.OrderId, ct.Id, ct.Amount, 1, m.Activa,
           DATEADD(day, ct.PaymentDelayDays, m.Activa), CAST(m.Activa AS date), 0,
           CONCAT(N'Order ', m.OrderNo, N' ', NCHAR(8212), N' ', m.Nombre, N' ', m.Apellido, N' (', m.MemberId, N')'),
           m.Activa, m.Email, m.Activa, 0
    FROM escala.Altas m
    JOIN escala.Altas s ON s.n = m.GenPadre
    JOIN (VALUES (2, 1), (2, 33), (3, 2), (3, 34), (4, 2), (4, 3), (4, 34), (4, 38)) t(Nivel, Tipo) ON t.Nivel = m.Nivel
    JOIN CommissionTypes ct ON ct.Id = t.Tipo
    WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    INSERT INTO SignupRiskFingerprints (VisitorId, RequestId, Flow, OrderId, MemberId, SponsorReplicateSite, IpAddress, UserAgent, CountryIso2,
                                        IsFlagged, FlagReason, CreationDate, CreatedBy, Cleared)
    SELECT CONCAT(N'unknown-', REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')), REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), 0,
           NULL, NULL, CASE WHEN s.n = 0 THEN N'escala' ELSE CONCAT(N'esc', s.n) END,
           CONCAT(N'10.', m.n / 65536 % 256, N'.', m.n / 256 % 256, N'.', m.n % 256), N'Mozilla/5.0 (escala)', NULL, 0, NULL,
           DATEADD(millisecond, -500, m.Alta), N'anonymous-signup', 0
    FROM escala.Altas m JOIN escala.Altas s ON s.n = m.GenPadre
    WHERE m.n BETWEEN @lo AND @lo + @lote - 1;
    COMMIT;
    SET @lo += @lote;
END
DECLARE @f bigint = (SELECT COUNT_BIG(*) FROM PaymentHistories WHERE MemberId LIKE N'ESC-%');
EXEC #marca N'd. Cobros Entrax, cuentas atras, comisiones y huellas', @f;
GO
-- MemberStatistics con los deltas ya aplicados (ApplyMemberStatisticDeltasJob) y los puntos de pierna.
SET XACT_ABORT ON;
INSERT INTO MemberStatistics (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize, EnrollmentTeamSize, DualTeamPoints, EnrollmentPoints,
                              QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers, EnrollmentTeamGrowth, DualteamGrowth,
                              EnrollmentTeamPointsGrowth, DualTeamPointsGrowth, CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth,
                              CurrentYearIncomeGrowth, CreationDate, CreatedBy)
SELECT m.MemberId, m.Puntos, 0, 0, e.Equipo, CAST(p.IzqP + p.DerP AS int), CAST(m.Puntos + e.PuntosEquipo AS int), e.Directos, 0,
       0, 0, 0, 0, 0, 0, 0, ISNULL(m.Activa, m.Alta), ISNULL(m.Email, N'SYSTEM')
FROM escala.Altas m JOIN escala.Equipo e ON e.n = m.n JOIN escala.Piernas p ON p.n = m.n;
DECLARE @f bigint = @@ROWCOUNT;
EXEC #marca N'5b. MemberStatistics', @f;
GO
