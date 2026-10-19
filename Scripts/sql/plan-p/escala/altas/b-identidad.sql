/*
Plan P, Bloque D. Generador de altas completas en MLMConquerorGlobalEditionDb_Escala: AspNetUsers, AspNetUserRoles y MemberProfiles.
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
-- 4. Identity, perfil, inscripcion.
-- ============================================================================================================
SET XACT_ABORT ON;
DECLARE @hash nvarchar(200) = (SELECT Hash FROM escala.Par), @rol nvarchar(450) = (SELECT RolAmbassador FROM escala.Par);
DECLARE @lo int = 0, @tope int = (SELECT MAX(n) FROM escala.Altas), @lote int = 100000;
WHILE @lo <= @tope
BEGIN
    BEGIN TRAN;
    INSERT INTO AspNetUsers (Id, MemberProfileId, IsActive, LastLoginAt, RefreshToken, RefreshTokenExpiry, CreationDate, CreatedBy,
                             UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp,
                             ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled,
                             AccessFailedCount, PreferredTwoFactorChannel, TwoFactorPhoneConfirmed)
    SELECT m.AspId, m.MemberId, 1, NULL, NULL, NULL, m.Alta, m.Email,
           m.Email, UPPER(m.Email), m.Email, UPPER(m.Email), 1, @hash, REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''),
           LOWER(CONVERT(nvarchar(36), NEWID())), NULL, 0, 0, NULL, 1, 0, 0, 0
    FROM escala.Altas m WHERE m.n BETWEEN @lo AND @lo + @lote - 1 AND m.n > 0;
    INSERT INTO AspNetUserRoles (UserId, RoleId)
    SELECT m.AspId, @rol FROM escala.Altas m WHERE m.n BETWEEN @lo AND @lo + @lote - 1 AND m.n > 0;

    INSERT INTO MemberProfiles (Id, UserId, MemberId, FirstName, LastName, DateOfBirth, Phone, WhatsApp, Country, State, City, Address,
                                ZipCode, BusinessName, ShowBusinessName, MemberType, Status, EnrollDate, SponsorMemberId, ReplicateSiteSlug,
                                ProfilePhotoUrl, IsNamePublic, IsEmailPublic, IsPhonePublic, CreationDate, CreatedBy, LastUpdateDate,
                                LastUpdateBy, IsDeleted, Email, DefaultLanguage, PayoutFrequency, KeepQualified, FeesFromBalance)
    SELECT m.ProfileId, m.UserGuid, m.MemberId, m.Nombre, m.Apellido, DATEADD(day, m.n % 15000, '1960-01-01'),
           CONCAT(N'+1305', RIGHT(CONCAT(N'0000000', m.n), 7)), NULL, N'US', N'FL', N'Miami', CONCAT(m.n, N' Ocean Dr'),
           N'33139', NULL, 0, 0, 1, m.Alta, s.MemberId, CASE WHEN m.n = 0 THEN N'escala' ELSE CONCAT(N'esc', m.n) END,
           NULL, 0, 0, 0, DATEADD(millisecond, 170, m.Alta), CASE WHEN m.n = 0 THEN N'SYSTEM' ELSE m.Email END,
           CASE WHEN m.n = 0 THEN DATEADD(millisecond, 170, m.Alta) ELSE m.Activa END, CASE WHEN m.n = 0 THEN NULL ELSE m.Email END,
           0, ISNULL(m.Email, N''), N'en', ISNULL((SELECT TOP 1 DefaultPayoutFrequency FROM CompanyInfo), 2), 0, 1
    FROM escala.Altas m LEFT JOIN escala.Altas s ON s.n = m.GenPadre
    WHERE m.n BETWEEN @lo AND @lo + @lote - 1;
    COMMIT;
    SET @lo += @lote;
END
DECLARE @f bigint = (SELECT COUNT_BIG(*) FROM MemberProfiles WHERE MemberId LIKE N'ESC-%');
EXEC #marca N'4a. AspNetUsers, AspNetUserRoles y MemberProfiles', @f;
GO
