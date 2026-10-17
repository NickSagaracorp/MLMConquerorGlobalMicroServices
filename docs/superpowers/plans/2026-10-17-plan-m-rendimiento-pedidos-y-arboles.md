# Plan M — Rendimiento de pedidos y árboles

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que los reportes del BizCenter y la facturación dejen de recorrer tablas enteras cuando haya millones de miembros. Para eso hay que acotar e indexar `Orders`, `OrderDetails` y `PaymentHistories`, pasar el árbol de inscripción a `hierarchyid` y recorrer el binario con una CTE recursiva en SQL. Además se activa `READ_COMMITTED_SNAPSHOT` como paso de despliegue.

**Architecture:** Hay tres migraciones de EF. Cada una lleva guardas que lanzan `THROW` antes de truncar o de crear una clave sobre datos que no la cumplen.
- **Pedidos:** se acotan las columnas `nvarchar(max)` que se filtran o se unen y se crean los índices que usan las consultas reales. Las FK en sombra (`OrdersId`, `LastOrderId1`) se sustituyen por relaciones explícitas.
- **Árbol de inscripción:** gana la columna `GenealogyTree.Node` (`hierarchyid`), con un índice único en profundidad y otro en anchura. La rellena un script idempotente por lotes, fuera de la migración. Después, todas las consultas de equipo pasan de `HierarchyPath.StartsWith` a `Node.IsDescendantOf`. `HierarchyPath` se mantiene en paralelo y se retirará en un plan posterior.
- **Árbol binario:** no cabe en `hierarchyid` a escala (lo justifica R4). Usa una CTE sobre `ParentMemberId` con un tope de filas. Por encima del tope sigue la estrategia actual.
- **RCSI:** lo activa un script versionado, con los servicios parados. Antes se revisan los candados (ver «Revisión de candados bajo RCSI»).

**Tech Stack:**
- .NET 10 y EF Core 10.0.11 sobre SQL Server.
- `Microsoft.EntityFrameworkCore.SqlServer.HierarchyId` 10.0.11 y `Microsoft.EntityFrameworkCore.SqlServer.Abstractions` 10.0.11.
- xUnit, FluentAssertions, EF InMemory y pruebas `[SqlServerFact]` contra la base local. `sqlcmd` para los scripts.
- Rama: `plan-m-rendimiento-pedidos-y-arboles`, creada desde `master` **después** de fusionar el Plan K (R13).

**Documento de decisiones:** `docs/arquitectura/decisiones-rendimiento-pedidos-y-arboles.md`. Lo mantiene el coordinador: no se edita en este plan. El plan concreta sus D1–D6 (D3 y D4 con las cifras medidas abajo).

| Bloque | Tareas | Qué entrega | Despliegue |
|---|---|---|---|
| 0. Medición | 1 | Pruebas de medición con objetivo, en rojo antes del plan | — |
| 1. Pedidos e índices | 2–5 | `OrderNo` acotado y único, índices de `Orders`/`OrderDetails`/`PaymentHistories`/`MemberStatistics`/`DualTeamTree`, relaciones explícitas; migración `PedidosAcotadosEIndices` | Despliegue único |
| 2. Nodo de inscripción (escritura) | 6–10 | `Node hierarchyid` en el modelo, escritores, regla, migración `NodoDeGenealogia`, script de relleno | Despliegue único (con el relleno) |
| 3. Nodo de inscripción (lectura) | 11–13 | `GenealogySubtree`, todas las consultas de equipo por `Node`, regla contra `StartsWith` | Despliegue único: los servicios arrancan solo con el relleno al 100 % |
| 4. Binario | 14–15 | `DualSubtree` (CTE), *Mi equipo* del Dual Team por la CTE en subárboles normales | Cualquiera |
| 5. RCSI | 16–17 | Prueba de reclamo concurrente, script de activación, activación local | Local ya; producción con condición (R6) |
| Cierre | 18 | Medición después y verificación completa | — |

**Reglas que se aplican en todo el plan:**

- **TDD.** Primero la prueba que falla, luego el código mínimo y después la prueba en verde. Se usa EF InMemory para la lógica y `[SqlServerFact]` para la traducción a SQL, los tipos de columna, los índices y los planes de ejecución. Las pruebas de SQL Server corren dentro de una transacción que se deshace y no dejan datos. La infraestructura está en `BizCenter.Tests/Reportes/SqlServerReportes.cs` (`SqlServerReportes.Db()`, `ConReintentoAsync`, `NuevoId`) y en `Signups.Tests/Helpers/SqlServerPrueba.cs`.
- **Hora de servidor.** En C#, `IDateTimeProvider.Now`, nunca `UtcNow`. En SQL, `GETDATE()`, nunca `GETUTCDATE()`. Las pruebas de medición usan `DateTime.Now` solo como extremo de un rango.
- **Commits.** El mensaje va en español, sin tildes (como el historial) y solo describe lo que se hizo: **sin** `Co-Authored-By` ni menciones de herramientas o IA. `git add` siempre con rutas explícitas, nunca `git add .` ni `-A`.
- **Una sola base: la local.** Toda migración, script o consulta va contra `MLMConquerorGlobalEditionDb` en `.`, y nunca contra otra base. Las migraciones **se crean** con `dotnet ef migrations add` y **se aplican a mano** con `--connection` (ver «Comandos»). Cualquier servicio que se arranque en local las aplica solo (`MigrateAsync` en los ocho `Program.cs`), así que se aplican **antes** de arrancar nada.
- **Componentes unificados.** Cada regla de árbol vive en un único sitio de `Repository/Queries`: `GenealogyNodes`, `GenealogySubtree` y `DualSubtree`. Los servicios y los manejadores la llaman y no la copian.

---

## Hechos medidos el 2026-10-17 (base local, solo `SELECT`)

| Hecho | Valor |
|---|---|
| `Orders` | 303.554 filas. `Id`: 36 caracteres. `MemberId`: 10 como máximo. `OrderNo`: de 9 a 23, ninguno nulo. `MembershipSubscriptionId`: 36 |
| `OrderNo` duplicados | **68 grupos, 152 filas**, todos del formato antiguo de 2 letras (p. ej. `TA-E0523AQ`, pedidos `Completed` de 99 $ del 23-05-2026, de pruebas de carga con correos `@example.com`) |
| `OrderDetails` | 199.716 filas. `OrderId` y `ProductId`: 36. `OrdersId` (sombra) vacío en todas. Líneas sin pedido: **0**. Líneas sin producto: **0** |
| `PaymentHistories` | 22.613 filas. `OrderId`: 36. `MemberId`: 10. `GatewayTransactionId`: 36. `OrdersId` (sombra) vacío. Pagos sin pedido: **0**. Pagos sin miembro: **0**. `GatewayTransactionId` repetidos: **0** |
| `MembershipSubscriptions` | `LastOrderId`: 36, relleno en 119.445 filas. `LastOrderId1` (sombra, con índice `IX_MembershipSubscriptions_LastOrderId1`) vacío. `LastOrderId` sin pedido: **0** |
| Órdenes sin miembro | **0** |
| `MemberStatistics` | 118.262 filas. `MemberId` `nvarchar(max)` sin índice (solo PK), 10 caracteres, **0** duplicados |
| `GenealogyTree` | 119.446 filas. Nivel máximo 8. Ruta máxima de 89 caracteres. **8 raíces** (`ParentMemberId` nulo: `ROOT001` con `Level 0` y siete `AMB-` con `Level 1`). **0** miembros con dos nodos. **0** rutas incoherentes con `ParentMemberId`. Mayor número de inscritos directos: **990** (`AMB-322228`) |
| `DualTeamTree` | 119.433 filas. 3 raíces. Ruta máxima de 10.275 caracteres = **934 niveles** |
| `DualTeamTree` huecos repetidos | **939** pares `(ParentMemberId, Side)` con más de un hijo vivo (R6) |
| `hierarchyid` binario (ordinales 1 y 2) | 5 bits por nivel: 100 niveles = 63 B, **934 ≈ 584 B**, 1.400 = 875 B. **Máximo: 1.427 niveles** (892 B); el nivel 1.428 falla con 24001 |
| `hierarchyid` con ordinales grandes (100 niveles) | ordinal ≤ 3: 63 B · ≤ 15: 88 B · ≤ 79: 150 B · ≤ 1.103: 225 B · 5.000: 263 B · 100.000: 538 B |
| RCSI | `is_read_committed_snapshot_on = 0` |
| `hierarchyid` en el código nuevo | ninguno (el legado lo usaba en `WWDB.edmx`) |

**Profundidad que soporta el árbol de inscripción con `hierarchyid`** (892 bytes, ordinal = posición entre hermanos):
- Con hasta 1.103 inscritos directos por patrocinador, que es el caso real (máximo local: 990), el nodo ocupa unos 2,25 B por nivel. Caben **unos 396 niveles**.
- En el peor caso, con patrocinadores de 100.000 inscritos directos en todos los niveles, caben unos 165.
- Lo que limita es la **profundidad**, no el número de miembros. El árbol de patrocinio es ancho y poco profundo (8 niveles en local).
- Como comparación, `varchar(1700)` daba unos 150 niveles y `nvarchar(850)` unos 77, con ids de 11 caracteres por nivel. Ya no hace falta acortar `HierarchyPath`.

---

## Mediciones (la Tarea 1 rellena «Antes» y la Tarea 18 rellena «Después»)

| Medición (mediana de 3, base local) | Antes | Después | Objetivo |
|---|---|---|---|
| *Actividad por fechas* de la raíz, 3 meses | 5.307 ms (5.307 · 5.714 · 3.901) | | < 3 s |
| *Clientes*, alcance Equipo, raíz, página 1 | 526 ms (1.146 · 526 · 477): ya cumple | | < 3 s |
| *Eventos*, «todo el equipo», raíz, rejilla + totales | 6.180 ms (6.602 · 6.180 · 3.906) | | < 3 s |
| Comprobación de `OrderNo` (`AnyAsync`) | 430 ms (134 · 430 · 471) | | < 50 ms |
| *Mi equipo* del Dual Team, subárbol de ≤ 20.000 nodos | (la mide la Tarea 14) | | < 2 s |

---

## Riesgos

| # | Riesgo | Mitigación en este plan |
|---|---|---|
| R1 | **Rellenar `Node` en millones de filas.** Dentro de una migración sería una sola transacción: el registro de transacciones crecería, la tabla quedaría bloqueada y se superaría el *timeout* de 30 s de los comandos de EF (R8) en los ocho servicios a la vez. | El relleno es un **script idempotente por lotes** de 50.000 filas, con una transacción por lote (`Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql`, Tarea 10). Se puede cortar y relanzar. La migración solo añade la columna, que admite nulos, y los índices: es una operación de metadatos. Si un lote choca con un alta concurrente (índice único), falla entero y se relanza. Lo mejor es lanzarlo con poco tráfico. |
| R2 | **Nodos incoherentes:** un escritor nuevo crea un nodo de inscripción sin `Node`, o reescribe rutas sin tocar `Node`. Ese miembro desaparecería de todos los equipos. | `NodosDeGenealogiaTests` (Tarea 8) cumple el papel de `RutasConBarraFinalTests` para `Node`: todo `new GenealogyEntity { … }` asigna `Node`, la lista de sitios que crean nodos es la revisada, y nadie hace `ExecuteUpdate` ni `UPDATE GenealogyTree`. El índice único rechaza dos nodos iguales. La consulta de coherencia de la Tarea 10 es condición de despliegue y se vigila después. |
| R3 | **Mover subárboles.** Hoy **nadie** mueve nodos de inscripción. Las colocaciones (`PlacementService`, `PlaceMemberHandler` ×2, `AdminPlaceMember`, `RunAutoPlacement`, `RemovePlacement` ×2) solo escriben en `DualTeamTree`, y no hay cambio de patrocinador. | Si llega un cambio de patrocinador, `NodosDeGenealogiaTests` obliga a revisarlo. Debe ser **un único** `UPDATE` sobre todo el subárbol con `Node.GetReparentedValue(@viejo, @nuevo)`, dentro de una transacción con `sp_getapplock` sobre los dos patrocinadores, y antes hay que reservar el ordinal nuevo con `GenealogyNodes.NextChildAsync`. Queda anotado en la clase. |
| R4 | **Profundidad del `hierarchyid`.** Inscripción: unos 396 niveles con el reparto real. Binario: **1.427 niveles como máximo**. En local ya hay 934 (584 B, el 65 %), con 119.000 miembros y el derrame por el lado más profundo. Con millones de miembros la pierna exterior crece casi en proporción, así que no cabría. | Inscripción en `hierarchyid` (D3). Binario con CTE (D4, M17). Si un alta superara los 892 B, SQL Server lanzaría 6522/24001 al guardar y el alta fallaría sin escribir nada. Con la profundidad real no se espera que pase. |
| R5 | **Ordinales concurrentes:** dos altas a la vez bajo el mismo patrocinador calculan el mismo `Node`. | Índice único `IX_GenealogyTree_Node`. El alta ya reintenta cuando choca con `AK_MemberProfiles_MemberId`, y el reintento se amplía al índice del nodo (Tarea 8). Con RCSI pasa más a menudo (la lectura no espera), pero sigue cerrado. |
| R6 | **RCSI y colocaciones en el binario.** `PlacementService` serializa con un `sp_getapplock` **por patrocinador**. Dos patrocinadores distintos pueden derramar al mismo hueco. Hoy, en lectura confirmada con bloqueos, la lectura de los hijos del hueco **espera** a la inserción sin confirmar del otro (bloqueo X en `IX_DualTeamTree_ParentMemberId`), y eso los serializa por accidente. Con RCSI lee la versión anterior y ve el hueco libre. **Ya hay 939 huecos repetidos en local.** | **Condición de producción:** no activar RCSI en producción hasta que un plan aparte repare los huecos repetidos y cree `UX_DualTeamTree_ParentMemberId_Side` (único, `IsDeleted = 0`). Ese índice cierra la carrera con o sin RCSI. En local se activa ya (aprobado por Nick, Tarea 17), sabiendo que los datos locales ya tienen huecos repetidos. Ver «Revisión de candados bajo RCSI». |
| R7 | **RCSI y webhooks de pago.** `ProcessPaymentWebhookHandler` busca por `GatewayTransactionId` y, si no lo encuentra, inserta, sin índice único. Hoy el recorrido completo de la tabla espera a la fila sin confirmar del otro webhook. Con RCSI y con el índice nuevo, los dos webhooks iguales pueden insertar. | Se propone `UX_PaymentHistories_GatewayName_GatewayTransactionId` (filtrado, no nulo; hoy 0 repetidos), **pendiente de que Nick confirme** que ningún flujo repite el id de transacción (M7). Mientras no esté, es condición de producción de RCSI, igual que R6. |
| R8 | **Las migraciones corren al arrancar** (`MigrateAsync` en los ocho servicios), con el *timeout* por defecto de 30 s. Un `ALTER COLUMN` de `nvarchar(max)` a una longitud acotada reescribe y bloquea la tabla con Sch-M. Con millones de filas tarda minutos: los servicios no arrancan y la tabla queda bloqueada. | **En producción las migraciones del plan se aplican antes de desplegar**, con `dotnet ef migrations script --idempotent` y `sqlcmd -b -I`, que no tiene *timeout*, en una ventana sin tráfico (M24). Así, cuando arranquen, los servicios las encontrarán aplicadas. En local las tablas son pequeñas (segundos). Si la guarda de huérfanos supera los 30 s por bloqueos de los servicios levantados, se paran y se repite. |
| R9 | **Las FK nuevas** (`OrderDetails.OrderId` → `Orders`, `MembershipSubscriptions.LastOrderId` → `Orders`) rompen a cualquier escritor que inserte la línea o la suscripción **antes** que el pedido en otro `SaveChanges`. | Con la relación modelada, EF ordena las inserciones dentro de un mismo `SaveChanges`. `RecurringBillingProcessor` ya guarda primero el pedido. Antes de crear las FK, la migración comprueba que no hay huérfanos (51024, 51027). Las pruebas de SQL Server del alta, de Billing y de los reportes deben seguir en verde (Tarea 18). `PaymentHistories` **no** lleva FK (M7). |
| R10 | `hierarchyid` en contenedores Linux. El tipo de EF depende de `Microsoft.SqlServer.Types`, que en la parte de `hierarchyid` es código gestionado. | Antes del despliegue, levantar un servicio en su imagen Docker y llamar a *Mi equipo*. Si falla al cargar el tipo, parar y avisar. |
| R11 | **InMemory no tiene `hierarchyid`.** Sin conversor, EF trataría `HierarchyId` como entidad. | En InMemory, `Node` se guarda como texto (`HasConversion`, Tarea 6). `IsDescendantOf` y `GetAncestor` se evalúan en memoria con la implementación de EF, que es la misma semántica. La traducción a SQL y el *seek* los prueban las `[SqlServerFact]`. |
| R12 | **`IsDescendantOf` con *seek*** solo se consigue con la forma «la columna `Node` contra un parámetro». La forma en que se lee el nodo del candidato con una subconsulta escalar no hace *seek*, pero parte de un conjunto pequeño (altas del rango, inscripciones a un evento, cambios de estado). | `SqlServerPlanesDeEjecucionTests` lee el plan estimado de cada consulta que debe hacer *seek* (Tareas 5, 11 y 14). Las consultas de equipo completo usan `CurrentUnder` (M16). |
| R13 | **Conflicto con el Plan K:** su migración `CuotaAnual` y el *snapshot* están sin fusionar en `plan-k-cuota-anual`. | La rama del Plan M sale de `master` después de fusionar el Plan K. Si no se puede esperar, rebasar y regenerar las migraciones del Plan M (no fusionar el *snapshot* a mano). |
| R14 | **El espacio de `MemberId` se agota en 1.000.000.** `GenerateUniqueMemberIdAsync` sortea `AMB-######`. Con millones de miembros, el alta devolvería `MEMBER_ID_EXHAUSTED`. | **Fuera de alcance, pero bloquea la escala.** Anotado para Nick (pregunta P1 del informe). `MemberProfiles.MemberId` ya es `nvarchar(20)`, así que admite un formato más largo sin cambiar el esquema. |
| R15 | **Renumerar los `OrderNo` duplicados** cambia números de pedido que un cliente pudo ver. | En local son pedidos de pruebas de carga y se renumeran con un sufijo `-Dn` (Tarea 4). Para producción, primero se ejecuta la consulta de duplicados de la Tarea 4. Si hay alguno, la migración se detiene (51021) y decide Nick. |
| R16 | **Valores largos que llegan de fuera.** Con las columnas acotadas, un webhook con `GatewayTransactionId` de más de 200 caracteres o `MemberId` de más de 100 falla con un error de truncado en vez de guardarse. | Los máximos actuales son 36 y 10. Fallar cerrado es preferible a guardar basura. Se anota en «Fuera de alcance» la validación de longitud en `ProcessPaymentWebhookHandler`. |
| R17 | **`Node` admite nulos en este plan** (M11). Si una fila quedara sin `Node`, ese miembro no aparecería en ningún equipo y no se notaría. | Hay dos condiciones de despliegue para el Bloque 3: `SinNode = 0` e `Incoherentes = 0` en la Tarea 10. Después del relleno, solo un escritor sin `Node` podría crear un nulo, y eso lo impide R2. `NOT NULL` llega con el plan que retire `HierarchyPath` (cuando las pruebas creen nodos con `Node`). |

## Revisión de candados bajo RCSI (crítico)

Con `READ_COMMITTED_SNAPSHOT ON`, una lectura en `READ COMMITTED` deja de tomar bloqueos compartidos. Lee la última versión **confirmada** y no espera a quien escribe. Las escrituras no cambian: `UPDATE`, `DELETE` y `MERGE` leen las filas que van a tocar con bloqueos de actualización y vuelven a evaluar el `WHERE` sobre la versión confirmada más reciente. La pregunta es qué lógica dependía de que una **lectura** esperara.

| Mecanismo | Dónde | Veredicto bajo RCSI |
|---|---|---|
| `sp_getapplock` con dueño Transaction | `MemberFundsGate` (`member-funds:{id}`), compartido por Billing, AdminAPI, BizCenter y SignupAPI | **Sigue siendo correcto.** El candado de aplicación no depende del aislamiento. Lo que se lee dentro del candado es lo último confirmado, y todo escritor de fondos confirma antes de soltar el mismo candado (los Planes E, F y K lo exigen). Lo cubre `SqlServerMemberFundsGateTests`. |
| `UPDATE … WHERE` condicional (reclamo) | `SignupOrderClaim` (`Pending/Processing → Completed`), `EntraxSignupSettlement:367`, `EntraxSignupStatusService:109` | **Sigue siendo correcto.** El segundo `UPDATE` espera al bloqueo X de la fila, vuelve a evaluar `Status IN (…)` sobre lo confirmado y afecta a 0 filas. La Tarea 16 lo prueba con dos conexiones. |
| `MERGE … WITH (HOLDLOCK)` | `ApplyMemberStatisticDeltasJob:131`, `SignupActivationService:72` | **Sigue siendo correcto.** Las pistas explícitas se respetan con RCSI. Con el índice nuevo de `MemberStatistics.MemberId` (Tarea 3), el `HOLDLOCK` bloquea un rango de clave y no la tabla entera. |
| Token de concurrencia | `TicketSequence.LastSequence` (`IsConcurrencyToken`) | **Sigue siendo correcto** (optimista: `DbUpdateConcurrencyException`). El comentario de `TicketSequence` dice «serializable» y no lo es. Es anterior a este plan y queda anotado. |
| Leer y luego insertar, con índice único detrás | `MemberId` (`AK_MemberProfiles_MemberId` + reintento), `OrderNo` (índice único nuevo), `GenealogyTree.Node` (índice único nuevo + reintento), `DualTeamTree.MemberId` (único filtrado) | **Sigue siendo correcto: falla cerrado.** Con RCSI la comprobación previa ve menos, y el choque llega como `DbUpdateException`. El alta ya reintenta. En `OrderNo`, la probabilidad de choque es despreciable (456.976 combinaciones por milisegundo). |
| Leer y luego insertar **sin** índice único | Colocación en el binario (hueco `ParentMemberId + Side`), webhook de pago (`GatewayTransactionId`) | **Deja de excluir.** Ver R6 y R7. Son condiciones para activar RCSI en producción. |
| `AnyAsync` de comprobación previa sin efecto | `EntraxSignupCheckout:114`, `EntraxSignupStatusService:62` | Son atajos de lectura. La exclusión la da el reclamo de la fila siguiente: correcto. |

---

## Decisiones

**Del encargo (vinculantes):**
- Acortar los `nvarchar(max)` que haga falta y crear los índices que falten.
- Diseñar para millones de miembros.
- `hierarchyid` para el árbol de inscripción. Para el binario, solo si cabe con margen; si no, CTE.
- Transición en dos pasos para `HierarchyPath`.
- RCSI aprobado por Nick, como paso de despliegue con un script, sin migración.
- Pedidos, otros índices y medición sin cambios respecto al encargo.
- TDD, hora de servidor, commits sin firma, solo la base local.

**Tomadas aquí (con su porqué):**

| # | Decisión | Por qué |
|---|---|---|
| M1 | **`Orders.Id` se queda en `nvarchar(450)`.** | Es la clave primaria agrupada. SQL Server guarda los `nvarchar` por su longitud real, así que bajar a 36 no ahorra espacio ni acelera el *seek*. A cambio, obligaría a soltar y recrear la PK, el índice agrupado y toda FK hacia `Orders`, con 300.000 filas y ocho servicios migrando. Las columnas que apuntan a `Orders.Id` (`OrderDetails.OrderId`, `PaymentHistories.OrderId`, `MembershipSubscriptions.LastOrderId`, `Orders.MembershipSubscriptionId`) pasan a `nvarchar(450)` para que el tipo coincida con la FK. |
| M2 | **`Orders.MemberId` → `nvarchar(20)`, sin FK.** | Mide lo mismo que `MemberProfiles.MemberId`, con la que se une. Sin FK, la regla de Nick admite 100 o 450, pero 20 es el máximo posible de un `MemberId` y la guarda 51022 lo comprueba. No se pone FK porque algún flujo de alta o de Entrax podría crear el pedido en un `SaveChanges` distinto del perfil. Nick puede pedir la FK (pregunta del informe). |
| M3 | **`Orders.OrderNo` → `nvarchar(40)` con índice único filtrado (`IS NOT NULL`).** `OrderNumberHelper` limita las iniciales a 8 y expone `MaxLength = 40`. | Hoy mide de 9 a 23. Las iniciales salen de las palabras de una descripción (`ChargeRequest.Description`), que es texto libre, y sin límite una descripción larga produciría un número que no cabe. Con 8 iniciales mide 27 como máximo. El índice único convierte en *seek* los seis bucles `AnyAsync` (`ChargeHandler`, `RenewMembershipHandler` ×2, `RecurringBillingProcessor`, `SignupAmbassadorHandler`, `SignupMemberHandler`), que se quedan como están: ya son baratos. |
| M4 | **Duplicados de `OrderNo`:** un script idempotente renumera todos menos el más antiguo de cada grupo con el sufijo `-D1`, `-D2`… La migración se detiene con 51021 si queda alguno. | En local son de pruebas de carga. En producción decide Nick (R15). |
| M5 | **Índices de `Orders`.** `(MemberId, OrderDate) INCLUDE (Status, CreationDate, IsDeleted)`: lo usan la orden de alta de *Actividad*, `SignupEnrollmentOrder` (`!Orders.Any(p => p.MemberId == … && p.OrderDate < …)`), el historial de facturación, `RankQualificationService`, `GetRankProgress`, `UpdateMemberStatus`, `FastStartBonusSweepJob`, los perfiles del Admin y las comisiones del Admin. `(MembershipSubscriptionId)`: `AdminMemberMembershipController` y la unión del panel CEO. `(OrderDate) INCLUDE (Status, TotalAmount)`: paneles CEO y financiero y estadísticas de promociones (rangos de fecha). | Cada uno sale de una consulta real (grep de `.Orders` en el código, sin pruebas). No se indexan `Notes` ni `CheckoutScreenshotUrl`, que no se filtran. |
| M6 | **`OrderDetails`:** `OrderId` y `ProductId` → `nvarchar(450)` (el tipo de `Orders.Id` y `Products.Id`). Índice `(OrderId) INCLUDE (ProductId, Quantity, UnitPrice)`. Relación `Orders.OrderDetails` 1–N por `OrderId` con `OnDelete(Restrict)`. Se elimina `OrdersId` y su índice. | Todas las uniones son `o.Id == od.OrderId` y después `od.ProductId == p.Id` (`OrderMembershipLevelQueries`, *Actividad*, comisiones, rangos, alta). El índice las cubre sin ir a la tabla. `Restrict` porque los pedidos se borran de forma lógica: un borrado físico en cascada no debe ocurrir. |
| M7 | **`PaymentHistories`:** `OrderId` → 450, `MemberId` → 100, `GatewayTransactionId` → 200. Índices `(OrderId)`, `(MemberId, TransactionStatus) INCLUDE (ProcessedAt)` y `(GatewayTransactionId)`. **Sin FK.** Se elimina la navegación `Orders.Payments`, que nadie usa, y con ella `OrdersId`. El índice **único** de `(GatewayName, GatewayTransactionId)` se propone y no se crea (R7). | El webhook crea pagos con `OrderId = payload.OrderId ?? ""` y `MemberId = payload.MemberId ?? ""`: una FK lo haría fallar y la pasarela reintentaría sin fin. Los índices los usan `TokenPurchaseService:892`, el panel CEO, `EnrollmentTeamService:206` (último pago por miembro) y los dos webhooks. |
| M8 | **`MembershipSubscriptions.LastOrder`:** relación explícita por `LastOrderId` (`nvarchar(450)`, `Restrict`). Se elimina `LastOrderId1`. | Es el mismo fallo que `OrdersId`: la navegación `LastOrder` creaba una FK en sombra vacía. Los reportes del Plan J (N2) llegan a la orden por `LastOrderId`. |
| M9 | **`MemberStatistics.MemberId` → `nvarchar(100)` con índice** (no único). | Lo usan el `JOIN` por miembro de `LegPointMaintenance` en cada colocación, el `MERGE … WITH (HOLDLOCK) ON MemberId` de `ApplyMemberStatisticDeltasJob` (sin índice bloquea un rango de la tabla entera) y el enriquecimiento de cada página de reportes (`Contains(ids)`). No es único para no cambiar el comportamiento de ningún escritor. Si Nick quiere «uno por miembro», se hará en otro plan. |
| M10 | **`IX_DualTeamTree_ParentMemberId` pasa a `INCLUDE (MemberId, Side, IsDeleted)`.** | Cada paso de la CTE binaria (M17) y de `LegPointMaintenance` busca por `ParentMemberId` y lee esas tres columnas. Así no hay búsqueda en la tabla por cada fila. |
| M11 | **`GenealogyTree.Node hierarchyid`, admite nulos en este plan.** Índice único filtrado `IX_GenealogyTree_Node` (`[Node] IS NOT NULL`) con `INCLUDE (MemberId, Level)`. | El índice en profundidad convierte el subárbol en un rango contiguo: `IsDescendantOf(@raiz)` hace *seek*. `INCLUDE` cubre el `JOIN` a `MemberProfiles` y el nivel relativo. Los nulos son necesarios mientras dura el relleno. `NOT NULL` llegará con la retirada de `HierarchyPath` (R17). |
| M12 | **Índice en anchura `(NodeLevel, Node)`**, con `NodeLevel AS [Node].GetLevel() PERSISTED` (propiedad en sombra). **Solo** para reservar el ordinal de una raíz nueva. | Los hijos de un patrocinador se buscan por `ParentMemberId`, que ya tiene índice. Las raíces (altas sin patrocinador, o con un patrocinador sin nodo) no tienen `ParentMemberId` común, y sin este índice cada una recorrería el índice entero. Ninguna consulta de equipo lo necesita. |
| M13 | **Ordinal = posición entre hermanos:** `padre.GetDescendant(ultimoHijo, null)`, en `GenealogyNodes.NextChildAsync`. El padre es el nodo más reciente del patrocinador (R8 del Plan J). Los hermanos se buscan con `ParentMemberId` y `GetAncestor(1)`, incluidos los borrados de forma lógica, para no reutilizar un ordinal. | Da ordinales pequeños (unos 2,25 B por nivel, 396 niveles). Derivarlo del número del `MemberId` daría ordinales de hasta 1.000.000 (5–6 B por nivel, unos 150 niveles) y chocaría entre prefijos (`AMB-000123` frente a `MBR-000123`). La carrera de R5 la cierra el índice único con reintento. |
| M14 | **El relleno es un script idempotente por lotes, no una migración.** | R1. Sigue la estructura (`ParentMemberId`), nivel a nivel: solo rellena filas cuyo padre ya tiene `Node`, así que cada pasada avanza un nivel. Usa los mismos ordinales que `NextChildAsync` (último ordinal del grupo más `ROW_NUMBER` por `CreationDate, Id`). |
| M15 | **`HierarchyPath` se mantiene.** Se sigue escribiendo y la usan quienes sacan la línea ascendente partiendo la ruta (alta, cancelación, rangos, bonos, `UplineAggregator`, `DownstreamTriggers`) y quienes calculan la pierna (`CommissionsService.GetLeg`, comisiones del Admin). Solo dejan de usarla los **filtros de equipo**. | Es la transición en dos pasos de D3. La ascendencia por `Node.GetAncestor` y la retirada de la columna son del plan siguiente. |
| M16 | **Dos formas de leer el equipo.** **A**, a partir de un conjunto pequeño ya filtrado (altas del rango, inscripciones a un evento, cambios de estado): se cambia `Select(n => n.HierarchyPath)` por `Select(n => n.Node)` y `node.StartsWith(prefix)` por `node.IsDescendantOf(raiz)`. **B**, el equipo entero (Mi equipo, Clientes, Eventos con todo el equipo, puntos, comisiones): `_db.GenealogyTree.CurrentUnder(raiz)` y después `JOIN` a `MemberProfiles`. | La forma B parte del rango del índice y descarta los nodos obsoletos (R8) con el índice `(MemberId, CreationDate)`. La forma actual («de cada perfil, su nodo más reciente») recorre todos los perfiles: con millones de miembros es inviable. |
| M17 | **El binario no usa `hierarchyid`: usa una CTE recursiva sobre `ParentMemberId`.** | R4: hoy usa el 65 % del máximo y crece con los miembros. La CTE funciona a cualquier profundidad. |
| M18 | **La CTE va en `Database.SqlQuery<T>` con SQL en línea** (`Repository/Queries/DualSubtree.cs`). No se usa procedimiento almacenado ni función. | Es la convención del repositorio: `LegPointMaintenance` y `MemberFundsGate` llevan el SQL en línea y no hay ningún procedimiento ni función en las migraciones. Una función de tabla en línea no admite `OPTION (MAXRECURSION)` y quedaría limitada a 100 niveles. La consulta no se compone con LINQ: `ToListAsync` directo. |
| M19 | **Tope de 20.000 filas** (`DualSubtree.MaxRowsForCte`). Por encima, o en InMemory, `DescendantsAsync` devuelve `null` y el llamador sigue con la estrategia actual (prefijo de la ruta, o primero fecha y estado). Los ids van a la rejilla con `EF.Parameter(ids).Contains(...)`, que se traduce a un único parámetro JSON (`OPENJSON`). | `SELECT TOP (@tope + 1)` corta la recursión en cuanto se pasa, así que saber que el equipo es enorme cuesta como mucho 20.001 filas. La raíz (119.000 en local) sigue por el camino actual. Si se pasaran 20.000 ids como parámetros sueltos, se superaría el límite de 2.100. |
| M20 | **`OPTION (MAXRECURSION 32767)`**, el máximo que admite SQL Server. | La profundidad local es de 934 niveles. Con 0 (sin límite), un ciclo en los datos no terminaría nunca. Con 32.767, un ciclo acaba en error. |
| M21 | **`UseHierarchyId()` se activa en `AppDbContext.OnConfiguring`**, solo si el proveedor es SQL Server, junto a los interceptores. | Se hace en un solo sitio en lugar de en ocho `Program.cs`, la fábrica de diseño y los ayudantes de pruebas. Un anfitrión que lo olvidara fallaría al construir el modelo. Usa `SqlServerOptionsExtension`, que es una API interna, así que lleva `#pragma warning disable EF1001` y una prueba que lo vigila (Tarea 6). |
| M22 | **RCSI con un script, fuera de EF** (`Scripts/sql/plan-m/03-activar-rcsi.sql`). El script se niega a seguir si hay otras sesiones en la base y usa `ALTER DATABASE CURRENT … WITH ROLLBACK IMMEDIATE`. | Necesita acceso exclusivo y no puede ir dentro de una transacción. Dentro de una migración cortaría las conexiones de los ocho servicios al arrancar. |
| M23 | **`HierarchyPath` no se acorta** (ni a `varchar(1700)` ni a `nvarchar(850)`). | Con `Node` indexado, la ruta deja de filtrarse. |
| M24 | **En producción, las tres migraciones se aplican con un script idempotente y `sqlcmd`** antes de arrancar los servicios: `PedidosAcotadosEIndices` y `NodoDeGenealogia`, en un único despliegue (revisión C1: en producción nunca se aplicó ninguna versión de `NodoDeGenealogia`). Script generado: `Scripts/sql/plan-m/00-migraciones-plan-m.sql`. Si `DeploymentScripts/migrate-rds.sql` es el que se usa para RDS, se regenera con `--idempotent`. | R8. **Revisión:** solo con `sqlcmd -b -I` (nunca SSMS ni sin `-b`), con los servicios parados durante la ventana y sin arrancar ninguno hasta que el script termine (ver «Condiciones de despliegue»). |

## Fuera de alcance

- **Retirar `HierarchyPath`** del árbol de inscripción y poner `Node NOT NULL`. Incluye pasar la ascendencia a `Node.GetAncestor` y que las pruebas creen los nodos con `Node`. Es el plan siguiente (D3, paso 2).
- **Reparar los 939 huecos repetidos del binario** y crear `UX_DualTeamTree_ParentMemberId_Side`. Es condición para activar RCSI en producción (R6). Plan aparte; decide Nick qué hijo se queda.
- **Índice único de `PaymentHistories (GatewayName, GatewayTransactionId)`** (R7, M7). La validación de longitud del webhook de pagos (R16) sí entró, tras la revisión de las Tareas 1–5.
- **Otras lecturas binarias** que siguen recorriendo la ruta: `NetworkController` (carga el subárbol entero aunque pinte 6 niveles), `DualTeamService.SearchBinarySubtreeAsync` y `GetDeepestNodeAsync`, `PlacementService`, los `PlaceMemberHandler` y `GetRankProgressHandler` (parte binaria). *Cambios de estado* del Dual Team ya reduce primero por fecha (Plan J, J17) y no cambia.
- **`CalculateCarBonusHandler`**, que carga en memoria todas las suscripciones activas con su ruta. Es un trabajo por lotes; se revisará con datos reales.
- **El espacio de `MemberId`** (R14).
- **Closure table** del binario y **particionado** de tablas (§4 del documento de decisiones).
- **FK de `Orders.MemberId`** hacia `MemberProfiles` (M2).
- `DownlineGuard`: compara en memoria dos rutas leídas por clave. No recorre la tabla y no cambia.

## Mapa de archivos

Rutas abreviadas: todos los proyectos llevan el prefijo `MLMConquerorGlobalEdition.`. En los bloques **Files** van completas.

| Archivo | Responsabilidad |
|---|---|
| `BizCenter.Tests/Rendimiento/SqlServerMedicionPlanMTests.cs` (nuevo) | Medición antes y después con objetivo |
| `BizCenter.Tests/Rendimiento/PlanDeEjecucion.cs`, `SqlServerPlanesDeEjecucionTests.cs` (nuevos) | Plan estimado (`SHOWPLAN_XML`) y comprobación de *seek* |
| `SharedKernel/OrderNumberHelper.cs`; `Domain.Tests/OrderNumberHelperTests.cs` (nuevo) | Iniciales ≤ 8, `MaxLength = 40` |
| `Repository/Configurations/OrdersConfiguration.cs`, `OrderDetailConfiguration.cs`, `PaymentHistoryConfiguration.cs`, `MembershipSubscriptionConfiguration.cs`, `MemberStatisticConfiguration.cs` (nuevos); `DualTeamConfiguration.cs` | Longitudes, índices y relaciones |
| `Domain/Entities/Orders/Orders.cs` | Fuera la navegación `Payments` |
| `BizCenter.Tests/Rendimiento/IndicesDePedidosTests.cs`, `SqlServerPedidosTests.cs` (nuevos) | Modelo y base de pedidos |
| `Scripts/sql/plan-m/01-orderno-duplicados.sql` (nuevo) | Renumerar duplicados |
| Migración `PedidosAcotadosEIndices` | Bloque 1 |
| `Domain/MLMConquerorGlobalEdition.Domain.csproj`, `Repository/MLMConquerorGlobalEdition.Repository.csproj` | Paquetes `hierarchyid` |
| `Domain/Entities/Tree/GenealogyEntity.cs`, `Repository/Configurations/GenealogyConfiguration.cs`, `Repository/Context/AppDbContext.cs` | `Node`, índices, `UseHierarchyId`, conversor de InMemory |
| `Repository/Queries/GenealogyNodes.cs` (nuevo) | Siguiente ordinal y detección del choque |
| `SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs`, `SignupMember/SignupMemberHandler.cs`, `Repository/Seeders/RootAmbassadorSeeder.cs` | Escritores de `Node` |
| `BizCenter.Tests/Rendimiento/NodoDeGenealogiaModeloTests.cs`, `GenealogyNodesTests.cs`, `NodosDeGenealogiaTests.cs`, `SqlServerNodoDeGenealogiaTests.cs` (nuevos) | Pruebas del nodo |
| Migración `NodoDeGenealogia`; `Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql` (nuevo) | Columna e índices; relleno |
| `Repository/Queries/GenealogySubtree.cs` (nuevo); `BizCenter.Tests/Reportes/ReportesDb.cs` | Lectura por `Node`; las pruebas crean nodos con `Node` |
| `Repository/Services/Teams/EnrollmentTeamService.cs`, `EnrollmentActivityService.cs`, `TeamStatusChangesService.cs`, `Ranks/EnrollmentTeamPointsService.cs`, `Events/TeamEventRegistrationsService.cs`, `Commissions/CommissionsService.cs` | Lectores de `Repository` |
| `RankEngine/Features/GetRankProgress/GetRankProgressHandler.cs`, `BizCenter/Features/Placement/GetAvailableNodes/GetAvailableNodesHandler.cs`, `BizCenter/Features/Tokens/TokenCustody.cs`, `BizCenter/Features/Teams/GetAllTeamMembers/GetAllTeamMembersHandler.cs`, `BizCenter/Features/Teams/GetVisualizerChildren/GetVisualizerChildrenHandler.cs`, `AdminAPI/Controllers/AdminMemberCommissionsController.cs` | Lectores de los anfitriones |
| `BizCenter.Tests/Rendimiento/GenealogySubtreeTests.cs`, `SinPrefijoEnInscripcionTests.cs` (nuevos) | Pruebas de lectura y regla |
| `Repository/Queries/DualSubtree.cs` (nuevo), `Repository/Services/Teams/DualTeamService.cs`; `BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs` (nuevo) | CTE binaria |
| `Signups.Tests/Services/SqlServerReclamoConcurrenteTests.cs` (nuevo); `Scripts/sql/plan-m/03-activar-rcsi.sql` (nuevo) | RCSI |

**Comandos que se repiten:**

- **Crear una migración** (el nombre lo da cada tarea):

  ```bash
  dotnet ef migrations add NOMBRE \
    --project MLMConquerorGlobalEdition.Repository \
    --startup-project MLMConquerorGlobalEdition.SignupAPI
  ```

  Revisar `Up`: debe contener solo las operaciones que diga la tarea. Si aparecen bloques `UpdateData(...)` de semillas que no cambian nada, se borran de `Up` y de `Down`, pero no del `.Designer.cs` ni del *snapshot*. Si aparece cualquier otra operación sobre tablas ajenas, el modelo traía deriva: parar y avisar.

- **Aplicar a la base local** (paso explícito, nunca contra otra base):

  ```bash
  dotnet ef database update \
    --project MLMConquerorGlobalEdition.Repository \
    --startup-project MLMConquerorGlobalEdition.SignupAPI \
    --connection "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;"
  ```

- **Ejecutar un script de `Scripts/sql/plan-m`** (solo en local):

  ```bash
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -i Scripts/sql/plan-m/NOMBRE.sql
  ```

---

# Bloque 0 — Medición

### Task 1: Pruebas de medición con objetivo (línea base en rojo)

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerMedicionPlanMTests.cs`
- Modify: `docs/superpowers/plans/2026-10-17-plan-m-rendimiento-pedidos-y-arboles.md` (tabla «Mediciones», columna «Antes»)

- [x] **Step 1: Escribir las pruebas**

```csharp
using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M: lo que se mide antes y después con la raíz real de la base local. Cada prueba imprime las
/// tres ejecuciones y su mediana, y falla si la mediana pasa del objetivo. ANTES del plan fallan: es
/// la línea base. En ReadUncommitted, como las de Reportes: se mide la consulta, no los bloqueos de
/// los servicios que haya levantados.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerMedicionPlanMTests
{
    public static readonly TimeSpan ObjetivoReporte = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ObjetivoOrderNo = TimeSpan.FromMilliseconds(50);

    private readonly ITestOutputHelper _salida;

    public SqlServerMedicionPlanMTests(ITestOutputHelper salida) => _salida = salida;

    internal static Task<string> RaizAsync(AppDbContext db) =>
        db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == null).OrderBy(g => g.Level).Select(g => g.MemberId).FirstAsync();

    private async Task<TimeSpan> MedianaAsync(string que, Func<Task> paso)
    {
        var tiempos = new List<TimeSpan>();
        for (var i = 0; i < 3; i++)
        {
            var cronometro = Stopwatch.StartNew();
            await paso();
            cronometro.Stop();
            tiempos.Add(cronometro.Elapsed);
        }
        var mediana = tiempos.OrderBy(t => t).ElementAt(1);
        _salida.WriteLine($"{que}: {string.Join(" · ", tiempos.Select(t => $"{t.TotalMilliseconds:0} ms"))} → mediana {mediana.TotalMilliseconds:0} ms");
        return mediana;
    }

    [SqlServerFact]
    public Task Actividad_de_la_raiz_con_tres_meses() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz  = await RaizAsync(db);
        var reloj = new RelojFijo(DateTime.Now); // extremo de un rango de medición, no lógica de negocio
        var servicio = new EnrollmentActivityService(db, reloj);

        var t = await MedianaAsync($"Actividad {raiz}", async () =>
            (await servicio.GetGridAsync(raiz, ReportDateRange.ActivityMinFrom(reloj.Now), reloj.Now.Date,
                new GridDataRequest { PageSize = 20 })).IsSuccess.Should().BeTrue());

        t.Should().BeLessThan(ObjetivoReporte);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Clientes_de_todo_el_equipo_de_la_raiz() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz = await RaizAsync(db);
        var servicio = ClientesDelEquipoTests.Servicio(db);

        var t = await MedianaAsync($"Clientes {raiz}", () =>
            servicio.GetCustomersGridAsync(raiz, new GridDataRequest { PageSize = 20 }, CustomerScope.Team));

        t.Should().BeLessThan(ObjetivoReporte);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Eventos_con_todo_el_equipo_de_la_raiz() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz   = await RaizAsync(db);
        var evento = "TM-EV-" + Guid.NewGuid().ToString("N")[..12];
        db.CorporateEvents.Add(new CorporateEvent { Id = evento, Title = "Medición Plan M", EventDate = new DateTime(2026, 12, 1), IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);

        var t = await MedianaAsync($"Eventos {raiz} (todo el equipo)", async () =>
        {
            (await servicio.GetGridAsync(raiz, evento, registeredOnly: false, new GridDataRequest { PageSize = 20 })).IsSuccess.Should().BeTrue();
            (await servicio.GetTotalsAsync(raiz, evento)).IsSuccess.Should().BeTrue();
        });

        t.Should().BeLessThan(ObjetivoReporte);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Comprobacion_de_numero_de_pedido() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var numero = OrderNumberHelper.Generate("Medicion Plan M", DateTime.Now);

        var t = await MedianaAsync("OrderNo AnyAsync", async () =>
            (await db.Orders.AnyAsync(o => o.OrderNo == numero)).Should().BeFalse());

        t.Should().BeLessThan(ObjetivoOrderNo);
        await tx.RollbackAsync();
    });
}
```

- [x] **Step 2: Ejecutarlas y anotar la línea base**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerMedicionPlanMTests --logger "console;verbosity=detailed" -nologo`
Expected: FAIL en las cuatro (o en la mayoría), con las líneas «… → mediana N ms» en la salida. Copiar cada mediana a la columna «Antes» de la tabla «Mediciones» de este plan. Si alguna sale `Skipped`, es que SQL Server no está accesible: hay que arreglarlo antes de seguir.

- [x] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerMedicionPlanMTests.cs \
        docs/superpowers/plans/2026-10-17-plan-m-rendimiento-pedidos-y-arboles.md
git commit -m "Plan M: pruebas de medicion con objetivo (actividad, clientes y eventos de la raiz por debajo de 3 s, comprobacion de OrderNo por debajo de 50 ms) y linea base anotada en el plan"
```

---

# Bloque 1 — Pedidos e índices

### Task 2: `OrderNumberHelper` con iniciales acotadas

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedKernel/OrderNumberHelper.cs`
- Create: `MLMConquerorGlobalEdition.Domain.Tests/OrderNumberHelperTests.cs`

- [x] **Step 1: Escribir las pruebas que fallan**

```csharp
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Domain.Tests;

/// <summary>Plan M (M3): el número de pedido cabe siempre en Orders.OrderNo, nvarchar(40).</summary>
public class OrderNumberHelperTests
{
    [Fact]
    public void El_numero_conserva_su_forma()
        => OrderNumberHelper.Generate("Travel Advantage Elite", new DateTime(2026, 10, 17, 6, 46, 56, 681))
            .Should().MatchRegex(@"^TAE1017-064656681-[A-Z]{4}$");

    [Fact]
    public void Una_descripcion_larga_no_pasa_de_la_columna()
    {
        var numero = OrderNumberHelper.Generate(string.Join(' ', Enumerable.Repeat("Palabra", 40)), new DateTime(2026, 10, 17));

        numero.Should().StartWith("PPPPPPPP1017-");
        numero.Length.Should().BeLessThanOrEqualTo(OrderNumberHelper.MaxLength);
    }

    [Fact]
    public void La_columna_mide_40()
        => OrderNumberHelper.MaxLength.Should().Be(40);
}
```

- [x] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter OrderNumberHelperTests -nologo`
Expected: FAIL de compilación (`MaxLength` no existe).

- [x] **Step 3: Implementar**

En `OrderNumberHelper.cs`, añadir dentro de la clase, antes de `Generate`:

```csharp
    /// <summary>Longitud de Orders.OrderNo (Plan M, M3). Con 8 iniciales el número mide 27 como mucho.</summary>
    public const int MaxLength = 40;

    private const int MaxInitials = 8;
```

y sustituir:

```csharp
        var initials = string.Concat(
            orderName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Select(w => char.ToUpper(w[0])));
```

por:

```csharp
        // Plan M: las iniciales salen de una descripción libre (ChargeRequest.Description); sin tope,
        // una descripción larga daría un número que no cabe en la columna.
        var initials = string.Concat(
            orderName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Select(w => char.ToUpper(w[0]))
                     .Take(MaxInitials));
```

- [x] **Step 4: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter OrderNumberHelperTests -nologo`
Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel/OrderNumberHelper.cs \
        MLMConquerorGlobalEdition.Domain.Tests/OrderNumberHelperTests.cs
git commit -m "OrderNumberHelper limita las iniciales a 8 y expone MaxLength = 40, la longitud de Orders.OrderNo"
```

---

### Task 3: Configuración de pedidos, pagos, suscripciones, estadísticas y binario

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/OrdersConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/OrderDetailConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/PaymentHistoryConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/MembershipSubscriptionConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/MemberStatisticConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/DualTeamConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Orders/Orders.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/IndicesDePedidosTests.cs`

- [x] **Step 1: Escribir las pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.SharedKernel;
using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M, Bloque 1: longitudes, índices y relaciones de pedidos, pagos, suscripciones, estadísticas
/// y binario, en el modelo. Que existan en la base lo comprueba SqlServerPedidosTests.
/// </summary>
public class IndicesDePedidosTests
{
    // Las columnas incluidas y los filtros son configuración de diseño: no están en el modelo de ejecución.
    private static IModel Modelo()
    {
        using var db = ReportesDb.Nueva();
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Tipo(Type t) => Modelo().FindEntityType(t)!;

    private static int? Longitud(Type t, string propiedad) => Tipo(t).FindProperty(propiedad)!.GetMaxLength();

    private static IIndex Indice(Type t, params string[] columnas) =>
        Tipo(t).GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(columnas));

    [Fact]
    public void Pedidos_acotados()
    {
        Longitud(typeof(OrdersEntity), "OrderNo").Should().Be(OrderNumberHelper.MaxLength);
        Longitud(typeof(OrdersEntity), "MemberId").Should().Be(20, "mide lo mismo que MemberProfiles.MemberId (M2)");
        Longitud(typeof(OrdersEntity), "MembershipSubscriptionId").Should().Be(450);
    }

    [Fact]
    public void Numero_de_pedido_unico()
    {
        var i = Indice(typeof(OrdersEntity), "OrderNo");
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[OrderNo] IS NOT NULL");
    }

    [Fact]
    public void Pedidos_por_miembro_y_fecha_cubren_estado_y_borrado()
        => Indice(typeof(OrdersEntity), "MemberId", "OrderDate").GetIncludeProperties()
            .Should().BeEquivalentTo("Status", "CreationDate", "IsDeleted");

    [Fact]
    public void Pedidos_por_suscripcion_y_por_fecha()
    {
        Indice(typeof(OrdersEntity), "MembershipSubscriptionId").Should().NotBeNull();
        Indice(typeof(OrdersEntity), "OrderDate").GetIncludeProperties().Should().BeEquivalentTo("Status", "TotalAmount");
    }

    [Fact]
    public void Lineas_por_pedido_con_clave_foranea_y_sin_sombra()
    {
        var t = Tipo(typeof(OrderDetail));
        t.FindProperty("OrdersId").Should().BeNull("la FK en sombra vacía desaparece (M6)");
        Longitud(typeof(OrderDetail), "OrderId").Should().Be(450);
        Longitud(typeof(OrderDetail), "ProductId").Should().Be(450);
        var fk = t.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(OrdersEntity));
        fk.Properties.Single().Name.Should().Be("OrderId");
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        Indice(typeof(OrderDetail), "OrderId").GetIncludeProperties().Should().BeEquivalentTo("ProductId", "Quantity", "UnitPrice");
    }

    [Fact]
    public void Pagos_acotados_indexados_y_sin_clave_foranea()
    {
        var t = Tipo(typeof(PaymentHistory));
        t.FindProperty("OrdersId").Should().BeNull();
        t.GetForeignKeys().Should().BeEmpty("el webhook guarda pagos sin pedido conocido (M7)");
        Longitud(typeof(PaymentHistory), "OrderId").Should().Be(450);
        Longitud(typeof(PaymentHistory), "MemberId").Should().Be(100);
        Longitud(typeof(PaymentHistory), "GatewayTransactionId").Should().Be(200);
        Indice(typeof(PaymentHistory), "OrderId").Should().NotBeNull();
        Indice(typeof(PaymentHistory), "MemberId", "TransactionStatus").GetIncludeProperties().Should().BeEquivalentTo("ProcessedAt");
        Indice(typeof(PaymentHistory), "GatewayTransactionId").Should().NotBeNull();
    }

    [Fact]
    public void La_ultima_orden_de_la_suscripcion_es_una_clave_foranea_real()
    {
        var t = Tipo(typeof(MembershipSubscription));
        t.FindProperty("LastOrderId1").Should().BeNull();
        Longitud(typeof(MembershipSubscription), "LastOrderId").Should().Be(450);
        t.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(OrdersEntity))
            .Properties.Single().Name.Should().Be("LastOrderId");
    }

    [Fact]
    public void Estadisticas_por_miembro()
    {
        Longitud(typeof(MemberStatisticEntity), "MemberId").Should().Be(100);
        Indice(typeof(MemberStatisticEntity), "MemberId").Should().NotBeNull();
    }

    [Fact]
    public void Los_hijos_del_binario_se_leen_sin_ir_a_la_tabla()
        => Indice(typeof(DualTeamEntity), "ParentMemberId").GetIncludeProperties()
            .Should().BeEquivalentTo("MemberId", "Side", "IsDeleted");
}
```

- [x] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDePedidosTests -nologo`
Expected: FAIL (longitudes nulas, índices inexistentes, `OrdersId` presente).

- [x] **Step 3: Quitar la navegación sin uso**

En `Orders.cs`, borrar la línea:

```csharp
    public ICollection<PaymentHistory> Payments { get; set; } = new List<PaymentHistory>();
```

(Nadie la usa; comprobado con `grep -rn "\.Payments\b"`. Volver a comprobarlo antes de borrar.)

- [x] **Step 4: Crear las configuraciones**

`OrdersConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.SharedKernel;
using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M1–M6). Id se queda en nvarchar(450) (es la PK: acortarla no gana nada y obligaría a
/// recrear la PK y sus FK). Cada índice sale de una consulta real; ver el plan.
/// </summary>
public class OrdersConfiguration : IEntityTypeConfiguration<OrdersEntity>
{
    public void Configure(EntityTypeBuilder<OrdersEntity> builder)
    {
        builder.Property(o => o.MemberId).HasMaxLength(20).IsRequired();
        builder.Property(o => o.OrderNo).HasMaxLength(OrderNumberHelper.MaxLength);
        builder.Property(o => o.MembershipSubscriptionId).HasMaxLength(450);

        // Los seis bucles de unicidad (AnyAsync) pasan de recorrer la tabla a un seek.
        builder.HasIndex(o => o.OrderNo).IsUnique().HasFilter("[OrderNo] IS NOT NULL");
        // Orden de alta (Actividad, SignupEnrollmentOrder), historial de facturación, rangos, bonos.
        builder.HasIndex(o => new { o.MemberId, o.OrderDate })
            .IncludeProperties(o => new { o.Status, o.CreationDate, o.IsDeleted });
        // Pedidos de una suscripción (Admin) y unión del panel CEO.
        builder.HasIndex(o => o.MembershipSubscriptionId);
        // Paneles CEO y financiero, estadísticas de promociones: rangos de fecha.
        builder.HasIndex(o => o.OrderDate).IncludeProperties(o => new { o.Status, o.TotalAmount });

        builder.HasMany(o => o.OrderDetails)
            .WithOne()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

`OrderDetailConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>Plan M (M6): toda unión es o.Id == od.OrderId y después od.ProductId == p.Id.</summary>
public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.Property(d => d.OrderId).HasMaxLength(450).IsRequired();
        builder.Property(d => d.ProductId).HasMaxLength(450).IsRequired();
        builder.HasIndex(d => d.OrderId).IncludeProperties(d => new { d.ProductId, d.Quantity, d.UnitPrice });
    }
}
```

`PaymentHistoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M7). Sin FK: el webhook de pagos guarda filas con OrderId/MemberId vacíos si la pasarela no
/// los manda, y una FK lo haría fallar en bucle. El índice ÚNICO por (GatewayName, GatewayTransactionId)
/// está propuesto y pendiente de Nick (R7).
/// </summary>
public class PaymentHistoryConfiguration : IEntityTypeConfiguration<PaymentHistory>
{
    public void Configure(EntityTypeBuilder<PaymentHistory> builder)
    {
        builder.Property(p => p.OrderId).HasMaxLength(450).IsRequired();
        builder.Property(p => p.MemberId).HasMaxLength(100).IsRequired();
        builder.Property(p => p.GatewayTransactionId).HasMaxLength(200);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => new { p.MemberId, p.TransactionStatus }).IncludeProperties(p => p.ProcessedAt);
        builder.HasIndex(p => p.GatewayTransactionId);
    }
}
```

`MembershipSubscriptionConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M8): la navegación LastOrder creaba una FK en sombra (LastOrderId1) vacía en todas las
/// filas. La relación va sobre LastOrderId, que es la columna que se rellena.
/// </summary>
public class MembershipSubscriptionConfiguration : IEntityTypeConfiguration<MembershipSubscription>
{
    public void Configure(EntityTypeBuilder<MembershipSubscription> builder)
    {
        builder.Property(s => s.LastOrderId).HasMaxLength(450);
        builder.HasOne(s => s.LastOrder)
            .WithMany()
            .HasForeignKey(s => s.LastOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

`MemberStatisticConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M9): LegPointMaintenance une por MemberId en cada colocación, el MERGE WITH (HOLDLOCK) de
/// ApplyMemberStatisticDeltasJob busca por MemberId (sin índice bloquea un rango de toda la tabla) y
/// cada página de reportes hace Contains(ids). No es único: no cambia ningún escritor.
/// </summary>
public class MemberStatisticConfiguration : IEntityTypeConfiguration<MemberStatisticEntity>
{
    public void Configure(EntityTypeBuilder<MemberStatisticEntity> builder)
    {
        builder.Property(s => s.MemberId).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.MemberId);
    }
}
```

En `DualTeamConfiguration.cs`, sustituir:

```csharp
        builder.HasIndex(x => x.ParentMemberId);
```

por:

```csharp
        // Plan M (M10): la CTE de DualSubtree y LegPointMaintenance buscan por ParentMemberId y leen
        // MemberId, Side e IsDeleted; incluidas, cada paso es un seek sin ir a la tabla.
        builder.HasIndex(x => x.ParentMemberId).IncludeProperties(x => new { x.MemberId, x.Side, x.IsDeleted });
```

- [x] **Step 5: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDePedidosTests -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo`
Expected: `0 Error(s)`. Si algo usaba `Orders.Payments`, el compilador lo dice: parar y avisar.

- [x] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Configurations/OrdersConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/OrderDetailConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/PaymentHistoryConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/MembershipSubscriptionConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/MemberStatisticConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/DualTeamConfiguration.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Orders/Orders.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/IndicesDePedidosTests.cs
git commit -m "Pedidos, lineas, pagos, suscripciones y estadisticas con longitudes acotadas e indices de las consultas reales; relaciones explicitas por OrderId y LastOrderId en lugar de las FK en sombra vacias; hijos del binario con indice que cubre la CTE"
```

---

### Task 4: Script de duplicados de `OrderNo`

**Files:**
- Create: `Scripts/sql/plan-m/01-orderno-duplicados.sql`

- [x] **Step 1: Comprobar que hay duplicados (estado de partida)**

Run: `sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -W -I -Q "SET NOCOUNT ON; SELECT COUNT(*) AS Grupos FROM (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL GROUP BY OrderNo HAVING COUNT(*) > 1) d"`
Expected: `68` o un número parecido (no 0). Si sale un deadlock (1205), repetir.

- [x] **Step 2: Escribir el script**

```sql
/*
Plan M, Tarea 4 (M4). Renumera los pedidos que repiten OrderNo: el mas antiguo de cada grupo conserva
el numero y los demas reciben "-D1", "-D2"... Idempotente: si no hay duplicados no cambia nada.
SOLO para la base local MLMConquerorGlobalEditionDb. En produccion NO se ejecuta sin decision de Nick (R15).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51029, N'Este script es solo para MLMConquerorGlobalEditionDb.', 1;

DECLARE @Renumerados int;

BEGIN TRAN;

;WITH d AS (
    SELECT o.Id, o.OrderNo,
           ROW_NUMBER() OVER (PARTITION BY o.OrderNo ORDER BY o.CreationDate, o.Id) AS rn
    FROM Orders o
    WHERE o.OrderNo IN (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL
                        GROUP BY OrderNo HAVING COUNT(*) > 1)
)
UPDATE o
SET    o.OrderNo        = d.OrderNo + N'-D' + CAST(d.rn - 1 AS nvarchar(10)),
       o.LastUpdateDate = GETDATE(),           -- hora de servidor
       o.LastUpdateBy   = N'plan-m-duplicados'
FROM   Orders o
JOIN   d ON d.Id = o.Id
WHERE  d.rn > 1;

SET @Renumerados = @@ROWCOUNT;
COMMIT;

PRINT CONCAT(N'Pedidos renumerados: ', @Renumerados);

SELECT COUNT(*) AS GruposDuplicadosRestantes
FROM (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL GROUP BY OrderNo HAVING COUNT(*) > 1) x;
```

- [x] **Step 3: Ejecutarlo en local**

Run: `sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-m/01-orderno-duplicados.sql` (tras la revisión: sin `-v Confirmo=LOCAL`, con otra base o contra un servidor que no sea la máquina local, el script termina en `THROW` 51029–51031 sin tocar nada; si un sufijo `-Dn` ya está ocupado prueba el siguiente)
Expected: `Pedidos renumerados: 84` (152 filas menos 68 grupos) y `GruposDuplicadosRestantes 0`. Ejecutarlo otra vez da `Pedidos renumerados: 0`.

- [x] **Step 4: Commit**

```bash
git add Scripts/sql/plan-m/01-orderno-duplicados.sql
git commit -m "Script idempotente que renumera los OrderNo duplicados de la base local con sufijo -Dn, conservando el mas antiguo de cada grupo"
```

---

### Task 5: Migración `PedidosAcotadosEIndices` con guardas

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_PedidosAcotadosEIndices.cs` (con `dotnet ef`, y la guarda añadida a mano)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/PlanDeEjecucion.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerPedidosTests.cs`

- [x] **Step 1: Escribir las pruebas contra SQL Server (fallan hasta aplicar)**

`PlanDeEjecucion.cs`:

```csharp
using Microsoft.Data.SqlClient;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan estimado (SHOWPLAN_XML) de una consulta, sin ejecutarla. Para comprobar seeks (R12).</summary>
public static class PlanDeEjecucion
{
    public static async Task<string> EstimadoAsync(string sql)
    {
        await using var conexion = new SqlConnection(SqlServerReportes.ConnectionString);
        await conexion.OpenAsync();
        await using (var activar = new SqlCommand("SET SHOWPLAN_XML ON", conexion))
            await activar.ExecuteNonQueryAsync();
        await using var consulta = new SqlCommand(sql, conexion);
        return (string?)await consulta.ExecuteScalarAsync() ?? string.Empty;
    }

    public const string Seek = "PhysicalOp=\"Index Seek\"";
}
```

`SqlServerPedidosTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M, Bloque 1: lo que la migración PedidosAcotadosEIndices deja en la base.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPedidosTests
{
    private static Task<int> BytesAsync(AppDbContext db, string tabla, string columna) =>
        db.Database.SqlQuery<int>($"""
            SELECT CAST(c.max_length AS int) AS [Value] FROM sys.columns c
            WHERE c.object_id = OBJECT_ID({tabla}) AND c.name = {columna}
            """).SingleAsync();

    private static Task<int> CuentaAsync(AppDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<int>(sql).SingleAsync();

    [SqlServerFact]
    public async Task Las_columnas_quedan_acotadas()
    {
        await using var db = SqlServerReportes.Db();

        (await BytesAsync(db, "Orders", "OrderNo")).Should().Be(80);
        (await BytesAsync(db, "Orders", "MemberId")).Should().Be(40);
        (await BytesAsync(db, "Orders", "MembershipSubscriptionId")).Should().Be(900);
        (await BytesAsync(db, "OrderDetails", "OrderId")).Should().Be(900);
        (await BytesAsync(db, "OrderDetails", "ProductId")).Should().Be(900);
        (await BytesAsync(db, "PaymentHistories", "OrderId")).Should().Be(900);
        (await BytesAsync(db, "PaymentHistories", "MemberId")).Should().Be(200);
        (await BytesAsync(db, "PaymentHistories", "GatewayTransactionId")).Should().Be(400);
        (await BytesAsync(db, "MembershipSubscriptions", "LastOrderId")).Should().Be(900);
        (await BytesAsync(db, "MemberStatistics", "MemberId")).Should().Be(200);
    }

    [SqlServerFact]
    public async Task Las_sombras_desaparecen_y_las_claves_foraneas_existen()
    {
        await using var db = SqlServerReportes.Db();

        (await CuentaAsync(db, $"""
            SELECT COUNT(*) AS [Value] FROM sys.columns
            WHERE (object_id = OBJECT_ID('OrderDetails') AND name = 'OrdersId')
               OR (object_id = OBJECT_ID('PaymentHistories') AND name = 'OrdersId')
               OR (object_id = OBJECT_ID('MembershipSubscriptions') AND name = 'LastOrderId1')
            """)).Should().Be(0);
        (await CuentaAsync(db, $"""
            SELECT COUNT(*) AS [Value] FROM sys.foreign_keys
            WHERE name IN ('FK_OrderDetails_Orders_OrderId', 'FK_MembershipSubscriptions_Orders_LastOrderId')
            """)).Should().Be(2);
    }

    [SqlServerFact]
    public async Task Numero_de_pedido_y_lineas_hacen_seek()
    {
        await using var db = SqlServerReportes.Db();
        var numero = "NO-EXISTE";
        var pedido = "NO-EXISTE";

        var planNumero = await PlanDeEjecucion.EstimadoAsync(
            db.Orders.Where(o => o.OrderNo == numero).Select(o => o.Id).ToQueryString());
        var planLineas = await PlanDeEjecucion.EstimadoAsync(
            db.OrderDetails.Where(d => d.OrderId == pedido).Select(d => d.ProductId).ToQueryString());

        planNumero.Should().Contain("IX_Orders_OrderNo").And.Contain(PlanDeEjecucion.Seek);
        planLineas.Should().Contain("IX_OrderDetails_OrderId").And.Contain(PlanDeEjecucion.Seek);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerPedidosTests -nologo`
Expected: FAIL (columnas en -1, sombras presentes).

- [x] **Step 2: Crear la migración**

Usar el comando de la cabecera con el nombre `PedidosAcotadosEIndices`. `Up` debe traer solo lo siguiente:
- `DropForeignKey`, `DropIndex` y `DropColumn` de `OrderDetails.OrdersId`, `PaymentHistories.OrdersId` y `MembershipSubscriptions.LastOrderId1`.
- `AlterColumn` de las diez columnas de la prueba del paso 1 y de `DualTeamTree`, si EF la toca por el índice.
- `CreateIndex` de los índices de la Tarea 3, y `DropIndex` + `CreateIndex` de `IX_DualTeamTree_ParentMemberId`.
- `AddForeignKey` de `FK_OrderDetails_Orders_OrderId` y `FK_MembershipSubscriptions_Orders_LastOrderId`.

- [x] **Step 3: Añadir la guarda al principio de `Up`**

```csharp
        // Plan M (R8, M24): ninguna alteración trunca ni crea una clave sobre datos que no la cumplen.
        // Si una comprobación falla, la migración se detiene ANTES de tocar nada.
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([OrderNo]) / 2 > 40)
    THROW 51020, N'Orders.OrderNo tiene valores de mas de 40 caracteres (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [Orders] WHERE [OrderNo] IS NOT NULL GROUP BY [OrderNo] HAVING COUNT(*) > 1)
    THROW 51021, N'Orders.OrderNo tiene duplicados: ver Scripts/sql/plan-m/01-orderno-duplicados.sql y R15 del Plan M.', 1;
IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([MemberId]) / 2 > 20 OR DATALENGTH([MembershipSubscriptionId]) / 2 > 450)
    THROW 51022, N'Orders.MemberId o MembershipSubscriptionId no caben (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [OrderDetails] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([ProductId]) / 2 > 450)
    THROW 51023, N'OrderDetails.OrderId o ProductId no caben (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [OrderDetails] d WHERE NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = d.[OrderId]))
    THROW 51024, N'OrderDetails tiene lineas sin pedido: no se puede crear FK_OrderDetails_Orders_OrderId.', 1;
IF EXISTS (SELECT 1 FROM [PaymentHistories] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([MemberId]) / 2 > 100 OR DATALENGTH([GatewayTransactionId]) / 2 > 200)
    THROW 51025, N'PaymentHistories.OrderId, MemberId o GatewayTransactionId no caben (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] WHERE DATALENGTH([LastOrderId]) / 2 > 450)
    THROW 51026, N'MembershipSubscriptions.LastOrderId no cabe (Plan M, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] s WHERE s.[LastOrderId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = s.[LastOrderId]))
    THROW 51027, N'MembershipSubscriptions.LastOrderId apunta a pedidos que no existen: no se puede crear la FK.', 1;
IF EXISTS (SELECT 1 FROM [MemberStatistics] WHERE DATALENGTH([MemberId]) / 2 > 100)
    THROW 51028, N'MemberStatistics.MemberId no cabe (Plan M, Tarea 5).', 1;");
```

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_PedidosAcotadosEIndices.cs`
Expected: `0`

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [x] **Step 4: Aplicar a la base local**

Antes, comprobar que la Tarea 4 se ejecutó (`GruposDuplicadosRestantes 0`). Después, lanzar el comando de aplicar de la cabecera.
Expected: `Applying migration '…_PedidosAcotadosEIndices'.` y `Done.` Si sale 51021, falta la Tarea 4. Si se agota el tiempo en una guarda, parar los servicios locales y repetir (R8).

- [x] **Step 5: Ejecutar las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "SqlServerPedidosTests|SqlServerMedicionPlanMTests.Comprobacion_de_numero_de_pedido" -nologo`
Expected: PASS. La comprobación de `OrderNo` ya baja de 50 ms.

- [x] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Migrations/*_PedidosAcotadosEIndices.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_PedidosAcotadosEIndices.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/PlanDeEjecucion.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerPedidosTests.cs
git commit -m "Migracion PedidosAcotadosEIndices: columnas acotadas, OrderNo unico, indices de pedidos, lineas, pagos y estadisticas, FK reales por OrderId y LastOrderId; guardas 51020-51028 que se niegan a truncar o a crear claves sobre huerfanos"
```

---

# Bloque 2 — Nodo de inscripción (escritura)

### Task 6: `GenealogyTree.Node` en el modelo

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/MLMConquerorGlobalEdition.Domain.csproj`
- Modify: `MLMConquerorGlobalEdition.Repository/MLMConquerorGlobalEdition.Repository.csproj`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Tree/GenealogyEntity.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/GenealogyConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/NodoDeGenealogiaModeloTests.cs`

- [x] **Step 1: Comprobar que la versión del paquete existe**

Run: `dotnet package search Microsoft.EntityFrameworkCore.SqlServer.HierarchyId --exact-match`
Expected: aparece `10.0.11`, la versión de EF Core del repositorio. Si no aparece, usar la 10.0.x estable más alta que esté publicada y que sea igual a la de `Microsoft.EntityFrameworkCore.SqlServer`. Si no coinciden, parar y avisar.

- [x] **Step 2: Escribir las pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M (M11, M12, M21): Node es hierarchyid en SQL Server sin que el anfitrión llame a
/// UseHierarchyId, con índice único filtrado e índice en anchura; en InMemory funciona como texto
/// y IsDescendantOf se evalúa en memoria (R11).
/// </summary>
public class NodoDeGenealogiaModeloTests
{
    // Solo construye el modelo: no abre conexión.
    private static AppDbContext SqlServerSinUseHierarchyId() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=.;Database=NoSeAbre;").Options);

    [Fact]
    public void En_SQL_Server_Node_es_hierarchyid_aunque_el_anfitrion_no_lo_pida()
    {
        using var db = SqlServerSinUseHierarchyId();
        db.Model.FindEntityType(typeof(GenealogyEntity))!.FindProperty(nameof(GenealogyEntity.Node))!
            .GetRelationalTypeMapping().StoreType.Should().Be("hierarchyid");
    }

    [Fact]
    public void Indice_unico_filtrado_en_profundidad_y_en_anchura_por_nivel()
    {
        using var db = SqlServerSinUseHierarchyId();
        var tipo = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GenealogyEntity))!;

        var profundidad = tipo.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Node" }));
        profundidad.IsUnique.Should().BeTrue();
        profundidad.GetFilter().Should().Be("[Node] IS NOT NULL");
        profundidad.GetDatabaseName().Should().Be("IX_GenealogyTree_Node");
        profundidad.GetIncludeProperties().Should().BeEquivalentTo("MemberId", "Level");

        tipo.FindProperty("NodeLevel")!.GetComputedColumnSql().Should().Be("[Node].GetLevel()");
        tipo.GetIndexes().Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "NodeLevel", "Node" }));
    }

    [Fact]
    public async Task En_InMemory_se_guarda_y_se_filtra_por_descendencia()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, "/AMB-1/", 1).Node = HierarchyId.Parse("/1/");
        ReportesDb.Nodo(db, "AMB-2", "AMB-1", "/AMB-1/AMB-2/", 2).Node = HierarchyId.Parse("/1/1/");
        ReportesDb.Nodo(db, "AMB-3", null, "/AMB-3/", 1).Node = HierarchyId.Parse("/2/");
        await db.SaveChangesAsync();
        var raiz = HierarchyId.Parse("/1/");

        var equipo = await db.GenealogyTree.Where(g => g.Node!.IsDescendantOf(raiz)).Select(g => g.MemberId).ToListAsync();

        equipo.Should().BeEquivalentTo("AMB-1", "AMB-2");
    }
}
```

(Hasta la Tarea 11, `ReportesDb.Nodo` no rellena `Node`, y por eso la prueba lo asigna a mano.)

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter NodoDeGenealogiaModeloTests -nologo`
Expected: FAIL de compilación (`Node` no existe).

- [x] **Step 3: Paquetes**

En `MLMConquerorGlobalEdition.Domain.csproj`, dentro de un `<ItemGroup>` (crearlo si no hay ninguno):

```xml
    <!-- Plan M: solo el tipo HierarchyId (GenealogyEntity.Node), sin el proveedor de EF. -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer.Abstractions" Version="10.0.11" />
```

En `MLMConquerorGlobalEdition.Repository.csproj`, junto a `Microsoft.EntityFrameworkCore.SqlServer`:

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer.HierarchyId" Version="10.0.11" />
```

- [x] **Step 4: Entidad**

`GenealogyEntity.cs` completo:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Interfaces;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

public class GenealogyEntity : AuditChangesStringKey, ITreeNode
{
    public string MemberId { get; set; } = string.Empty;
    public string? ParentMemberId { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public int Level { get; set; }

    /// <summary>
    /// Posición en el árbol de inscripción (hierarchyid, Plan M). El equipo de un miembro es
    /// Node.IsDescendantOf(su Node). Lo escribe GenealogyNodes.NextChildAsync; nulo solo mientras dura
    /// el relleno inicial (Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql).
    /// </summary>
    public HierarchyId? Node { get; set; }
}
```

- [x] **Step 5: Configuración**

`GenealogyConfiguration.cs` completo:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class GenealogyConfiguration : IEntityTypeConfiguration<GenealogyEntity>
{
    /// <summary>Índice único de Node; GenealogyNodes.IsDuplicateNode lo reconoce por el nombre.</summary>
    public const string NodeIndexName = "IX_GenealogyTree_Node";

    public void Configure(EntityTypeBuilder<GenealogyEntity> builder)
    {
        builder.HasKey(x => x.Id);
        // Ruta de texto: se sigue escribiendo y la usan quienes parten la línea ascendente (Plan M, M15).
        // Ya no se filtra por ella: el equipo va por Node.
        builder.Property(x => x.HierarchyPath).IsRequired();
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
        builder.HasIndex(x => x.ParentMemberId);

        // Plan M (M11): en profundidad, el subárbol es un rango contiguo del índice.
        builder.Property(x => x.Node);
        builder.HasIndex(x => x.Node)
            .IsUnique()
            .HasFilter("[Node] IS NOT NULL")
            .HasDatabaseName(NodeIndexName)
            .IncludeProperties(x => new { x.MemberId, x.Level });

        // Plan M (M12): en anchura, solo para reservar el ordinal de una raíz nueva.
        builder.Property<short?>("NodeLevel").HasComputedColumnSql("[Node].GetLevel()", stored: true);
        builder.HasIndex("NodeLevel", nameof(GenealogyEntity.Node));

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

- [x] **Step 6: `AppDbContext`**

Añadir los `using`:

```csharp
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
```

Sustituir `OnModelCreating` y `OnConfiguring` por:

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Plan M (R11): fuera de SQL Server (InMemory en las pruebas) no hay hierarchyid; Node se guarda
        // como su texto ("/1/2/") y IsDescendantOf/GetAncestor se evalúan en memoria con la misma semántica.
        if (!Database.IsSqlServer())
            modelBuilder.Entity<GenealogyEntity>().Property(g => g.Node)
                .HasConversion(h => h!.ToString(), s => HierarchyId.Parse(s));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // SecurityFactorsInterceptor va aquí y no en el registro de cada anfitrión por la misma
        // razón que existe: un anfitrión que se olvidara de añadirlo cambiaría factores sin sellar.
        // StatusHistoryInterceptor (Plan J), por lo mismo: un anfitrión sin él cambiaría estados
        // sin dejar historial.
        optionsBuilder.AddInterceptors(
            new AuditInterceptor(), new SecurityFactorsInterceptor(), new StatusHistoryInterceptor());

        // Plan M (M21): GenealogyTree.Node es hierarchyid. Se activa aquí por la misma razón: un
        // anfitrión (o la fábrica de diseño, o un ayudante de pruebas) que lo olvidara fallaría al
        // construir el modelo. SqlServerOptionsExtension es API interna; lo vigila
        // NodoDeGenealogiaModeloTests.
#pragma warning disable EF1001
        if (optionsBuilder.Options.FindExtension<SqlServerOptionsExtension>() is not null)
            new SqlServerDbContextOptionsBuilder(optionsBuilder).UseHierarchyId();
#pragma warning restore EF1001
    }
```

- [x] **Step 7: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter NodoDeGenealogiaModeloTests -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo`
Expected: `0 Error(s)`.

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo` (suite InMemory del BizCenter)
Expected: PASS. Ninguna prueba existente lee todavía `Node`.

- [x] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/MLMConquerorGlobalEdition.Domain.csproj \
        MLMConquerorGlobalEdition.Repository/MLMConquerorGlobalEdition.Repository.csproj \
        MLMConquerorGlobalEdition.Domain/Entities/Tree/GenealogyEntity.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/GenealogyConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/NodoDeGenealogiaModeloTests.cs
git commit -m "GenealogyTree.Node hierarchyid en el modelo con indice unico filtrado en profundidad y NodeLevel calculado con indice en anchura; UseHierarchyId en AppDbContext y conversor a texto fuera de SQL Server"
```

---

### Task 7: `GenealogyNodes.NextChildAsync`

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Queries/GenealogyNodes.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/GenealogyNodesTests.cs`

- [x] **Step 1: Escribir las pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (M13): el siguiente Node es el hermano siguiente del último hijo del padre.</summary>
public class GenealogyNodesTests
{
    private static void Nodo(AppDbContext db, string id, string? padre, string node)
        => ReportesDb.Nodo(db, id, padre, "/" + id + "/", 1).Node = HierarchyId.Parse(node);

    [Fact]
    public async Task El_primer_hijo_cuelga_del_padre()
    {
        await using var db = ReportesDb.Nueva();
        Nodo(db, "AMB-1", null, "/1/");
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().Be(HierarchyId.Parse("/1/1/"));
    }

    [Fact]
    public async Task El_siguiente_hijo_va_detras_del_ultimo_aunque_este_borrado()
    {
        await using var db = ReportesDb.Nueva();
        Nodo(db, "AMB-1", null, "/1/");
        Nodo(db, "AMB-2", "AMB-1", "/1/1/");
        Nodo(db, "AMB-3", "AMB-1", "/1/2/");
        await db.SaveChangesAsync();
        var borrado = await db.GenealogyTree.SingleAsync(g => g.MemberId == "AMB-3");
        borrado.IsDeleted = true;
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().Be(HierarchyId.Parse("/1/3/"),
            "un ordinal no se reutiliza: el índice único no está filtrado por IsDeleted");
    }

    [Fact]
    public async Task Si_el_padre_aun_no_tiene_nodo_devuelve_nulo()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, "/AMB-1/", 1).Node = null;
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().BeNull();
    }

    [Fact]
    public async Task Una_raiz_nueva_va_detras_de_la_ultima_raiz()
    {
        await using var db = ReportesDb.Nueva();
        (await GenealogyNodes.NextChildAsync(db, null)).Should().Be(HierarchyId.Parse("/1/"));

        Nodo(db, "AMB-1", null, "/1/");
        Nodo(db, "AMB-2", "AMB-HUERFANO", "/2/");   // padre inexistente: cuelga de la raíz
        Nodo(db, "AMB-3", "AMB-1", "/1/1/");
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, null)).Should().Be(HierarchyId.Parse("/3/"));
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter GenealogyNodesTests -nologo`
Expected: FAIL de compilación.

- [x] **Step 2: Implementar**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Configurations;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Escritura de GenealogyTree.Node (Plan M, M13). Único sitio que decide el Node de un nodo nuevo.
/// Mover un subárbol (cambio de patrocinador, hoy inexistente) NO se hace nodo a nodo: un único UPDATE
/// con Node.GetReparentedValue(@viejo, @nuevo) sobre todo el subárbol, en una transacción con
/// sp_getapplock sobre los dos patrocinadores y con el ordinal nuevo reservado aquí (R3).
/// </summary>
public static class GenealogyNodes
{
    /// <summary>
    /// El Node del siguiente hijo de <paramref name="parentMemberId"/> (null = una raíz nueva). Devuelve
    /// null si el padre todavía no tiene Node (relleno inicial pendiente): el relleno lo pondrá.
    /// Dos altas simultáneas bajo el mismo padre obtienen el mismo valor; el índice único
    /// IX_GenealogyTree_Node rechaza a la segunda y el alta reintenta (IsDuplicateNode).
    /// </summary>
    public static async Task<HierarchyId?> NextChildAsync(
        AppDbContext db, string? parentMemberId, CancellationToken ct = default)
    {
        // Con los borrados lógicos: un ordinal no se reutiliza (el índice único no filtra IsDeleted).
        var arbol = db.GenealogyTree.AsNoTracking().IgnoreQueryFilters();

        if (parentMemberId is null)
        {
            // Hijas de la raíz de hierarchyid: índice (NodeLevel, Node), M12.
            var ultimaRaiz = await arbol
                .Where(g => g.Node != null && g.Node.GetLevel() == 1)
                .OrderByDescending(g => g.Node)
                .Select(g => g.Node)
                .FirstOrDefaultAsync(ct);
            return HierarchyId.GetRoot().GetDescendant(ultimaRaiz, null);
        }

        // El nodo más reciente del padre (R8 del Plan J).
        var padre = await arbol
            .Where(g => g.MemberId == parentMemberId && g.Node != null)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);
        if (padre is null)
            return null;

        // Los hermanos por ParentMemberId (índice); GetAncestor descarta los que cuelgan de un nodo
        // antiguo del mismo padre.
        var ultimoHijo = await arbol
            .Where(g => g.ParentMemberId == parentMemberId && g.Node != null && g.Node.GetAncestor(1) == padre)
            .OrderByDescending(g => g.Node)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);

        return padre.GetDescendant(ultimoHijo, null);
    }

    /// <summary>True si el guardado falló porque otro alta ya ocupó ese Node (R5).</summary>
    public static bool IsDuplicateNode(DbUpdateException ex)
        => ex.InnerException?.Message is { } m
           && m.Contains(GenealogyConfiguration.NodeIndexName, StringComparison.OrdinalIgnoreCase);
}
```

- [x] **Step 3: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter GenealogyNodesTests -nologo`
Expected: PASS.

- [x] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Queries/GenealogyNodes.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/GenealogyNodesTests.cs
git commit -m "GenealogyNodes.NextChildAsync: el Node de un nodo nuevo es el hermano siguiente del ultimo hijo del nodo mas reciente del padre, sin reutilizar ordinales borrados; las raices cuelgan de la raiz de hierarchyid"
```

---

### Task 8: Los escritores mantienen `Node` y la regla los vigila

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs:216-235` y su `catch` (línea ~334)
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs:156-175` y su `catch` (línea 205)
- Modify: `MLMConquerorGlobalEdition.Repository/Seeders/RootAmbassadorSeeder.cs:67-76`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupMemberHandlerTests.cs` (prueba nueva al final)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupAmbassadorHandlerTests.cs` (prueba nueva al final)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/NodosDeGenealogiaTests.cs`

- [x] **Step 1: Escribir las pruebas que fallan**

Al final de `SignupMemberHandlerTests` (antes de su última `}`):

```csharp
    [Fact]
    public async Task Handle_ElNodoDelInscritoCuelgaDelNodoDelPatrocinador()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.MemberProfiles.AddAsync(BuildSponsor("AMB-000001", "pedro-site"));
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId = "AMB-000001", HierarchyPath = "/AMB-000001/", Level = 1,
            Node = HierarchyId.Parse("/1/"),
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: "pedro-site")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.GenealogyTree.SingleAsync(g => g.MemberId != "AMB-000001")).Node
            .Should().Be(HierarchyId.Parse("/1/1/"));
    }
```

Al final de `SignupAmbassadorHandlerTests` (antes de su última `}`):

```csharp
    [Fact]
    public async Task Handle_ElNodoDelInscritoCuelgaDelNodoDelPatrocinador()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.MemberProfiles.AddAsync(BuildSponsor("AMB-000001", "maria-site"));
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId = "AMB-000001", HierarchyPath = "/AMB-000001/", Level = 1,
            Node = HierarchyId.Parse("/1/"),
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new SignupAmbassadorCommand(BuildRequest(sponsorSlug: "maria-site", slug: "nodo-site")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.GenealogyTree.SingleAsync(g => g.MemberId != "AMB-000001")).Node
            .Should().Be(HierarchyId.Parse("/1/1/"));
    }
```

(Si alguna de las dos clases no tiene `BuildLevel`, `FixedNow` o el parámetro `slug` con esos nombres, se usan los equivalentes que ya tenga la clase. Las dos clases tienen `using Microsoft.EntityFrameworkCore;`, que trae `HierarchyId`.)

`NodosDeGenealogiaTests.cs`:

```csharp
using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Regla del Plan M (R2), hermana de RutasConBarraFinalTests: quien crea un nodo del árbol de
/// inscripción le asigna Node, los sitios que lo hacen son los revisados, y nadie reescribe
/// GenealogyTree en bloque (un ExecuteUpdate o un UPDATE en SQL dejarían Node incoherente con la
/// estructura). Un sitio nuevo hace fallar la prueba: hay que revisarlo (GenealogyNodes) y añadirlo.
/// </summary>
public class NodosDeGenealogiaTests
{
    private static readonly Regex Alta =
        new(@"new\s+GenealogyEntity\s*\{(?<cuerpo>.*?)\};", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AsignaNode = new(@"\bNode\s*=(?!=)", RegexOptions.Compiled);
    private static readonly Regex EscrituraEnBloque =
        new(@"ExecuteUpdate|ExecuteDelete|UPDATE\s+\[?GenealogyTree", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Archivo (relativo, con "/") → cuántos nodos de inscripción crea.</summary>
    private static readonly Dictionary<string, int> Revisados = new()
    {
        ["MLMConquerorGlobalEdition.Repository/Seeders/RootAmbassadorSeeder.cs"]                                     = 1,
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs"] = 1,
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs"]         = 1,
    };

    private static IEnumerable<(string Archivo, string Texto)> Fuentes()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}"))
                continue;
            yield return (Path.GetRelativePath(raiz, archivo).Replace('\\', '/'), File.ReadAllText(archivo));
        }
    }

    [Fact]
    public void Todo_nodo_de_inscripcion_nuevo_lleva_Node()
    {
        var sinNode = Fuentes()
            .SelectMany(f => Alta.Matches(f.Texto).Select(m => (f.Archivo, Cuerpo: m.Groups["cuerpo"].Value)))
            .Where(a => !AsignaNode.IsMatch(a.Cuerpo))
            .Select(a => a.Archivo)
            .ToList();

        sinNode.Should().BeEmpty("un nodo sin Node no aparece en ningún equipo (Plan M, R2)");
    }

    [Fact]
    public void Los_sitios_que_crean_nodos_de_inscripcion_son_los_revisados()
    {
        var actuales = Fuentes()
            .Select(f => (f.Archivo, Altas: Alta.Matches(f.Texto).Count))
            .Where(f => f.Altas > 0)
            .ToDictionary(f => f.Archivo, f => f.Altas);

        actuales.Should().BeEquivalentTo(Revisados,
            "un sitio nuevo que crea nodos de inscripción tiene que calcular Node con GenealogyNodes y añadirse aquí");
    }

    [Fact]
    public void Nadie_reescribe_GenealogyTree_en_bloque()
    {
        var hallazgos = Fuentes()
            .SelectMany(f => f.Texto.Split(';').Select(s => (f.Archivo, Sentencia: s)))
            .Where(s => s.Sentencia.Contains("GenealogyTree", StringComparison.Ordinal)
                     && EscrituraEnBloque.IsMatch(s.Sentencia))
            .Select(s => s.Archivo)
            .ToList();

        hallazgos.Should().BeEmpty("mover nodos exige GetReparentedValue sobre todo el subárbol (Plan M, R3)");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter ElNodoDelInscritoCuelgaDelNodoDelPatrocinador -nologo`
Expected: FAIL (`Node` nulo).

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter NodosDeGenealogiaTests -nologo`
Expected: FAIL en `Todo_nodo_de_inscripcion_nuevo_lleva_Node`, con los tres archivos.

- [x] **Step 2: Implementar en `SignupMemberHandler.cs`**

Añadir `using MLMConquerorGlobalEdition.Repository.Queries;`. Sustituir:

```csharp
        var genealogyNode = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = sponsorMemberId,
```

por:

```csharp
        // Plan M: el Node cuelga del nodo del patrocinador (o es una raíz nueva si no tiene nodo).
        // Si el patrocinador aún no tiene Node (relleno pendiente), queda nulo y lo pondrá el relleno.
        var genealogyNodeId = await GenealogyNodes.NextChildAsync(_db, sponsorNode?.MemberId, ct);

        var genealogyNode = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = sponsorMemberId,
            Node           = genealogyNodeId,
```

y sustituir:

```csharp
        catch (DbUpdateException ex) when (IsDuplicateMemberIdViolation(ex) && idAttempt < 5)
```

por:

```csharp
        // Plan M (R5): dos altas simultáneas bajo el mismo patrocinador pueden calcular el mismo Node;
        // el índice único rechaza a la segunda, que reintenta con el siguiente ordinal.
        catch (DbUpdateException ex) when ((IsDuplicateMemberIdViolation(ex) || GenealogyNodes.IsDuplicateNode(ex)) && idAttempt < 5)
```

- [x] **Step 3: Implementar en `SignupAmbassadorHandler.cs`**

Exactamente las mismas dos sustituciones que en el paso 2 (los bloques de origen son idénticos) y el mismo `using`.

- [x] **Step 4: Implementar en `RootAmbassadorSeeder.cs`**

Añadir `using MLMConquerorGlobalEdition.Repository.Queries;`. Sustituir:

```csharp
        var genealogy = new GenealogyEntity
        {
            MemberId       = RootMemberCode,
            ParentMemberId = null,
```

por:

```csharp
        var genealogy = new GenealogyEntity
        {
            MemberId       = RootMemberCode,
            ParentMemberId = null,
            Node           = await GenealogyNodes.NextChildAsync(db, null),
```

- [x] **Step 5: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SignupMemberHandlerTests|SignupAmbassadorHandlerTests" -nologo`
Expected: PASS (también las anteriores).

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "NodosDeGenealogiaTests|RutasConBarraFinalTests" -nologo`
Expected: PASS. Si `Nadie_reescribe_GenealogyTree_en_bloque` señala un archivo, se revisa: si de verdad reescribe nodos de inscripción, parar y avisar; si es un falso positivo (la sentencia nombra la tabla pero actualiza otra), añadir una exclusión explícita con su motivo.

- [x] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs \
        MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs \
        MLMConquerorGlobalEdition.Repository/Seeders/RootAmbassadorSeeder.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupMemberHandlerTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupAmbassadorHandlerTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/NodosDeGenealogiaTests.cs
git commit -m "El alta y la semilla de la raiz escriben GenealogyTree.Node con GenealogyNodes y el alta reintenta si otro alta ocupo el mismo nodo; regla que exige Node en todo nodo de inscripcion nuevo y prohibe reescribir el arbol en bloque"
```

---

### Task 9: Migración `NodoDeGenealogia`

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_NodoDeGenealogia.cs` (con `dotnet ef`)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerNodoDeGenealogiaTests.cs`

- [x] **Step 1: Escribir la prueba de esquema (falla hasta aplicar)**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M, Bloque 2: esquema, relleno y coherencia de GenealogyTree.Node en la base local.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerNodoDeGenealogiaTests
{
    [SqlServerFact]
    public async Task Node_es_hierarchyid_con_sus_dos_indices()
    {
        await using var db = SqlServerReportes.Db();

        var tipo = await db.Database.SqlQuery<string>($"""
            SELECT TYPE_NAME(c.user_type_id) AS [Value] FROM sys.columns c
            WHERE c.object_id = OBJECT_ID('GenealogyTree') AND c.name = 'Node'
            """).SingleAsync();
        var indices = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE object_id = OBJECT_ID('GenealogyTree')
              AND ((name = 'IX_GenealogyTree_Node' AND is_unique = 1 AND has_filter = 1)
                OR name = 'IX_GenealogyTree_NodeLevel_Node')
            """).SingleAsync();

        tipo.Should().Be("hierarchyid");
        indices.Should().Be(2);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerNodoDeGenealogiaTests -nologo`
Expected: FAIL.

- [x] **Step 2: Crear la migración**

Con el comando de la cabecera y el nombre `NodoDeGenealogia`. `Up` debe traer solo esto:
- `AddColumn<HierarchyId>` `Node` (`hierarchyid`, admite nulos).
- `AddColumn<short>` `NodeLevel` con `computedColumnSql: "[Node].GetLevel()", stored: true`.
- `CreateIndex` `IX_GenealogyTree_Node` (único, `filter: "[Node] IS NOT NULL"`, con `IncludeProperties` `MemberId` y `Level`).
- `CreateIndex` `IX_GenealogyTree_NodeLevel_Node`.

No lleva guarda: no altera ni trunca nada y es una operación de metadatos (R1).

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_NodoDeGenealogia.cs`
Expected: `0`

- [x] **Step 3: Aplicar a la base local**

Lanzar el comando de aplicar de la cabecera. Expected: `Applying migration '…_NodoDeGenealogia'.` y `Done.`

- [x] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerNodoDeGenealogiaTests -nologo`
Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Migrations/*_NodoDeGenealogia.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_NodoDeGenealogia.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerNodoDeGenealogiaTests.cs
git commit -m "Migracion NodoDeGenealogia: columna Node hierarchyid que admite nulos, NodeLevel calculado y persistido, indice unico filtrado en profundidad e indice en anchura"
```

---

### Task 10: Relleno de `Node` y comprobación de coherencia

**Files:**
- Create: `Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerNodoDeGenealogiaTests.cs`

- [x] **Step 1: Escribir las pruebas que fallan**

Añadir a `SqlServerNodoDeGenealogiaTests` (y `using System.Data;`, `using MLMConquerorGlobalEdition.Repository.Queries;`):

```csharp
    /// <summary>Condición de despliegue del Bloque 3 (R17): ninguna fila sin Node y todas coherentes.</summary>
    internal const string Coherencia = """
        SELECT
          (SELECT COUNT(*) FROM GenealogyTree WHERE Node IS NULL) AS SinNode,
          (SELECT COUNT(*) FROM GenealogyTree c
           OUTER APPLY (SELECT TOP (1) p.Node FROM GenealogyTree p
                        WHERE p.MemberId = c.ParentMemberId ORDER BY p.CreationDate DESC) pp
           WHERE c.Node IS NOT NULL
             AND ((pp.Node IS NULL AND c.Node.GetLevel() <> 1)
               OR (pp.Node IS NOT NULL AND c.Node.GetAncestor(1) <> pp.Node))) AS Incoherentes
        """;

    private sealed class Resultado { public int SinNode { get; set; } public int Incoherentes { get; set; } }

    [SqlServerFact]
    public async Task Todas_las_filas_tienen_un_Node_coherente_con_su_padre()
    {
        await using var db = SqlServerReportes.Db();
        var r = await db.Database.SqlQueryRaw<Resultado>(Coherencia).SingleAsync();

        r.SinNode.Should().Be(0);
        r.Incoherentes.Should().Be(0);
    }

    [SqlServerFact]
    public Task El_equipo_por_Node_es_el_mismo_que_por_la_ruta() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var muestra = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.Level >= 1 && g.Level <= 4 && g.Node != null)
            .OrderBy(g => g.MemberId)
            .Select(g => new { g.HierarchyPath, g.Node })
            .Take(20)
            .ToListAsync();

        foreach (var m in muestra)
        {
            var prefijo = TreePrefix.Of(m.HierarchyPath);
            var porRuta = await db.GenealogyTree.CountAsync(g => g.HierarchyPath.StartsWith(prefijo));
            var porNodo = await db.GenealogyTree.CountAsync(g => g.Node!.IsDescendantOf(m.Node));
            porNodo.Should().Be(porRuta, m.HierarchyPath);
        }
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Un_alta_nueva_cuelga_de_su_patrocinador_en_SQL() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var patrocinador = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.Node != null && g.Level == 2).OrderBy(g => g.MemberId)
            .Select(g => new { g.MemberId, g.Node }).FirstAsync();

        var nuevo = await GenealogyNodes.NextChildAsync(db, patrocinador.MemberId);
        var otraRaiz = await GenealogyNodes.NextChildAsync(db, null);

        nuevo!.GetAncestor(1).Should().Be(patrocinador.Node);
        (await db.GenealogyTree.IgnoreQueryFilters().AnyAsync(g => g.Node == nuevo)).Should().BeFalse();
        otraRaiz!.GetLevel().Should().Be(1);
        (await db.GenealogyTree.IgnoreQueryFilters().AnyAsync(g => g.Node == otraRaiz)).Should().BeFalse();
        await tx.RollbackAsync();
    });
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerNodoDeGenealogiaTests -nologo`
Expected: FAIL en las tres nuevas (todas las filas tienen `Node` nulo).

- [x] **Step 2: Escribir el script**

```sql
/*
Plan M, Tarea 10 (M14, R1). Rellena GenealogyTree.Node a partir de ParentMemberId.
- Idempotente: solo toca filas con Node NULL cuyo padre ya tiene Node, o que son raices (sin padre o
  con un padre que no existe). Cada pasada avanza un nivel; se puede cortar y relanzar.
- Por lotes de @Lote filas, cada lote en su transaccion.
- Ordinales como GenealogyNodes.NextChildAsync: ultimo ordinal del grupo + ROW_NUMBER por (CreationDate, Id).
- Si un alta simultanea ocupa un Node, el lote falla (indice unico) y basta con relanzar.
- En local: solo MLMConquerorGlobalEditionDb. En produccion: ventana con poco trafico, tras la migracion
  NodoDeGenealogia y ANTES de arrancar los servicios (despliegue unico, revision C1). Version vigente: el script del repositorio.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51031, N'Este script es solo para MLMConquerorGlobalEditionDb.', 1;

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
```

- [x] **Step 3: Ejecutarlo en local**

Run: `sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -i Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql`
Expected: unas 9 pasadas (8 niveles más la última, vacía). `Rellenadas 119446 filas en total` (o el número de filas de la tabla). Al final, `SinNode 0` e `Incoherentes 0`. Si se relanza, `Rellenadas 0`.

- [x] **Step 4: Ejecutar las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerNodoDeGenealogiaTests -nologo`
Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerNodoDeGenealogiaTests.cs
git commit -m "Script idempotente por lotes que rellena GenealogyTree.Node nivel a nivel desde ParentMemberId con los mismos ordinales que el alta; pruebas de coherencia, de equivalencia con la ruta y del calculo del siguiente nodo en SQL"
```

**Despliegue único (revisión C1).** No hay despliegue intermedio: el Bloque 3 se despliega junto con este, con los servicios parados hasta que el relleno deje `SinNode = 0` e `Incoherentes = 0` (ver «Condiciones de despliegue»).

---

# Bloque 3 — Nodo de inscripción (lectura)

### Task 11: `GenealogySubtree` y nodos de prueba con `Node`

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Queries/GenealogySubtree.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportesDb.cs:65-77`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/GenealogySubtreeTests.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerPlanesDeEjecucionTests.cs`

- [x] **Step 1: Escribir las pruebas que fallan**

`GenealogySubtreeTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (M16): el equipo por Node, con la regla del nodo más reciente (R8 del Plan J).</summary>
public class GenealogySubtreeTests
{
    [Fact]
    public async Task ReportesDb_da_a_cada_nodo_el_Node_de_su_ruta()
    {
        await using var db = ReportesDb.Nueva();
        var a = ReportesDb.Nodo(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        var b = ReportesDb.Nodo(db, "AMB-2", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-2"), 2);
        var c = ReportesDb.Nodo(db, "AMB-3", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-3"), 2);
        await db.SaveChangesAsync();

        b.Node!.GetAncestor(1).Should().Be(a.Node);
        c.Node!.GetAncestor(1).Should().Be(a.Node);
        b.Node.Should().NotBe(c.Node);
    }

    [Fact]
    public async Task El_equipo_incluye_al_que_mira_y_descarta_los_nodos_obsoletos()
    {
        await using var db = ReportesDb.Nueva();
        var yo = ReportesDb.Nodo(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        ReportesDb.Nodo(db, "AMB-2", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-2"), 2);
        ReportesDb.Nodo(db, "AMB-9", null, ReportesDb.Ruta("AMB-9"), 1);
        await db.SaveChangesAsync();
        // AMB-3 estuvo bajo AMB-1 (nodo antiguo) y ahora cuelga de AMB-9 (nodo más reciente).
        await ReportesDb.NodoObsoletoAsync(db, "AMB-3", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-3"), 2);
        ReportesDb.Nodo(db, "AMB-3", "AMB-9", ReportesDb.Ruta("AMB-9", "AMB-3"), 2);
        await db.SaveChangesAsync();

        var equipo = await db.GenealogyTree.AsNoTracking().CurrentUnder(yo.Node!).Select(g => g.MemberId).ToListAsync();

        equipo.Should().BeEquivalentTo("AMB-1", "AMB-2");
    }

    [Fact]
    public async Task El_nodo_de_un_miembro_es_el_mas_reciente()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        ReportesDb.Nodo(db, "AMB-9", null, ReportesDb.Ruta("AMB-9"), 1);
        await db.SaveChangesAsync();
        await ReportesDb.NodoObsoletoAsync(db, "AMB-3", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-3"), 2);
        var actual = ReportesDb.Nodo(db, "AMB-3", "AMB-9", ReportesDb.Ruta("AMB-9", "AMB-3"), 2);
        await db.SaveChangesAsync();

        (await db.GenealogyTree.AsNoTracking().NodeOfAsync("AMB-3")).Should().Be(actual.Node);
        (await db.GenealogyTree.AsNoTracking().NodeOfAsync("NADIE")).Should().BeNull();
    }
}
```

`SqlServerPlanesDeEjecucionTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (R12): las consultas que deben hacer seek, con el SQL que genera EF de verdad.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPlanesDeEjecucionTests
{
    [SqlServerFact]
    public async Task El_equipo_por_Node_hace_seek_en_el_indice_en_profundidad()
    {
        await using var db = SqlServerReportes.Db();
        var raiz = HierarchyId.Parse("/1/");

        var plan = await PlanDeEjecucion.EstimadoAsync(
            db.GenealogyTree.AsNoTracking().CurrentUnder(raiz).Select(g => g.MemberId).ToQueryString());

        plan.Should().Contain("IX_GenealogyTree_Node").And.Contain(PlanDeEjecucion.Seek);
    }

    [SqlServerFact]
    public async Task La_ultima_raiz_hace_seek_en_el_indice_en_anchura()
    {
        await using var db = SqlServerReportes.Db();

        var plan = await PlanDeEjecucion.EstimadoAsync(db.GenealogyTree.AsNoTracking().IgnoreQueryFilters()
            .Where(g => g.Node != null && g.Node.GetLevel() == 1)
            .OrderByDescending(g => g.Node).Select(g => g.Node).Take(1).ToQueryString());

        plan.Should().Contain("IX_GenealogyTree_NodeLevel_Node").And.Contain(PlanDeEjecucion.Seek);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "GenealogySubtreeTests|SqlServerPlanesDeEjecucionTests" -nologo`
Expected: FAIL de compilación (`CurrentUnder` no existe).

- [x] **Step 2: Implementar `GenealogySubtree`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Lectura del equipo de inscripción por GenealogyTree.Node (Plan M, M16). Sustituye a
/// HierarchyPath.StartsWith(TreePrefix.Of(...)), que recorría la tabla entera. SinPrefijoEnInscripcionTests
/// vigila que nadie vuelva a filtrar el árbol de inscripción por la ruta.
/// </summary>
public static class GenealogySubtree
{
    /// <summary>El Node vigente del miembro: el de su nodo más reciente (R8 del Plan J). Null si no tiene.</summary>
    public static Task<HierarchyId?> NodeOfAsync(
        this IQueryable<GenealogyEntity> tree, string memberId, CancellationToken ct = default) =>
        tree.Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Los nodos VIGENTES (el más reciente de cada miembro) que cuelgan de <paramref name="root"/>, él
    /// incluido. Parte del rango de IX_GenealogyTree_Node (seek) y descarta los obsoletos con el índice
    /// (MemberId, CreationDate). Es la forma B: para el equipo entero.
    /// </summary>
    public static IQueryable<GenealogyEntity> CurrentUnder(this IQueryable<GenealogyEntity> tree, HierarchyId root) =>
        tree.Where(g => g.Node!.IsDescendantOf(root)
                     && !tree.Any(o => o.MemberId == g.MemberId && o.CreationDate > g.CreationDate));
}
```

- [x] **Step 3: `ReportesDb.Nodo` rellena `Node`**

En `ReportesDb.cs`, añadir `using Microsoft.EntityFrameworkCore;` si falta, sustituir el cuerpo de `Nodo` por:

```csharp
    public static GenealogyEntity Nodo(AppDbContext db, string memberId, string? padre, string ruta, int nivel)
    {
        var nodo = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = padre,
            HierarchyPath  = ruta,
            Level          = nivel,
            Node           = NodoDesdeRuta(db, ruta),
            CreatedBy      = "prueba"
        };
        db.GenealogyTree.Add(nodo);
        return nodo;
    }

    /// <summary>
    /// Plan M: rellena Node en los nodos seguidos que no lo tengan, a partir de su ruta, como hace el
    /// relleno (02-relleno-nodo-genealogia.sql). Para las pruebas que crean GenealogyEntity a mano.
    /// </summary>
    public static void RellenarNodos(AppDbContext db)
    {
        foreach (var g in db.GenealogyTree.Local.Where(g => g.Node is null).OrderBy(g => g.HierarchyPath.Length).ToList())
            g.Node = NodoDesdeRuta(db, g.HierarchyPath);
    }

    /// <summary>
    /// El Node que corresponde a una ruta. Las raíces reciben un ordinal alto al azar, para no chocar con
    /// las de la base local en las pruebas [SqlServerFact]; los hijos, el siguiente entre los hermanos.
    /// </summary>
    private static HierarchyId NodoDesdeRuta(AppDbContext db, string ruta)
    {
        var segmentos = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segmentos.Length <= 1)
            return HierarchyId.Parse($"/{Random.Shared.Next(1_000_000, int.MaxValue)}/");

        var rutaPadre = "/" + string.Join('/', segmentos[..^1]) + "/";
        var padre = db.GenealogyTree.Local
                        .Where(g => g.HierarchyPath == rutaPadre && g.Node is not null)
                        .OrderByDescending(g => g.CreationDate).Select(g => g.Node).FirstOrDefault()
                    ?? db.GenealogyTree.AsNoTracking()
                        .Where(g => g.HierarchyPath == rutaPadre && g.Node != null)
                        .OrderByDescending(g => g.CreationDate).Select(g => g.Node).FirstOrDefault()
                    ?? throw new InvalidOperationException($"Crea antes el nodo padre {rutaPadre}.");

        var hermanosSeguidos = db.GenealogyTree.Local.Count(g => g.Node is not null && g.Node.GetAncestor(1) == padre);
        var hermanosGuardados = db.GenealogyTree.AsNoTracking().IgnoreQueryFilters()
            .Count(g => g.Node != null && g.Node.GetAncestor(1) == padre);
        return HierarchyId.Parse($"{padre}{Math.Max(hermanosSeguidos, hermanosGuardados) + 1}/");
    }
```

(En SQL Server, `hermanosGuardados` usa la consulta por `GetAncestor` sobre el árbol de prueba recién creado, que cuelga de una raíz al azar. Es pequeño y no afecta a la medición.)

- [x] **Step 4: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "GenealogySubtreeTests|SqlServerPlanesDeEjecucionTests|NodoDeGenealogiaModeloTests" -nologo`
Expected: PASS.
- Si `NodoDeGenealogiaModeloTests.En_InMemory…` falla porque ahora `Nodo` asigna un `Node` al azar antes de que la prueba lo sobrescriba, no pasa nada: la prueba lo sobrescribe y debe seguir en verde.
- Si `La_ultima_raiz_hace_seek…` falla sin *seek*: cambiar en `GenealogyNodes` el filtro `g.Node.GetLevel() == 1` por `EF.Property<short?>(g, "NodeLevel") == 1`, cambiar la prueba igual y repetir. Si sigue sin *seek*, parar y avisar (M12).

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`
Expected: PASS (las pruebas de reportes siguen leyendo la ruta).

- [x] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Queries/GenealogySubtree.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportesDb.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/GenealogySubtreeTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerPlanesDeEjecucionTests.cs
git commit -m "GenealogySubtree: nodo vigente de un miembro y nodos vigentes bajo un Node con seek en el indice en profundidad; los nodos de prueba reciben el Node de su ruta"
```

---

### Task 12: Los lectores de `Repository` pasan a `Node`

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityService.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/TeamStatusChangesService.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Ranks/EnrollmentTeamPointsService.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventRegistrationsService.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Commissions/CommissionsService.cs`
- Test: las existentes (`Reportes/*`, `CalificadoEnLosEquiposTests`, `TeamHandlersTests`, `CommissionHandlersTests`) y `Rendimiento/SinPrefijoEnInscripcionTests.cs` (nuevo)

- [x] **Step 1: Escribir la regla que falla**

`SinPrefijoEnInscripcionTests.cs`:

```csharp
using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Regla del Plan M (M16): ninguna consulta del árbol de INSCRIPCIÓN filtra por la ruta de texto
/// (HierarchyPath.StartsWith/Contains, o una ruta leída del árbol con StartsWith). El equipo se
/// acota con Node (GenealogySubtree). Se mira por sentencia (hasta el ';') que nombre GenealogyTree,
/// sin comentarios. El binario (DualTeamTree) no entra: sigue con su ruta o con DualSubtree.
/// </summary>
public class SinPrefijoEnInscripcionTests
{
    private static readonly Regex Prefijo =
        new(@"\b(HierarchyPath|node|path|ruta)\s*\.\s*(StartsWith|Contains)\s*\(", RegexOptions.Compiled);
    private static readonly Regex Comentario = new(@"//.*$", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void Ninguna_consulta_del_arbol_de_inscripcion_filtra_por_la_ruta()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        var hallazgos = new List<string>();

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}"))
                continue;

            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            foreach (var sentencia in Comentario.Replace(File.ReadAllText(archivo), string.Empty).Split(';'))
                if (sentencia.Contains("GenealogyTree", StringComparison.Ordinal) && Prefijo.IsMatch(sentencia))
                    hallazgos.Add($"{relativo}: {Prefijo.Match(sentencia).Value}");
        }

        hallazgos.Should().BeEmpty("el equipo de inscripción se acota con Node.IsDescendantOf (Plan M), no con un LIKE sobre HierarchyPath");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SinPrefijoEnInscripcionTests -nologo`
Expected: FAIL con la lista de sitios. Debe incluir, como mínimo, los archivos de esta tarea y de la 13. Esa lista es la que hay que cumplir.

- [x] **Step 2: `EnrollmentTeamService.cs`**

| Sitio | Antes | Después |
|---|---|---|
| `GetMyTeamAsync` y la rejilla (líneas 84 y 128) | `BuildMyTeamQueryable(myNode.HierarchyPath, myNode.Level, memberId)` | `BuildMyTeamQueryable(myNode.Node!, myNode.Level, memberId)` |
| `BuildMyTeamQueryable`, firma | `(string pathPrefix, int rootLevel, string memberId)` | `(HierarchyId root, int rootLevel, string memberId)` |
| `BuildMyTeamQueryable`, cuerpo | `var prefix = TreePrefix.Of(pathPrefix);` y `where g.HierarchyPath.StartsWith(prefix) && g.MemberId != memberId` | se borra la línea de `prefix`; `where g.Node!.IsDescendantOf(root) && g.MemberId != memberId` |
| `GetBranchDetailAsync` (líneas 412–416) | `var pathPrefix = TreePrefix.Of(branchNode.HierarchyPath);` y `.Where(g => g.HierarchyPath.StartsWith(pathPrefix) && g.MemberId != branchMemberId)` | se borra la línea de `pathPrefix`; `.Where(g => g.Node!.IsDescendantOf(branchNode.Node!) && g.MemberId != branchMemberId)` |
| `GetCustomersAsync` / `GetCustomersGridAsync` (526, 559) | `BuildCustomerQueryable(memberId, myNode.HierarchyPath, myNode.Level, …)` | `BuildCustomerQueryable(memberId, myNode.Node!, myNode.Level, …)` |
| `BuildCustomerQueryable`, firma | `(string memberId, string pathPrefix, int rootLevel, CustomerScope scope)` | `(string memberId, HierarchyId root, int rootLevel, CustomerScope scope)` |
| `GetVisualizerStatsAsync` (676–683) | `var pattern = "/" + memberId + "/";` y `where g.HierarchyPath.Contains(pattern)` | `var me = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);` + `if (me is null) return new EnrollmentVisualizerStatsView();` y `where g.Node!.IsDescendantOf(me)` |

En `BuildCustomerQueryable`, rama Equipo, sustituir desde el comentario «Se parte del perfil…» hasta `where node != null && node.HierarchyPath.StartsWith(prefix)` por:

```csharp
        // Plan M (M16, forma B): se parte del rango de Node del que mira y se queda con el nodo VIGENTE de
        // cada miembro (R8): ni duplica a quien tiene dos nodos ni mete a quien se fue con uno obsoleto.
        return
            from g in _db.GenealogyTree.AsNoTracking().CurrentUnder(root)
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            where m.MemberType == MemberType.ExternalMember && m.MemberId != memberId
```

y en el `select` que sigue, `Level = node!.Level - rootLevel,` → `Level = g.Level - rootLevel,`. El resto del `select` no cambia.

Si en el archivo ya no queda ningún uso de `TreePrefix`, se quita su `using` solo si lo marca el compilador como no usado.

- [x] **Step 3: `EnrollmentActivityService.cs`** (forma A, que parte de las altas del rango)

| Antes | Después |
|---|---|
| `.Select(g => new { g.HierarchyPath, g.Level })` (nodo del que mira, línea 61) | `.Select(g => new { g.Node, g.Level })` |
| `var prefix = TreePrefix.Of(me.HierarchyPath);` | se borra; si `me.Node` es nulo: `if (me.Node is null) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));` y `var root = me.Node;` |
| `.Select(g => new { g.HierarchyPath, g.Level })` (`sponsorNode`, línea 79) | `.Select(g => new { g.Node, g.Level })` |
| `where sponsorNode != null && sponsorNode.HierarchyPath.StartsWith(prefix)` | `where sponsorNode != null && sponsorNode.Node!.IsDescendantOf(root)` |

- [x] **Step 4: `EnrollmentTeamPointsService.cs`**

Sustituir:

```csharp
        var subtreePrefix = TreePrefix.Of(node.HierarchyPath);
```

por:

```csharp
        if (node.Node is null)
            return 0;
        var root = node.Node;
```

y `.Where(g => g.HierarchyPath.StartsWith(subtreePrefix))` por `.Where(g => g.Node!.IsDescendantOf(root))`.

- [x] **Step 5: `TeamEventRegistrationsService.cs`**

Sustituir `PrefixAsync` y `Team` completos por:

```csharp
    /// <summary>Node vigente del que mira (R8); null si no tiene nodo.</summary>
    private Task<HierarchyId?> RootAsync(string memberId, CancellationToken ct) =>
        _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);

    /// <summary>Los perfiles del equipo, él incluido: nodo vigente bajo el del que mira (Plan M, forma B).</summary>
    private IQueryable<Domain.Entities.Member.MemberProfile> Team(HierarchyId root) =>
        from g in _db.GenealogyTree.AsNoTracking().CurrentUnder(root)
        join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
        select m;
```

En los dos llamadores (líneas 62 y 120): `var prefix = await PrefixAsync(memberId, ct);` → `var root = await RootAsync(memberId, ct);`, `if (prefix is null)` → `if (root is null)` y `Team(prefix)` → `Team(root)`. En las dos subconsultas `let node = …` (líneas 80–85 y 127–132), que siguen la forma A porque parten de las inscripciones del evento: `.Select(n => n.HierarchyPath)` → `.Select(n => n.Node)` y `where node != null && node.StartsWith(prefix)` → `where node != null && node.IsDescendantOf(root)`.

- [x] **Step 6: `TeamStatusChangesService.cs`** (`EnrollmentQueryAsync`, forma A)

Sustituir:

```csharp
        var path = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(path)) return null;
        var prefix = TreePrefix.Of(path);
```

por:

```csharp
        var root = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);
        if (root is null) return null;
```

y en la subconsulta `let node`: `.Select(g => g.HierarchyPath)` → `.Select(g => g.Node)` y `where node != null && node.StartsWith(prefix)` → `where node != null && node.IsDescendantOf(root)`. La parte binaria (líneas 125 en adelante) no cambia.

- [x] **Step 7: `CommissionsService.cs`**

En los cinco métodos, justo después de calcular `hierarchyFilter` (o al principio, en `GetBoostBonusMemberSummaryAsync`), añadir la lectura del nodo vigente. El filtro de la ruta pasa a `Node`; `g.HierarchyPath` se sigue **seleccionando** donde lo use `GetLeg` (M15).

| Método (línea) | Antes | Después |
|---|---|---|
| `GetBoostBonusMemberSummaryAsync` (536) | `.Where(g => g.HierarchyPath.Contains($"/{memberId}/") && g.MemberId != memberId)` | antes de la consulta: `var me = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);` y, si es `null`, el mismo retorno vacío que ya tiene para `downlineMemberIds.Count == 0`; filtro: `.Where(g => g.Node!.IsDescendantOf(me) && g.MemberId != memberId)` |
| semana de inscripciones (599) | `&& g.HierarchyPath.Contains(hierarchyFilter)` | `var me = …NodeOfAsync(memberId, ct)` antes (si es nulo, el retorno vacío del método) y `&& g.Node!.IsDescendantOf(me)` |
| suscripciones activas (736) | `where g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId` | igual, `where g.Node!.IsDescendantOf(me) && g.MemberId != memberId` |
| bono de coche (781) | `where g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId` | igual |
| rama (822) | `where g.MemberId == branchMemberId \|\| g.HierarchyPath.Contains(hierarchyFilter)` | `var me = …NodeOfAsync(branchMemberId, ct)` antes y `where g.Node!.IsDescendantOf(me)` (incluye a la propia rama) |

En cada método, si `me` es `null`, se devuelve lo mismo que hoy cuando el subárbol sale vacío. Se mira el `return` del método y se repite.

- [x] **Step 8: Ejecutar las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`
Expected: PASS, salvo `SinPrefijoEnInscripcionTests`, que aún señala los archivos de la Tarea 13. Si falla una prueba que crea `GenealogyEntity` a mano (`TeamHandlersTests`, `DistribucionDeTokensTests`, `PropiedadEnElCentroDeNegociosTests`, `TransferTokenInstanceHandlerTests`, `GetDashboardStatsHandlerTests`, `CommissionHandlersTests`) porque sus nodos no tienen `Node`, se llama a `ReportesDb.RellenarNodos(db);` justo antes del `SaveChanges` que siembra el árbol. No se cambia ninguna aserción.

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "SqlServerActividadTests|SqlServerClientesTests|SqlServerEventosTests|SqlServerCambiosDeEstadoTests" -nologo`
Expected: PASS (las pruebas de SQL Server crean sus nodos con `ReportesDb.Nodo`, que ya pone `Node`).

- [x] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/TeamStatusChangesService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Ranks/EnrollmentTeamPointsService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventRegistrationsService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Commissions/CommissionsService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SinPrefijoEnInscripcionTests.cs
# y los archivos de pruebas en los que se haya añadido ReportesDb.RellenarNodos, con su ruta explícita
git commit -m "Mi equipo, clientes, actividad, eventos, cambios de estado, puntos de equipo y comisiones acotan el equipo de inscripcion con Node.IsDescendantOf en lugar del LIKE sobre HierarchyPath; regla que lo vigila"
```

---

### Task 13: Los lectores de los anfitriones pasan a `Node`

**Files:**
- Modify: `MLMConquerorGlobalEdition.RankEngine/Features/GetRankProgress/GetRankProgressHandler.cs:122-130`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Placement/GetAvailableNodes/GetAvailableNodesHandler.cs:143-155`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Tokens/TokenCustody.cs:29-43`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Teams/GetAllTeamMembers/GetAllTeamMembersHandler.cs:24-31`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Teams/GetVisualizerChildren/GetVisualizerChildrenHandler.cs:39-44`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCommissionsController.cs:205-214`

- [x] **Step 1: Comprobar que la regla sigue en rojo con estos archivos**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SinPrefijoEnInscripcionTests -nologo`
Expected: FAIL, y la lista contiene los seis archivos de esta tarea.

- [x] **Step 2: Sustituciones**

En cada archivo, añadir `using MLMConquerorGlobalEdition.Repository.Queries;` si falta.

| Archivo | Antes | Después |
|---|---|---|
| `GetRankProgressHandler` | `var subtreePath = TreePrefix.Of(memberGenealogyNode.HierarchyPath);` y `.Where(g => g.HierarchyPath.StartsWith(subtreePath) && g.MemberId != memberId)` | `var subtreeRoot = memberGenealogyNode.Node;` y `.Where(g => subtreeRoot != null && g.Node!.IsDescendantOf(subtreeRoot) && g.MemberId != memberId)` (la parte binaria, `dualSubtreePath`, no cambia) |
| `GetAvailableNodesHandler` | `var sponsorPrefix = TreePrefix.Of(sponsorGenNode.HierarchyPath);` y `.Where(g => g.HierarchyPath.StartsWith(sponsorPrefix))` | `if (sponsorGenNode.Node is null) return new List<string> { sponsorMemberId };`, `var sponsorRoot = sponsorGenNode.Node;` y `.Where(g => g.Node!.IsDescendantOf(sponsorRoot))` |
| `TokenCustody` | `.Select(g => new { g.MemberId, g.HierarchyPath })`, `if (sender is null \|\| string.IsNullOrEmpty(sender.HierarchyPath))`, `var senderPath = TreePrefix.Of(sender.HierarchyPath);` y `&& g.HierarchyPath.StartsWith(senderPath)` | `.Select(g => new { g.MemberId, g.Node })`, `if (sender is null \|\| sender.Node is null)`, `var senderRoot = sender.Node;` y `&& g.Node!.IsDescendantOf(senderRoot)` |
| `GetAllTeamMembersHandler` | `var hierarchySearchPattern = "/" + memberId + "/";` y `where g.HierarchyPath.Contains(hierarchySearchPattern)` | `var me = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);` + si es `null`, el mismo resultado vacío que da hoy un equipo sin miembros (`totalCount = 0`), y `where g.Node!.IsDescendantOf(me)` |
| `GetVisualizerChildrenHandler` | `var pattern = "/" + currentMemberId + "/";` y `.AnyAsync(g => g.MemberId == request.ParentMemberId && g.HierarchyPath.Contains(pattern), ct)` | `var me = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(currentMemberId, ct);` y `var isInSubtree = me is not null && await _db.GenealogyTree.AsNoTracking().AnyAsync(g => g.MemberId == request.ParentMemberId && g.Node!.IsDescendantOf(me), ct);` |
| `AdminMemberCommissionsController` | `&& g.HierarchyPath.Contains(hierarchyFilter)` | antes de la consulta: `var me = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);` (si es nulo, el resultado vacío que el método ya devuelve sin inscripciones) y `&& g.Node!.IsDescendantOf(me)`. `g.HierarchyPath` se sigue seleccionando para `GetLegMemberId` (M15) |

- [x] **Step 3: Ejecutar las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`
Expected: PASS, **incluida** `SinPrefijoEnInscripcionTests`. Si una prueba de estos manejadores falla porque sus nodos no tienen `Node`, se aplica `ReportesDb.RellenarNodos(db)` como en la Tarea 12.

Run: `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests -nologo` y `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests -nologo`
Expected: PASS. Si una prueba de estos proyectos crea nodos sin `Node` y falla, se asigna `Node = HierarchyId.Parse("/1/…/")` a mano en sus nodos (siguiendo su ruta) sin cambiar ninguna aserción.

- [x] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.RankEngine/Features/GetRankProgress/GetRankProgressHandler.cs \
        MLMConquerorGlobalEdition.BizCenter/Features/Placement/GetAvailableNodes/GetAvailableNodesHandler.cs \
        MLMConquerorGlobalEdition.BizCenter/Features/Tokens/TokenCustody.cs \
        MLMConquerorGlobalEdition.BizCenter/Features/Teams/GetAllTeamMembers/GetAllTeamMembersHandler.cs \
        MLMConquerorGlobalEdition.BizCenter/Features/Teams/GetVisualizerChildren/GetVisualizerChildrenHandler.cs \
        MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCommissionsController.cs
# y las pruebas tocadas, con su ruta explícita
git commit -m "Progreso de rango, nodos disponibles, custodia de tokens, todo el equipo, visualizador y comisiones del Admin acotan el equipo de inscripcion con Node; ninguna consulta del arbol de inscripcion filtra ya por la ruta"
```

**Despliegue único (revisión C1).** Los lectores por `Node` se despliegan con todo lo demás; los servicios solo arrancan con `SinNode = 0` e `Incoherentes = 0` (R17) y con R10 comprobado.

---

# Bloque 4 — Binario

### Task 14: `DualSubtree` (CTE sobre `ParentMemberId`)

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Queries/DualSubtree.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (M17–M20): la CTE del binario, con árboles propios dentro de una transacción.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDualSubtreeTests
{
    [SqlServerFact]
    public Task Devuelve_cada_descendiente_con_su_profundidad_y_su_pierna() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId(); var a = SqlServerReportes.NuevoId(); var b = SqlServerReportes.NuevoId(); var c = SqlServerReportes.NuevoId();
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        ReportesDb.NodoBinario(db, a, r, ReportesDb.Ruta(r, a), TreeSide.Left);
        ReportesDb.NodoBinario(db, b, r, ReportesDb.Ruta(r, b), TreeSide.Right);
        ReportesDb.NodoBinario(db, c, a, ReportesDb.Ruta(r, a, c), TreeSide.Right);
        await db.SaveChangesAsync();

        var filas = await DualSubtree.DescendantsAsync(db, r, 100);

        filas!.Select(f => (f.MemberId, f.Depth, (TreeSide)f.Leg)).Should().BeEquivalentTo(new[]
        {
            (a, 1, TreeSide.Left), (b, 1, TreeSide.Right), (c, 2, TreeSide.Left)
        });
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Por_encima_del_tope_devuelve_nulo() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId();
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        for (var i = 0; i < 4; i++)
        {
            var h = SqlServerReportes.NuevoId();
            ReportesDb.NodoBinario(db, h, r, ReportesDb.Ruta(r, h), i % 2 == 0 ? TreeSide.Left : TreeSide.Right);
        }
        await db.SaveChangesAsync();

        (await DualSubtree.DescendantsAsync(db, r, 3)).Should().BeNull();
        (await DualSubtree.DescendantsAsync(db, r, 4)).Should().HaveCount(4);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Una_pierna_de_mil_quinientos_niveles_no_choca_con_MAXRECURSION() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId();
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        var padre = r;
        for (var i = 0; i < 1500; i++)
        {
            var h = SqlServerReportes.NuevoId();
            // La ruta no importa a la CTE: se pone corta para no inflar la prueba.
            ReportesDb.NodoBinario(db, h, padre, ReportesDb.Ruta(r, h), TreeSide.Left);
            padre = h;
        }
        await db.SaveChangesAsync();

        var filas = await DualSubtree.DescendantsAsync(db, r, DualSubtree.MaxRowsForCte);

        filas.Should().HaveCount(1500);
        filas!.Max(f => f.Depth).Should().Be(1500);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public async Task Cada_paso_de_la_CTE_es_un_seek_por_ParentMemberId()
    {
        var plan = await PlanDeEjecucion.EstimadoAsync("""
            DECLARE @r nvarchar(450) = N'NADIE';
            WITH sub AS (
                SELECT d.MemberId, 1 AS Depth, CAST(d.Side AS int) AS Leg FROM DualTeamTree d
                WHERE d.ParentMemberId = @r AND d.IsDeleted = 0
                UNION ALL
                SELECT c.MemberId, s.Depth + 1, s.Leg FROM sub s
                JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0)
            SELECT TOP (20001) MemberId, Depth, Leg FROM sub OPTION (MAXRECURSION 32767);
            """);

        plan.Should().Contain("IX_DualTeamTree_ParentMemberId").And.Contain(PlanDeEjecucion.Seek);
        plan.Should().NotContain("Key Lookup", "el índice incluye MemberId, Side e IsDeleted (M10)");
    }
}
```

(Si `ReportesDb.NodoBinario` no admite `TreeSide` como quinto parámetro con ese nombre, se usa la firma que tenga: `NodoBinario(AppDbContext db, string memberId, string? padre, string ruta, TreeSide lado)`.)

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerDualSubtreeTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 2: Implementar**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Descendientes de un nodo del árbol BINARIO (Plan M, M17–M20). El binario no cabe en hierarchyid a
/// escala (1.427 niveles como máximo; en local ya hay 934) y su ruta de texto no es indexable, así
/// que se baja por ParentMemberId con una CTE recursiva en SQL (índice que la cubre, M10). SQL en
/// línea, como LegPointMaintenance; una función de tabla no admitiría MAXRECURSION.
/// </summary>
public static class DualSubtree
{
    /// <summary>Por encima de esto el llamador sigue con su estrategia de siempre (raíz o equipos enormes).</summary>
    public const int MaxRowsForCte = 20_000;

    /// <summary>El máximo de SQL Server: con 0, un ciclo en los datos no terminaría nunca.</summary>
    public const int MaxRecursion = 32_767;

    public sealed class Row
    {
        public string MemberId { get; set; } = string.Empty;
        /// <summary>1 = hijo directo del nodo de partida.</summary>
        public int    Depth    { get; set; }
        /// <summary>TreeSide del hijo directo por el que se baja: la pierna del nodo de partida.</summary>
        public int    Leg      { get; set; }
    }

    /// <summary>
    /// Los descendientes de <paramref name="rootMemberId"/> (él no). Null si son más de
    /// <paramref name="maxRows"/> (TOP corta la recursión: saberlo cuesta maxRows + 1 filas) o si el
    /// proveedor no es SQL Server (InMemory en las pruebas): el llamador usa entonces su camino de siempre.
    /// </summary>
    public static async Task<IReadOnlyList<Row>?> DescendantsAsync(
        AppDbContext db, string rootMemberId, int maxRows, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlServer())
            return null;

        var tope = maxRows + 1;
        // ToListAsync sin componer: EF manda el SQL tal cual (una CTE no se puede envolver en subconsulta).
        var filas = await db.Database.SqlQuery<Row>($"""
            WITH sub AS (
                SELECT d.MemberId, 1 AS Depth, CAST(d.Side AS int) AS Leg
                FROM DualTeamTree d
                WHERE d.ParentMemberId = {rootMemberId} AND d.IsDeleted = 0
                UNION ALL
                SELECT c.MemberId, s.Depth + 1, s.Leg
                FROM sub s
                JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0
            )
            SELECT TOP ({tope}) MemberId, Depth, Leg FROM sub
            OPTION (MAXRECURSION 32767)
            """).ToListAsync(ct);

        return filas.Count > maxRows ? null : filas;
    }
}
```

- [ ] **Step 3: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerDualSubtreeTests -nologo`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Queries/DualSubtree.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs
git commit -m "DualSubtree: descendientes de un nodo binario con su profundidad y pierna mediante una CTE recursiva sobre ParentMemberId, con tope de filas y MAXRECURSION 32767"
```

---

### Task 15: *Mi equipo* del Dual Team por la CTE en subárboles normales

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/DualTeamService.cs:56-158`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CalificadoEnLosEquiposTests.cs:24` (`Rangos` pasa a `internal`)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

En `CalificadoEnLosEquiposTests.cs`, cambiar `private static RankComputationService Rangos(` por `internal static RankComputationService Rangos(`.

Añadir a `SqlServerDualSubtreeTests` (y los `using` `System.Data`, `System.Diagnostics`, `MLMConquerorGlobalEdition.Repository.Grid`, `MLMConquerorGlobalEdition.Repository.Services.Teams` y `Xunit.Abstractions`, con el constructor que recibe `ITestOutputHelper _salida`):

```csharp
    [SqlServerFact]
    public Task Mi_equipo_por_la_CTE_da_lo_mismo_que_por_la_ruta() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId(); var a = SqlServerReportes.NuevoId(); var b = SqlServerReportes.NuevoId();
        foreach (var id in new[] { r, a, b }) ReportesDb.Miembro(db, id);
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        ReportesDb.NodoBinario(db, a, r, ReportesDb.Ruta(r, a), TreeSide.Left);
        ReportesDb.NodoBinario(db, b, a, ReportesDb.Ruta(r, a, b), TreeSide.Right);
        await db.SaveChangesAsync();
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var filas = (await servicio.GetMyTeamGridAsync(r, new GridDataRequest { PageSize = 50 })).Items.ToList();

        filas.Select(f => (f.MemberId, f.Level, f.Leg)).Should().BeEquivalentTo(new[]
        {
            (a, 1, "Left"), (b, 2, "Left")
        });
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Mi_equipo_de_un_subarbol_normal_cabe_en_tiempo() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        // Se baja desde una raíz por el hijo con más descendientes hasta dar con un subárbol que quepa en la CTE.
        var actual = await db.DualTeamTree.AsNoTracking().Where(d => d.ParentMemberId == null)
            .OrderBy(d => d.MemberId).Select(d => d.MemberId).FirstAsync();
        IReadOnlyList<DualSubtree.Row>? sub = null;
        for (var i = 0; i < 60 && sub is null; i++)
        {
            sub = await DualSubtree.DescendantsAsync(db, actual, DualSubtree.MaxRowsForCte);
            if (sub is null)
                actual = await db.DualTeamTree.AsNoTracking().Where(d => d.ParentMemberId == actual)
                    .OrderBy(d => d.MemberId).Select(d => d.MemberId).FirstAsync();
        }
        sub.Should().NotBeNull();
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var cronometro = Stopwatch.StartNew();
        var pagina = await servicio.GetMyTeamGridAsync(actual, new GridDataRequest { PageSize = 20 });
        cronometro.Stop();

        _salida.WriteLine($"Mi equipo Dual de {actual}: {sub!.Count} nodos, {pagina.TotalCount} filas en {cronometro.ElapsedMilliseconds} ms");
        pagina.TotalCount.Should().Be(sub.Count);
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        await tx.RollbackAsync();
    });
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "SqlServerDualSubtreeTests.Mi_equipo" -nologo`
Expected: la primera puede pasar ya, porque el camino por la ruta da el mismo resultado: queda como prueba de equivalencia. La segunda debe FALLAR por tiempo, o pasar muy justa. Anotar su cifra como «Antes» en la última fila de la tabla «Mediciones».

- [ ] **Step 2: Implementar**

En `DualTeamService.cs` (y `using MLMConquerorGlobalEdition.Repository.Queries;` si falta), sustituir `BuildMyTeamRowQueryable` completo por:

```csharp
    /// <summary>
    /// Plan M (M19): un subárbol normal sale de la CTE de DualSubtree y la rejilla filtra por esos ids;
    /// la raíz o un equipo de más de MaxRowsForCte (o InMemory) siguen con el prefijo de la ruta.
    /// </summary>
    private async Task<IQueryable<DualMyTeamRow>> MyTeamRowsAsync(
        DualTeamEntity myNode, string memberId, CancellationToken ct)
    {
        var sub = await DualSubtree.DescendantsAsync(_db, memberId, DualSubtree.MaxRowsForCte, ct);
        if (sub is null)
        {
            var prefix = TreePrefix.Of(myNode.HierarchyPath);
            return Project(_db.DualTeamTree.AsNoTracking()
                .Where(d => d.HierarchyPath.StartsWith(prefix) && d.MemberId != memberId));
        }

        // EF.Parameter: un único parámetro JSON (OPENJSON); 20.000 parámetros sueltos pasarían de 2.100.
        var ids = sub.Select(r => r.MemberId).ToList();
        return Project(_db.DualTeamTree.AsNoTracking().Where(d => EF.Parameter(ids).Contains(d.MemberId)));
    }

    private IQueryable<DualMyTeamRow> Project(IQueryable<DualTeamEntity> nodes) =>
        from d in nodes
        join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
        select new DualMyTeamRow
        {
            MemberId        = m.MemberId,
            FullName        = m.FirstName + " " + m.LastName,
            Email           = m.Email,
            Phone           = m.Phone,
            Country         = m.Country,
            EnrollDate      = m.EnrollDate,
            SponsorMemberId = m.SponsorMemberId,
            AccountStatus   = m.Status.ToString(),
            HierarchyPath   = d.HierarchyPath,
            ParentMemberId  = d.ParentMemberId,
            LeftLegPoints   = d.LeftLegPoints,
            RightLegPoints  = d.RightLegPoints
        };
```

En `GetMyTeamAsync`, sustituir `var q = BuildMyTeamRowQueryable(myNode.HierarchyPath, memberId);` por `var q = await MyTeamRowsAsync(myNode, memberId, ct);`. En `GetMyTeamGridAsync`, sustituir `var q     = BuildMyTeamRowQueryable(myNode.HierarchyPath, memberId);` por `var q     = await MyTeamRowsAsync(myNode, memberId, ct);`. `BuildDualViewsForPageAsync` no cambia: sigue sacando nivel y pierna de la ruta, pero solo para las filas de la página.

- [ ] **Step 3: Ejecutar las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "SqlServerDualSubtreeTests|CalificadoEnLosEquiposTests" -nologo`
Expected: PASS. Anotar la cifra de `Mi_equipo_de_un_subarbol_normal_cabe_en_tiempo` como «Después» en la tabla.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Teams/DualTeamService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CalificadoEnLosEquiposTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs
git commit -m "Mi equipo del Dual Team recorre los subarboles de hasta 20.000 nodos con la CTE de DualSubtree y filtra la rejilla por esos ids en un solo parametro; la raiz y los equipos enormes siguen con el prefijo de la ruta"
```

---

# Bloque 5 — RCSI

### Task 16: El reclamo del pedido sigue excluyendo con lecturas versionadas

**Files:**
- Create: `MLMConquerorGlobalEdition.Signups.Tests/Services/SqlServerReclamoConcurrenteTests.cs`

- [ ] **Step 1: Escribir la prueba**

Es una prueba de regresión: debe pasar **antes y después** de activar RCSI. Fija el veredicto de la tabla «Revisión de candados bajo RCSI».

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Services;

/// <summary>
/// Plan M (revisión de candados bajo RCSI): dos caminos que leyeron el pedido en Pending a la vez y lo
/// reclaman. El UPDATE condicional del segundo ESPERA al bloqueo de fila del primero (con o sin
/// READ_COMMITTED_SNAPSHOT: las escrituras no leen versiones) y, al confirmar el primero, afecta a 0 filas.
/// </summary>
public class SqlServerReclamoConcurrenteTests
{
    [SqlServerFact]
    public async Task Dos_reclamos_a_la_vez_solo_gana_uno()
    {
        var id = Guid.NewGuid().ToString();
        await using (var siembra = SqlServerTestDb.Db())
        {
            siembra.Orders.Add(new OrdersEntity
            {
                Id = id, MemberId = "PLANM-0001", OrderNo = "PLANM-" + id[..8], Status = OrderStatus.Pending,
                OrderDate = DateTime.Now, CreatedBy = "prueba"
            });
            await siembra.SaveChangesAsync();
        }

        try
        {
            await using var db1 = SqlServerTestDb.Db();
            await using var db2 = SqlServerTestDb.Db();
            await using var tx1 = await db1.Database.BeginTransactionAsync();
            await using var tx2 = await db2.Database.BeginTransactionAsync();
            var o1 = await db1.Orders.SingleAsync(o => o.Id == id);
            var o2 = await db2.Orders.SingleAsync(o => o.Id == id);   // los dos han leído Pending

            (await new SignupOrderClaim(db1).TryCompleteAsync(o1, DateTime.Now, "uno", default)).Should().BeTrue();
            var segundo = new SignupOrderClaim(db2).TryCompleteAsync(o2, DateTime.Now, "dos", default);
            await Task.Delay(500);
            segundo.IsCompleted.Should().BeFalse("el UPDATE del segundo espera al bloqueo de fila del primero");

            await tx1.CommitAsync();
            (await segundo).Should().BeFalse("al reevaluar el WHERE sobre lo confirmado ya no está en Pending");
            await tx2.RollbackAsync();
        }
        finally
        {
            await using var limpieza = SqlServerTestDb.Db();
            await limpieza.Orders.Where(o => o.Id == id).ExecuteDeleteAsync();
        }
    }
}
```

- [ ] **Step 2: Ejecutarla (antes de RCSI)**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SqlServerReclamoConcurrenteTests -nologo`
Expected: PASS (con RCSI apagado). La Tarea 17 la repite con RCSI encendido.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.Signups.Tests/Services/SqlServerReclamoConcurrenteTests.cs
git commit -m "Prueba de regresion: dos reclamos simultaneos del mismo pedido de alta, el segundo espera al bloqueo de fila y no gana, con o sin lecturas versionadas"
```

---

### Task 17: Script de RCSI y activación en la base local

**Files:**
- Create: `Scripts/sql/plan-m/03-activar-rcsi.sql`

- [ ] **Step 1: Escribir el script**

```sql
/*
Plan M, Tarea 17 (D6, M22). Activa READ_COMMITTED_SNAPSHOT en MLMConquerorGlobalEditionDb.

ANTES (obligatorio):
  1. Parar TODOS los servicios que usan la base: los ocho anfitriones (SignupAPI, AdminAPI, BizCenter,
     Billing, CommissionEngine, RankEngine, TicketManagementSystem, SharedAPICenter), los trabajadores
     de Hangfire y cualquier SSMS o sqlcmd abierto contra ella. En local: los del worktree "-run".
  2. tempdb: el version store vive alli. Vigilar su tamano tras activar (consulta de verificacion).
  3. Produccion: NO ejecutar hasta cumplir las condiciones de R6 (huecos del binario reparados e indice
     unico ParentMemberId+Side) y R7 (indice unico del webhook o decision de Nick).

QUE HACE: se niega a seguir si hay otras sesiones en la base. ALTER DATABASE necesita acceso exclusivo
y no puede ir dentro de una transaccion; por eso no es una migracion de EF.

PARA DESHACER: ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;
(con los servicios parados, igual).
*/
SET NOCOUNT ON;

IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51040, N'Conectate a MLMConquerorGlobalEditionDb (sqlcmd -d MLMConquerorGlobalEditionDb).', 1;

IF EXISTS (SELECT 1 FROM sys.databases WHERE name = DB_NAME() AND is_read_committed_snapshot_on = 1)
BEGIN
    PRINT N'READ_COMMITTED_SNAPSHOT ya estaba activado. Nada que hacer.';
    RETURN;
END

IF EXISTS (SELECT 1 FROM sys.dm_exec_sessions WHERE database_id = DB_ID() AND session_id <> @@SPID)
    THROW 51041, N'Hay otras sesiones en la base: para los servicios antes de activar RCSI.', 1;

ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;

-- Verificacion
SELECT name, is_read_committed_snapshot_on, snapshot_isolation_state_desc
FROM sys.databases WHERE name = DB_NAME();

SELECT SUM(version_store_reserved_page_count) * 8 / 1024 AS VersionStoreMB
FROM tempdb.sys.dm_db_file_space_usage;
```

- [ ] **Step 2: Commit del script**

```bash
git add Scripts/sql/plan-m/03-activar-rcsi.sql
git commit -m "Script versionado que activa READ_COMMITTED_SNAPSHOT con los servicios parados, se niega si hay otras sesiones y muestra la verificacion y el version store"
```

- [ ] **Step 3: Activación en la base LOCAL (la ejecuta el coordinador o Nick, no el implementador)**

1. Parar los servicios del worktree `-run` y cualquier otra conexión a la base local.
2. Run: `sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -i Scripts/sql/plan-m/03-activar-rcsi.sql`
   Expected: una fila `MLMConquerorGlobalEditionDb 1 OFF` y `VersionStoreMB` con un número pequeño. Si sale 51041, quedan sesiones abiertas.
3. Verificar: `sqlcmd -S . -d master -E -C -W -I -Q "SELECT name, is_read_committed_snapshot_on FROM sys.databases WHERE name = N'MLMConquerorGlobalEditionDb'"`. Expected: `1`.
4. Repetir las pruebas de candados: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SqlServerReclamoConcurrenteTests -nologo` y `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter SqlServerMemberFundsGateTests -nologo`. Expected: PASS.

---

# Cierre

### Task 18: Medición después y verificación completa

**Files:**
- Modify: `docs/superpowers/plans/2026-10-17-plan-m-rendimiento-pedidos-y-arboles.md` (tabla «Mediciones», columna «Después»)

- [ ] **Step 1: Medición**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "SqlServerMedicionPlanMTests|SqlServerDualSubtreeTests.Mi_equipo_de_un_subarbol_normal_cabe_en_tiempo" --logger "console;verbosity=detailed" -nologo`
Expected: PASS en las cinco. Copiar las medianas a la columna «Después». Si alguna pasa del objetivo, no se sube el umbral: se anota la cifra y se avisa con el plan de ejecución de esa consulta (`PlanDeEjecucion.EstimadoAsync`).

- [ ] **Step 2: Suites completas**

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo`
Expected: `0 Error(s)`.

Run, uno a uno: `dotnet test` de `MLMConquerorGlobalEdition.Domain.Tests`, `BizCenter.Tests`, `Signups.Tests`, `Billing.Tests`, `AdminAPI.Tests`, `CommissionEngine.Tests`, `RankEngine.Tests`, `Notifications.Tests`, `Authn.Tests` y `TicketManagementSystem.Tests`, con `-nologo`.
Expected: PASS en todas. Las `[SqlServerFact]` deben pasar, no salir como `Skipped`.

- [ ] **Step 3: Condiciones de la base local**

Run: `sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -W -I -Q "SET NOCOUNT ON; SELECT COUNT(*) AS SinNode FROM GenealogyTree WHERE Node IS NULL; SELECT COUNT(*) AS DuplicadosOrderNo FROM (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL GROUP BY OrderNo HAVING COUNT(*) > 1) d"`
Expected: `0` y `0`. Si hay altas nuevas desde el relleno, siguen en 0 porque el alta escribe `Node`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/plans/2026-10-17-plan-m-rendimiento-pedidos-y-arboles.md
git commit -m "Plan M: medicion despues anotada en el plan"
```

**Condiciones de despliegue (resumen):**
1. **Un solo despliegue (revisión C1).** En producción no se aplicó nunca ninguna versión de `NodoDeGenealogia`, así que no hay despliegue intermedio. Orden, sin saltarse ningún paso:
   1. **Antes de la ventana:** ejecutar la consulta de duplicados de la Tarea 4. Si hay alguno, decide Nick (R15); la guarda 51021 detendría la migración.
   2. **Parar todos los servicios.** Ninguno arranca hasta el paso 6: cada uno ejecuta `MigrateAsync` al arrancar, con el *timeout* de 30 s de EF, y se pondría a aplicar las mismas migraciones sobre tablas grandes; además, un alta durante el relleno podría ocupar un ordinal.
   3. **Script idempotente de migraciones**, generado con `dotnet ef migrations script CuotaAnual --idempotent` (`Scripts/sql/plan-m/00-migraciones-plan-m.sql`; regenerarlo si cambia alguna migración), **solo con `sqlcmd -b -I`**:
      - nunca desde SSMS ni sin `-b`: sin `-b`, un `THROW` de una guarda (51020–51028, 51033) no detiene `sqlcmd`;
      - nunca sin `-I`: el script de EF **no** emite `SET QUOTED_IDENTIFIER ON` y, sin él, crear los índices filtrados y los de columnas calculadas falla con 1934 (ensayado: la transacción de la migración se deshace entera, pero el despliegue no avanza).
   4. **Relleno:** `sqlcmd -b -I -i Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql`. Al final hace `ALTER INDEX IX_GenealogyTree_Node REBUILD` y `UPDATE STATISTICS GenealogyTree WITH FULLSCAN`, y devuelve las dos cifras.
   5. **Comprobar `SinNode = 0` e `Incoherentes = 0`.** Si no, no se arranca: se investiga y se relanza el relleno (es idempotente).
   6. **Arrancar los servicios**, primero uno solo (encontrará las migraciones aplicadas). Con R10 comprobado en Docker.
   - Ensayo en una copia local restaurada (revisión I2): migraciones 37 s y relleno 43 s con 306.000 pedidos y 119.446 nodos; los Node salen idénticos a los de la base local.
2. **Fallar cerrado mientras tanto:** si por error algún miembro quedara sin `Node`, el alta se niega a colgar a nadie de él (I4), los puntos de equipo devuelven «sin dato» y el job de estadísticas no escribe 0 (C1), y el bono de coche lo omite (M6).
3. **RCSI en producción:** solo con R6 y R7 resueltos (planes aparte), los servicios parados y el script de la Tarea 17.

---

## Desviaciones durante la ejecución

Lo que no encajaba con el plan escrito y cómo se adaptó, respetando su intención.

- **Rama y Plan K (R13).** La rama real es `plan-m-rendimiento`, que sale de `master` d24e103 con el Plan K ya fusionado. Su migración `CuotaAnual` crea `AnnualFeePayments.OrderId` como `nvarchar(450)` **sin** FK a `Orders`, así que no choca con el Bloque 1.
- **Hechos medidos, repetidos el 2026-10-17 antes de la Tarea 4.** `Orders` tiene ahora 306.134 filas. `Orders.MemberId` llega a 20 caracteres en 2 filas: son restos `TST-…` de pruebas de SQL Server (`CreatedBy = sql-test`/`sql-a`). Caben en `nvarchar(20)`, así que no cambia nada. `OrderDetails` tiene 202.161 filas y `PaymentHistories` 22.768. Los huérfanos y las sombras siguen a 0, y los grupos de `OrderNo` duplicados siguen siendo 68.
- **Tarea 1.** *Clientes* de la raíz ya cumplía el objetivo antes del plan (mediana de 526 ms), así que su prueba pasa en la línea base. Las otras tres fallan, como se esperaba.
- **Tarea 2: dos generadores más de `OrderNo`, sin bucle de unicidad.** Además de los seis bucles de M3, el Plan K y la compra de tokens generan el número sin comprobarlo: `AnnualFeeChargeService` (`"Annual Fee"`) y `TokenPurchaseService` (`"Token Purchase"`). Con el índice único, un choque (456.976 combinaciones por milisegundo) fallaría cerrado con `DbUpdateException` en vez de duplicar el número. No se cambia nada.
- **Tarea 4.** Se ejecutó en local: `Pedidos renumerados: 84` y `GruposDuplicadosRestantes 0`. La segunda ejecución da 0, como se esperaba.
- **Tarea 5: deriva en la base local.** `IX_DualTeamTree_ParentMemberId` no existía en local, aunque lo crea `MakeHierarchyPathUnboundedDropPathIndex` y está en el *snapshot*. Alguien lo borró a mano, y el primer intento de aplicar falló con 3701 y se deshizo entero. En `Up`, el `DropIndex` generado se cambió por `DROP INDEX IF EXISTS`, que vale para las dos situaciones. Antes de producción conviene comprobar si allí existe (no cambia el resultado).
- **Tarea 5: guarda probada.** Dentro de una transacción con `XACT_ABORT`, se alargó un `OrderNo` a 41 caracteres y la guarda lanzó 51020 y lo deshizo todo.
- **Tarea 5: medición intermedia, tras aplicar `PedidosAcotadosEIndices`.** Es la misma prueba de la Tarea 1; la columna «Después» la rellena la Tarea 18.
  - *Actividad*: de 5.307 ms a **17 ms** (162 · 17 · 16).
  - *Clientes*: de 526 ms a **84 ms** (575 · 84 · 69).
  - Comprobación de `OrderNo`: de 430 ms a **1 ms** (281 · 1 · 1).
  - *Eventos* con todo el equipo sigue por encima del objetivo: **4.454 ms** (5.617 · 4.454 · 2.367). Depende del árbol (Bloques 2 y 3).
- **Tarea 5: los servicios del worktree `-run`.** Corren con el modelo antiguo contra la base local. Tras la migración, cualquier consulta suya que cargue `OrderDetails`, `PaymentHistories` o `MembershipSubscriptions` pedirá columnas que ya no existen (`OrdersId`, `LastOrderId1`) y fallará con «Invalid column name» hasta que se actualicen.
- **Revisión de las Tareas 1–5 (arreglos).**
  - **Webhook de pagos (R16):** `ProcessPaymentWebhookHandler` rechaza con `WEBHOOK_FIELD_TOO_LONG` (400, que la pasarela no reintenta) un `TransactionId`, `OrderId` o `MemberId` más largo que su columna, antes de tocar la base. Las longitudes viven en `PaymentHistory` (`OrderIdMaxLength`, `MemberIdMaxLength`, `GatewayTransactionIdMaxLength`) y las usa la configuración. Prueba: `Signups.Tests/Webhooks/LongitudesDelWebhookDePagoTests`.
  - **Script de duplicados:** exige `-v Confirmo=LOCAL` y que `SERVERPROPERTY('MachineName') = HOST_NAME()` (51030 y 51031), además del nombre de la base (51029). Fija `QUOTED_IDENTIFIER ON`, porque sqlcmd arranca con OFF y, con el índice filtrado, un `UPDATE` de `Orders` falla con 1934. Si `-Dn` está ocupado, prueba el siguiente sufijo. Se probó dentro de una transacción deshecha: con `-D1` ocupado, el duplicado recibió `-D2`.
  - **`Scripts/UpdateExistingOrderNumbers.sql`:** lleva un aviso. Numera desde `ORD000001` sin mirar los que existen, así que con el índice único chocaría. Hoy no hay pedidos sin número.
  - **Pruebas de medición:** llevan `[Trait("Categoria", "Medicion")]` y `BizCenter.Tests.runsettings` (`RunSettingsFilePath` del `.csproj`) las excluye por defecto. Ese filtro se combina con `--filter`. Para medir: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --settings MLMConquerorGlobalEdition.BizCenter.Tests/Medicion.runsettings --logger "console;verbosity=detailed"`.
  - **Otros arreglos menores:** la raíz se elige con un orden determinista (`Level`, luego `MemberId`). `SHOWPLAN_XML OFF` va en un `finally`. `SqlServerPedidosTests` comprueba en la base las claves y el `INCLUDE` de `IX_DualTeamTree_ParentMemberId`. Hay comentarios sobre las longitudes de `MemberId` en `OrdersConfiguration` y `MemberStatisticConfiguration`.
  - **Cuota anual y compra de tokens guardan el pedido antes de cobrar (verificado, sin cambios).**
    - `AnnualFeeChargeService`:
      - por token y por comisiones, pedido y cobro van en la misma transacción bajo el candado, y un choque se deshace entero;
      - por eWallet, la fase 1 guarda y confirma el pedido antes del débito de la fase 2.
    - `TokenPurchaseService`:
      - por eWallet, tarjeta y cripto, `SaveReservationAsync` confirma el pedido en la fase 1, antes de cobrar;
      - por comisiones, guarda el pedido antes de `FundTokenPurchaseAsync`, dentro de la misma transacción.
    - En los dos servicios, un choque de `OrderNo` falla sin cobrar.
  - **Despliegue en producción:** M24 y «Condiciones de despliegue» dicen ahora tres cosas. El script se aplica solo con `sqlcmd -b -I`. Los servicios están parados durante la ventana. Ninguno arranca antes de que termine el script.
- **Tarea 6: los paquetes.** `Microsoft.EntityFrameworkCore.SqlServer.HierarchyId` y `…SqlServer.Abstractions` están publicados en 10.0.11, la misma versión que EF Core del repositorio.
- **Tarea 7: más de nueve hermanos.** Se añadió una prueba con diez o más hijos. Comprueba que el orden es por `Node` y no por su texto, también en InMemory, donde `Node` se guarda como texto.
- **Tarea 9: SQL Server no admite un índice filtrado sobre una columna CLR.** `HasFilter("[Node] IS NOT NULL")` falla con el error 10619, y tampoco se puede filtrar por una columna calculada.
  - **Solución:** el índice único es `(Node, NodeNullKey)`. `NodeNullKey AS CASE WHEN [Node] IS NULL THEN CAST([Id] AS varchar(100)) END` es una columna calculada y persistida, y el índice lleva `HasFilter(null)`.
    - Las filas sin `Node` no chocan entre sí, porque cada una lleva su `Id`.
    - Dos filas con el mismo `Node` chocan como `(Node, NULL)`, y `IsDuplicateNode` lo reconoce por el nombre del índice. Lo prueba `SqlServerNodoDeGenealogiaTests.El_indice_admite_muchos_nodos_nulos_y_rechaza_dos_iguales`.
  - **Guarda nueva 51033:** la migración se detiene si algún `GenealogyTree.Id` mide más de 100 caracteres.
  - **Contra R1:** `NodeLevel` y `NodeNullKey` son columnas persistidas, así que añadirlas escribe en cada fila. La migración no es solo de metadatos. En local tarda segundos; en producción va en la ventana de M24.
- **Tarea 10: opciones de sesión en el relleno.** El script fija `QUOTED_IDENTIFIER`, `ARITHABORT` y el resto de opciones `SET` en su propio lote, porque hay índices sobre columnas calculadas y sqlcmd arranca con `QUOTED_IDENTIFIER OFF`. La guarda del nombre de la base pasa a 51034, porque 51031 ya la usa el script de duplicados.
  - **Resultado en local:** 10 pasadas, 119.446 filas rellenadas, `SinNode 0` e `Incoherentes 0`. La segunda ejecución rellena 0.
  - **Nodos:** el más largo ocupa 9 bytes y el nivel máximo es 8.
- **Tarea 11: nodos de prueba sin padre.** `ReportesDb.NodoDesdeRuta` reserva el `Node` del padre cuando el padre todavía no existe, porque hay pruebas que crean el hijo antes que el padre o cuelgan nodos de un patrocinador que no está en el árbol. Cuando después se crea el padre, usa ese mismo `Node`. Los hijos se numeran detrás del último hermano, no por recuento.
- **Tarea 13: un sitio que el plan no listaba.** `AdminMemberDualTeamController.GetAvailableForPlacement` filtraba el árbol de inscripción con `HierarchyPath.Contains`. Ahora va por `Node`.
- **Tarea 13: cambios en pruebas existentes.**
  - `TeamHandlersTests`: dos pruebas de `GetAllTeamMembers` no sembraban el nodo del que mira. Se añadió ese nodo.
  - `GetAllTeamMembers` excluye ahora al propio miembro. Su prueba lo pedía («descendientes»), y el código antiguo solo lo cumplía porque al miembro le faltaba el nodo.
  - `MemberStatisticSnapshotJobTests`, `TransferTokenInstanceHandlerTests` y `DistribucionDeTokensTests` llaman a `ReportesDb.RellenarNodos`.
  - `RankEngine.Tests/EnrollmentTeamPointsServiceTests` asigna `Node` a mano.
  - No se cambió ninguna aserción.
- **Tareas 11–13: rendimiento con el equipo grande (corrección importante).**
  - **La medición no medía el equipo grande.** `RaizAsync` elegía ROOT001, la raíz de nivel 0, que solo tiene **33** personas debajo. El equipo grande es el de **AMB-700829**, con 119.403. Ahora `RaizAsync` elige la raíz con más equipo.
  - **El problema:** con AMB-700829 y la forma del plan (`IsDescendantOf`), *Clientes* tardaba 7,9 s y *Eventos* 11,3 s. Hay tres causas, medidas con los planes reales:
    1. EF manda `IsDescendantOf(@p) = CAST(1 AS bit)`. Con parámetro, SQL Server no lo convierte en un *seek*; con `= 1`, sí.
    2. Con `IsDescendantOf` en el filtro, SQL Server estima **1 fila** para cualquier subárbol. Elige entonces bucles con búsquedas por clave: 9 s para contar 119.400 filas, frente a 46 ms con el rango.
    3. El mismo SQL sirve a equipos de 30 personas y de 119.400. El plan en caché del pequeño hacía que el grande tardara 3–6 s.
  - **La corrección:**
    - `GenealogySubtree.Under(root)` lee el equipo como el rango `[root, hermano siguiente)` del índice, sin `IsDescendantOf`.
    - El rango es exacto porque los ordinales son siempre enteros. Lo vigila `SqlServerNodoDeGenealogiaTests.Ningun_Node_tiene_ordinales_fraccionarios`.
    - `IX_GenealogyTree_Node` incluye también `CreationDate` e `IsDeleted`, para que cubra `CurrentUnder`. La migración `NodoDeGenealogia` se regeneró, porque solo estaba aplicada en local, y el relleno se relanzó.
    - `RecompileTaggedQueriesInterceptor`, registrado en `AppDbContext`, añade `OPTION (RECOMPILE)` a las consultas que pasan por `Under`, para que cada tamaño de equipo tenga su plan.
    - Todas las consultas que filtran filas del árbol por equipo usan `Under` o `CurrentUnder`. La forma A, la subconsulta sobre un conjunto pequeño, sigue con `IsDescendantOf`.
  - **Vigilancia:**
    - Una regla de `SinPrefijoEnInscripcionTests` prohíbe `g.Node.IsDescendantOf` sobre filas.
    - `SqlServerPlanesDeEjecucionTests` lee el plan **real**: el mismo comando de EF, con parámetros y con el interceptor, bajo `SET STATISTICS XML`.
  - **Pendiente a escala:** para un equipo pequeño, la subconsulta del nodo vigente (R8) se resuelve con un *hash* que recorre `IX_GenealogyTree_Node` entero: unos 120 ms en local, que crecerán con millones de nodos. Con `RECOMPILE` el optimizador debería elegir bucles cuando el rango es pequeño, pero conviene vigilarlo con datos reales. Si no lo hace, la propuesta es una columna «nodo vigente» mantenida por el alta.
- **Medición tras los Bloques 1–3,** con la raíz del equipo grande (AMB-700829) y mediana de 3. No hay «antes» para AMB-700829, porque la línea base se midió con ROOT001. La columna «Después» la rellena la Tarea 18.
  - *Actividad* de 3 meses: **8 ms**.
  - *Clientes*, equipo: **188 ms**.
  - *Eventos*, todo el equipo: **594 ms**.
  - Comprobación de `OrderNo`: **2 ms**.
  - Las cuatro pruebas de medición pasan.
- **Servicios del worktree `-run`.** Corren con el código de 3df58ff, sin el Bloque 2, así que sus altas crean nodos de inscripción con `Node` nulo. Cuando se actualicen hay que relanzar `02-relleno-nodo-genealogia.sql`, que es idempotente. En este momento hay 0 nulos.
- **Prueba intermitente:** `EntraxQrPaymentTests.Caducado_o_fallido_ofrece_un_QR_nuevo_u_otro_metodo(Expired)` falló una vez con la suite completa y pasó sola y en la siguiente ejecución completa. No tiene relación con este plan.

- **Revisión de las Tareas 6–13 (arreglos).**
  - **C1, un solo despliegue:** las condiciones de despliegue describen ahora una sola ventana: servicios parados → script idempotente de migraciones → relleno → `REBUILD` y estadísticas → comprobación → arranque. Ya no quedan referencias a dos despliegues.
  - **C1, fallar cerrado:** `RecomputeEnrollmentPointsAsync` devuelve `int?`, y `null` cuando el miembro no tiene nodo vigente con `Node`. `MemberStatisticSnapshotJob` no escribe y lo registra como error (`MemberStatisticSnapshotJobTests.ExecuteAsync_SinNode_NoEscribeCeroYRegistraUnError`).
    - La prueba de RankEngine que esperaba 0 para un miembro sin nodo espera ahora `null`. Es la única aserción cambiada, por decisión de la revisión.
    - El recálculo suma por SQL con `CurrentUnder`; antes devolvía la lista de ids del equipo y la usaba en un `Contains`.
  - **I1, relleno rehecho:**
    - Calcula nivel a nivel en `#nodos`, con una sola lectura de las filas sin `Node` en la primera pasada; cada pasada siguiente lee solo los hijos de la anterior.
    - Aplica por lotes de 4.000 filas en orden de `Id`, cada lote en su transacción, y termina con `REBUILD` y `UPDATE STATISTICS … WITH FULLSCAN`.
    - Medido en local con 119.446 nodos: cálculo 14 s, aplicación 17 s, índice y estadísticas 3 s, **35 s** en total. Relanzado, 0 filas en 90 ms.
    - **Comparación con los `Node` anteriores** (copia en `tempdb`): 119.272 iguales y 174 distintos. Los 174 son hijos de un mismo patrocinador, AMB-922290, que tiene 175. El script antiguo los numeraba por lotes de 50.000 y su orden no seguía `(CreationDate, Id)`: 174 fuera de orden. El nuevo sigue exactamente la regla del alta (0 fuera de orden) y da los mismos valores en cada ejecución.
    - El padre es el nodo **vivo** más reciente con desempate por `Id`, como `NextChildAsync`. En local no hay nodos borrados ni miembros con dos nodos.
  - **I2, `-I` y ensayo:**
    - Todos los `sqlcmd` del plan y de los scripts llevan `-I`.
    - **El script de EF no emite `SET QUOTED_IDENTIFIER ON`.** Se generó con `dotnet ef migrations script CuotaAnual --idempotent` y se guarda como `Scripts/sql/plan-m/00-migraciones-plan-m.sql`.
    - **Ensayo en `MLMConquerorGlobalEditionDb_PlanMEnsayo`,** restaurada de una copia `COPY_ONLY` de la base local:
      1. La copia se llevó a `CuotaAnual` con `dotnet ef database update CuotaAnual`.
      2. Con un `OrderNo` duplicado, `sqlcmd -b -I` se detuvo con 51021, sin aplicar nada y sin transacción abierta.
      3. Sin el duplicado, aplicó las dos migraciones en **37 s**. La segunda ejecución no hizo nada (0,1 s).
      4. El relleno, con una copia del script con el nombre de la base cambiado, tardó 43 s y dio `Node` idénticos a los de la base local.
      5. Sin `-I`, el script falla con 1934 y la migración se deshace entera.
    - La base de ensayo y el backup se borraron.
    - **Por qué no se creó la copia con las migraciones:** desde una base vacía no se puede. `HelpdeskModule` vuelve a crear `AspNetRoles` (2714), así que hubo que restaurar.
    - **Aviso:** el log de la base local mide 248 GB (modelo `FULL`, espera `LOG_BACKUP`), y por eso restaurarla tardó 20 minutos.
  - **I3, altas concurrentes:**
    - `GenealogyChildrenGate`: `sp_getapplock` sobre `genealogy-children:{patrocinador}` en una transacción que abarca `NextChildAsync` y el `SaveChanges`. Está en `SignupMemberHandler` y en `SignupAmbassadorHandler`.
    - Si alguien activara `EnableRetryOnFailure`, falla con un mensaje claro: el alta tendría que ir dentro de `CreateExecutionStrategy().ExecuteAsync`. SignupAPI no lo activa.
    - `SqlServerAltasConcurrentesTests`: 10 altas a la vez al mismo patrocinador terminan todas, con 10 `Node` distintos y **0 choques** por el índice. Sin el candado hubo 25 choques que salvaba el reintento. La prueba borra sus filas.
  - **I4:** `NextChildAsync` lanza `InvalidOperationException` si el nodo vigente del patrocinador no tiene `Node`. Un patrocinador que no está en el árbol da una raíz nueva, como su ruta. Las pruebas del alta siembran el `Node` del patrocinador.
  - **I5:** Mi equipo, puntos, comisiones (BizCenter y Admin), todo el equipo, visualizador, custodia de tokens, candidatos a colocar y progreso de rango usan `CurrentUnder`. **Medido con AMB-700829** (119.403 personas):
    - *Clientes*: 221 ms.
    - *Eventos*: 994 ms.
    - *Mi equipo*: 638 ms (medición nueva).
    - Ninguna pasa de 3 s.
  - **M1–M3:**
    - El padre se busca entre los nodos vivos; los hermanos, también entre los borrados.
    - `CurrentOfAsync` y `NodeOfAsync` eligen el nodo vigente con desempate por `Id`, y sustituyen a los 10 `FirstOrDefaultAsync` sin orden: Mi equipo ×2, rama, puntos, nodos disponibles, progreso y evaluación de rango, cancelación, alta ×2 y activación.
    - `CurrentUnder` desempata también por `Id`.
  - **M4:** SQL Server admite el CHECK con un método de `hierarchyid`: `CK_GenealogyTree_Node_OrdinalesEnteros` (`[Node] IS NULL OR CHARINDEX('.', [Node].ToString()) = 0`). Probado en una transacción deshecha: rechaza `/999999.1/` con 547. Se mantiene también la prueba en SQL.
  - **M5:** `IX_GenealogyTree_ParentMemberId` lleva `INCLUDE (Node)`. La migración se regeneró (`20261017215312_NodoDeGenealogia`), porque solo estaba aplicada en local.
  - **M8:** `NodeNullKey` pasa a `nvarchar(100)`: la clave queda en 892 + 200 bytes, por debajo de 1.700, y el índice se crea sin avisos. La migración tarda unos 2,7 s en SQL en local con 119.446 filas: cinco sentencias de 0,2 a 0,7 s, sin contar el arranque de EF.
  - **M6:**
    - El bono de coche suma el equipo por el `Node` vigente, con sumas acumuladas sobre la lista ordenada por `Node`. Omite al embajador sin `Node`, y un miembro con dos nodos ya no revienta el `ToDictionary`. Tiene pruebas nuevas.
    - `DownlineGuard.PuedeVerRamaDePatrocinioAsync` autoriza por `Node`.
    - La regla `Nadie_filtra_en_memoria_filas_de_genealogia_por_la_ruta` detecta `x.HierarchyPath.Contains/StartsWith` en archivos que leen `GenealogyTree`, fuera del binario. Se comprobó que habría detectado el código antiguo del bono.
  - **M7:** el webhook rechaza también un `provider` de más de 100 caracteres (`PaymentHistory.GatewayNameMaxLength`; la columna sigue en `nvarchar(max)`). Comentario corregido: no todas las pasarelas dejan de reintentar un 4xx.
  - **M9 y M10:** la revisión pide solo una nota en el plan, y el mensaje no dice su contenido. Quedan pendientes de que el coordinador las precise.
  - **Guarda del relleno:** comprueba que la base se llama `MLMConquerorGlobalEditionDb`. Si en producción tiene otro nombre, hay que ajustarla antes de la ventana.

---

## Autorrevisión

**Cobertura del encargo:**

| Encargo | Dónde |
|---|---|
| `Orders`: `Id`, `MemberId` y `OrderNo` acotados (`MemberId` igual que `MemberProfiles`) | M1 (el `Id` se queda y se justifica), M2, M3; Tareas 3 y 5 |
| Índice único de `OrderNo` con tratamiento de duplicados | M3, M4; Tareas 4 y 5 (68 grupos medidos) |
| `(MemberId, OrderDate) INCLUDE` según las consultas | M5 (grep de `.Orders`); Tarea 3 |
| `OrderDetails` y `PaymentHistories`: acotar, índice `(OrderId) INCLUDE`, relación explícita, fuera `OrdersId`, guarda de huérfanos | M6, M7 (los pagos sin FK, con su porqué), M8 (`LastOrderId1`, el mismo fallo); Tareas 3 y 5 (51024 y 51027) |
| Árbol de inscripción con `hierarchyid`, índice en profundidad, evaluar el de anchura, paquete con versión exacta | M11, M12; Tarea 6 (10.0.11, comprobado con `dotnet package search`) |
| Calcular `Node`: migración o proceso aparte | M14 y R1; Tarea 10 |
| Escritores coherentes, prueba de regla, `GetReparentedValue` | Tareas 7 y 8, R2, R3 (hoy nadie mueve nodos de inscripción) |
| `HierarchyPath` en paralelo y retirada posterior; no acortarla a 1700 | M15, M23; «Fuera de alcance» |
| Consultas de equipo con `IsDescendantOf` y *seek* | M16; Tareas 11–13; `SqlServerPlanesDeEjecucionTests` |
| Binario: medir la profundidad y el tamaño de `hierarchyid` y decidir | «Hechos medidos» (934 niveles, 584 B, máximo 1.427); R4, M17 |
| CTE con `MAXRECURSION`, prueba de SQL Server, solo en subárboles normales | M18–M20; Tareas 14–15 |
| Otros índices justificados por su consulta | M9 (`MemberStatistics`), M10 (`DualTeamTree`), M5 y M7 |
| RCSI como tarea: script, instrucciones, verificación, activación local, revisión de candados | M22; «Revisión de candados bajo RCSI»; Tareas 16–17; R6 y R7 |
| Medición antes y después con umbrales | Tablas «Mediciones»; Tareas 1, 15 y 18 |
| Migraciones agrupadas con guardas, aviso de arranque y aplicación local explícita | Tres migraciones (la tercera no hace falta: `NOT NULL` se pospone, R17); guardas 51020–51028; R8, M24; «Comandos» |
| TDD, hora de servidor, commits sin firma, solo la base local | «Reglas»; cada tarea |
| Enlace al documento de decisiones | Cabecera |

**Marcadores sin contenido:** no queda ninguno.
- La tabla «Mediciones» se deja vacía a propósito: la rellenan las Tareas 1, 15 y 18 con cifras medidas.
- Las Tareas 12 y 13 describen sustituciones línea a línea con el texto de antes y el de después, porque el resto de cada método no cambia.

**Coherencia de nombres:** las tareas usan los mismos nombres en todas partes:
- `GenealogyNodes.NextChildAsync` / `IsDuplicateNode`.
- `GenealogyConfiguration.NodeIndexName` = `IX_GenealogyTree_Node`.
- `GenealogySubtree.NodeOfAsync` / `CurrentUnder`.
- `DualSubtree.DescendantsAsync` / `MaxRowsForCte` / `Row` (`MemberId`, `Depth`, `Leg`).
- `ReportesDb.RellenarNodos`, `PlanDeEjecucion.EstimadoAsync` / `Seek`.
- `OrderNumberHelper.MaxLength`.
- Los índices `IX_Orders_OrderNo`, `IX_OrderDetails_OrderId`, `IX_GenealogyTree_NodeLevel_Node` e `IX_DualTeamTree_ParentMemberId`.
