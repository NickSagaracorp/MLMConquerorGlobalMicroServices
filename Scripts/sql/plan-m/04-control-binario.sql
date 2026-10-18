/*
Plan M, revision M-c. Consulta de control (solo lectura) para la conciliacion del arbol binario.

DualSubtree (la CTE de Mi equipo del Dual Team) baja por ParentMemberId saltandose los nodos borrados:
un nodo borrado corta su rama y sus descendientes no salen. El camino antiguo por la ruta
(HierarchyPath LIKE prefijo%) solo quita el nodo borrado y sigue contando a sus descendientes mientras
su ruta conserve el prefijo. Lo correcto es lo de la CTE: un nodo borrado ya no esta en el arbol y lo
que colgaba de el no es alcanzable por la colocacion (al descolocar, sus descendientes quedan
"flotantes"; AdminRemovePlacement ademas les reescribe la ruta).

Las filas que salen aqui son las que cuentan distinto por los dos caminos: nodos vivos cuyo padre
esta borrado o no existe. Deberian ser 0, o explicarse una a una.

    sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -W -i Scripts/sql/plan-m/04-control-binario.sql
*/
SET NOCOUNT ON;

SELECT COUNT(*) AS VivosColgandoDeUnPadreBorradoOInexistente
FROM DualTeamTree c
WHERE c.IsDeleted = 0
  AND c.ParentMemberId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0);

SELECT TOP (20) c.MemberId, c.ParentMemberId, c.HierarchyPath,
       CASE WHEN EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = c.ParentMemberId) THEN 'padre borrado' ELSE 'padre inexistente' END AS Motivo
FROM DualTeamTree c
WHERE c.IsDeleted = 0
  AND c.ParentMemberId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0)
ORDER BY c.MemberId;
