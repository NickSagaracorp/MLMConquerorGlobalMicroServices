# Decisiones de rendimiento: pedidos y árboles

**Estado:** aprobado por Nick el 2026-10-17. Lo implementa el Plan M (`docs/superpowers/plans/2026-10-17-plan-m-rendimiento-pedidos-y-arboles.md`).

**Alcance:** base de datos `MLMConquerorGlobalEditionDb` y todos los servicios que la usan (SignupAPI, AdminAPI, BizCenter, Billing, CommissionEngine, RankEngine, TicketManagementSystem, SharedAPICenter).

---

## 1. Contexto

El sistema tiene que soportar **millones de miembros**. Las mediciones en la base local (unos 119.000 miembros y 289.000 pedidos) ya mostraban tres problemas:

| Síntoma | Causa |
|---|---|
| Billing tarda unos 9,5 s en cada comprobación de número de pedido único al arrancar, y bloquea las altas de SignupAPI (*deadlocks*). | `Orders.OrderNo` es `nvarchar(max)` y no tiene índice. Los seis bucles de unicidad (`AnyAsync`) recorren la tabla entera. |
| El reporte *Actividad por fechas* tarda unos 6 s con 3 meses. | Para saber el nivel de cada inscrito hay que encontrar su orden de alta. `Orders.MemberId` y `OrderDetails.OrderId` son `nvarchar(max)` y no tienen índice, así que se recorren las dos tablas. |
| Cada consulta de equipo lee el árbol completo. | `GenealogyTree.HierarchyPath` y `DualTeamTree.HierarchyPath` son `nvarchar(max)` sin índice. El `StartsWith(prefijo)` (`LIKE 'x%'`) obliga a SQL Server a leer todas las filas. |

**No hay bucles en C#.** Cada consulta es una sola sentencia SQL. Lo que se recorre es la tabla, dentro del motor, porque no hay índice que usar.

**Por qué no basta con «crear índices»:** SQL Server no permite indexar columnas `nvarchar(max)`. Antes de indexar hay que acotar su longitud.

---

## 2. Decisiones

### D1. Acotar e indexar `Orders`

- `Id`, `MemberId` y `OrderNo` pasan a una longitud acotada y coherente con sus valores:
  - `Id` es un GUID en texto.
  - `MemberId` es el código del miembro (`AMB-123456`) y debe medir lo mismo que `MemberProfiles.MemberId`.
  - `OrderNo` tiene la forma `TA-E1017-064656681-TNQC`.
- **Índice único en `OrderNo`.** Las comprobaciones de unicidad pasan de recorrer la tabla entera a un *seek*. Antes de crear el índice hay que tratar los duplicados que haya.
- **Índice `(MemberId, OrderDate)`**, con las columnas incluidas que necesiten los reportes y Billing.

### D2. Acotar e indexar `OrderDetails` y corregir sus relaciones

- `OrderDetails.OrderId` es el GUID en texto del pedido (`Orders.Id`), no un valor numérico. Se acota junto con `ProductId`, y se crea el índice `(OrderId) INCLUDE (ProductId, …)`.
- La relación `Order` 1–N `OrderDetails` se configura de forma explícita sobre `OrderId`.
- La columna en sombra `OrdersId` está vacía en todas las filas. Se elimina junto con su índice, que hoy no sirve para nada.
- `PaymentHistories` tiene el mismo problema y se corrige igual.
- Antes de crear las claves foráneas se comprueba que no haya filas huérfanas.

### D3. Árbol de inscripción con `hierarchyid`

El legacy MWR Life usaba `hierarchyid`. El sistema nuevo lo había sustituido por una ruta de texto. Se recupera.

- Se añade a `GenealogyTree` una columna `hierarchyid`. Lleva un índice en profundidad y, si alguna consulta lo necesita, otro en anchura por nivel.
- Las consultas de equipo leen un **rango del índice en profundidad**: desde el nodo del miembro hasta su hermano siguiente (`GenealogySubtree.Under` y `CurrentUnder`, este último solo con el nodo vigente de cada miembro). Así leen solo las filas del equipo, sea de diez personas o de un millón.
  - **No se usa `IsDescendantOf` como filtro.** Medido en el Plan M: con parámetro, SQL Server no lo convierte en *seek* y estima una fila para cualquier equipo; contar 119.400 filas tardaba 9 s frente a 46 ms con el rango. Una regla de pruebas lo prohíbe.
  - El rango es exacto porque los ordinales son siempre enteros; lo garantiza el CHECK `CK_GenealogyTree_Node_OrdinalesEnteros`.
  - Las consultas del equipo llevan `OPTION (RECOMPILE)` (interceptor `RecompileTaggedQueriesInterceptor`), para que un equipo pequeño y uno de cien mil no compartan plan.
  - SQL Server no admite índices filtrados sobre `hierarchyid`: el índice único es `(Node, NodeNullKey)`.
  - Las altas bajo un mismo patrocinador se serializan con `sp_getapplock` (`GenealogyChildrenGate`).
- **Por qué cabe:** el árbol de inscripción es **ancho y poco profundo**. Cada patrocinador tiene muchos inscritos directos, así que la profundidad se mide en decenas de niveles, aunque haya millones de miembros. Un `hierarchyid` de ese tamaño ocupa decenas de bytes, muy por debajo del máximo de 892.
- **Cómo se mantiene:** el alta, las colocaciones y los seeders tienen que escribir el nodo. Una prueba de regla vigila a todo el código que escribe en el árbol, igual que hoy `RutasConBarraFinalTests` vigila las rutas. Las colocaciones que mueven un subárbol recalculan sus nodos con `GetReparentedValue`.
- **Transición en dos pasos:**
  1. Plan M: se añade el nodo, se rellena y todas las consultas pasan a usarlo.
  2. Un plan posterior retira `HierarchyPath`, cuando nada dependa de ella.
- El cálculo inicial es un script aparte, idempotente y que aguanta un corte (`Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql`): nivel a nivel, por lotes, y termina con error si queda algún nodo sin `Node` o incoherente. Se despliega en una sola ventana con los servicios parados: migraciones (`sqlcmd -b -I`) → relleno → arranque. En local tarda unos 35 s.
- **Fallar cerrado:** si un miembro no tiene `Node`, los cálculos que persisten puntos de equipo no escriben nada y lo registran, en vez de escribir 0.

**Resultado** (base local, equipo de AMB-700829 con 119.403 personas): Actividad 6 ms, Clientes 233 ms, Eventos 798 ms, Mi equipo 592 ms, comprobación de `OrderNo` 1 ms.

### D4. Árbol binario: `hierarchyid` solo si la profundidad lo permite

- El binario tiene los mismos miembros que el de inscripción, pero **cada nodo admite solo dos hijos**. Por eso es mucho más profundo, y el derrame forma piernas larguísimas. En la base local ya hay ramas de unos **930 niveles** (rutas de hasta 10.275 caracteres).
- Un `hierarchyid` crece con cada **nivel**, no con cada miembro: unos pocos bits por nivel en un binario. A 930 niveles ronda los 600–700 bytes, cerca del máximo de **892 bytes**. Una pierna de 1.500 o 2.000 niveles, normal en binarios grandes, no cabría.
- **Decisión:** el Plan M mide la profundidad real y el tamaño resultante.
  - Si cabe con margen: `hierarchyid` también en el binario.
  - Si no: **consulta recursiva (CTE) en SQL** sobre `ParentMemberId`, que ya tiene índice. Funciona a cualquier profundidad y se ejecuta en el servidor de base de datos.
- En ambos casos, los reportes binarios reducen primero por pierna (izquierda o derecha) y por fecha o estado, y solo después comprueban la pertenencia.
- **Resultado de la medición** (2026-10-17, con `hierarchyid::Parse` en SQL Server):
  - **Binario:** cada nivel ocupa 5 bits, así que el máximo son unos 1.427 niveles (892 B). La rama más honda en local ya tiene 934 niveles (584 B, el 65 % del límite) con solo 119.000 miembros, y crece con ellos. **El binario usa la CTE** (`MAXRECURSION 32767`, tope de 20.000 filas; por encima de eso o con la raíz se sigue reduciendo primero por pierna y fecha).
  - **Inscripción:** con el reparto real (hasta 990 inscritos directos por patrocinador, unos 2,25 B por nivel) admite unos 396 niveles. En el peor caso teórico, con 100.000 inscritos directos en cada nivel, admite unos 165. Hoy hay 8 niveles.

### D5. Índices de las consultas más usadas

Se revisan las consultas de reportes (Plan J), del procesador recurrente de Billing, del alta y de comisiones. Se crean los índices que falten, **cada uno justificado por la consulta que lo usa**, sin crear índices de más.

Índices que ya existen por el Plan J:

| Tabla | Índice |
|---|---|
| `DailyResidualEarnings` | `(BeneficiaryMemberId, EarnedDate) INCLUDE (Amount, Status)` |
| `MemberProfiles` | `(SponsorMemberId, EnrollDate)` |
| `MemberProfiles` | `(EnrollDate)` |
| Tablas de historial de estados | Por miembro y fecha |

### D6. `READ_COMMITTED_SNAPSHOT` activado

- **Aprobado por Nick.** Las lecturas de los reportes ven la última versión confirmada y no bloquean a las escrituras de facturación y altas, ni se bloquean por ellas.
- **No va en una migración de EF.** `ALTER DATABASE … SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE` necesita acceso exclusivo y cortaría las conexiones de los ocho servicios al arrancar. Es un **paso de despliegue explícito**: script versionado, servicios parados y comprobación en `sys.databases`.
- **Coste:** el *version store* vive en `tempdb`, que hay que dimensionar y vigilar.
- **Riesgo que hay que revisar antes:** cualquier lógica que dependa de que una lectura se bloquee deja de excluir con RCSI. El Plan M revisa los candados y reclamos atómicos (`MemberFundsGate`, `SignupOrderClaim`, `ExecuteUpdate` condicionales, `UPDLOCK`/`HOLDLOCK`) y confirma que siguen siendo correctos:
  - Los `UPDATE … WHERE` condicionales y los candados explícitos siguen funcionando.
  - Un «leo y luego decido» sin candado, no.
- **Resultado de la revisión** (Plan M):
  - Siguen siendo correctos con RCSI: `MemberFundsGate`, `SignupOrderClaim`, los `ExecuteUpdate` condicionales y los `MERGE … WITH (HOLDLOCK)`.
  - Dejan de excluir dos casos, que son **condiciones previas a activar RCSI en producción**:
    1. **Colocación en el binario.** Ya hay 939 huecos repetidos en `DualTeamTree`: el mismo padre y el mismo lado con más de un hijo. `PlacementService` bloquea por patrocinador, y hoy la exclusión real sale de que la lectura espera. Antes de activar RCSI hay que reparar esos huecos y crear el índice único `(ParentMemberId, Side)`. Va en un plan aparte.
    2. **Webhook de pagos.** Busca y luego inserta sin índice único, así que dos avisos iguales podrían crear dos pagos. Hace falta el índice único `(GatewayName, GatewayTransactionId)` en `PaymentHistories`.
  - En local se puede activar ya (con los servicios parados), para probar.

---

## 3. Garantías comunes a todas las migraciones

- **Nunca truncan.** Cada cambio de longitud lleva una guarda que lanza `THROW` si algún valor no cabe (`DATALENGTH(col)/2 > N`). La migración se detiene sin tocar datos.
- **Corren al arrancar** en los ocho servicios (`MigrateAsync`). Hay alteraciones e índices sobre tablas grandes, así que se despliegan en una ventana tranquila y con un solo servicio arrancando primero.
- **Se miden antes y después en local:**
  - *Actividad por fechas* con la raíz;
  - *Clientes* y *Eventos* con «todo el equipo»;
  - el bucle de números de pedido de Billing.

  Objetivo: los reportes por debajo de 3 s con la raíz y la comprobación de número de pedido en milisegundos.

---

## 4. Hallazgos que bloquean la escala (fuera del Plan M)

- **Formato de `MemberId`.** El código `AMB-######` tiene seis dígitos, así que se agota en 1.000.000 de miembros. Con el objetivo de millones hay que ampliar el formato (más dígitos o un prefijo distinto) antes de llegar a ese volumen.
- **Huecos repetidos en el binario** (ver D6): además de bloquear RCSI, son un error de datos que afecta a los puntos de pierna y a las comisiones binarias.
- **Equipos binarios de más de 20.000 nodos y la raíz** siguen leyéndose por `HierarchyPath`, que no tiene índice. También van por la ruta el Mi equipo binario del Admin, `NetworkController` y las colocaciones. El bono de coche carga en memoria a todos los miembros activos. Nada de esto escala a millones; queda para un plan aparte.
- **Webhook de pagos sin autenticación:** `WebhooksController` no comprueba firma ni origen; cualquiera puede marcar un pedido como pagado. Existía antes del Plan M; necesita plan propio.

## 5. Lo que queda fuera

- Retirar `HierarchyPath` del árbol de inscripción: plan posterior.
- Una closure table para el binario: solo si ni `hierarchyid` ni la CTE dan el rendimiento necesario.
- Particionar tablas: no hace falta con los volúmenes previstos tras los índices; se reevaluará con datos reales.

---

## 6. Referencias

- Plan J, riesgos R5, R9 y R16: `docs/superpowers/plans/2026-10-16-plan-j-reportes-bizcenter.md`
- Pruebas de seguridad §12: `docs/deployment/pruebas-de-seguridad.md`
- Helper de prefijo de árbol: `MLMConquerorGlobalEdition.Repository/Queries/TreePrefix.cs`
- Recorrido recursivo existente: `MLMConquerorGlobalEdition.Repository/Services/Teams/LegPointMaintenance.cs`
