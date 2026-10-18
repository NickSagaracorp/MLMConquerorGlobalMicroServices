# Diagnóstico: huecos repetidos en el árbol binario (`DualTeamTree`)

Fecha: 2026-10-18. Base: `MLMConquerorGlobalEditionDb` local, sobre HEAD `f5f0f21`. Solo se han hecho lecturas con `SELECT`. Las pruebas de reproducción corren en una transacción que se deshace (comprobado: 0 filas `RPH%` después).

Un binario admite un solo hijo por `(ParentMemberId, Side)`. En local hay **939 huecos con más de un hijo vivo**, que suman **45.182 filas**. Todos están en el lado izquierdo (`Side = 0`). Hay **44.243 intrusos**: todas las filas salvo la más antigua de cada hueco. Sus subárboles suman **96.033 nodos**, el 80 % del árbol. Cada hueco tiene entre 2 y 256 hijos.

---

## 0. Veredicto por origen

«Intruso» es una fila que ocupa un hueco que ya tenía hijo: el segundo y siguientes por `(CreationDate, Id)`. La que llegó primero está bien colocada aunque luego otras se le pusieran al lado.

| Origen (`CreatedBy`) | Filas en huecos | De ellas, intrusas | Quién escribió | Veredicto |
|---|---|---|---|---|
| `rank-climb` | 44.339 | 44.119 | Un `INSERT` masivo hecho a mano, que **nunca se subió al repositorio** | **(a)** No existe en el código actual. Nada en la BD impide que otro `INSERT` directo lo repita, y eso lo cierra el índice único (Bloque A). |
| `placement-service` | 205 | 116 | `PlacementService.PlaceBulkAsync`, el mismo código que hay hoy (sin cambios desde el 6-jun 16:48) | **(b) Sigue presente.** Lo usan `AutoPlacementJob` (producción, cada 6 h), `run-auto-now` y los `place-bulk`/`force-place-all` de desarrollo. Reproducido con 3 pruebas. |
| `force-place` | 47 | **0** | El antiguo `DevSeedController.PlaceMemberAsync` (`5d12257^`) | **No causó ningún hueco**: sus 47 filas son las primeras de su hueco y los intrusos llegaron después (46 de `rank-climb` y 1 de `placement-service`). Su sustituto actual, `force-place-all`, llama a `PlaceBulkAsync` y hereda el fallo **(b)**. |
| `distributed-wave` | 121 | **0** | `place_members.sql` (raíz del repositorio) | **No causó ningún hueco**: comprueba con `NOT EXISTS` que el lado esté libre y sus 121 filas son todas las primeras de su hueco. Los intrusos son 116 de `rank-climb` y 5 de `AMB-…`. Aun así se retira (Bloque A). |
| `system` | 4 | 3 | La primera versión de `AutoPlacementJob` (`dab8b9e`) | **(a)** Se corrigió en `f2683d6` (28-abr) con un `SaveChanges` por miembro. **Pero el mismo tipo de fallo vuelve** con `PlaceBulkAsync`, que deja sin guardar hasta 1.000 colocaciones (ver `placement-service`). Además `RunAutoPlacementHandler` (AdminAPI) tiene hoy exactamente el fallo de `dab8b9e` **(b)**. |
| `AMB-…` (el miembro se coloca a sí mismo) | 466 | 5 | `SignupAPI` `PlaceMemberHandler`, llamado por los scripts `Scripts/backfill-*.ps1` | **(b)** El manejador sigue sin candado, sin transacción y sin índice único que lo frene: dos peticiones simultáneas eligen el mismo hueco. Lo que disparó estos 5 casos sin carrera **no se puede demostrar** con los datos (ver §1.6). |

Además, **dos escritores que no aparecen en los datos tienen hoy el mismo fallo (b)**: `RunAutoPlacementHandler` (AdminAPI) y la comprobación de lado ocupado sin candado de `BizCenter PlaceMemberHandler` y `AdminPlaceMemberHandler` (ver §2).

---

## 1. Mecanismo por origen

### 1.1 `rank-climb`: inserción directa sin buscar hueco (no está en el repositorio)

- **Firma en los datos.** Las 44.339 filas cumplen tres cosas. El padre es el patrocinador (`ParentMemberId = MemberProfiles.SponsorMemberId` en el 100 %). El lado es siempre `Side = 0`. Ninguna tiene `PlacementLog`.
- **Seis instantes exactos.** `2026-06-03 14:14:26.695`, `20:51:29.355`, `06-05 11:56:31.748`, `13:18:20.662`, `13:54:42.371` y `22:52:15.607`. El mayor reúne 17.293 filas. Seis instantes son seis `INSERT … SELECT` con una variable `@Now`.
- **Los miembros.** Son altas de prueba (`lt.…@example.com`) del generador `Scripts/SignupLoadTest`, hechas unas horas antes de cada lote. Por ejemplo, los perfiles del lote de las 14:14 se crearon entre las 07:23 y las 07:44.
- **Los patrocinadores.** `Scripts/SignupLoadTest/climb-left18.txt` los lista, y coinciden con los huecos más grandes: AMB-908172, AMB-428673, AMB-915687, AMB-116666 y AMB-139876. Hay 844 padres distintos.
- **Historial.** `git log -S"rank-climb"` solo encuentra comentarios en `5d12257`: `PlacementService.cs:37` y `ApplyMemberStatisticDeltasJob.cs:41`. Ningún fichero del historial ni del disco contiene la sentencia `INSERT`; se ejecutó a mano con `sqlcmd`.
- **Mecanismo.** Un generador de datos que inserta como hijo izquierdo del patrocinador sin mirar si ese lado está ocupado.
- **Por qué es (a).** El código no está en el repositorio. Lo que sí sigue igual es que la BD no lo impide: no hay índice único `(ParentMemberId, Side)`. Lo cierra el índice del Bloque A.

### 1.2 `placement-service`: `PlaceBulkAsync`, el código actual

**Evidencia de que es el código de hoy.** En la copia `C:\Users\sagar\source\repos\ClaudeRepository`, `PlacementService.cs` tiene fecha del **2026-06-06 16:48** y `DualTeamPlacementRules.cs` del 16:47. Las dos son idénticas a las de HEAD. La primera fila `placement-service` es de las 17:33 de ese día. Las filas del 6 y 7 de junio vienen en bloques de exactamente 1.000 con el mismo `CreationDate`, que es un `SaveChanges` por bloque: `BulkSaveChunk = 1000` en `PlacementService.cs:46`, guardado en `:379`.

**Quién lo llama en producción.**
- `AutoPlacementJob.ExecuteAsync`, en `BizCenter/Jobs/AutoPlacementJob.cs:82`. Está programado cada 6 h en `BizCenter/Program.cs:459-463`.
- `POST /api/v1/bizcenter/admin/placement/run-auto-now`, en `BizCenter/Controllers/AdminPlacementController.cs:27-32`.
- En desarrollo, `DevSeedController.cs:73` (`place-bulk`) y `:420` (`force-place-all`).
- **`PlaceAsync` no lo llama nadie.** El candado por patrocinador que cita la documentación (R6 del Plan M, D6) **no protege el camino que se usa de verdad**.

**Los 116 intrusos se explican con tres fallos de `PlaceBulkAsync`.** Todos están en el código actual.

1. **Un derrame sin cola cae en el lado ocupado del propio patrocinador.** Explica 69 intrusos con patrocinador = padre.
   - `DualTeamPlacementRules.cs:39`: `var parent = deepestNodeId ?? sponsorMemberId;` y `:40`: `side = Left` cuando `deepestNodeHasLeftChild` es `false`.
   - En la ruta masiva, el estado de cada patrocinador se siembra una sola vez (`PlacementService.cs:399-428`). `DeepestId` solo se calcula si el patrocinador **ya tenía los dos lados** al sembrar (`:418`).
   - Sea un patrocinador P que tiene hijo izquierdo y no derecho. El 1.er patrocinado de la tanda va a la derecha (regla b), lo que **no** avanza la cola, porque el lado preferido de P es el izquierdo (`:370-377`). El 2.º patrocinado ve los dos lados ocupados y `DeepestId = null`. `Decide` devuelve entonces `(P, Left)`, que ya está ocupado.
   - Los datos lo confirman. En los 15 casos más grandes, el hijo derecho de P y el intruso izquierdo tienen el **mismo** `CreationDate`, es decir, entraron en el mismo bloque (consulta en §4).
   - Reproducido en `Bulk_dos_patrocinados_con_izquierdo_ya_ocupado_repite_el_hueco`.
   - **Hay una prueba que da este comportamiento por bueno.** `Domain.Tests/DualTeamPlacementRulesTests.cs:55-63`, `Decide_WhenSponsorFullButNoCachedTail_FallsBackToSponsor`, lo documenta como «Defensive» y comprueba que el padre sea el patrocinador. El Plan P la sustituye por una que exige fallar.
2. **Una cola que ya tiene hijo izquierdo recibe otro izquierdo.**
   - `PlacementService.cs:351-352` fija `deepestNodeHasLeftChild: false`, con el comentario «In-batch the deepest node is always a freshly stacked chain tail». Eso es falso cuando la cola viene de la BD.
   - `ResolveDeepestAsync` da por buena la cola cacheada si tiene menos de 2 hijos (`:187-189`), y la BFS elige nodos con menos de 2 hijos (`:216-220`). En los dos casos la cola puede tener ya un hijo izquierdo, por ejemplo uno que patrocinó ella misma (regla a).
   - Reproducido con la caché en `Bulk_cola_cacheada_con_hijo_izquierdo_recibe_otro_izquierdo`.
3. **El estado de un patrocinador no ve lo que la propia tanda ha añadido.** Explica los 47 intrusos por derrame y los casos «derrame → regla a».
   - Cada patrocinador tiene su `LegState` en memoria, sembrado desde la BD con `AsNoTracking` (`:405-416`). Esa lectura no ve las filas añadidas y aún sin guardar, que pueden ser hasta 1.000.
   - Cuando el derrame de P cae bajo L, el estado de L no se entera. Si después llega un patrocinado de L, su estado se siembra sin el hijo nuevo y repite el hueco.
   - Lo mismo pasa cuando dos patrocinadores comparten cola. En local, `DualTeamLegFrontiers` tiene colas compartidas por 3 patrocinadores (por ejemplo AMB-243549 y AMB-919583), y las dos derraman bajo la misma cola.
   - Reproducido en `Bulk_derrame_de_un_patrocinador_y_alta_directa_de_otro_repiten_el_hueco`.

**Agravantes en el mismo código.**
- `PlaceBulkAsync` **no abre transacción ni toma candado** (`:305-397`). Dos ejecuciones simultáneas, por ejemplo el job recurrente y `run-auto-now`, trabajan cada una con su estado en memoria. Si falla a mitad, los bloques ya guardados se quedan y devuelve `Failure` (`:393-396`). Los puntos de pierna de esos bloques no se aplican: `:386` no llega a ejecutarse.
- `PlaceAsync`, aunque nadie lo use, bloquea por **patrocinador** (`:81-83`) y no por el hueco destino. Dos patrocinadores distintos que derraman al mismo nodo, o el derrame de S bajo D a la vez que una regla a de D, no se esperan. Hoy les salva por accidente que la lectura espere a la inserción sin confirmar (D6). Con RCSI, ni eso.
- La BFS de autocuración (`:194-223`) hace **una consulta por nodo**. Con 400.000 nodos en la pierna son 400.000 viajes a la BD.
- El desempate de la BFS no es determinista: `:211-214` lee los hijos sin `ORDER BY`.

**Escenario mínimo que lo reproduce hoy en producción.** P ya tiene hijo izquierdo. Dos personas patrocinadas por P cumplen su ventana de 30 días antes de la misma ejecución de `AutoPlacementJob`. La primera queda a la derecha de P y la segunda a la izquierda de P, que ya estaba ocupada.

### 1.3 `force-place`: no causó huecos

- **El código vigente el 3 de junio.** Era `DevSeedController.cs` de `5d12257^`, líneas 267-447.
  - Lee los dos lados del patrocinador (`:348-354`) y elige hueco: izquierda, luego derecha y luego el nodo más profundo con hueco (`FindDeepestSlotAsync`, `:403-447`).
  - Guarda después de cada miembro (`:300-301`).
  - No toma candado, y su propio comentario reconoce que competía con `AutoPlacementJob` (`DevSeedController.cs:406-410` actual).
- **Datos.** Sus 47 filas son las primeras de su hueco. Los intrusos de esos huecos son posteriores: 46 de `rank-climb` y 1 de `placement-service`. No ha generado ningún intruso.
- **Hoy.** `force-place-all` (`DevSeedController.cs:401-427`) delega en `PlaceBulkAsync` y hereda el fallo de §1.2.

### 1.4 `distributed-wave`: no causó huecos

- `place_members.sql:49-64` elige el nodo menos profundo sin hijo izquierdo y, si no lo hay, el menos profundo sin hijo derecho, siempre con `NOT EXISTS (… c.Side = …)`. Todo va en una sola transacción.
- **Datos.** Sus 121 filas son las primeras de su hueco. Los intrusos son posteriores: 116 de `rank-climb` y 5 de `AMB-…`.
- **Defectos que no causan huecos.**
  - Usa `SYSUTCDATETIME()` (`:28`), cuando la regla es la hora del servidor. Por eso sus filas dicen 21:56: es UTC, y en hora local eran las 17:56.
  - Filtra con `HierarchyPath LIKE` (`:51`).
  - Recalcula los puntos de todo el árbol con un `LIKE` cruzado de coste O(N²) (`:106-122`).
- Se retira en el Bloque A.

### 1.5 `system`: guardado único al final de `AutoPlacementJob`, corregido

- **Datos.** Cuatro hijos izquierdos bajo ROOT001, todos con el mismo instante (`2026-04-26 09:23:44.296`) y un `PlacementLog` `AutoPlaced` de `system`.
- **Código vigente.** `AutoPlacementJob.cs` de `dab8b9e` (6-abr) recorre los miembros (`:80`) leyendo los hijos con `AsNoTracking` y hace **un solo `SaveChanges` al final** (`:96`). Ninguna lectura ve las colocaciones anteriores de la misma ejecución, así que todos van a la izquierda.
- **Corrección.** `f2683d6` (28-abr) guarda después de cada miembro (`:94-96`, con el comentario «avoids 2+ members landing on the same side of a parent»). En `5d12257` se sustituyó por `PlaceBulkAsync`, **que vuelve a dejar hasta 1.000 colocaciones sin guardar** y reintroduce el fallo de otra forma (§1.2, fallo 3).

### 1.6 `AMB-…`: `SignupAPI PlaceMemberHandler`, sin candado

- **Datos.** 466 filas en huecos, con `CreatedBy` = el propio miembro. Es el `PlaceMemberHandler` de SignupAPI (`:94`, `:107`), al que llaman los scripts `Scripts/backfill-*.ps1` con un padre y un lado explícitos (`backfill-both-fast.ps1:120-122`). 461 son las primeras de su hueco.
- **Los 5 intrusos** son del 24 de mayo y están todos sobre un hijo de `distributed-wave` del día anterior: AMB-147382, AMB-511044, AMB-759688, AMB-886312 y AMB-960705. Los `PlacementLogs` (Id 1185 y 1189 frente a 2288 y siguientes) confirman ese orden.
- **El código actual del manejador** (igual al del 23-may salvo la comprobación `ALREADY_PLACED`, `:64-65`):
  - comprueba el hueco pedido por `ParentMemberId` (`:187-188`);
  - si está ocupado, carga el subárbol por `HierarchyPath.StartsWith(rootPath)` (`:195-199`) y decide qué lados hay ocupados **a partir de esa instantánea** (`:203-225`);
  - no abre transacción ni toma candado, y guarda con un `SaveChanges` suelto (`:131`).
- **Lo que no se puede demostrar.** Con ese código, una sola petición solo repite el hueco si la instantánea por ruta no incluye al hijo existente, es decir, si su ruta no empezaba por la del padre (Bug 10, véase `Scripts/sql/fix-malformed-dualteam-paths.sql`). También lo repite si el proceso que corría era un binario anterior a `11fb19e`. La versión de `dee027b` (`:50-77`) insertaba bajo el padre pedido **sin comprobar nada**. Hoy las rutas son coherentes (0 incoherencias), así que no se puede saber cuál de las dos pasó.
- **Por qué es (b).** El manejador actual sigue con «comprobar y luego insertar» sin candado y sin índice único. Dos peticiones simultáneas para el mismo hueco, o una que compite con `AutoPlacementJob`, insertan las dos.

---

## 2. Todos los escritores de `DualTeamTree`

Se buscó `DualTeamTree.Add/AddAsync/Remove/Update`, `ExecuteUpdate/Delete`, SQL crudo `INSERT/UPDATE/DELETE … DualTeamTree` y asignaciones a `ParentMemberId`, `Side`, `HierarchyPath` e `IsDeleted` en todo el código que no es de pruebas ni migraciones.

| Escritor | Qué escribe | Comprobación del hueco | Candado / transacción | Veredicto |
|---|---|---|---|---|
| `PlacementService.PlaceBulkAsync` (`Repository/Services/Teams/PlacementService.cs:305-397`) | Inserta | Estado en memoria por patrocinador, sembrado con `AsNoTracking`; `deepestNodeHasLeftChild: false` fijo | **Ninguno**, `SaveChanges` cada 1.000 | **(b)** §1.2 |
| `PlacementService.PlaceAsync` (`:61-127`) | Inserta | Lee los hijos dentro del candado | `sp_getapplock` por **patrocinador** y transacción | **(b)**: candado por el recurso equivocado. Sin llamadores hoy. |
| `RunAutoPlacementHandler` (`AdminAPI/Features/Placement/RunAutoPlacement/RunAutoPlacementHandler.cs`), `POST` en `AdminAPI/Controllers/AdminPlacementController.cs:82` | Inserta | Lee con `AsNoTracking` (`:121-127`, `:158-164`) dentro del bucle (`:66-111`) | **Ninguno**, un `SaveChanges` al final (`:113`) | **(b)**: es el mismo fallo de `dab8b9e`. Dos patrocinados del mismo patrocinador en una ejecución van al mismo lado. Usa además otra regla de hueco (BFS por anchura, `:148-177`). |
| `SignupAPI PlaceMemberHandler` (`SignupAPI/Features/Placement/Commands/PlaceMember/PlaceMemberHandler.cs`) | Inserta | `:187-188` y la instantánea por ruta en `:195-225` | **Ninguno** (`:131`) | **(b)** §1.6 |
| `BizCenter PlaceMemberHandler` (`BizCenter/Features/Placement/PlaceMember/PlaceMemberHandler.cs`) | Inserta o mueve el subárbol | `slotOccupied` en `:78-85`, **fuera** de la transacción | Transacción en `:114`, sin candado | **(b)**: dos colocaciones manuales al mismo hueco, o una contra el job, pasan las dos la comprobación. |
| `AdminPlaceMemberHandler` (`AdminAPI/Features/Placement/AdminPlaceMember/AdminPlaceMemberHandler.cs`), la colocación forzada del admin | Inserta o mueve el subárbol | `:49-54`, fuera de la transacción | Transacción en `:71`, sin candado | **(b)**, por el mismo motivo. |
| `AdminRemovePlacementHandler` (`:34-104`), `BizCenter RemovePlacementHandler` (`:74`), `SignupAPI UnplaceMemberHandler` (`:47`) | Borran el nodo (`Remove` es borrado físico) y, en el admin, reescriben las rutas | — | Transacción (salvo Unplace) | No crean huecos repetidos. Dejan hijos con `ParentMemberId` hacia un nodo que ya no existe (huérfanos por adyacencia): hoy hay 0. Hay que tenerlo en cuenta en la clave nueva (Bloque B). |
| `SignupAmbassadorHandler:285-298`, `RootAmbassadorSeeder:80-119`, raíz del patrocinador en `PlacementService:139-143` y `:336-347` | Insertan raíces | `ParentMemberId = NULL` | — | No crean huecos. Sí crean **varias raíces**: hay 3 (ROOT001, AMB-700829 y AMB-552697, esta de `force-place`). |
| `LegPointMaintenance.ApplyIncrementalAsync` (`:62-68`) y `DualTeamPointsRecalculator` (`:37-40`) | Solo puntos | — | — | No tocan la estructura. Ver el impacto en §3. |
| `DevSeedController` (`place-bulk` `:55-79`, `force-place-all` `:401-427`, `backfill-frontier` `:86+`) | Por `PlaceBulkAsync` | — | — | Heredan **(b)**. |
| `place_members.sql` (raíz del repositorio) | `INSERT` directo | `NOT EXISTS` por lado | Transacción | No causó huecos. Se retira. |
| `Scripts/backfill-*.ps1`, `promote-to-*.ps1` | Llaman a SignupAPI | El hueco lo calcula el script sobre una instantánea | — | Dependen de §1.6. |

`SignupActivationService`, la reactivación, `ApplyMemberStatisticDeltasJob` y `MemberStatisticSnapshotJob` **no escriben** en `DualTeamTree` (solo leen).

**¿Está protegido `DevSeedController`?**
- El controlador **existe en todos los entornos**, porque `MapControllers` lo registra (`AdminAPI/Program.cs:376`). Cada una de sus 10 acciones empieza con `if (!_env.IsDevelopment()) return NotFound();` (`:59, :89, :183, :253, :312, :360, :404, :441, :506, :563`), así que fuera de Development responde 404.
- **No tiene `[Authorize]`**, y AdminAPI no define una política por defecto que lo exija. En Development es anónimo, incluido `seed-superadmin`.
- Si un despliegue arrancara por error con `ASPNETCORE_ENVIRONMENT=Development`, quedaría abierto. El Bloque A lo cambia por un filtro único a nivel de clase, con una prueba que lo comprueba.

---

## 3. Comprobaciones con SQL (BD local)

| Comprobación | Resultado |
|---|---|
| Filas, vivas y borradas | 119.433 filas, todas vivas (0 con `IsDeleted = 1`) |
| `Side` | Solo hay 0 (117.335 filas) y 1 (2.098). Coincide con `TreeSide { Left, Right }`. El árbol está muy cargado a la izquierda por la regla de la cadena más profunda. |
| Huérfanos (`ParentMemberId` que no existe) | 0 |
| Raíces | 3: ROOT001 (25 nodos), AMB-700829 (119.404) y AMB-552697 (4) |
| Miembros con varios nodos vivos | 0 (el índice único filtrado de `MemberId` existe) |
| Ciclos | 0 nodos con `ParentMemberId = MemberId`, y ningún nodo cuyo padre no esté en su propia ruta |
| `HierarchyPath` coherente con el padre (`ruta del padre + Id + '/'`) | 0 incoherencias, ni en los intrusos ni en todo el árbol. Las raíces también están bien. |
| ¿Tienen la misma ruta los hijos de un mismo hueco? | **No** (0 pares). Cada uno tiene `…/P/Id/`, así que sus subárboles **no se mezclan** en los filtros por prefijo de `P`. Todos caen dentro del prefijo de `P` y ninguno dentro del prefijo de otro hermano. |
| Profundidad | Máxima 934, media 123. La ruta más larga ocupa 10.275 caracteres (20.550 bytes). |
| Intrusos con hijos | 562 de 44.243. Sus subárboles suman 96.033 nodos. Hay 11 padres de hueco que están dentro del subárbol de un intruso (huecos anidados). |
| `DualTeamLegFrontiers` | 2.118 filas. Varias colas son compartidas por 2 o 3 patrocinadores. |
| RCSI en local | Apagado (`is_read_committed_snapshot_on = 0`) |

Consultas empleadas (solo lectura). La de detección vale tal cual para producción:

```sql
-- Huecos repetidos
SELECT ParentMemberId, Side, COUNT(*) AS Hijos, MIN(CreationDate) AS Primero, MAX(CreationDate) AS Ultimo
FROM DualTeamTree
WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL
GROUP BY ParentMemberId, Side
HAVING COUNT(*) > 1
ORDER BY Hijos DESC;

-- Firma del fallo 1 de PlaceBulkAsync: el intruso izquierdo y el hijo derecho de P en el mismo bloque
WITH d AS (
    SELECT t.*, ROW_NUMBER() OVER (PARTITION BY ParentMemberId, Side ORDER BY CreationDate, Id) AS rn,
           COUNT(*) OVER (PARTITION BY ParentMemberId, Side) AS c
    FROM DualTeamTree t WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL)
SELECT d.ParentMemberId, d.MemberId, d.CreationDate,
       (SELECT MIN(r.CreationDate) FROM DualTeamTree r WHERE r.ParentMemberId = d.ParentMemberId AND r.Side = 1) AS DerechoDesde
FROM d JOIN MemberProfiles mp ON mp.MemberId = d.MemberId AND mp.SponsorMemberId = d.ParentMemberId
WHERE d.c > 1 AND d.rn > 1 AND d.CreatedBy = 'placement-service';

-- Integridad
SELECT COUNT(*) FROM DualTeamTree c WHERE c.IsDeleted = 0 AND c.ParentMemberId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0);   -- huérfanos
SELECT COUNT(*) FROM DualTeamTree c JOIN DualTeamTree p ON p.MemberId = c.ParentMemberId
WHERE c.HierarchyPath <> p.HierarchyPath + c.MemberId + '/';                                              -- rutas
```

---

## 4. Impacto

- **Puntos de pierna.** `LegPointMaintenance.ApplyIncrementalAsync` sube por `ParentMemberId` (`:41-60`). Un intruso suma sus puntos en la pierna izquierda de P y de todos sus antepasados, así que los puntos de pierna **incluyen todos los subárboles del hueco**.
  - `DualTeamPointsRecalculator` (`:61-63`) suma solo el subárbol del **primer hijo** que devuelve `FirstOrDefault(ParentMemberId == P && Side == side)`, sin orden, es decir, uno cualquiera de los 256.
  - Cada miembro tiene hoy los puntos del último escritor que pasó por él (incremental, recalculador, el `LIKE` de `place_members.sql`…). Son valores no reproducibles.
- **Comisiones binarias y rangos.** `CalculateDailyResidualHandler` califica y paga con `MemberStatistics.DualTeamPoints = Left + Right` (`:68`, `:112-127`, `:144`). En local hay 415.338 filas en `DailyResidualEarnings` (del 12-may al 16-oct) calculadas sobre este árbol. Los rangos con base en el Dual Team (Titanium y superiores) y el bono presidencial leen los mismos puntos.
- **Mi equipo y conteos.**
  - El filtro por prefijo (`DualTeamService.cs:156-158`, `AdminMemberDualTeamController.cs:99`, `NetworkController.cs:44`) y la CTE de `DualSubtree` **incluyen** a todos los hijos del hueco. Los conteos de P cuentan bien a las personas, pero el árbol que se dibuja no es binario.
  - `NetworkController.cs:79-80` calcula la pierna con `Any` sobre los hijos izquierdos, y con 256 hijos es O(n²).
  - La «pierna» de un descendiente se deduce del lado del hijo directo, que en los huecos es ambiguo.
- **Colocación.** `DualTeamLegFrontier` y la BFS toman el número de hijos como señal de hueco libre (`< 2`). Con duplicados, un nodo con dos hijos izquierdos «está lleno» y su lado derecho no se usa nunca.
- **RCSI en producción.** Sigue bloqueado por R6 hasta reparar y crear el índice único.

---

## 5. Propuesta de reparación de datos

### 5.1 La regla de derrame del sistema

`DualTeamPlacementRules` (`Domain/Services/DualTeamPlacementRules.cs:8-14`) y `PlacementService` (`:19-23`):

- **a)** Si el patrocinador no tiene hijo izquierdo, a la IZQUIERDA del patrocinador.
- **b)** Si tiene izquierdo y no derecho, a la DERECHA del patrocinador.
- **c)** Si tiene los dos, al nodo **más profundo** con hueco del **lado preferido** del patrocinador (su propio `Side`; IZQUIERDA si es raíz). Dentro de ese nodo, izquierda primero y luego derecha, lo que va formando la cadena.

### 5.2 Regla de reparación propuesta (determinista)

1. **Quién se queda el hueco.** El hijo más antiguo por `(CreationDate, Id)`: el guardián K. En los 939 huecos eso deja a K en su sitio y a los primeros escritores correctos (`force-place`, `distributed-wave`, las 461 filas `AMB-…`) donde estaban.
2. **Dónde van los demás.** Cada intruso I baja **con todo su subárbol** dentro de la misma pierna (P, izquierda). Va al nodo con hueco más profundo del subárbol de K, como en la regla c, aplicada a la pierna de P.
   - **Orden.** Primero los huecos con P menos profundo (la profundidad se recalcula en cada lote, porque los movimientos la cambian y hay 11 huecos anidados). Dentro de un hueco, los intrusos por `(CreationDate, Id)`.
   - **Destino.** El nodo más profundo del subárbol de K con menos de 2 hijos, y dentro de él izquierda si está libre y si no derecha. Empate en profundidad: el de menor `(CreationDate, Id)`. La BFS del motor no desempata de forma determinista (`PlacementService.cs:211-214`); la reparación fija este criterio.
   - **Cómo se calcula.** El nodo más profundo de un subárbol siempre es una hoja. Tras colgar I bajo el destino T, el nuevo destino es la hoja más profunda del subárbol de I. Si I es una hoja, el nuevo destino es I mismo. Así no hace falta recorrer el subárbol de K en cada movimiento: una CTE por hueco para el primer destino y otra pequeña por intruso.
3. **Rutas.** Se recalcula `HierarchyPath` del subárbol movido sustituyendo el prefijo `…/P/I/` por `ruta(T) + I + '/'`, en el mismo lote. La clave compacta (Bloque B) aún no existe en ese momento y la rellena después su propio script.
4. **Puntos.** Al terminar se recalculan desde cero todas las piernas: suma de abajo arriba, nivel a nivel, de `MemberStatistics.PersonalPoints`, la misma semántica que `LegPointMaintenance`. También se recalcula `MemberStatistics.DualTeamPoints`. No basta con la versión incremental, porque los valores actuales salen de algoritmos mezclados. Mover dentro de la pierna de P no cambia el total de P ni de sus antepasados; sí sube los puntos de los nodos entre K y cada destino.
5. **Caché de colas.** Se vacía `DualTeamLegFrontiers` y se reconstruye con el script del Bloque B.

### 5.3 El script

Va en `Scripts/sql/plan-p/01-reparar-huecos-binario.sql`, con el código completo en el Plan P, Tarea 3.
- **Guardas.** Tres, como `Scripts/sql/plan-m/01-orderno-duplicados.sql`: `DB_NAME()`, `-v Confirmo=LOCAL` y `SERVERPROPERTY('MachineName') = HOST_NAME()`.
- **Ejecución.** Con `sqlcmd -b -I`.
- **Lotes.** **Un hueco por lote y una transacción por lote**, con `UPDLOCK, HOLDLOCK` sobre los hijos del hueco. Es idempotente: cada lote vuelve a buscar el siguiente hueco repetido y, si ya no queda ninguno, no hace nada. Aguanta un corte, porque cada lote confirmado deja el árbol coherente.
- **Informe.** Antes y después: huecos, filas, intrusos por origen, profundidad máxima, huérfanos, rutas incoherentes y número de raíces.
- **Modo `-v SoloInforme=1`.** Simula todos los lotes dentro de una sola transacción que se deshace e informa de la **profundidad máxima resultante**. El script se niega a aplicar si superaría 12.000 niveles, el margen bajo el límite de la clave compacta (13.567).
- **Puntos y caché.** Se recalculan en `02-recalcular-piernas.sql`, con las mismas guardas, nivel a nivel y una transacción por nivel.

**Profundidad esperada.** La suma de intrusos por la cadena de antepasados de cada hueco da una cota inferior de **≈ 1.481 niveles** (hueco de AMB-971956). Las colas compartidas pueden apilar más, y la cifra real la da `SoloInforme`. Si pasa de 12.000, Nick tiene que elegir otra regla (ver 5.4).

### 5.4 Decisiones que necesitan a Nick

1. **Regla del destino.** La regla del motor (lo más profundo, 5.2) o la colocación menos profunda de la pierna, que es la que usan `place_members.sql` y SignupAPI y que mantiene el árbol bajo. La del motor es la «oficial», pero alarga la cadena.
2. **Quién se queda el hueco.** El más antiguo por `(CreationDate, Id)` (propuesta) u otro criterio, por ejemplo el que tenga más volumen.
3. **Producción.**
   - Antes de nada, ejecutar allí la consulta de detección de §3, que es de solo lectura.
   - `AutoPlacementJob` usa `PlaceBulkAsync` desde `5d12257`. Si producción ya tiene ese código desplegado, **puede tener huecos repetidos de tipo `placement-service`**.
   - Si los tiene, hay **comisiones binarias (`DailyResidualEarnings`) y rangos calculados sobre puntos inflados o arbitrarios**. Nick decide si se recalculan y regularizan los pagos ya hechos o si la reparación solo corrige de aquí en adelante.
   - Lo mismo con los avances de rango que dependen de `DualTeamPoints`.
4. **Datos locales de carga.** En local los intrusos son casi todos miembros sintéticos (`lt.…@example.com`). Nick puede preferir regenerar la base local antes que repararla. La reparación sigue haciendo falta para producción y como prueba del script.
5. **Puntos tras la reparación.** Algunos miembros verán cambiar sus puntos de pierna, hacia arriba o hacia abajo frente al valor guardado. Hay que decidir si se avisa o se registra la diferencia, por ejemplo en una tabla de auditoría de la reparación.

---

## 6. Pruebas de reproducción

En `C:\Users\sagar\AppData\Local\Temp\claude\…\scratchpad\huecos\` (sin commit): `Huecos.Repro.csproj` y `ReproHuecosTests.cs`. Son 5 pruebas en verde contra la BD local, cada una en una transacción que se deshace.

| Prueba | Demuestra |
|---|---|
| `Decide_sin_cola_devuelve_el_hueco_izquierdo_ocupado_del_patrocinador` | `DualTeamPlacementRules.cs:39`: con los dos lados ocupados y sin cola, devuelve `(P, Left)` |
| `Bulk_dos_patrocinados_con_izquierdo_ya_ocupado_repite_el_hueco` | Fallo 1 en `PlaceBulkAsync`: 2 hijos en P-izquierda |
| `Bulk_derrame_de_un_patrocinador_y_alta_directa_de_otro_repiten_el_hueco` | Fallo 3: estado en memoria que no ve la tanda; 2 hijos en L-izquierda |
| `Bulk_cola_cacheada_con_hijo_izquierdo_recibe_otro_izquierdo` | Fallo 2: `deepestNodeHasLeftChild: false` con la cola cacheada; 2 hijos en L-izquierda |
| `Bulk_cola_con_hijo_izquierdo_recibe_otro_izquierdo` | Control: sin caché, la BFS sí acierta. El fallo 2 aparece con una cola cacheada que ya tiene hijo. |

Se ejecutan con `dotnet test` desde esa carpeta. El Plan P las convierte en pruebas permanentes que esperan **0** huecos, en rojo antes de la corrección.
