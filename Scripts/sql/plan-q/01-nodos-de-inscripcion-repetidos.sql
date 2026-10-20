/*
Plan Q, Tarea 21 (DQ9). Deja un solo nodo vivo por miembro en GenealogyTree: los demas (R8 del Plan J) se marcan como
borrados (IsDeleted = 1, DeletedBy = 'plan-q-01'), por lotes de 5.000, cada lote en su transaccion. El vigente es el mas
reciente por (CreationDate, Id), el mismo criterio que GenealogySubtree.CurrentOfAsync. Idempotente. Va antes de la
migracion UnNodoPorMiembro, cuya guarda (51108) falla si quedan repetidos.
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -v SoloInforme=1 -i Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql
Con QUOTED_IDENTIFIER ON en su propio lote, como los scripts de los Planes M y P: las tablas con columnas calculadas
indexadas (MemberProfiles, tras BusquedaYNivelDeAlta) rechazan escrituras sin el, y sqlcmd sin -I lo deja apagado.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(Confirmo)' NOT IN (N'LOCAL', N'ESCALA')
    THROW 51120, N'Ejecutar con -v Confirmo=LOCAL (o ESCALA en la base de escala).', 1;
IF NOT ((N'$(Confirmo)' = N'LOCAL'  AND DB_NAME() = N'MLMConquerorGlobalEditionDb')
     OR (N'$(Confirmo)' = N'ESCALA' AND DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'))
    THROW 51121, N'Confirmo no corresponde a esta base.', 1;
IF N'$(SoloInforme)' NOT IN (N'0', N'1')
    THROW 51122, N'SoloInforme debe ser 0 o 1.', 1;

SELECT Id INTO #obsoletos
FROM (SELECT Id, ROW_NUMBER() OVER (PARTITION BY MemberId ORDER BY CreationDate DESC, Id DESC) AS rn
      FROM GenealogyTree WHERE IsDeleted = 0) v
WHERE v.rn > 1;
DECLARE @n int = @@ROWCOUNT;
DECLARE @msg nvarchar(200) = CONCAT(N'Nodos no vigentes a marcar: ', @n);
RAISERROR(@msg, 0, 1) WITH NOWAIT;
IF N'$(SoloInforme)' = N'1' RETURN;

DECLARE @hechos int = 1;
WHILE @hechos > 0
BEGIN
    BEGIN TRAN;
    UPDATE TOP (5000) g
    SET IsDeleted = 1, DeletedAt = GETDATE(), DeletedBy = N'plan-q-01'
    FROM GenealogyTree g JOIN #obsoletos o ON o.Id = g.Id
    WHERE g.IsDeleted = 0;
    SET @hechos = @@ROWCOUNT;
    COMMIT;
END
SET @msg = CONCAT(N'Quedan miembros con mas de un nodo vivo: ',
    (SELECT COUNT(*) FROM (SELECT MemberId FROM GenealogyTree WHERE IsDeleted = 0 GROUP BY MemberId HAVING COUNT(*) > 1) x));
RAISERROR(@msg, 0, 1) WITH NOWAIT;
