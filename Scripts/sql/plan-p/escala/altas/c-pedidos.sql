/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: Orders, OrderDetails, MembershipSubscriptions y SubscriptionBillingStates (despues de b: FK a MemberProfiles).
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
-- ============================================================================================================
-- 5. Pedido, suscripcion, cobro (Entrax), facturacion recurrente, comisiones y cuentas atras.
-- ============================================================================================================
SET XACT_ABORT ON;
DECLARE @lo int = 1, @tope int = (SELECT MAX(n) FROM escala.Altas), @lote int = 100000;
WHILE @lo <= @tope
BEGIN
    BEGIN TRAN;
    INSERT INTO Orders (Id, MemberId, MembershipSubscriptionId, TotalAmount, Status, OrderDate, Notes, CreationDate, CreatedBy,
                        LastUpdateDate, LastUpdateBy, IsDeleted, OrderNo)
    SELECT m.OrderId, m.MemberId, m.SubId, m.Total, 4, m.Alta, CONCAT(N'Ambassador signup ', NCHAR(8212), N' ', m.NombreNivel),
           DATEADD(millisecond, 170, m.Alta), m.Email, m.Activa, m.Email, 0, m.OrderNo
    FROM escala.Altas m WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    INSERT INTO OrderDetails (OrderId, ProductId, Quantity, UnitPrice, CreationDate, CreatedBy)
    SELECT m.OrderId, x.ProductId, 1, x.Precio, DATEADD(millisecond, 410, m.Alta), m.Email
    FROM escala.Altas m
    CROSS APPLY (VALUES (m.Producto, m.Precio),
                        (N'00000005-prod-0000-0000-000000000005', m.Total - m.Precio)) x(ProductId, Precio)
    WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    -- Plan Q (DQ16): la activa aporta los puntos de su pedido (el producto de membresia; Lifestyle Ambassador da 0), lo que
    -- deja MembershipPointsDeltas al activar; PersonalContribution sigue a 0, como en las altas reales.
    INSERT INTO MembershipSubscriptions (Id, MemberId, MembershipLevelId, ChangeReason, SubscriptionStatus, StartDate, EndDate, RenewalDate,
                                         IsFree, IsAutoRenew, LastOrderId, CreationDate, CreatedBy, LastUpdateDate, LastUpdateBy, IsDeleted,
                                         DualTeamContribution, EnrollmentContribution, PersonalContribution)
    SELECT m.SubId, m.MemberId, m.Nivel, 0, 1, m.Activa, DATEADD(month, 1, m.Activa), DATEADD(month, 1, m.Activa),
           ml.IsFree, ml.IsAutoRenew, m.OrderId, DATEADD(millisecond, 170, m.Alta), m.Email, m.Activa, m.Email, 0, m.Puntos, m.Puntos, 0
    FROM escala.Altas m JOIN MembershipLevels ml ON ml.Id = m.Nivel
    WHERE m.n BETWEEN @lo AND @lo + @lote - 1;

    INSERT INTO SubscriptionBillingStates (Id, MembershipSubscriptionId, MemberId, RecurringBillingPlanId, BillingAnchorDate, NextBillingDate,
                                           CurrentAttemptIndex, NextAttemptDate, Status, CreationDate, CreatedBy, LastUpdateDate, IsDeleted, Purpose)
    SELECT LOWER(CONVERT(nvarchar(36), NEWID())), m.SubId, m.MemberId, rp.RecurringBillingPlanId, m.Activa, DATEADD(day, 30, m.Activa),
           0, DATEADD(day, 30, m.Activa), 1, m.Activa, m.Email, m.Activa, 0, 1
    FROM escala.Altas m
    CROSS APPLY (SELECT TOP 1 pp.RecurringBillingPlanId FROM RecurringBillingPlanProducts pp
                 JOIN RecurringBillingPlans pl ON pl.Id = pp.RecurringBillingPlanId AND pl.IsActive = 1 AND pl.IsAnnualFeePlan = 0
                 WHERE pp.ProductId = m.Producto ORDER BY pp.Id) rp
    WHERE m.n BETWEEN @lo AND @lo + @lote - 1;
    COMMIT;
    SET @lo += @lote;
END
DECLARE @f bigint = (SELECT COUNT_BIG(*) FROM Orders WHERE MemberId LIKE N'ESC-%');
EXEC #marca N'c. Pedidos, lineas, suscripciones y facturacion recurrente', @f;
GO
