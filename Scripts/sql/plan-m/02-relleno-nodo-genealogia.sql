/*
Plan M, Tarea 10 (M14, R1). Rellena GenealogyTree.Node a partir de ParentMemberId.
- Idempotente: solo toca filas con Node NULL cuyo padre ya tiene Node, o que son raices (sin padre o
  con un padre que no existe). Cada pasada avanza un nivel; se puede cortar y relanzar.
- Por lotes de @Lote filas, cada lote en su transaccion.
- Ordinales como GenealogyNodes.NextChildAsync: ultimo ordinal del grupo + ROW_NUMBER por (CreationDate, Id).
- Si un alta simultanea ocupa un Node, el lote falla (indice unico) y basta con relanzar.
- En local: solo MLMConquerorGlobalEditionDb. En produccion: ventana con poco trafico, tras la migracion
  NodoDeGenealogia y ANTES del despliegue 2 (Bloque 3).
- Como ejecutarlo (siempre con -b, que detiene sqlcmd en el primer error):
    sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -i Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql
*/
-- sqlcmd arranca con QUOTED_IDENTIFIER OFF. GenealogyTree tiene indices sobre columnas calculadas
-- (NodeLevel, NodeNullKey) y un UPDATE con esa opcion apagada falla con 1934. Se fijan en su propio
-- lote porque QUOTED_IDENTIFIER se aplica al compilar.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51034, N'Este script es solo para MLMConquerorGlobalEditionDb.', 1;

DECLARE @Lote int = 50000, @Hechas int = 1, @Total int = 0;

WHILE @Hechas > 0
BEGIN
    BEGIN TRAN;

    DROP TABLE IF EXISTS #lote;

    SELECT TOP (@Lote) c.Id, c.CreationDate, pp.MemberId AS PadreId, pp.Node AS PadreNode
    INTO   #lote
    FROM   GenealogyTree c
    OUTER APPLY (SELECT TOP (1) p.MemberId, p.Node
                 FROM GenealogyTree p
                 WHERE p.MemberId = c.ParentMemberId
                 ORDER BY p.CreationDate DESC) pp           -- nodo mas reciente del padre (R8 del Plan J)
    WHERE  c.Node IS NULL
      AND  (pp.MemberId IS NULL OR pp.Node IS NOT NULL);    -- raiz, huerfano o padre ya rellenado

    SET @Hechas = @@ROWCOUNT;

    IF @Hechas > 0
    BEGIN
        ;WITH grupos AS (
            SELECT DISTINCT l.PadreId, ISNULL(l.PadreNode, hierarchyid::GetRoot()) AS PadreNode
            FROM #lote l
        ), ultimos AS (
            SELECT g.PadreId, g.PadreNode,
                   (SELECT MAX(h.Node) FROM GenealogyTree h
                    WHERE h.Node IS NOT NULL
                      AND h.Node.GetAncestor(1) = g.PadreNode
                      AND (g.PadreId IS NULL OR h.ParentMemberId = g.PadreId)) AS Ultimo
            FROM grupos g
        ), nuevos AS (
            SELECT l.Id, u.PadreNode,
                   ISNULL(CAST(REVERSE(SUBSTRING(REVERSE(u.Ultimo.ToString()), 2,
                          CHARINDEX('/', REVERSE(u.Ultimo.ToString()), 2) - 2)) AS int), 0)
                   + ROW_NUMBER() OVER (PARTITION BY u.PadreId ORDER BY l.CreationDate, l.Id) AS Ordinal
            FROM #lote l
            JOIN ultimos u ON u.PadreId = l.PadreId OR (u.PadreId IS NULL AND l.PadreId IS NULL)
        )
        UPDATE g
        SET    g.Node = hierarchyid::Parse(n.PadreNode.ToString() + CAST(n.Ordinal AS varchar(12)) + '/')
        FROM   GenealogyTree g
        JOIN   nuevos n ON n.Id = g.Id
        WHERE  g.Node IS NULL;

        SET @Total += @@ROWCOUNT;
    END

    COMMIT;
    RAISERROR(N'Rellenadas %d filas en total', 0, 1, @Total) WITH NOWAIT;
END

-- Condicion de despliegue del Bloque 3 (R17): las dos cifras a 0.
SELECT
  (SELECT COUNT(*) FROM GenealogyTree WHERE Node IS NULL) AS SinNode,
  (SELECT COUNT(*) FROM GenealogyTree c
   OUTER APPLY (SELECT TOP (1) p.Node FROM GenealogyTree p
                WHERE p.MemberId = c.ParentMemberId ORDER BY p.CreationDate DESC) pp
   WHERE c.Node IS NOT NULL
     AND ((pp.Node IS NULL AND c.Node.GetLevel() <> 1)
       OR (pp.Node IS NOT NULL AND c.Node.GetAncestor(1) <> pp.Node))) AS Incoherentes;
