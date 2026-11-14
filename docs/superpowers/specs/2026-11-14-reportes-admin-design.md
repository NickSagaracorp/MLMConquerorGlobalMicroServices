# Sección de Reportes del Admin — especificación (borrador)

Fecha: 2026-11-14. Estado: **completa, pendiente de revisión de Nick**. Nick va pasando los reportes de la herramienta del cliente
(hecha leyendo CSV) y aquí se define el equivalente en el sistema nuevo.

## Principios
- Los datos salen de **nuestra base**, con endpoints del AdminAPI. Nada de CSV.
- Las fechas y los cortes usan la **hora del servidor** (`IDateTimeProvider.Now`), no UTC.
- Acceso: roles SuperAdmin y Admin.
- **Exportar a CSV en todos los reportes**, solo para un rol específico de admin/superadmin (permiso propio,
  p. ej. `reports.export`); quien no lo tenga ve el reporte pero no el botón ni el endpoint de exportación.
- Lo que dependa de fechas pasadas sale de **fotos diarias** precalculadas, no de reconstruir historia en cada consulta.
  Con más de 1M de miembros, eso hace que la consulta sea instantánea.

## Base común: foto diaria de miembros por país

Proceso nocturno que guarda, para el día que cierra, un conteo por país, tipo de miembro y estado de membresía:

| Campo | Notas |
|---|---|
| SnapshotDate | `date`; el día al cierre |
| Country | el mismo valor que `MemberProfile.Country` |
| MemberType | Ambassador / ExternalMember (Customer) |
| Status | `MemberAccountStatus`: Active, Inactive, Suspended, Terminated, Pending |
| MemberCount | conteo al cierre del día |

- Clave única: (SnapshotDate, Country, MemberType, Status). Pocas filas por día (países × 2 × 5).
- Si el proceso falla un día, se puede rellenar después.
- **No se reconstruye el pasado**: la primera foto es la del día en que se activa el feature (decisión del 2026-11-14).
- Es idempotente: recalcular un día reemplaza sus filas.

## Reporte 1 — Distribución de miembros por país

Objetivo: ver el flujo de miembros por país.

**Filtros**
- Estado de membresía (multiselección). Por defecto, todos los estados.
- Rango de fechas.

**Gráfica**: total de miembros por período (barras), **mensual por defecto**.

**Tabla de países** (ordenada por Total, de mayor a menor):

| Columna | Definición |
|---|---|
| Country | país |
| Total | Ambassadors + Customers al cierre del período, con los estados filtrados |
| Ambassadors | MemberType = Ambassador |
| Customers | MemberType = ExternalMember |
| Change | Total de hoy (o del cierre) − Total de **30 días antes**; flecha verde si sube y roja si baja |

### Reporte 1b — Comparación por frecuencia
Mismos datos y filtros, con tres vistas, todas sobre la foto diaria:
1. **Mensual**: un mes contra otro.
2. **Semanas del mes**: las semanas de un mes elegido.
3. **Diaria**: los días de una semana elegida.

Las semanas empiezan el **lunes** y **se cortan en los límites del mes**: la primera semana va del día 1 al primer
domingo y la última, del último lunes al cierre del mes. Así, cada semana pertenece a un solo mes.

## Reporte 2 — Earnings Lifetime (ranking de comisiones acumuladas)

Objetivo: ranking de miembros por el total de comisiones ganadas en toda su vida, hasta una fecha de corte.

**Filtros**
- Búsqueda por nombre o email.
- Fecha de corte (en la herramienta del cliente es "Snapshot date", un desplegable con las fechas de los CSV).
  Por defecto, el día anterior.

**Encabezado**: número de resultados.

**Tabla** (paginada en el servidor, ordenada por Total de mayor a menor):

| Columna | Definición |
|---|---|
| Rank | posición en el ranking a esa fecha (#1, #2…) |
| Username | `MemberProfile.ReplicateSiteSlug` |
| Member Name | nombre completo |
| Email | email |
| Country | `MemberProfile.Country` |
| Total | suma de `CommissionEarning.Amount` del beneficiario con `EarnedDate` ≤ fecha de corte y estado **Paid o Pending** (las Cancelled no cuentan) |

**Fuente**: `CommissionEarning` (`BeneficiaryMemberId`, `Amount`, `Status`, `EarnedDate`).
Los residuales diarios (`DailyResidualEarning`) se consolidan en `CommissionEarning`: no se suman aparte,
para no contarlos dos veces. Los que todavía no se consolidaron no entran.

**Exclusión**: las cuentas corporativas (p. ej. «MWR Corp.») no aparecen en el ranking. Se marcan con un indicador
nuevo `MemberProfile.IsCorporateAccount` (bit, por defecto 0), que el admin activa en el perfil del miembro; el cambio
queda auditado. Cualquier reporte que tenga que excluirlas usa ese indicador.

**Rendimiento**: el ranking se calcula de una vez para todos los miembros. Opciones: calcularlo al vuelo con un
índice (BeneficiaryMemberId, EarnedDate) INCLUDE (Amount, Status), o guardar una foto diaria del acumulado por
miembro (~110k filas por día). Se decide midiendo contra la base de escala.

## Reporte 3 — Earnings 180 Days

Mismo reporte que Earnings Lifetime (columnas, búsqueda, fecha de corte, paginación, exclusión de cuentas
corporativas, pagadas + pendientes, Username = `ReplicateSiteSlug`), pero el Total solo suma las comisiones con
`EarnedDate` en la ventana **(fecha de corte − 180 días, fecha de corte]**.

**Implementación**: un solo componente y un solo endpoint de ranking de ganancias, con un parámetro de ventana
(`days`: vacío = lifetime, 180 = últimos 180 días). Las dos entradas del menú lo usan con valores distintos.
En Earnings 180 Days el admin puede cambiar la ventana con un selector (30, 90, 180, 365 días); por defecto, 180.

## Reporte 4 — Lifetime Rank

Objetivo: el rango de por vida (el más alto alcanzado) de cada Ambassador y desde cuándo lo tiene.

**Filtros**
- Búsqueda por nombre, email o país.
- Rango (desplegable con los `RankDefinitions` activos sin el rango 0; "All ranks" por defecto).
- Rango de fechas sobre "Lifetime Rank Since" (desde/hasta).
- Estado de membresía (multiselección, como en el reporte 1; sin `Terminated`, que nunca aparece).

**Encabezado**: número de resultados.

**Tabla** (paginada en el servidor; orden por defecto: Lifetime Rank de mayor a menor por `SortOrder`):

| Columna | Definición |
|---|---|
| Ambassador | nombre completo |
| Email | email |
| Country | `MemberProfile.Country` |
| Status | `MemberProfile.Status` como etiqueta (Active en verde, el resto en gris) |
| Lifetime Rank | el rango de mayor `SortOrder` en `MemberRankHistories` |
| Lifetime Rank Since | fecha en que alcanzó **por primera vez** ese rango (el `AchievedAt` más antiguo de las filas con ese rango) |

**Quién aparece** (decisión del 2026-11-14):
- Solo `MemberType = Ambassador`.
- **No** aparecen: los que su rango es el 0 (`SortOrder = 0`, el rango de inicio «Lifestyle Consultant» en
  `RankDefinitions`; Nick lo llama «LifeStyle Ambassador»), ni los que no tienen historial de rangos, ni las cuentas
  canceladas (`Status = Terminated`), ni las cuentas corporativas (`IsCorporateAccount`).
- Fuente: `MemberRankQueries` ya decide el rango máximo (`LifetimeRanks`), pero trabaja con una lista de ids y,
  ante un empate de rango, toma la fila de menor Id, no la más antigua. Para este reporte hace falta una versión
  sobre toda la población (85k+ Ambassadors, filtrable y paginada) y que la fecha sea la primera vez que lo alcanzó.
  Se resuelve con una consulta agregada por miembro (MAX(SortOrder) y MIN(AchievedAt) de ese rango) o,
  si no rinde a escala, con el rango de por vida guardado en la tabla de estadísticas del miembro.

## Reporte 5 — Current Rank

Igual que Lifetime Rank (filtros, mismas exclusiones, filtro de estado, paginación y exportación), pero con el
rango **vigente**:

| Columna | Definición |
|---|---|
| Current Rank | la fila del historial con el `AchievedAt` más reciente (`MemberRankQueries.CurrentRankRows`) |
| Current Rank Since | el `AchievedAt` de esa fila |

Orden por defecto: Current Rank de mayor a menor y, dentro de un mismo rango, por fecha de la más reciente a la
más antigua (así se ve en la herramienta del cliente). El filtro de fechas se aplica sobre «Current Rank Since».
Se excluyen los que su rango vigente es el 0.

**Implementación**: un solo componente y un solo endpoint de ranking de rangos con un parámetro
(`lifetime` | `current`); las dos entradas del menú lo usan.

## Sección 6 — Club Recognition

Objetivo: detectar en automático a los Ambassadors que alcanzan un nivel de reconocimiento por ganancias, y que el
admin lleve el control de su reconocimiento (en la llamada del CEO y en un evento) y del premio.

### Niveles (parametrizables)
Pantalla de parámetros en el admin para el catálogo `RecognitionLevel`:

| Campo | Notas |
|---|---|
| Name | p. ej. «100k Earner», «100k Lifetime Earner», «Million Dollar Earner», «Lifetime Million Dollar Earner» |
| Threshold | monto que hay que alcanzar |
| WindowDays | ventana en días (p. ej. 180); vacío = de por vida |
| Reward | descripción del premio (p. ej. anillo) |
| RewardNeedsSize | si el premio lleva talla |
| SortOrder, IsActive | orden en listas y filtros; desactivar sin borrar |

Valores iniciales (semilla, editables después en la pantalla de parámetros; decisión del 2026-11-14):

| Nivel | Threshold | WindowDays |
|---|---|---|
| 100k Earner | 50.000 | 180 |
| 500k Earner | 250.000 | 180 |
| Million Dollar Earner | 500.000 | 180 |
| 100k Lifetime Earner | 100.000 | (de por vida) |
| 500k Lifetime Earner | 500.000 | (de por vida) |
| Lifetime Million Dollar Earner | 1.000.000 | (de por vida) |

(Los niveles de 500k aparecen en las vistas del cliente; el umbral de «500k Earner» se deduce de sus totales,
~250k, igual que los otros de 180 días.)

### Detección automática
- Proceso nocturno: para cada nivel activo, calcula las ganancias de cada Ambassador con la misma regla de los
  reportes Earnings (pagadas + pendientes, sin las canceladas; ventana = `WindowDays` hasta el día que cierra;
  sin cuentas corporativas ni canceladas).
- Quien llega al umbral y no tiene registro de ese nivel recibe uno: `MemberRecognition` con «Reached» = la fecha
  del proceso nocturno que lo detectó, y el total con que calificó.
- **Sí se lee el histórico** (corrección del 2026-11-14): al activarse, una carga inicial recorre las comisiones
  históricas y crea los registros de quien ya alcanzó cada nivel. «Reached» en la carga inicial = el día en que se
  cruzó el umbral (de por vida: la fecha de la comisión que hizo cruzar el acumulado; 180 días: el primer día en que
  la ventana móvil llegó al umbral). Desde ahí, el proceso nocturno registra a los nuevos con la fecha de la corrida.
- **Una fila por Ambassador y nivel** (clave única). Una vez calificado no se pierde aunque después baje.
- Es idempotente: correrlo dos veces el mismo día no duplica.

### Captura del admin: pestaña «Recognition» en el perfil del Ambassador
Por cada nivel alcanzado, el admin llena dos resultados, **CEO Call Recognized** (lo que el menú llama «Online») y
**Event Recognized**. En las vistas del cliente, cada uno es una de estas opciones, no solo una fecha:
- reconocido: en la llamada del CEO, el mes y el año («10 2026»); en evento, el evento (de `CorporateEvents`,
  p. ej. «Bootcamp CIS 2026»);
- «Recognized Higher Level»: ya se le reconoció en un nivel superior;
- «Recognized Long Time Ago»: se le reconoció hace tiempo, antes de llevar este control;
- «Do not recognize – <motivo>» (p. ej. a pedido del sponsor).
Cualquier valor saca el registro de la lista de «no reconocidos» correspondiente.
Las opciones son un **catálogo editable** en la pantalla de parámetros (decisión del 2026-11-14).
- **Reward Size** (p. ej. «US9»), si el nivel lleva premio con talla.
- Notas. Cada cambio queda auditado.

### Vistas (menú Club Recognition)
Todas sobre `MemberRecognition`, con búsqueda (nombre, email, país), filtro por nivel y número de resultados:

Las vistas «Not Recognized…» muestran también la otra columna (p. ej. «Not Recognized on Event» enseña el valor de CEO Call).

| Vista | Filtro |
|---|---|
| All Records | todas |
| Not Recognized Online | sin CEO Call Recognized |
| Not Recognized on Events | sin Event Recognized |
| Recognized Online | con CEO Call Recognized |
| Recognized Events | con Event Recognized |

Columnas: Ambassador, Email, Username (`ReplicateSiteSlug`), Country, Level, Reached, Total (con el que calificó),
CEO Call Recognized, Event Recognized, Reward Size. En cuanto el admin llena el dato, el registro sale de la lista de
«no reconocidos».

Hoy no existe nada de esto en el sistema: es un módulo nuevo (catálogo, detección, pestaña de captura y vistas).

## Historial de rangos y migración
Los reportes Lifetime Rank y Current Rank leen `MemberRankHistories`. Para que «Since» sea correcto con los miembros
que vienen de legacy, **la migración debe importar el histórico de rank achievement** (cada rango alcanzado y su
fecha), no solo el rango actual. Mientras no exista ese historial importado, un miembro migrado mostraría la fecha de
la migración. También se importan desde legacy los reconocimientos de clubes ya capturados (CEO Call y evento),
para que no aparezcan como pendientes.

## Fuera de alcance de esta especificación: Notes y Tasks
La herramienta del cliente tiene además **Notes** (My Notes, Shared With Me) y **Tasks** (All Tasks, Pending:
título y descripción, departamento, vencimiento con aviso de vencida, asignado a, estado, creado por; filtros por
estado, departamento y «solo vencidas»; vista lista y Kanban; botón New task). Son un gestor de trabajo interno del
staff, no reportes: van en una especificación propia si Nick los pide. Hoy no existe el concepto de departamento.

## Estructura del menú (según la herramienta del cliente)
Reports → Top Countries · Earnings 180 Days · Earnings Lifetime · Lifetime Rank · Current Rank.
Club Recognition → All Records · Not Recognized Online · Not Recognized on Events · Recognized Online · Recognized Events.
Parámetros → Niveles de reconocimiento · Opciones de reconocimiento.
(El «Overview» de la herramienta del cliente está vacío: no se incluye.)
En la misma barra hay otras secciones (Club Recognition, Notes, Tasks); fuera de alcance mientras Nick no las pida.

## Decisiones tomadas
- 2026-11-14: Total cuenta a todos los miembros; el estado de membresía es un filtro. Change compara contra 30 días antes.
  La gráfica es mensual por defecto. Hay vistas de comparación mensual, semanas del mes y diaria de una semana,
  sobre la foto diaria.
- 2026-11-14: las semanas empiezan el lunes. Las fotos diarias empiezan el día que se activa el feature, sin reconstruir el pasado.
- 2026-11-14: Earnings Lifetime suma lo pagado y lo pendiente; Username = ReplicateSiteSlug; se excluyen las cuentas corporativas;
  los residuales cuentan solo una vez consolidados. Exportar a CSV en todos los reportes, con un rol/permiso específico.
- 2026-11-14: las cuentas corporativas se marcan con un indicador `IsCorporateAccount` en el perfil, que activa el admin (opción A).
- 2026-11-14: los reportes de rangos excluyen el rango 0, a quienes no tienen historial, las cuentas canceladas (Terminated)
  y las corporativas; llevan filtro de estado de membresía. Current Rank es el mismo reporte con el rango vigente.
- 2026-11-14: Club Recognition: niveles por parámetros (monto, ventana, premio); detección automática; el admin
  captura la talla del premio y marca el reconocimiento en la llamada del CEO y en el evento.
- 2026-11-14: valores iniciales de los niveles 50k/180d, 100k de por vida, 500k/180d y 1M de por vida; Reached = fecha del proceso
  nocturno; solo cuenta lo que se alcance desde que se activa el proceso, sin revisar el pasado.
- 2026-11-14 (corrección): Club Recognition sí lee el histórico de comisiones; solo lo basado en fotos diarias empieza al activarse.
  Hay niveles de 500k (180 días y de por vida). Los resultados de reconocimiento son opciones (reconocido / nivel superior /
  hace tiempo / no reconocer con motivo), no solo una fecha.
- 2026-11-14: el «después de activar» es para los reportes de rangos: su histórico (rank achievement) se importa en la migración.
  Las opciones de reconocimiento son un catálogo. Los reconocimientos ya capturados por el cliente se importan en la migración.
- 2026-11-14: las semanas se cortan en el cierre del mes. El Overview no lleva nada y no se incluye.
- 2026-11-14: la ventana de Earnings 180 Days es seleccionable (30/90/180/365), 180 por defecto.
- 2026-11-14: la exportación es a CSV, no a Excel.
