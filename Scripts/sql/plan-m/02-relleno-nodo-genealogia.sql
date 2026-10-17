/*
Plan M, Tarea 10 (M14, R1), rehecho en la revision de las Tareas 6-13 (I1) para que escale a millones.
Rellena GenealogyTree.Node a partir de ParentMemberId con los MISMOS ordinales que el alta
(GenealogyNodes.NextChildAsync):
  - el padre es el nodo vigente del patrocinador: el vivo mas reciente, con desempate por Id;
  - el ordinal es el siguiente al ultimo hermano (vivos y borrados), en orden de (CreationDate, Id);
  - sin padre en el arbol (o sin patrocinador), una raiz nueva detras de la ultima raiz.

Como trabaja:
  0. Deshace lo incoherente que dejara una ejecucion interrumpida: pone Node = NULL en las filas cuyo
     Node no cuelga del nodo vigente de su padre, en bucle hasta que no quede ninguna (al soltar un
     padre, sus hijos pasan a ser incoherentes). Es seguro porque las altas estan paradas.
  1. Calcula los nodos en una tabla temporal estrecha #nodos(Id, MemberId, Node, Nivel), nivel a nivel.
     La primera pasada mira una vez las filas sin Node (raices nuevas e hijos de nodos ya rellenados en
     una ejecucion anterior); cada pasada siguiente lee SOLO los hijos de la anterior, por
     IX_GenealogyTree_ParentMemberId. No toca GenealogyTree hasta el paso 2.
  2. Aplica #nodos nivel a nivel y, dentro de cada nivel, por lotes de 4.000 filas en orden de Id,
     cada lote en su propia transaccion. Un padre se confirma siempre antes que sus hijos: si la
     ejecucion se corta, lo aplicado es coherente y el relanzado sigue desde ahi.
  3. ALTER INDEX IX_GenealogyTree_Node REBUILD y UPDATE STATISTICS GenealogyTree WITH FULLSCAN.
  4. Comprobacion de despliegue: SinNode = 0 e Incoherentes = 0. Si no, THROW 51035: con sqlcmd -b el
     despliegue se corta y los servicios no se arrancan.

Idempotente: el paso 0 solo suelta lo incoherente y el resto solo toca filas con Node NULL; relanzado
sin nada pendiente no cambia nada, y relanzado tras un corte termina coherente.

SE EJECUTA CON LAS ALTAS PARADAS: en la ventana de despliegue del Plan M, con todos los servicios
parados, despues del script de migraciones y antes de arrancarlos (ver "Condiciones de despliegue").
Un alta concurrente podria ocupar un ordinal calculado aqui.

RECUPERACION FULL: cada lote es una transaccion corta, pero el total escribe todo GenealogyTree y el
REBUILD reescribe el indice. Con el modelo de recuperacion FULL hay que mantener las copias del log
durante la ventana (o el log crece sin limite hasta la siguiente).

Como ejecutarlo (siempre con -b, que detiene sqlcmd en el primer error, e -I, QUOTED_IDENTIFIER ON):
    sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -i Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql
*/
-- GenealogyTree tiene indices sobre columnas calculadas (NodeLevel, NodeNullKey) y un CHECK con un
-- metodo de hierarchyid: con QUOTED_IDENTIFIER u otra de estas opciones apagada, un UPDATE falla
-- con 1934. Se fijan en su propio lote porque QUOTED_IDENTIFIER se aplica al compilar (sqlcmd sin -I
-- arranca con OFF).
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

DECLARE @Inicio datetime2 = SYSDATETIME();   -- solo para medir lo que tarda; hora de servidor
DECLARE @Nivel int = 0, @Nuevas int = 1, @Calculadas int = 0, @Aplicadas int = 0, @Lote int = 4000;

-- ── 0. Deshacer lo incoherente de una ejecucion interrumpida ────────────────────────────────────
DECLARE @Soltadas int = 1, @SoltadasTotal int = 0;
WHILE @Soltadas > 0
BEGIN
    UPDATE c
    SET    c.Node = NULL
    FROM   GenealogyTree c
    OUTER APPLY (SELECT TOP (1) p.Id, p.Node FROM GenealogyTree p
                 WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
                 ORDER BY p.CreationDate DESC, p.Id DESC) pp
    WHERE  c.Node IS NOT NULL
      AND ((pp.Id IS NULL AND c.Node.GetLevel() <> 1)                       -- sin padre vivo: debe ser raiz
        OR (pp.Id IS NOT NULL AND pp.Node IS NULL)                          -- su padre no tiene Node
        OR (pp.Node IS NOT NULL AND c.Node.GetAncestor(1) <> pp.Node));     -- cuelga de otro nodo
    SET @Soltadas = @@ROWCOUNT;
    SET @SoltadasTotal += @Soltadas;
END
IF @SoltadasTotal > 0
    RAISERROR(N'Paso 0: %d nodos incoherentes soltados (Node = NULL) para recalcularlos', 0, 1, @SoltadasTotal) WITH NOWAIT;

-- ── 1. Calculo en #nodos ─────────────────────────────────────────────────────────────────────────
DROP TABLE IF EXISTS #nodos;
CREATE TABLE #nodos (
    Id       nvarchar(450) NOT NULL PRIMARY KEY,
    MemberId nvarchar(450) NOT NULL,
    Node     hierarchyid   NOT NULL,
    Nivel    int           NOT NULL);
CREATE INDEX IX_nodos_Nivel ON #nodos (Nivel) INCLUDE (MemberId, Node);

-- Las filas a calcular en cada pasada, con su padre ya resuelto.
DROP TABLE IF EXISTS #pasada;
CREATE TABLE #pasada (
    Id           nvarchar(450) NOT NULL PRIMARY KEY,
    CreationDate datetime2     NOT NULL,
    PadreId      nvarchar(450) NULL,       -- MemberId del padre; NULL = raiz nueva
    PadreNode    hierarchyid   NOT NULL);  -- hierarchyid::GetRoot() para las raices

WHILE @Nuevas > 0
BEGIN
    TRUNCATE TABLE #pasada;

    IF @Nivel = 0
        -- Primera pasada: una lectura de las filas sin Node. Entran las raices nuevas (sin patrocinador
        -- o con uno que no esta vivo en el arbol) y los hijos de un nodo vigente que ya tiene Node.
        INSERT INTO #pasada (Id, CreationDate, PadreId, PadreNode)
        SELECT c.Id, c.CreationDate,
               CASE WHEN pp.Id IS NULL THEN NULL ELSE pp.MemberId END,
               ISNULL(pp.Node, hierarchyid::GetRoot())
        FROM GenealogyTree c
        OUTER APPLY (SELECT TOP (1) p.Id, p.MemberId, p.Node
                     FROM GenealogyTree p
                     WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
                     ORDER BY p.CreationDate DESC, p.Id DESC) pp
        WHERE c.Node IS NULL
          AND (pp.Id IS NULL OR pp.Node IS NOT NULL);
    ELSE
        -- Siguientes pasadas: solo los hijos de los nodos calculados en la pasada anterior, y solo si
        -- ese nodo es el vigente de su miembro (como en el alta).
        INSERT INTO #pasada (Id, CreationDate, PadreId, PadreNode)
        SELECT c.Id, c.CreationDate, prev.MemberId, prev.Node
        FROM #nodos prev
        JOIN GenealogyTree c ON c.ParentMemberId = prev.MemberId AND c.Node IS NULL
        WHERE prev.Nivel = @Nivel - 1
          AND prev.Id = (SELECT TOP (1) p.Id FROM GenealogyTree p
                         WHERE p.MemberId = prev.MemberId AND p.IsDeleted = 0
                         ORDER BY p.CreationDate DESC, p.Id DESC)
          AND NOT EXISTS (SELECT 1 FROM #nodos n WHERE n.Id = c.Id);

    -- Ordinales: el ultimo hermano ya guardado (vivos y borrados) mas ROW_NUMBER por (CreationDate, Id).
    ;WITH grupos AS (
        SELECT DISTINCT PadreId, PadreNode FROM #pasada
    ), ultimos AS (
        SELECT g.PadreId, g.PadreNode,
               CASE WHEN g.PadreId IS NULL
                    THEN (SELECT MAX(h.Node) FROM GenealogyTree h
                          WHERE h.NodeLevel = 1)                                   -- IX_GenealogyTree_NodeLevel_Node
                    ELSE (SELECT MAX(h.Node) FROM GenealogyTree h
                          WHERE h.ParentMemberId = g.PadreId AND h.Node IS NOT NULL
                            AND h.Node.GetAncestor(1) = g.PadreNode)               -- IX_GenealogyTree_ParentMemberId
               END AS Ultimo
        FROM grupos g
    )
    INSERT INTO #nodos (Id, MemberId, Node, Nivel)
    SELECT p.Id, c.MemberId,
           hierarchyid::Parse(u.PadreNode.ToString()
               + CAST(ISNULL(CAST(REVERSE(SUBSTRING(REVERSE(u.Ultimo.ToString()), 2,
                          CHARINDEX('/', REVERSE(u.Ultimo.ToString()), 2) - 2)) AS int), 0)
                      + ROW_NUMBER() OVER (PARTITION BY p.PadreId ORDER BY p.CreationDate, p.Id) AS varchar(12))
               + '/'),
           @Nivel
    FROM #pasada p
    JOIN GenealogyTree c ON c.Id = p.Id
    JOIN ultimos u ON (u.PadreId = p.PadreId) OR (u.PadreId IS NULL AND p.PadreId IS NULL);

    SET @Nuevas = @@ROWCOUNT;
    SET @Calculadas += @Nuevas;
    IF @Nuevas > 0
        RAISERROR(N'Nivel %d: %d nodos calculados', 0, 1, @Nivel, @Nuevas) WITH NOWAIT;
    SET @Nivel += 1;
END

DECLARE @MsCalculo int = DATEDIFF(MILLISECOND, @Inicio, SYSDATETIME());
RAISERROR(N'Calculo: %d ms', 0, 1, @MsCalculo) WITH NOWAIT;

-- ── 2. Aplicacion nivel a nivel y, dentro de cada nivel, por lotes en orden de Id ───────────────
-- Un padre (nivel n) se confirma siempre antes que sus hijos (nivel n + 1): un corte deja lo aplicado
-- coherente y el relanzado continua sin dejar subarboles colgando de un ordinal que cambie.
DECLARE @Desde nvarchar(450), @Hasta nvarchar(450), @Filas int, @NivelAplicado int = 0, @MaxNivel int;
SELECT @MaxNivel = MAX(Nivel) FROM #nodos;
WHILE @NivelAplicado <= ISNULL(@MaxNivel, -1)
BEGIN
    SET @Desde = N'';
    WHILE 1 = 1
    BEGIN
        SET @Hasta = NULL;
        SELECT @Hasta = MAX(Id) FROM (SELECT TOP (@Lote) Id FROM #nodos
                                      WHERE Nivel = @NivelAplicado AND Id > @Desde ORDER BY Id) x;
        IF @Hasta IS NULL BREAK;

        BEGIN TRAN;
        UPDATE g
        SET    g.Node = n.Node
        FROM   GenealogyTree g
        JOIN   #nodos n ON n.Id = g.Id
        WHERE  n.Nivel = @NivelAplicado AND n.Id > @Desde AND n.Id <= @Hasta AND g.Node IS NULL;
        SET @Filas = @@ROWCOUNT;
        COMMIT;

        SET @Aplicadas += @Filas;
        SET @Desde = @Hasta;
    END
    SET @NivelAplicado += 1;
END
RAISERROR(N'Calculados %d nodos, aplicados %d', 0, 1, @Calculadas, @Aplicadas) WITH NOWAIT;
DECLARE @MsAplicado int = DATEDIFF(MILLISECOND, @Inicio, SYSDATETIME());
RAISERROR(N'Calculo y aplicacion: %d ms', 0, 1, @MsAplicado) WITH NOWAIT;

-- ── 3. Indice y estadisticas ────────────────────────────────────────────────────────────────────
-- El indice se creo con todos los Node a NULL y el relleno lo deja fragmentado; las estadisticas
-- de entonces no sirven para estimar los rangos del equipo.
IF @Aplicadas > 0
BEGIN
    ALTER INDEX IX_GenealogyTree_Node ON GenealogyTree REBUILD;
    UPDATE STATISTICS GenealogyTree WITH FULLSCAN;
END

DECLARE @Ms int = DATEDIFF(MILLISECOND, @Inicio, SYSDATETIME());
RAISERROR(N'Tiempo total: %d ms', 0, 1, @Ms) WITH NOWAIT;

-- ── 4. Condicion de despliegue (R17): las dos cifras a 0; si no, se corta el despliegue ──────────
DECLARE @SinNode int, @Incoherentes int;
SELECT @SinNode = COUNT(*) FROM GenealogyTree WHERE Node IS NULL;
SELECT @Incoherentes = COUNT(*) FROM GenealogyTree c
OUTER APPLY (SELECT TOP (1) p.Node FROM GenealogyTree p
             WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
             ORDER BY p.CreationDate DESC, p.Id DESC) pp
WHERE c.Node IS NOT NULL
  AND ((pp.Node IS NULL AND c.Node.GetLevel() <> 1)
    OR (pp.Node IS NOT NULL AND c.Node.GetAncestor(1) <> pp.Node));

SELECT @SinNode AS SinNode, @Incoherentes AS Incoherentes;

IF @SinNode <> 0 OR @Incoherentes <> 0
BEGIN
    DECLARE @Msg nvarchar(200) = CONCAT(N'Relleno incompleto: SinNode = ', @SinNode, N', Incoherentes = ', @Incoherentes,
                                        N'. No arrancar los servicios (Plan M, condiciones de despliegue).');
    THROW 51035, @Msg, 1;
END
