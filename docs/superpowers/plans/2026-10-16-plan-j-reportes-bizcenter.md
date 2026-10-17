# Plan J — Reportes de prioridad media del BizCenter

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar al miembro del BizCenter seis reportes que tenía en MWR Life y aquí faltan o están mal: *Mis enlaces*, *Residuales diarios* (sobre la tabla buena), *Clientes del equipo* con alcance y datos de contacto solo de los personales, *Actividad por fechas*, *Cambios de estado* (inscripción, Dual Team y clientes) y *Eventos del equipo* (con la importación de inscripciones desde el Admin), más los índices que esas consultas necesitan.

**Architecture:** Cada reporte es un servicio de `Repository` (lo comparten BizCenter y, donde ya existe, el Admin), un endpoint fino que saca el miembro **del token** (`ICurrentUserService.MemberId`, nunca de la petición) y un componente único de `SharedComponents` que recibe lo específico por parámetro (`ApiBaseUrl`, `Scope`); las páginas de `BizCenterWeb` son envoltorios de tres líneas. El árbol de inscripción se acota por prefijo de `GenealogyTree.HierarchyPath`; el binario se reduce primero por fecha y después se comprueba la pertenencia. El historial de estados lo escribe un interceptor de `SaveChanges`, para que empiece a acumularse en cuanto se despliegue el Bloque 5.

**Tech Stack:** .NET 10, ASP.NET Core MVC, EF Core 10 (SQL Server), Blazor Server (BizCenterWeb, AdminWeb) con Syncfusion `SfGrid` y `ServerGridAdaptor`, xUnit + FluentAssertions + Moq + bUnit, EF InMemory y pruebas `[SqlServerFact]` contra la base local. Rama: `plan-j-reportes-bizcenter`, creada desde `master` antes de la Tarea 1.

**Seis bloques entregables, en este orden.** Cada uno se revisa y se fusiona por separado; un bloque solo depende de los anteriores.

| Bloque | Tareas | Qué entrega |
|---|---|---|
| 1. Mis enlaces | 1–6 | Plantillas validadas en configuración, endpoint, tarjeta con copiar, página y menú |
| 2. Residuales diarios | 7–14 | Arreglo del menú, índice, endpoint sobre `DailyResidualEarnings`, hora de servidor, sección 4 y gráfico nuevos, borrado de `DualResidualReport.razor` |
| 3. Clientes del equipo | 15–18 | Personales/Equipo, `Level`, contacto solo de los personales, índice de patrocinador (19 y 20 salen: N1) |
| 4. Actividad por fechas | 21–26 | Rango de fechas común (3 meses hacia atrás, N4), servicio con el nivel de la orden (N2), índice de fecha de alta, endpoint, pantalla |
| 5. Cambios de estado | 27–33 | Modelo e interceptor primero; marca `KeepQualified` y servicio de calificación compartido (N3), servicio, endpoint y componente con tres vistas |
| 6. Eventos del equipo | 34, 39–42 | Entidad y reporte en el BizCenter, vacío hasta que haya importación (35–38 salen: N5) |
| Cierre | 43–44 | §12 de `pruebas-de-seguridad.md` y verificación completa |

**Reglas del repo que aplican en todo el plan:**

- **Componentes unificados.** Se programa una vez y lo específico se inyecta por parámetro. Las páginas de `BizCenterWeb` solo montan el componente; la lógica vive en `SharedComponents` y en servicios de `Repository`. Si el Admin ya usa un componente que se toca (residuales, clientes), su endpoint se actualiza en la misma tarea.
- **Miembro del token.** Ningún endpoint nuevo del BizCenter acepta `memberId` (ni ruta, ni query, ni cuerpo). Lo vigila `MiembroDelTokenTests` (Tarea 4, ampliada en cada bloque).
- **Alcance de cada consulta.** Árbol de inscripción: `HierarchyPath.StartsWith(prefijo del nodo más reciente del miembro)`. Binario: primero se reduce por fecha (historial) y después se comprueba la pertenencia con `DualTeamTree.HierarchyPath.StartsWith`.
- **Hora de servidor.** `IDateTimeProvider.Now` en servicios. La única excepción es el interceptor del historial, que se construye en `AppDbContext.OnConfiguring` sin DI y usa `DateTime.Now`, como `AuditInterceptor` y `SecurityFactorsInterceptor` (J25).
- **Pruebas.** TDD con EF InMemory; `[SqlServerFact]` donde InMemory no reproduce la consulta (traducción a SQL, `UNION`, índices únicos). Las de SQL Server corren dentro de una transacción que se deshace: no dejan datos. Precedentes: `Signups.Tests/Helpers/SqlServerPrueba.cs` y `Billing.Tests/Services/SqlServerMemberFundsGateTests.cs`.
- **Migraciones.** Se crean con `dotnet ef migrations add`; **no** se aplican solas. Aplicarlas a la base local es un paso explícito, siempre con `--connection` y siempre contra `MLMConquerorGlobalEditionDb` local:
  `--connection "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;"`. Nunca contra otra base. Ojo: cualquier servicio que se arranque en local también las aplica (`MigrateAsync` en todos los `Program.cs`, R2), así que se aplican a mano **antes** de arrancar nada.
- **Textos.** Toda clave nueva va en los **nueve** `.resx` de `SharedComponents/Resources` (en, es, pt, fr, it, de, ko, zh, ka); el español, en tuteo. Lo vigila `TextosDeReportesTests`. Cada clave es una línea `<data name="CLAVE" xml:space="preserve"><value>TEXTO</value></data>` que se añade justo antes de `</root>`.
- **Commits.** Mensaje en español, sin tildes (como el historial), solo la descripción de lo que se hizo: **sin** `Co-Authored-By` ni menciones de herramientas o IA. `git add` con rutas explícitas, nunca `git add .` ni `-A`.
- No tocar la matriz de paridad (`MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md`).

---

## Correcciones de Nick (2026-10-16) — mandan sobre el resto del plan

Llegaron con los Bloques 1 y 2 en marcha. Donde una tarea, decisión o riesgo de más abajo diga otra cosa, **manda esta sección**; las filas y tareas afectadas llevan además una nota «Corrección de Nick».

| # | Corrección | Qué cambia en el plan |
|---|---|---|
| N1 | **No se migran datos del legado** (clientes, historial, eventos). Sí se permiten cambios de esquema pequeños en nuestra base: índices y tablas nuevas (historial de estados, inscripciones a eventos, la marca de N3). **`GenealogyTree.HierarchyPath` no se toca**: ni `nvarchar(450)` ni índice. | **Fuera:** Tareas 19 y 20, J21 y R1. Las consultas por prefijo del árbol de inscripción se quedan con el `StartsWith` actual sobre `nvarchar(max)` (funcionan igual, solo sin *seek*). Pasar la ruta a `nvarchar(450)` con índice queda anotado como **mejora de rendimiento futura**. P2 queda contestada: no se migran clientes. |
| N2 | **El nivel de membresía lo da el producto de la orden** (`OrderDetails → Products.MembershipLevelId`), no la suscripción. Un pedido lleva como mucho dos productos de membresía (Lifestyle Ambassador y, opcionalmente, VIP/Elite/Turbo) y su nivel es **el más alto**. Ya existe el sitio único que aplica esa regla: `Repository/Queries/OrderMembershipLevelQueries.cs` (`GetHighestMembershipLevelIdAsync`, `GetHighestMembershipLevelIdsByOrderAsync`). Se reutiliza; no se escribe otra consulta. | J10 cambia: la columna de *Actividad* sale del nivel más alto de la **orden de alta** del inscrito (la primera orden suya con productos de membresía), no de su primera suscripción. Todo reporte que **muestre** un nivel (*Cambios de estado*: `MembershipLevelName`) lo saca de la orden de la suscripción actual (`MembershipSubscription.LastOrderId`) con la misma regla; solo si la suscripción no tiene orden (semillas, altas manuales) se usa `MembershipSubscription.MembershipLevelId` como reserva. Se añade a `OrderMembershipLevelQueries` una variante por lote que recibe un `IQueryable<string>` de ids de orden (la de hoy recorre todos los pedidos), con su prueba. |
| N3 | **«Calificado» = membresía activa Y una de tres:** (a) se inscribió en los últimos 12 meses (`EnrollDate > Now − 12 meses`, hora de servidor); (b) se inscribió hace más de 12 meses y ya pagó su anualidad; (c) tiene la marca de mantener calificado. **Hoy no existe ni la marca ni el pago de la anualidad** (solo el `TokenType` «Annual Fee», sin fecha de pago por miembro). | Sustituye a J15. Un único servicio compartido `IQualificationService` (Repository) que usan todos los reportes y, como es sencillo, también `EnrollmentTeamService` y `DualTeamService` (hoy `IsQualified = suscripción Active`). La marca se crea: `MemberProfile.KeepQualified` (`bit`, por defecto `0`) en una migración pequeña propia (`MarcaMantenerCalificado`); editarla desde el Admin queda fuera de este plan. (b) es un punto de extensión explícito, `IAnnualFeeStatus.HasPaidCurrentAnnualFeeAsync` (y su versión por lote), cuya única implementación hoy, `AnnualFeeNotModeled`, devuelve `false` y lo documenta; el Plan K la sustituye. **Se retira «Calificado desde»** (`QualificationTimeline`, `QualifiedChangedAt`): con (a), (b) y (c) la calificación cambia por el paso del tiempo y por pagos que aún no existen, así que una fecha de último cambio sacada del historial de suscripciones sería falsa. *Cambios de estado* enseña «Calificado» (Sí/No, actual) y las fechas de último cambio de cuenta y de membresía. |
| N4 | **Rango de fechas de *Actividad por fechas*: como mucho 3 meses hacia atrás.** Por defecto, del día 1 del mes en curso a hoy. Se rechaza un rango cuyo inicio sea anterior a `hoy − 3 meses` o cuyo fin sea posterior a hoy. **Solo para Actividad** (aclaración de Nick tras revisar los Bloques 1 y 2). | Sustituye a J12 **solo para *Actividad***. `ReportDateRange` conserva la regla común de J12 (días inclusivos, `[desde, hasta + 1 día)`, 366 días como mucho, `REPORT_RANGE_INVALID`/`REPORT_RANGE_TOO_LONG`), que es la que usa *Cambios de estado*; *Actividad* usa además `ReportDateRange.TryCreateForActivity(desde, hasta, ahora)` con su propia constante `ActivityLookbackMonths = 3` y dos códigos más: `REPORT_RANGE_TOO_OLD` (inicio anterior a hoy − 3 meses) y `REPORT_RANGE_IN_FUTURE` (fin posterior a hoy). El servicio de actividad recibe `IDateTimeProvider` para saber qué es «hoy». `GET team/reports/setup` devuelve `DefaultFrom`, `DefaultTo`, `MaxRangeDays` (366, para *Cambios de estado*) y `ActivityMinFrom` (hoy − 3 meses). R5 se reduce para *Actividad*: ~92 días de altas como mucho. |
| N5 | **Eventos del equipo: no se importa nada todavía.** Solo se maqueta el reporte, aunque salga vacío. | **Fuera:** Tareas 35 (`CsvLite`), 36 (importador), 37 (endpoint del Admin) y 38 (pantalla del Admin), J19, R10 y las pruebas de inyección CSV. Se mantienen: la entidad mínima `CorporateEventRegistration` con su migración `InscripcionesAEventos` (Tarea 34, fuente del reporte), el servicio y el endpoint del BizCenter (Tareas 39 y 40), la página con selector de evento y tabla (Tarea 41, vacía hasta que haya importación) y las pruebas SQL Server del reporte (Tarea 42, sin las del importador). La importación será un plan aparte. |
| N6 | **Revisión de los Bloques 1 y 2.** Una fila `Paid` de `DailyResidualEarnings` no es dinero pagado; los débitos con saldo de comisiones ya no salen en *Residuales*; el endpoint viejo `commissions/dual-residual` sigue vivo. | J27, J28 y J29 (abajo).

---

## Riesgos

| # | Riesgo | Mitigación en este plan |
|---|---|---|
| R1 | **~~Retirado (corrección de Nick N1): `HierarchyPath` no se toca; el cambio a `nvarchar(450)` con índice queda como mejora de rendimiento futura. Lo que sigue se conserva solo como análisis para entonces.~~** **`GenealogyTree.HierarchyPath` a `nvarchar(450)`.** Hoy las rutas miden como mucho 89 caracteres (nivel 8; ids `AMB-######` y `MBR-######` de 10 caracteres: 11 por nivel más la barra inicial). Con 450 caben **40 niveles**. La base local parece sintética (119.446 nodos, profundidad 8, una sola raíz `AMB-700829`); **no sabemos la profundidad real de la red migrada de MWR Life en producción**, y un árbol de patrocinio real puede pasar de 40 niveles. | Va en su **propia migración y su propio commit** (Tarea 20), separable del resto del bloque: las consultas funcionan igual sin el índice, solo más lentas. Guarda doble: (1) `GenealogyPath.TryChild` (Tarea 19) hace que el alta de alguien que quedaría por debajo del límite falle con `GENEALOGY_TOO_DEEP` **antes** de crear nada, en vez de un 500 por truncado; (2) la migración lanza `THROW 51002` si existe alguna ruta de más de 450, así que nunca trunca en silencio. Condición de despliegue: `SELECT MAX(LEN(HierarchyPath)), MAX(Level) FROM GenealogyTree` en producción; si pasa de 360 (80 %), **no** desplegar la Tarea 20 (revertir su commit) y reabrir J21. |
| R2 | **Todos los servicios aplican migraciones al arrancar** (`MigrateAsync` en los ocho `Program.cs`). Las del plan alteran `MemberProfiles.SponsorMemberId` (de `nvarchar(max)` a `nvarchar(20)`), `GenealogyTree.HierarchyPath` y `MemberStatusHistories.MemberId`, y crean índices sobre tablas de ~120.000 filas: bloqueo de la tabla durante segundos. En local, arrancar cualquier servicio las aplica sin pedir permiso. | Cada migración que estrecha una columna comprueba antes con `THROW` que ningún valor existente se trunca (51001, 51002, 51003). Desplegar fuera de horas de tráfico. En local se aplican a mano con `--connection` (paso explícito en cada tarea) antes de arrancar nada. |
| R3 | `ExecuteUpdate`/`ExecuteDelete` y el SQL crudo se saltan el interceptor del historial: un cambio de estado hecho así no deja rastro. Tampoco lo deja un `Update()` sobre una entidad que no venía del contexto (el valor original sería el nuevo), que es lo que hace `GenericRepository.Update` de `IUnitOfWork.Members`/`Subscriptions`. Hoy nadie hace ninguna de las dos cosas con estados (`IUnitOfWork` no tiene consumidores). | `EstadosSinInterceptorTests` (Tarea 28) recorre el código fuente y falla si una sentencia con `ExecuteUpdate`, `ExecuteDelete` o `ExecuteSql` menciona `MemberProfiles` o `MembershipSubscriptions` y `Status`, si aparece un `UPDATE` de esas tablas en SQL, o si alguien llama a `Members.Update(` o `Subscriptions.Update(`. |
| R4 | **El historial empieza el día que se despliega el Bloque 5.** Antes solo hay 4 filas en `MemberStatusHistories` y ninguna de membresía: «fecha del último cambio» y «Calificado desde» no existen para lo anterior. | Fechas de reserva documentadas (J15): para la membresía, la columna propia del estado; para la cuenta, «—». El reporte lista miembros con un cambio **registrado** en el rango, así que antes del despliegue sale vacío; lo dice §12. Por eso el modelo y el interceptor son las dos primeras tareas del bloque (27 y 28) y se pueden fusionar y desplegar antes que la pantalla. |
| R5 | **Corrección de Nick N4: el rango es de 3 meses hacia atrás como mucho (~92 días), no 366; el riesgo se reduce en proporción.** **Actividad por fechas materializa en memoria** las altas del rango dentro del equipo (cuatro campos por alta) para pivotar por nivel. Para la raíz del árbol y 366 días pueden ser decenas de miles de filas. | Rango limitado a 366 días (`ReportDateRange.MaxDays`); el filtro por fecha y equipo y la unión con suscripciones van en SQL (sin `Contains` de miles de ids); el pivote y el orden, en memoria. `SqlServerActividadTests` (Tarea 26) mide la consulta de la raíz local; si un año tarda más de 3 s, abrir una tarea de agregación en SQL. |
| R6 | El legado filtraba texto en cualquier columna. Aquí, en *Cambios de estado* y en *Clientes*, solo se buscan, filtran y ordenan en la base las columnas que salen de `MemberProfiles`; las enriquecidas por página se descartan, igual que ya hace `EnrollmentTeamService`. | Esas columnas se pintan con `AllowSorting="false"` y `AllowFiltering="false"`. |
| R7 | Solo hay **3** `ExternalMember` en local: *Clientes* y *Cambios de clientes* no se pueden probar con volumen. | Pruebas con datos sintéticos (InMemory y SQL Server). Pregunta P2 a Nick. |
| R8 | `GenealogyTree` admite **dos nodos para el mismo miembro** (el índice `(MemberId, CreationDate)` no es único; ver e4af288). Un `JOIN` contra el árbol duplicaría filas y contaría dos veces. | El nodo del que mira es siempre el más reciente (`OrderByDescending(CreationDate)`); los equipos se acotan con un subconjunto `Distinct()` de `MemberId`; la actividad hace `DistinctBy` antes de contar. |
| R9 | **`Orders.OrderNo` y `Orders.MemberId` son `nvarchar(max)` sin índice** (no hay `OrdersConfiguration`) y seis bucles comprueban la unicidad de `OrderNo` con `AnyAsync`: provoca bloqueos al arrancar Billing (289.519 pedidos en local). | **Fuera de alcance**, trabajo aparte: hace falta la longitud real de `OrderNo`, un índice único y revisar los seis bucles (`ChargeHandler`, `RenewMembershipHandler`, `RecurringBillingProcessor`, `SignupAmbassadorHandler`, `SignupMemberHandler`…). Se anota aquí para que no se pierda. |
| R10 | **Retirado (corrección de Nick N5): no hay importación CSV en este plan.** La importación CSV rechaza cualquier campo que empiece por `=`, `+`, `-`, `@`, tabulador o retorno: una referencia legítima que empiece por `-` se rechaza. | Aceptado: la referencia la genera la plataforma de entradas. El rechazo dice motivo (`FORMULA_NOT_ALLOWED`) y línea. |
| R11 | Las plantillas de enlaces viven en `appsettings.json` del BizCenter: cambiarlas exige desplegar, y una plantilla inválida **impide arrancar** el BizCenter. | Es lo que se busca (J1). `ConfiguracionDeEnlacesTests` (Tarea 4) valida el `appsettings.json` del repositorio con la misma guarda que el arranque: una plantilla mala no llega a fusionarse. |
| R12 | EF InMemory no traduce a SQL, no aplica índices únicos y no reproduce el `UNION`. | Una tarea `[SqlServerFact]` por bloque. Exigen las migraciones aplicadas en local; sin SQL Server se marcan **saltadas** (no verdes). |
| R13 | **Choque de nombre:** el Plan I (`2026-10-16-plan-i-3ds-con-spreedly.md`, R12) dice que «el Plan J» pondrá en `BizCenterWeb` la CSP de Spreedly. Este Plan J no lo hace. | Fuera de alcance y anotado: hay que renombrar esa referencia del Plan I para que no se dé por hecha. |
| R14 | El interceptor añade una fila por cada cambio de estado de suscripción, y `RecurringBillingProcessor` puede pasar miles a `HoldByBilling` en una noche. | Una inserción pequeña por cambio, en la misma transacción. Índices `(MemberId, ChangedAt)` y `(ChangedAt)`. Se vigila la duración del primer ciclo tras desplegar (§12.30). |
| R15 | Las pantallas con Syncfusion (`SfGrid`) no tienen pruebas bUnit en el repo. | La lógica vive en servicios y endpoints, que sí se prueban; las pantallas se verifican a mano en la Tarea 44 con pasos concretos. Los componentes sin Syncfusion (`MyLinksCard`, `DailyResidualHistory`) llevan bUnit. |
| R16 | **El nivel por la orden (N2) recorre `Orders` y `OrderDetails` enteros.** `OrderDetails.OrderId` y `Orders.MemberId` son `nvarchar(max)` sin índice; el único índice de `OrderDetails` es `IX_OrderDetails_OrdersId`, sobre una FK en sombra (`OrdersId`) que está **vacía en las 190.517 filas locales** (la columna que se rellena es `OrderId`). *Actividad* une las altas del rango con sus órdenes por `MemberId` y *Clientes* busca las órdenes de una página por `Id` y sus líneas por `OrderId`. **Medido en local (Tarea 26): la raíz con 3 meses tarda 6,2 s** (umbral de R5: 3 s), y con servicios levantados contra la base local, una lectura `ReadCommitted` de *Actividad* sobre la raíz acaba víctima de un *deadlock* frente al bucle de `OrderNo` de R9. | Es R9 con un motivo más: antes de desplegar los reportes con N2, indexar `OrderDetails.OrderId` y `Orders.MemberId` (pasarlas a `nvarchar(450)` con su guarda de truncado) y revisar por qué `OrdersId` existe y está vacía. Trabajo aparte, no en este plan; la medición queda en `SqlServerActividadTests` para comparar después. |

## Decisiones

**Del encargo (vinculantes):** la cuota anual va en un Plan K aparte; orden de los seis bloques; plantillas de enlaces en configuración o en `GlobalParameters` (aquí se elige, J1); endpoint de residuales sobre `DailyResidualEarnings`, sección 4 y gráfico, `UtcNow` de `CommissionsService` y del componente, `IsDualTeamActive`, y borrado de `DualResidualReport.razor` si está huérfano; clientes con alcance Personales/Equipo, `Level` y contacto solo de los personales; actividad con columnas por los niveles reales y puntos actuales; cambios de estado con modelo e interceptor primero, componente único con tres vistas, estado actual y fecha del último cambio; eventos con entidad de inscripción e importación CSV con validación, informe de rechazos, idempotencia y protección contra inyección; índices `MemberProfiles(SponsorMemberId, EnrollDate)`, `DailyResidualEarnings(BeneficiaryMemberId, EarnedDate)` y `MemberStatusHistories` con `MemberId` a `nvarchar(450)`; evaluar `HierarchyPath`; reglas de componentes, token, alcance, hora, pruebas, migraciones y commits; §12 de pruebas de seguridad.

**Tomadas aquí (con su porqué):**

| # | Decisión | Por qué |
|---|---|---|
| J1 | **Plantillas de enlaces en `appsettings.json` del BizCenter** (sección `MyLinks`), no en `GlobalParameters`. Se validan al arrancar (`MyLinksOptionsGuard`): https absoluta, `{slug}` como mucho una vez y solo en la ruta, ningún otro marcador, sin usuario ni contraseña, claves únicas, etiqueta obligatoria de 60 como mucho, máximo 30 enlaces. | Hoy `GlobalParameters` solo se edita para claves `Crypto.*` (`GlobalParameterRules.ManagedPrefix`) y el controlador lo explica: «las demás claves tienen su propia pantalla, que valida su valor». No hay pantalla para enlaces, y un enlace es justo lo que alguien con un administrador comprometido cambiaría para que **todos** los miembros repartan su URL. En configuración el cambio pasa por revisión de código y por la guarda; los dominios cambian muy de vez en cuando. Si Nick quiere editarlos desde el Admin, será con pantalla propia y validación, no con el CRUD genérico. |
| J2 | `{slug}` es `MemberProfile.ReplicateSiteSlug`, escapado con `Uri.EscapeDataString`. Sin slug, los enlaces personales no salen y la tarjeta avisa con un enlace a `/profile` (donde está `WebsiteSlugCard`); los corporativos salen siempre. Las etiquetas vienen de la configuración, sin traducir (son marcas). | El legado usaba el usuario; el equivalente público aquí es el slug del sitio replicado. |
| J3 | Residuales diarios: servicio nuevo `DailyResidualReportService` sobre `DailyResidualEarnings`, filtrado por `BeneficiaryMemberId == miembro del token`, **sin** `Cancelled` ni en filas ni en totales; totales del periodo (pagado, pendiente y suma). El año por defecto y los años disponibles (desde el año de alta, 15 como mucho) los da el servidor. La caja de búsqueda desaparece (el servidor la ignoraba y el legado no la tenía). | `CommissionEarnings` de categoría 3 mezcla débitos negativos y consolidaciones; `DailyResidualEarnings` es lo que escribe el motor (`CalculateDailyResidualHandler.cs:153`). El año sale del reloj del servidor, no del navegador. |
| J4 | El gráfico conserva endpoint y contrato (`commissions/dual-residual/chart`), pero `CommissionsService.GetDualResidualChartAsync` pasa a leer `DailyResidualEarnings` con `IDateTimeProvider.Now` y un tope de 24 meses. | Sin cambiar llamadores (BizCenter y Admin). |
| J5 | `CommissionsService` recibe `IDateTimeProvider` por constructor y sustituye `DateTime.UtcNow` (líneas 32, 297, 562, 714) y `DateTime.Now` (338). | Hora de servidor. Los dos anfitriones ya registran `SharedKernel.Interfaces.IDateTimeProvider` (`BizCenter/Program.cs:138`, `AdminAPI/Program.cs:88`). |
| J6 | Se borra `SharedComponents/Components/Commissions/DualResidualReport.razor`: nadie lo monta (comprobado con `grep` en todo el repo; la Tarea 13 lo vuelve a comprobar antes de borrar). El endpoint `commissions/dual-residual` (sobre `CommissionEarnings`) **se queda**: lo prueban `CommissionHandlersTests` y puede tener consumidores fuera del repo. | Borrar código muerto, sí; cambiar un contrato público, no en este plan. |
| J7 | La sección 4 de `DualTeamResiduals.razor` sale a un componente propio, `DailyResidualHistory.razor` (sin Syncfusion, con bUnit). `DualTeamResiduals` pierde el parámetro `EnrollDate` (solo servía para los años) y el Admin deja de pasarlo. | Componente unificado y probable; saca de un archivo de 1.023 líneas toda la lógica que cambia, con su `UtcNow` (línea 604). |
| J8 | Clientes: alcance `Team` por defecto (lo de hoy) y `Personal` (`SponsorMemberId == yo`). El correo y el teléfono se anulan **en la proyección SQL** (`CASE`) para quien no es personal, de modo que ni la búsqueda ni el filtro por correo o teléfono delatan a un cliente ajeno. También en el `GET` antiguo `team/enrollment/customers`. | Si se anulara después de paginar, buscar `juan@` encontraría la fila de un cliente del nivel 3 y revelaría que existe. |
| J9 | `Level` = nivel del cliente menos el del que mira (1 = personal), igual que *Mi equipo*. En alcance Personal es siempre 1. | Una sola definición de nivel en el BizCenter. |
| J10 | **Corrección de Nick N2: el nivel del inscrito es el más alto de los productos de su orden de alta (`OrderMembershipLevelQueries`), no el de su primera suscripción. Sin orden con productos de membresía, «Otros». El resto de la fila se mantiene.** Actividad: columnas fijas por `MembershipLevelId` **1 a 4** (Lifestyle Ambassador, VIP, Elite, Turbo) con los nombres de `MembershipLevels`. **No hay columna «Plus»** porque ese nivel no existe aquí; **Turbo no tenía columna en el legado** y aquí la tiene; cualquier otro nivel o la falta de suscripción va a «Otros». Se cuenta el nivel de la **primera** suscripción del inscrito (la del alta), no el actual. | El reporte cuenta lo que se inscribió en el rango: una subida posterior no debe moverlo de columna. |
| J11 | Actividad: una fila por miembro del equipo (el que mira incluido) con al menos un inscrito en el rango, no los 120.000 del equipo. Puntos = `MemberStatistics.EnrollmentPoints` actuales, como el legado. | Las filas a cero no dicen nada y harían la consulta inmanejable. |
| J12 | **Vigente para *Cambios de estado*; *Actividad* añade el límite de 3 meses de N4.** Rango común `ReportDateRange`: `from` y `to` son días **inclusivos**; se convierten a `[from, to + 1 día)`; máximo **366 días**; errores `REPORT_RANGE_INVALID` y `REPORT_RANGE_TOO_LONG` (400). Valores por defecto del servidor: día 1 del mes en curso hasta hoy (`GET team/reports/setup`). | Una sola regla para actividad y cambios; el tope acota R5. |
| J13 | El historial de membresía va en una **tabla hermana**, `MembershipStatusHistories` (`MemberId`, `SubscriptionId`, `MembershipLevelId`, `OldStatus` anulable, `NewStatus`, `ChangedAt`), no en una columna `Kind` de `MemberStatusHistories`. | Los dos enumerados son distintos (`MemberAccountStatus` y `MembershipStatus`); mezclar sus enteros en una columna invita a leer mal. La tabla hermana no toca las filas ni los escritores existentes. |
| J14 | El interceptor (`StatusHistoryInterceptor`) registra: cambios de `MemberProfile.Status` (no la creación del perfil), la creación de una `MembershipSubscription` (`OldStatus = null`) y los cambios de `SubscriptionStatus`. Compara el valor original con el actual, como `SecurityFactorsInterceptor`. Si el mismo guardado ya trae una `MemberStatusHistory` añadida a mano para ese miembro y ese estado nuevo (lo hacen `UpdateMemberStatusHandler`, `CancelMembershipHandler` y `ProcessScheduledCancellationsJob`, con su motivo), no duplica. | Los escritores actuales no cambian y conservan su `Reason`; los que no escribían (activación del alta, facturación recurrente, subida y bajada de nivel) quedan cubiertos sin tocarlos. |
| J15 | **Sustituida por la corrección de Nick N3: «Calificado» = membresía activa Y (alta en los últimos 12 meses O anualidad pagada O `KeepQualified`), en `IQualificationService`; sin «Calificado desde».** ~~«Calificado» = la suscripción actual del miembro está `Active`~~, la misma definición que `EnrollmentTeamService.cs:240`. «Calificado desde» = el último instante en que cambió la condición «tiene alguna suscripción `Active`», calculado con `QualificationTimeline` **agrupando por instante**: una subida de nivel (la vieja pasa a `Expired` y la nueva nace `Active` en el mismo guardado) no cuenta como cambio. Suscripción actual = la de `StartDate` más reciente (desempate por `CreationDate`), sea cual sea su estado. Sin historial, la fecha de reserva es la propia del estado: `HoldDate` para `OnHold`/`HoldByBilling`, `CancellationDate` para `Cancelled`, `EndDate` para `Expired`, `StartDate` para el resto. | Nick pidió estado actual y fecha del último cambio, como el legado. Sin agrupar por instante, cada subida de nivel parecería una pérdida y una recuperación de la calificación. |
| J16 | *Cambios de estado* lista a los miembros del alcance con **algún cambio registrado en el rango** (cuenta o membresía) y, de cada uno, el estado **actual** y la fecha del **último** cambio (de siempre, no del rango). | Es el `EnrollmentTeamChangesReport` del legado con los datos que tenemos. |
| J17 | Vista Dual Team: `UNION` de los `MemberId` con cambios en el rango → `JOIN` a `DualTeamTree` por `MemberId` (índice `IX_DualTeamTree_MemberId_Lookup`) → `StartsWith` del prefijo binario solo sobre esas filas. La pierna sale del hijo directo del que mira en la ruta, como en `DualTeamService`. | `DualTeamTree.HierarchyPath` llega a 10.275 caracteres y no es indexable: hay que reducir antes. |
| J18 | **Corrección de Nick N5: la entidad se mantiene como fuente del reporte, pero no hay importación en este plan; la idempotencia por `(EventId, ExternalRef)` queda para el importador futuro (el índice único se crea igual).** Inscripciones a eventos: entidad `CorporateEventRegistration` (`EventId`, `MemberId`, `RegisteredAt`, `Tickets`, `Price` anulable, `Source = "CsvImport"`, `ExternalRef`) con índice **único** `(EventId, ExternalRef)` y claves foráneas a `CorporateEvents.Id` y `MemberProfiles.MemberId`. Reimportar el mismo archivo es idempotente: una referencia ya importada cuenta como `AlreadyImported` y **no** se actualiza. La comparación de referencias no distingue mayúsculas, como la intercalación de la base. | La referencia es la de la compra en la plataforma de entradas; un miembro puede comprar varias veces y el reporte suma sus entradas. |
| J19 | **Retirada (corrección de Nick N5): fuera de este plan.** ~~CSV:~~ UTF-8 (con o sin BOM; un byte inválido es `400 INVALID_ENCODING`), coma, comillas RFC 4180, cabecera exacta `MemberId,RegisteredAt,Tickets,Price,ExternalRef` (sin distinguir mayúsculas), 2 MB y 10.000 filas como mucho. Fechas `yyyy-MM-dd`, `yyyy-MM-dd HH:mm[:ss]` o con `T`, no más de un día en el futuro; entradas 1–100; precio vacío o 0–100.000 con punto; `MemberId` `^[A-Za-z0-9-]{1,20}$` que exista (sin distinguir mayúsculas: se normaliza a mayúsculas, como se generan); referencia `^[A-Za-z0-9._:/#-]{1,100}$`. Cualquier campo que empiece por `= + - @`, tabulador o retorno se rechaza (`FORMULA_NOT_ALLOWED`). En el informe, el texto devuelto se neutraliza (prefijo `'`) y se recorta a 100; se listan hasta 500 rechazos y se cuentan todos. | Validación, informe, idempotencia e inyección CSV, sin paquete nuevo. |
| J20 | Reporte de eventos: equipo de inscripción por prefijo, **el que mira incluido**; por defecto solo inscritos (`registeredOnly=true`) con la opción de ver a todo el equipo con «No»; totales (inscritos distintos y entradas) por consulta aparte; solo eventos `IsActive && !IsDeleted` (uno inactivo es `404 EVENT_NOT_FOUND`). | Como el legado (columna «Register» y totales), sin cargar el equipo entero por defecto. |
| J21 | **Retirada (corrección de Nick N1): `GenealogyTree.HierarchyPath` no se toca; mejora de rendimiento futura.** ~~Se propone~~ pasar `GenealogyTree.HierarchyPath` a `nvarchar(450)` con índice, en migración propia (Tarea 20), con la guarda de altas `GENEALOGY_TOO_DEEP` (Tarea 19) y el `THROW` de la migración. Solo escriben rutas de genealogía el alta (`SignupAmbassadorHandler.cs:228`, `SignupMemberHandler.cs:168`) y `RootAmbassadorSeeder` (raíz de 12 caracteres); colocar y retirar escriben `DualTeamTree`, no esta tabla. | Hoy cada consulta por prefijo recorre las 119.446 rutas en `nvarchar(max)`; con el índice es un *seek*. 40 niveles bastan con los datos que conocemos; R1 dice qué hacer si producción no cabe. |
| J22 | **Corregida por Nick: `nvarchar(450)`, no `nvarchar(20)`** (en la solución del cliente hay patrocinadores de 50 caracteres; 450 es la longitud de `GenealogyTree.MemberId`/`ParentMemberId`, `DualTeamTree.MemberId` y `DualTeamLegFrontiers.SponsorMemberId`, con los que se une, y lo máximo que admite índice; la guarda de la migración rechaza valores de más de 450). ~~`MemberProfiles.SponsorMemberId` pasa a `nvarchar(20)` (como `MemberId`)~~ para indexar `(SponsorMemberId, EnrollDate)`; además, índice `(EnrollDate)` para *Actividad*, que filtra por fecha sin patrocinador concreto. | Un `nvarchar(max)` no puede ser clave de índice; el compuesto sirve al alcance Personal, no a un rango de fechas suelto. |
| J23 | `MemberStatusHistories.MemberId`, y `MemberId`/`SubscriptionId` de la tabla hermana, a `nvarchar(450)`, como pide el encargo (los ids reales miden 10). Índices `(MemberId, ChangedAt)` y `(ChangedAt)` en las dos tablas. | `(MemberId, ChangedAt)` para la última fecha por miembro; `(ChangedAt)` para reducir por rango. |
| J24 | Los endpoints nuevos van en controladores nuevos (`MyLinksController`, `TeamReportsController`, `TeamEventsController`) o con `[FromServices]` en los existentes; ningún constructor existente cambia. | `PropiedadEnElCentroDeNegociosTests` construye `TeamController` y `CommissionsController` a mano. |
| J25 | El interceptor usa `DateTime.Now` (hora de servidor), como los otros dos. | Se crea en `OnConfiguring` sin contenedor; inyectar `IDateTimeProvider` obligaría a registrar el contexto de otra forma en ocho anfitriones. |
| J26 | Menú: *Mis enlaces* (`/my-links`) y *Eventos* (`/events`) como entradas propias tras *Marketing*; en *Equipo de inscripción*: *Cambios de clientes* (`/team/enrollment/customer-changes`), *Actividad* (`/team/enrollment/activity`) y *Cambios de estado* (`/team/enrollment/changes`); en *Dual Team*: *Cambios de estado* (`/team/dual-changes`). Qué grupo se abre lo decide `SidebarRoutes`, que se prueba. | Rutas que no se pisan con `IsActive` (que usa `Contains`): `customer-changes` no contiene `customers`. |
| J27 | **«Consolidado», no «Pagado».** En *Residuales diarios* el estado `Paid` de `DailyResidualEarnings` y el total del periodo correspondiente se rotulan «Consolidado» (`Reports.Consolidated`; en inglés *Consolidated*), y «Pendiente» se queda. El campo del API sigue llamándose `PeriodPaid` y el estado `"Paid"`: es el nombre del enumerado, no lo que ve el miembro. | `Paid` solo dice que la fila se sumó a un crédito de comisión (`CommissionEarnings`) por la consolidación semanal (`DailyResidualConsolidationJob`) o por una compra con saldo (`CommissionBalanceService`). Ese crédito puede seguir `Pending` de pago o haberse gastado en una renovación o en tokens: decir «Pagado» engaña. |
| J28 | **Débitos y créditos de consolidación: se ven en la pantalla general de comisiones, no en *Residuales*.** Los débitos negativos (`CommissionBalanceService`: renovación o tokens pagados con saldo, tipo «Daily Residual», `Pending`) y los créditos consolidados (`DailyResidualConsolidationJob`) son `CommissionEarnings`. Salen en `GET commissions` (lista agrupada por día de pago en la pestaña *Pendiente*, sumados) y, línea a línea con su tipo, su nota y el importe en rojo si es negativo, en el desglose `GET commissions/breakdown`. **No** salen en `GET commissions/history`, que solo suma `Paid`. | Comprobado en `CommissionsService.GetCommissionsAsync`/`GetBreakdownAsync` y en `CommissionsDashboard.razor`. Lo que se pierde es que el agrupado del día suma crédito y débito en una sola cifra; separar visualmente los débitos queda como mejora pendiente, no en este plan. |
| J29 | **Retirar `GET commissions/dual-residual`** (BizCenter y Admin, sobre `CommissionEarnings` de la categoría 3). Desde la Tarea 13 no lo llama ninguna pantalla del repositorio. | **Pendiente, no en este plan** (J6 lo mantenía por si había consumidores fuera del repo y porque lo prueba `CommissionHandlersTests`). Antes de retirarlo: buscar llamadas en los registros de acceso de producción y en la app MAUI publicada. |

## Fuera de alcance

- **Cuota anual de $99** (cobro aparte, preferencia de tokens, apagar `MembershipAutoRenewalJob`): Plan K.
- **Índice de `Orders.OrderNo`/`Orders.MemberId`** (R9): trabajo aparte.
- **CSP de Spreedly en `BizCenterWeb`** que el Plan I atribuye a «Plan J» (R13).
- **Exportación a Excel/CSV** de los reportes (P3).
- **Línea Manager→National** de *Clientes* (P4).
- **Migración de datos del legado** (clientes, historial, eventos): no se hace (corrección de Nick N1).
- **`GenealogyTree.HierarchyPath` a `nvarchar(450)` con índice** y la guarda `GENEALOGY_TOO_DEEP` (Tareas 19 y 20): mejora de rendimiento futura (N1).
- **Importación de inscripciones a eventos** (CSV, endpoint y pantalla del Admin; Tareas 35–38): plan aparte (N5).
- **Pago de la anualidad** para la rama (b) de «Calificado»: Plan K; aquí solo el punto de extensión `IAnnualFeeStatus` (N3).
- **Editar `KeepQualified` desde el Admin** (N3).
- **Índices de `OrderDetails.OrderId` y `Orders.MemberId`** (R9 y R16): necesarios para que el nivel por la orden no recorra las tablas enteras.
- **Nivel por la orden en *Mi equipo* de inscripción y de Dual Team** (`EnrollmentTeamService.GetMyTeam*`, `DualTeamService`): siguen sacando `MembershipLevelName` de la suscripción. N2 se aplicó a los reportes del Plan J (*Clientes* y *Actividad*); el método compartido `GetMembershipLevelIdsBySubscriptionAsync` ya existe para extenderlo.
- **Retirar `GET commissions/dual-residual`** (J29) y **separar visualmente los débitos** en la lista de comisiones (J28): pendientes.
- **Páginas de `BizCenterApp` (MAUI)** para los reportes nuevos: los componentes son los mismos y se podrán montar después.
- **Pestañas nuevas en el perfil de miembro del Admin** para actividad, cambios y eventos. Solo se añaden endpoints de Admin donde un componente que ya usa el Admin cambia (residuales y clientes).
- **Editar o borrar inscripciones importadas**, y otras fuentes de importación que no sean CSV.
- **Colocación manual**: cubierta por el árbol binario (decisión de Nick).
- **Búsqueda y filtro sobre columnas enriquecidas** (R6).

## Preguntas abiertas para Nick

No bloquean el plan: cada una tiene un valor por defecto que el plan implementa y que se cambia sin tocar código o con un cambio pequeño.

| # | Pregunta | Qué hace el plan mientras tanto |
|---|---|---|
| P1 | ¿Qué dominios de *Mis enlaces* siguen vivos? El legado tenía `mwrlife.com/{user}`, `join.`/`vip.`/`free.traveladvantage.com/{user}`, `lifeexperience.club/{user}` y los estáticos `mwracademy.com`, `mwrlife.online`, `mwrevents.com`, `traveladvantage.com` y `mwrlifespace.com`. | Los diez van en `appsettings.json` con https. Quitar uno es borrar su entrada. |
| P2 | **Contestada (N1): no se migran.** ¿Se migran los clientes (`ExternalMember`) del legado? En local solo hay 3. | Los reportes de clientes funcionan con los que haya. |
| P3 | ¿Se quiere exportación a Excel/CSV en el BizCenter? Hoy no hay ninguna. | No se añade. Si se añade, toda celda de texto pasará por `CsvLite.Neutralize` (Tarea 35). |
| P4 | ¿Hace falta la línea Manager→National de *Clientes*? | No se añade. |
| P5 | ~~¿Valen la definición de «Calificado» (J15) y que la actividad cuente el nivel de la primera suscripción (J10)?~~ | **Contestada (N2 y N3).** |
| P6 | ¿Se enseña al miembro el `PaymentComment` de los residuales («Consolidated into CommissionEarning #…»)? | Se enseña: es información suya. |

## Mapa de archivos

Rutas abreviadas: todos los proyectos llevan el prefijo `MLMConquerorGlobalEdition.` (`Repository/…` = `MLMConquerorGlobalEdition.Repository/…`). En los bloques **Files** de cada tarea van completas.

| Archivo | Responsabilidad |
|---|---|
| `BizCenter.Tests/Reportes/RelojFijo.cs`, `ReportesDb.cs`, `SqlServerReportes.cs` (nuevos) | Reloj fijo, base InMemory, siembra y `[SqlServerFact]` de todas las pruebas de reportes |
| `BizCenter.Tests/Reportes/TextosDeReportesTests.cs`, `MiembroDelTokenTests.cs` (nuevos) | Claves en los 9 idiomas; ningún endpoint nuevo acepta `memberId` |
| `Repository/Services/Links/*` (nuevos) | Plantillas, guarda, servicio y registro de *Mis enlaces* |
| `BizCenter/Controllers/MyLinksController.cs` (nuevo), `BizCenter/appsettings.json`, `BizCenter/Program.cs` | Endpoint, plantillas por defecto, registro |
| `SharedComponents/Components/Profile/MyLinksCard.razor` (nuevo), `BizCenterWeb/Components/Pages/MyLinks.razor` (nuevo) | Tarjeta con copiar; página fina |
| `SharedComponents/Components/Layout/SidebarRoutes.cs` (nuevo), `SidebarMenu.razor` | Qué grupo se abre; entradas nuevas |
| `Repository/Services/Commissions/DailyResidualDtos.cs`, `IDailyResidualReportService.cs`, `DailyResidualReportService.cs` (nuevos), `CommissionsService.cs` | Residuales diarios; reloj y gráfico |
| `Repository/Configurations/Commission/DailyResidualEarningConfiguration.cs`, migración `IndiceResidualesPorBeneficiarioYFecha` | Índice |
| `BizCenter/Controllers/CommissionsController.cs`, `AdminAPI/Controllers/AdminMemberCommissionsController.cs`, `AdminAPI/Program.cs` | `dual-residual/daily` en los dos portales |
| `SharedComponents/Components/Commissions/DailyResidualHistory.razor` (nuevo), `Team/DualTeamResiduals.razor`, `AdminWeb/Components/Pages/AdminMemberProfile.razor`; se borra `Commissions/DualResidualReport.razor` | Sección 4 nueva |
| `Repository/Services/Teams/CustomerScope.cs` (nuevo), `EnrollmentTeamService.cs`, `IEnrollmentTeamService.cs`, `EnrollmentDtos.cs` | Alcance, nivel y contacto de clientes |
| `BizCenter/Controllers/TeamController.cs`, `BizCenter/DTOs/Teams/EnrollmentCustomerDto.cs`, `BizCenter/Features/Teams/GetEnrollmentCustomers/GetEnrollmentCustomersHandler.cs`, `AdminAPI/Controllers/AdminMemberEnrollmentController.cs`, `SharedComponents/Components/Team/EnrollmentTeamCustomers.razor` | Clientes en los dos portales |
| `Repository/Configurations/MemberProfileConfiguration.cs`, migraciones `IndicePatrocinadorYFechaDeAlta` e `IndiceFechaDeAlta` | Índices de `MemberProfiles` |
| `Domain/Entities/Tree/GenealogyPath.cs` (nuevo), `SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs`, `SignupMember/SignupMemberHandler.cs`, `Repository/Configurations/GenealogyConfiguration.cs`, migración `IndiceRutaDeGenealogia` | **Fuera (N1).** Guarda de profundidad e índice de ruta |
| `Repository/Services/Teams/ReportDateRange.cs`, `EnrollmentActivityDtos.cs`, `IEnrollmentActivityService.cs`, `EnrollmentActivityService.cs` (nuevos) | Rango común y actividad |
| `BizCenter/Controllers/TeamReportsController.cs` (nuevo) | Setup, actividad y cambios de estado |
| `SharedComponents/Components/Team/EnrollmentTeamActivity.razor`, `BizCenterWeb/Components/Pages/EnrollmentActivity.razor` (nuevos) | Pantalla de actividad |
| `Domain/Entities/Membership/MembershipStatusHistory.cs`, `Repository/Configurations/MemberStatusHistoryConfiguration.cs`, `MembershipStatusHistoryConfiguration.cs`, `Repository/Interceptors/StatusHistoryInterceptor.cs` (nuevos), `Repository/Context/AppDbContext.cs`, migración `HistorialDeEstados` | Historial de estados |
| `Repository/Services/Teams/IQualificationService.cs`, `QualificationService.cs`, `IAnnualFeeStatus.cs` (N3, en lugar de `QualificationTimeline.cs`), `StatusChangesScope.cs`, `TeamStatusChangeDtos.cs`, `ITeamStatusChangesService.cs`, `TeamStatusChangesService.cs` (nuevos) | Cambios de estado |
| `SharedComponents/Components/Team/TeamStatusChangesGrid.razor`, `BizCenterWeb/Components/Pages/EnrollmentChanges.razor`, `DualTeamChanges.razor`, `CustomerChanges.razor` (nuevos) | Componente único con tres vistas |
| `Domain/Entities/Events/CorporateEventRegistration.cs`, `Repository/Configurations/CorporateEventRegistrationConfiguration.cs` (nuevos), migración `InscripcionesAEventos` | Inscripciones a eventos |
| `Repository/Services/Events/CsvLite.cs`, `EventRegistrationImportDtos.cs`, `IEventRegistrationImporter.cs`, `EventRegistrationImporter.cs`, `TeamEventDtos.cs`, `ITeamEventRegistrationsService.cs`, `TeamEventRegistrationsService.cs` (nuevos) | Reporte (`CsvLite`, `EventRegistrationImport*` e `IEventRegistrationImporter` quedan fuera: N5) |
| `AdminAPI/Controllers/CorporateEventsController.cs`, `AdminWeb/Components/Pages/AdminCorporateEvents.razor` | **Fuera (N5).** Importación en el Admin |
| `BizCenter/Controllers/TeamEventsController.cs`, `SharedComponents/Components/Team/TeamEventRegistrations.razor`, `BizCenterWeb/Components/Pages/TeamEvents.razor` (nuevos) | Reporte en el BizCenter |
| `SharedComponents/Resources/SharedResources*.resx` (9) | Textos |
| `docs/deployment/pruebas-de-seguridad.md` | §12 |

**Comandos que se repiten.** Para no repetirlos en cada tarea:

- **Crear una migración** (el nombre lo da cada tarea):

  ```bash
  dotnet ef migrations add NOMBRE \
    --project MLMConquerorGlobalEdition.Repository \
    --startup-project MLMConquerorGlobalEdition.SignupAPI
  ```

  Revisar `Up`: solo las operaciones que diga la tarea. Si aparecen bloques `migrationBuilder.UpdateData(...)` de semillas (`HasData`) que no cambian nada, **borrarlos de `Up` y de `Down`** (no del `.Designer.cs` ni del snapshot). Si aparece cualquier otra operación sobre tablas ajenas, el modelo traía deriva: parar y avisar.

- **Aplicar la migración a la base local** (paso explícito, nunca contra otra base):

  ```bash
  dotnet ef database update \
    --project MLMConquerorGlobalEdition.Repository \
    --startup-project MLMConquerorGlobalEdition.SignupAPI \
    --connection "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;"
  ```

---

# Bloque 1 — Mis enlaces

### Task 1: Infraestructura de pruebas de reportes

Todo el plan siembra lo mismo (perfiles, nodos de los dos árboles, suscripciones, puntos) y necesita un reloj fijo y un `[SqlServerFact]` en `BizCenter.Tests`, que hoy no lo tiene. Se escribe una vez aquí.

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/RelojFijo.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportesDb.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerReportes.cs`

- [ ] **Step 1: Crear `RelojFijo.cs`**

```csharp
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Reloj de servidor fijo para las pruebas de reportes. Es el <c>IDateTimeProvider</c> de SharedKernel
/// (el que reciben los servicios de Repository), no el de <c>BizCenter.Services</c>.
/// </summary>
public sealed class RelojFijo : IDateTimeProvider
{
    public RelojFijo(DateTime now) => Now = now;

    public DateTime Now { get; set; }

    public DateTime UtcNow => Now.ToUniversalTime();
}
```

- [ ] **Step 2: Crear `ReportesDb.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Base y siembra compartidas por las pruebas de reportes del Plan J. Los métodos de siembra solo
/// añaden al contexto (no guardan): valen para EF InMemory y, dentro de una transacción que se
/// deshace, para SQL Server.
/// </summary>
public static class ReportesDb
{
    /// <summary>Instante de referencia de las pruebas (hora de servidor, sin Kind).</summary>
    public static readonly DateTime Base = new(2026, 3, 15, 10, 0, 0);

    public static AppDbContext Nueva() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    /// <summary>"/A/B/C/": la forma de las rutas de los dos árboles (barra inicial y final).</summary>
    public static string Ruta(params string[] ids) => "/" + string.Join("/", ids) + "/";

    /// <summary>Los cuatro niveles reales (ids 1 a 4). Solo para InMemory: en SQL Server ya existen.</summary>
    public static void Niveles(AppDbContext db) => db.MembershipLevels.AddRange(
        new MembershipLevel { Id = 1, Name = "Lifestyle Ambassador",     SortOrder = 1, CreatedBy = "prueba" },
        new MembershipLevel { Id = 2, Name = "Travel Advantage - VIP",   SortOrder = 2, CreatedBy = "prueba" },
        new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" },
        new MembershipLevel { Id = 4, Name = "Travel Advantage - Turbo", SortOrder = 4, CreatedBy = "prueba" });

    public static MemberProfile Miembro(
        AppDbContext db, string memberId, string? patrocinador = null,
        MemberType tipo = MemberType.Ambassador, DateTime? alta = null,
        MemberAccountStatus estado = MemberAccountStatus.Active, string? slug = null,
        string? email = null, string? telefono = null)
    {
        var perfil = new MemberProfile
        {
            UserId            = Guid.NewGuid(),
            MemberId          = memberId,
            Email             = email ?? $"{memberId.ToLowerInvariant()}@prueba.test",
            FirstName         = "Nombre",
            LastName          = memberId,
            Phone             = telefono,
            Country           = "US",
            MemberType        = tipo,
            Status            = estado,
            EnrollDate        = alta ?? Base,
            SponsorMemberId   = patrocinador,
            ReplicateSiteSlug = slug,
            CreatedBy         = "prueba"
        };
        db.MemberProfiles.Add(perfil);
        return perfil;
    }

    public static GenealogyEntity Nodo(AppDbContext db, string memberId, string? padre, string ruta, int nivel)
    {
        var nodo = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = padre,
            HierarchyPath  = ruta,
            Level          = nivel,
            CreatedBy      = "prueba"
        };
        db.GenealogyTree.Add(nodo);
        return nodo;
    }

    public static DualTeamEntity NodoBinario(AppDbContext db, string memberId, string? padre, string ruta, TreeSide lado)
    {
        var nodo = new DualTeamEntity
        {
            MemberId       = memberId,
            ParentMemberId = padre,
            HierarchyPath  = ruta,
            Side           = lado,
            CreatedBy      = "prueba"
        };
        db.DualTeamTree.Add(nodo);
        return nodo;
    }

    public static MembershipSubscription Suscripcion(
        AppDbContext db, string memberId, int nivel, MembershipStatus estado, DateTime inicio)
    {
        var suscripcion = new MembershipSubscription
        {
            MemberId           = memberId,
            MembershipLevelId  = nivel,
            SubscriptionStatus = estado,
            StartDate          = inicio,
            CreatedBy          = "prueba"
        };
        db.MembershipSubscriptions.Add(suscripcion);
        return suscripcion;
    }

    public static MemberStatisticEntity Puntos(AppDbContext db, string memberId, int inscripcion)
    {
        var estadistica = new MemberStatisticEntity
        {
            MemberId         = memberId,
            EnrollmentPoints = inscripcion,
            CreatedBy        = "prueba"
        };
        db.MemberStatistics.Add(estadistica);
        return estadistica;
    }

    /// <summary>
    /// Sube desde el directorio de salida hasta la carpeta del .slnx, para leer archivos del
    /// repositorio que no se copian a la salida (appsettings, .resx, código fuente).
    /// </summary>
    public static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null &&
               !File.Exists(Path.Combine(directorio.FullName, "MLMConquerorGlobalEdition.slnx")))
        {
            directorio = directorio.Parent;
        }

        directorio.Should().NotBeNull("la prueba tiene que poder localizar la raíz del repositorio");
        return directorio!.FullName;
    }
}
```

- [ ] **Step 3: Crear `SqlServerReportes.cs`**

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Un [Fact] que se marca como SALTADO (no como pasado) cuando no hay SQL Server alcanzable, igual
/// que en Billing.Tests y Signups.Tests: el Skip se pone al descubrir las pruebas.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerReportes.Reachable)
            Skip = "SQL Server no alcanzable (define MLM_TEST_SQLSERVER para usar otra instancia).";
    }
}

/// <summary>
/// La base local de desarrollo (o la de MLM_TEST_SQLSERVER). Cada prueba abre una transacción,
/// siembra con <see cref="ReportesDb"/>, consulta con el mismo contexto y la deshace: no deja datos.
/// Necesita las migraciones del plan aplicadas en local (paso explícito de cada tarea de migración).
/// </summary>
public static class SqlServerReportes
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("MLM_TEST_SQLSERVER")
        ?? "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;Encrypt=True;Connect Timeout=3;";

    private static readonly Lazy<bool> LazyReachable = new(() =>
    {
        try
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
            return true;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            return false;
        }
    });

    public static bool Reachable => LazyReachable.Value;

    public static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>Un MemberId sintético de 14 caracteres (cabe en los 20 de la columna) que no choca con nada real.</summary>
    public static string NuevoId(string prefijo = "TJ") =>
        (prefijo + Guid.NewGuid().ToString("N")).Substring(0, 14).ToUpperInvariant();
}
```

- [ ] **Step 4: Compilar el proyecto de pruebas**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenter.Tests -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/RelojFijo.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportesDb.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerReportes.cs
git commit -m "Pruebas de reportes del BizCenter: reloj fijo, siembra comun de perfiles, arboles, suscripciones y puntos, y SqlServerFact con transaccion que se deshace"
```

---

### Task 2: Plantillas de enlaces y su guarda

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksOptions.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksOptionsGuard.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksOptionsGuardTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Links;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Una plantilla de enlace llega a todos los miembros, que la reparten. La guarda no deja arrancar
/// el BizCenter con una que no sea https, que meta el slug en el dominio o que traiga marcadores
/// que no entendemos.
/// </summary>
public class MyLinksOptionsGuardTests
{
    private static MyLinksOptions Con(params (string Key, string Label, string Url)[] enlaces) => new()
    {
        Links = enlaces.Select(e => new MyLinkTemplate { Key = e.Key, Label = e.Label, Url = e.Url }).ToList()
    };

    [Fact]
    public void Sin_enlaces_es_valido()
        => MyLinksOptionsGuard.Error(new MyLinksOptions()).Should().BeNull();

    [Theory]
    [InlineData("https://mwrlife.com/{slug}")]
    [InlineData("https://join.traveladvantage.com/{slug}")]
    [InlineData("https://mwracademy.com")]
    [InlineData("https://mwrevents.com/agenda?ref={slug}")]
    public void Acepta_https_con_el_slug_en_la_ruta_o_sin_slug(string url)
        => MyLinksOptionsGuard.Error(Con(("enlace", "Etiqueta", url))).Should().BeNull();

    [Theory]
    [InlineData("http://mwrlife.com/{slug}",                "https")]
    [InlineData("mwrlife.com/{slug}",                       "https")]
    [InlineData("javascript:alert(1)",                      "https")]
    [InlineData("https://{slug}.mwrlife.com/",              "ruta")]
    [InlineData("https://mwrlife.com/{slug}/{slug}",        "una vez")]
    [InlineData("https://mwrlife.com/{user}",               "{slug}")]
    [InlineData("https://usuario:clave@mwrlife.com/{slug}", "usuario")]
    public void Rechaza_la_url(string url, string motivo)
        => MyLinksOptionsGuard.Error(Con(("enlace", "Etiqueta", url))).Should().Contain(motivo);

    [Fact]
    public void Rechaza_claves_repetidas_sin_distinguir_mayusculas()
        => MyLinksOptionsGuard.Error(Con(
                ("mwrlife", "Uno", "https://mwrlife.com/{slug}"),
                ("MWRLIFE", "Dos", "https://mwrlife.online")))
            .Should().Contain("repetida");

    [Theory]
    [InlineData("con espacio")]
    [InlineData("")]
    public void Rechaza_claves_invalidas(string clave)
        => MyLinksOptionsGuard.Error(Con((clave, "Etiqueta", "https://mwrlife.com"))).Should().Contain("clave");

    [Fact]
    public void Rechaza_etiquetas_vacias_o_largas()
    {
        MyLinksOptionsGuard.Error(Con(("a", " ", "https://mwrlife.com"))).Should().Contain("etiqueta");
        MyLinksOptionsGuard.Error(Con(("a", new string('x', 61), "https://mwrlife.com"))).Should().Contain("etiqueta");
    }

    [Fact]
    public void Rechaza_mas_de_treinta_enlaces()
    {
        var muchos = Enumerable.Range(1, 31).Select(i => ($"e{i}", "Etiqueta", "https://mwrlife.com")).ToArray();
        MyLinksOptionsGuard.Error(Con(muchos)).Should().Contain("30");
    }

    [Fact]
    public void ThrowIfInvalid_lanza_con_el_motivo()
    {
        var act = () => MyLinksOptionsGuard.ThrowIfInvalid(Con(("a", "A", "http://mwrlife.com")));
        act.Should().Throw<InvalidOperationException>().WithMessage("*https*");
    }

    [Fact]
    public void Sabe_si_una_plantilla_es_personal()
    {
        new MyLinkTemplate { Url = "https://mwrlife.com/{slug}" }.IsPersonal.Should().BeTrue();
        new MyLinkTemplate { Url = "https://mwracademy.com" }.IsPersonal.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter MyLinksOptionsGuardTests -nologo`
Expected: FAIL de compilación (`MyLinksOptions` no existe).

- [ ] **Step 3: Crear `MyLinksOptions.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Links;

/// <summary>
/// Plantillas de "Mis enlaces" (sección <c>MyLinks</c> del appsettings del BizCenter). Viven en
/// configuración y no en GlobalParameters a propósito (Plan J, J1): un enlace lo reparten todos los
/// miembros, así que cambiarlo pasa por revisión de código y por <see cref="MyLinksOptionsGuard"/>.
/// </summary>
public sealed class MyLinksOptions
{
    public const string SectionName = "MyLinks";

    /// <summary>Marcador que se sustituye por el slug del sitio replicado del miembro.</summary>
    public const string SlugToken = "{slug}";

    public const int MaxLinks = 30;

    public List<MyLinkTemplate> Links { get; set; } = new();
}

public sealed class MyLinkTemplate
{
    /// <summary>Identificador estable (letras, números y guiones). Sirve de clave en la pantalla.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Texto que ve el miembro. Son marcas: no se traduce.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>URL https absoluta; <c>{slug}</c> como mucho una vez y solo en la ruta o la query.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Lleva el slug del miembro: es un enlace personal.</summary>
    public bool IsPersonal => Url.Contains(MyLinksOptions.SlugToken, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Crear `MyLinksOptionsGuard.cs`**

```csharp
using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.Repository.Services.Links;

/// <summary>
/// Valida las plantillas al arrancar. Una sola regla mala impide arrancar el BizCenter: es mejor que
/// repartir a miles de miembros un enlace http, con usuario y contraseña, o con el slug en el dominio
/// (que convertiría cada slug en un subdominio que no controlamos).
/// </summary>
public static class MyLinksOptionsGuard
{
    private const int MaxLabelLength = 60;

    private static readonly Regex KeyPattern = new(@"^[A-Za-z0-9-]{1,40}$", RegexOptions.Compiled);

    /// <summary>El primer problema encontrado, o null si todo es válido.</summary>
    public static string? Error(MyLinksOptions options)
    {
        if (options.Links.Count > MyLinksOptions.MaxLinks)
            return $"[MyLinks] Como mucho {MyLinksOptions.MaxLinks} enlaces.";

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in options.Links)
        {
            var key  = link.Key ?? string.Empty;
            var name = key.Length == 0 ? "(sin clave)" : key;

            if (!KeyPattern.IsMatch(key))
                return $"[MyLinks] '{name}': la clave solo admite letras, números y guiones (de 1 a 40).";
            if (!keys.Add(key))
                return $"[MyLinks] '{name}': clave repetida.";
            if (string.IsNullOrWhiteSpace(link.Label) || link.Label.Length > MaxLabelLength)
                return $"[MyLinks] '{name}': la etiqueta es obligatoria y de {MaxLabelLength} caracteres como mucho.";

            var urlError = UrlError(link.Url ?? string.Empty);
            if (urlError is not null)
                return $"[MyLinks] '{name}': {urlError}";
        }

        return null;
    }

    public static void ThrowIfInvalid(MyLinksOptions options)
    {
        var error = Error(options);
        if (error is not null) throw new InvalidOperationException(error);
    }

    private static string? UrlError(string url)
    {
        var tokens = CountOf(url, MyLinksOptions.SlugToken);
        if (tokens > 1)
            return "{slug} solo puede aparecer una vez.";

        var probe = url.Replace(MyLinksOptions.SlugToken, "slug", StringComparison.Ordinal);
        if (probe.Contains('{') || probe.Contains('}'))
            return "solo se admite el marcador {slug}.";

        if (!Uri.TryCreate(probe, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return "tiene que ser una URL https absoluta.";

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return "no puede llevar usuario ni contraseña.";

        if (tokens == 1)
        {
            // El primer '/' después de "https://" cierra el dominio: el slug tiene que ir detrás.
            var authorityEnd = url.IndexOf('/', "https://".Length);
            var at           = url.IndexOf(MyLinksOptions.SlugToken, StringComparison.Ordinal);
            if (authorityEnd < 0 || at < authorityEnd)
                return "{slug} tiene que ir en la ruta, no en el dominio.";
        }

        return null;
    }

    private static int CountOf(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter MyLinksOptionsGuardTests -nologo`
Expected: PASS (19 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksOptions.cs \
        MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksOptionsGuard.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksOptionsGuardTests.cs
git commit -m "Mis enlaces: plantillas en configuracion con guarda de arranque (https absoluta, slug solo en la ruta y una vez, sin otros marcadores ni credenciales, claves unicas, etiqueta obligatoria, maximo 30)"
```

---

### Task 3: Servicio de enlaces

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksService.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksServiceCollectionExtensions.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Repository.Services.Links;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class MyLinksServiceTests
{
    private static MyLinksOptions Opciones() => new()
    {
        Links =
        {
            new MyLinkTemplate { Key = "mwrlife", Label = "MWR Life",    Url = "https://mwrlife.com/{slug}" },
            new MyLinkTemplate { Key = "academy", Label = "MWR Academy", Url = "https://mwracademy.com" }
        }
    };

    [Fact]
    public async Task Con_slug_rellena_los_personales_y_da_los_corporativos()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001", slug: "ana");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000001");

        vista.SlugMissing.Should().BeFalse();
        vista.Personal.Should().ContainSingle().Which.Url.Should().Be("https://mwrlife.com/ana");
        vista.Corporate.Should().ContainSingle().Which.Url.Should().Be("https://mwracademy.com");
    }

    [Fact]
    public async Task Sin_slug_avisa_y_solo_da_los_corporativos()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000001");

        vista.SlugMissing.Should().BeTrue();
        vista.Personal.Should().BeEmpty();
        vista.Corporate.Should().HaveCount(1);
    }

    [Fact]
    public async Task El_slug_se_escapa()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001", slug: "ana lópez");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000001");

        vista.Personal.Single().Url.Should().Be("https://mwrlife.com/ana%20l%C3%B3pez");
    }

    [Fact]
    public async Task Usa_el_slug_del_miembro_pedido_y_no_el_de_otro()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001", slug: "ana");
        ReportesDb.Miembro(db, "AMB-000002", slug: "pedro");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000002");

        vista.Personal.Single().Url.Should().Be("https://mwrlife.com/pedro");
    }

    [Fact]
    public async Task Un_miembro_que_no_existe_es_como_sin_slug()
    {
        await using var db = ReportesDb.Nueva();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-999999");

        vista.SlugMissing.Should().BeTrue();
        vista.Personal.Should().BeEmpty();
    }

    [Fact]
    public void El_registro_valida_antes_de_registrar()
    {
        var malas = new MyLinksOptions { Links = { new MyLinkTemplate { Key = "a", Label = "A", Url = "http://x.com" } } };

        var act = () => new ServiceCollection().AddMyLinks(malas);

        act.Should().Throw<InvalidOperationException>();
        new ServiceCollection().AddMyLinks(new MyLinksOptions())
            .Should().Contain(d => d.ServiceType == typeof(IMyLinksService));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter MyLinksServiceTests -nologo`
Expected: FAIL de compilación (`MyLinksService` no existe).

- [ ] **Step 3: Crear `MyLinksService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Links;

public sealed class MyLinkView
{
    public string Key   { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Url   { get; set; } = string.Empty;
}

public sealed class MyLinksView
{
    /// <summary>El miembro no tiene slug: los enlaces personales no se pueden construir.</summary>
    public bool SlugMissing { get; set; }

    public List<MyLinkView> Personal  { get; set; } = new();
    public List<MyLinkView> Corporate { get; set; } = new();
}

public interface IMyLinksService
{
    /// <summary>Los enlaces de <paramref name="memberId"/>. Quien llama pasa el miembro del token.</summary>
    Task<MyLinksView> GetAsync(string memberId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class MyLinksService : IMyLinksService
{
    private readonly AppDbContext   _db;
    private readonly MyLinksOptions _options;

    public MyLinksService(AppDbContext db, MyLinksOptions options)
    {
        _db      = db;
        _options = options;
    }

    public async Task<MyLinksView> GetAsync(string memberId, CancellationToken ct = default)
    {
        var slug = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.ReplicateSiteSlug)
            .FirstOrDefaultAsync(ct);

        var view = new MyLinksView { SlugMissing = string.IsNullOrWhiteSpace(slug) };
        var escaped = view.SlugMissing ? string.Empty : Uri.EscapeDataString(slug!.Trim());

        foreach (var template in _options.Links)
        {
            if (!template.IsPersonal)
            {
                view.Corporate.Add(new MyLinkView { Key = template.Key, Label = template.Label, Url = template.Url });
                continue;
            }

            if (view.SlugMissing) continue;

            view.Personal.Add(new MyLinkView
            {
                Key   = template.Key,
                Label = template.Label,
                Url   = template.Url.Replace(MyLinksOptions.SlugToken, escaped, StringComparison.Ordinal)
            });
        }

        return view;
    }
}
```

- [ ] **Step 4: Crear `MyLinksServiceCollectionExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.Repository.Services.Links;

public static class MyLinksServiceCollectionExtensions
{
    /// <summary>
    /// Pasa la guarda ANTES de registrar nada (como AddEntrax): con una plantilla inválida el
    /// anfitrión no arranca, en cualquier entorno.
    /// </summary>
    public static IServiceCollection AddMyLinks(this IServiceCollection services, MyLinksOptions options)
    {
        MyLinksOptionsGuard.ThrowIfInvalid(options);

        services.AddSingleton(options);
        services.AddScoped<IMyLinksService, MyLinksService>();
        return services;
    }
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter MyLinksServiceTests -nologo`
Expected: PASS (6 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Links/MyLinksServiceCollectionExtensions.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksServiceTests.cs
git commit -m "Mis enlaces: servicio que rellena las plantillas personales con el slug escapado del miembro, avisa si no tiene slug y siempre da los corporativos; el registro pasa la guarda antes de registrar"
```

---

### Task 4: Endpoint, configuración por defecto y registro

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter/Controllers/MyLinksController.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/appsettings.json:79` (antes de `"Logging"`)
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs:106` (tras el registro de `ICommissionsService`)
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksControllerTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ConfiguracionDeEnlacesTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

`MyLinksControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.Repository.Services.Links;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class MyLinksControllerTests
{
    [Fact]
    public async Task Pide_los_enlaces_del_miembro_del_token()
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        var enlaces = new Mock<IMyLinksService>();
        enlaces.Setup(s => s.GetAsync("AMB-000777", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MyLinksView());

        var respuesta = await new MyLinksController(enlaces.Object, usuario.Object).Get(CancellationToken.None);

        respuesta.Should().BeOfType<OkObjectResult>();
        enlaces.Verify(s => s.GetAsync("AMB-000777", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

`ConfiguracionDeEnlacesTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Links;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El BizCenter no arranca con una plantilla mala (R11). Esta prueba pasa el appsettings.json del
/// repositorio por la misma guarda, para que una plantilla mala no llegue a fusionarse.
/// </summary>
public class ConfiguracionDeEnlacesTests
{
    [Fact]
    public void El_appsettings_del_BizCenter_trae_plantillas_validas()
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(),
            "MLMConquerorGlobalEdition.BizCenter", "appsettings.json");
        var configuracion = new ConfigurationBuilder().AddJsonFile(ruta, optional: false).Build();

        var opciones = configuracion.GetSection(MyLinksOptions.SectionName).Get<MyLinksOptions>();

        opciones.Should().NotBeNull();
        MyLinksOptionsGuard.Error(opciones!).Should().BeNull();
        opciones!.Links.Should().HaveCount(10);
        opciones.Links.Count(l => l.IsPersonal).Should().Be(5);
    }
}
```

`MiembroDelTokenTests.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Regla del Plan J: el miembro sale SIEMPRE del token. Ninguna acción nueva del BizCenter recibe
/// un memberId, ni suelto ni dentro del cuerpo. Los parámetros [FromServices] (el servicio, el
/// usuario actual) no vienen de la petición y no se miran. Cada bloque añade aquí sus acciones.
/// </summary>
public class MiembroDelTokenTests
{
    public static IEnumerable<object[]> Acciones() => new (Type Controlador, string Accion)[]
    {
        (typeof(MyLinksController), nameof(MyLinksController.Get)),
        // (acciones de los bloques siguientes)
    }.Select(a => new object[] { a.Controlador, a.Accion });

    [Theory]
    [MemberData(nameof(Acciones))]
    public void Ninguna_accion_nueva_recibe_un_memberId(Type controlador, string accion)
    {
        var metodo = controlador.GetMethod(accion, BindingFlags.Public | BindingFlags.Instance);
        metodo.Should().NotBeNull($"{controlador.Name}.{accion} tiene que existir");

        foreach (var parametro in metodo!.GetParameters()
                     .Where(p => p.GetCustomAttribute<FromServicesAttribute>() is null))
        {
            parametro.Name!.Should().NotContainEquivalentOf("memberId",
                $"{controlador.Name}.{accion} saca el miembro del token");
            parametro.ParameterType.GetProperty("MemberId").Should().BeNull(
                $"el cuerpo de {controlador.Name}.{accion} no puede traer un MemberId");
        }
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "MyLinksControllerTests|ConfiguracionDeEnlacesTests|MiembroDelTokenTests" -nologo`
Expected: FAIL de compilación (`MyLinksController` no existe).

- [ ] **Step 3: Crear `MyLinksController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Links;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>"Mis enlaces" del miembro que llama. El miembro sale del token: no hay sujeto en la URL.</summary>
[ApiController]
[Route("api/v1/bizcenter/my-links")]
[Authorize]
public class MyLinksController : ControllerBase
{
    private readonly IMyLinksService     _links;
    private readonly ICurrentUserService _currentUser;

    public MyLinksController(IMyLinksService links, ICurrentUserService currentUser)
    {
        _links       = links;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/v1/bizcenter/my-links</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<MyLinksView>.Ok(await _links.GetAsync(_currentUser.MemberId, ct)));
}
```

- [ ] **Step 4: Añadir las plantillas por defecto a `MLMConquerorGlobalEdition.BizCenter/appsettings.json`**

Sustituir:

```json
  "Logging": {
```

por:

```json
  "MyLinks": {
    "Links": [
      { "Key": "mwrlife",         "Label": "MWR Life",                 "Url": "https://mwrlife.com/{slug}" },
      { "Key": "ta-join",         "Label": "Travel Advantage - Join",  "Url": "https://join.traveladvantage.com/{slug}" },
      { "Key": "ta-vip",          "Label": "Travel Advantage - VIP",   "Url": "https://vip.traveladvantage.com/{slug}" },
      { "Key": "ta-free",         "Label": "Travel Advantage - Free",  "Url": "https://free.traveladvantage.com/{slug}" },
      { "Key": "lifeexperience",  "Label": "Life Experience Club",     "Url": "https://lifeexperience.club/{slug}" },
      { "Key": "mwracademy",      "Label": "MWR Academy",              "Url": "https://mwracademy.com" },
      { "Key": "mwrlife-online",  "Label": "MWR Life Online",          "Url": "https://mwrlife.online" },
      { "Key": "mwrevents",       "Label": "MWR Events",               "Url": "https://mwrevents.com" },
      { "Key": "traveladvantage", "Label": "Travel Advantage",         "Url": "https://traveladvantage.com" },
      { "Key": "mwrlifespace",    "Label": "MWR Life Space",           "Url": "https://mwrlifespace.com" }
    ]
  },
  "Logging": {
```

(Los diez del legado con https; cuáles siguen vivos es la pregunta P1.)

- [ ] **Step 5: Registrar el servicio en `MLMConquerorGlobalEdition.BizCenter/Program.cs`**

Sustituir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.CommissionsService>();
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.CommissionsService>();

// "Mis enlaces" (Plan J): plantillas de appsettings validadas al arrancar. Con una plantilla
// inválida el BizCenter no arranca (R11). Sin sección no hay enlaces, y arranca.
MLMConquerorGlobalEdition.Repository.Services.Links.MyLinksServiceCollectionExtensions.AddMyLinks(
    builder.Services,
    builder.Configuration
        .GetSection(MLMConquerorGlobalEdition.Repository.Services.Links.MyLinksOptions.SectionName)
        .Get<MLMConquerorGlobalEdition.Repository.Services.Links.MyLinksOptions>()
    ?? new MLMConquerorGlobalEdition.Repository.Services.Links.MyLinksOptions());
```

- [ ] **Step 6: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "MyLinksControllerTests|ConfiguracionDeEnlacesTests|MiembroDelTokenTests" -nologo`
Expected: PASS (3 pruebas).

- [ ] **Step 7: Compilar el BizCenter**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenter -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Controllers/MyLinksController.cs \
        MLMConquerorGlobalEdition.BizCenter/appsettings.json \
        MLMConquerorGlobalEdition.BizCenter/Program.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksControllerTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ConfiguracionDeEnlacesTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs
git commit -m "Mis enlaces: GET api/v1/bizcenter/my-links con el miembro del token, las diez plantillas del legado en https en el appsettings del BizCenter y su registro; pruebas de que la configuracion del repositorio es valida y de que ninguna accion nueva recibe un memberId"
```

---

### Task 5: Tarjeta `MyLinksCard`

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Profile/MyLinksCard.razor`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksCardTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Profile;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class MyLinksCardTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(f(r));
    }

    private const string ConSlug = """{"success":true,"data":{"slugMissing":false,"personal":[{"key":"mwrlife","label":"MWR Life","url":"https://mwrlife.com/ana"}],"corporate":[{"key":"academy","label":"MWR Academy","url":"https://mwracademy.com"}]}}""";
    private const string SinSlug = """{"success":true,"data":{"slugMissing":true,"personal":[],"corporate":[{"key":"academy","label":"MWR Academy","url":"https://mwracademy.com"}]}}""";

    private readonly List<string> _rutas = new();

    private IRenderedComponent<MyLinksCard> Montar(string json, HttpStatusCode estado = HttpStatusCode.OK,
        string api = "api/v1/bizcenter")
    {
        Services.AddSingleton(new HttpClient(new Manejador(r =>
        {
            _rutas.Add(r.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<MyLinksCard>(p => p.Add(c => c.ApiBaseUrl, api));
    }

    [Fact]
    public void Pinta_los_enlaces_personales_y_los_corporativos()
    {
        var tarjeta = Montar(ConSlug);

        tarjeta.WaitForAssertion(() => tarjeta.FindAll(".my-link").Should().HaveCount(2));
        tarjeta.Markup.Should().Contain("https://mwrlife.com/ana").And.Contain("https://mwracademy.com");
        tarjeta.FindAll(".my-links-noslug").Should().BeEmpty();
    }

    [Fact]
    public void Sin_slug_avisa_con_enlace_al_perfil_y_no_pinta_personales()
    {
        var tarjeta = Montar(SinSlug);

        tarjeta.WaitForAssertion(() => tarjeta.Find(".my-links-noslug"));
        tarjeta.Find(".my-links-noslug a").GetAttribute("href").Should().Be("/profile");
        tarjeta.FindAll(".my-link").Should().ContainSingle();
    }

    [Fact]
    public void Copiar_manda_la_url_al_portapapeles_y_lo_dice()
    {
        var tarjeta = Montar(ConSlug);
        tarjeta.WaitForAssertion(() => tarjeta.FindAll(".my-link-copy").Should().HaveCount(2));

        tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy").Click();

        JSInterop.VerifyInvoke("navigator.clipboard.writeText").Arguments[0].Should().Be("https://mwrlife.com/ana");
        tarjeta.Find(".my-link[data-key='mwrlife'] .my-link-copy").TextContent.Trim().Should().Be("MyLinks.Copied");
    }

    [Fact]
    public void Un_error_de_la_api_se_ve()
    {
        var tarjeta = Montar("""{"success":false}""", HttpStatusCode.InternalServerError);

        tarjeta.WaitForAssertion(() => tarjeta.Find(".my-links-error").TextContent.Should().Contain("MyLinks.LoadFailed"));
    }

    [Fact]
    public void Usa_la_ruta_base_que_recibe()
    {
        var tarjeta = Montar(ConSlug, api: "api/v1/admin/members/AMB-1");

        tarjeta.WaitForAssertion(() => _rutas.Should().Contain("/api/v1/admin/members/AMB-1/my-links"));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter MyLinksCardTests -nologo`
Expected: FAIL de compilación (`MyLinksCard` no existe).

- [ ] **Step 3: Crear `MyLinksCard.razor`**

```razor
@inject HttpClient Http
@inject IJSRuntime JS
@inject IStringLocalizer<SharedResources> L
@using System.Net.Http.Json
@using Microsoft.Extensions.Localization
@using Microsoft.JSInterop
@using MLMConquerorGlobalEdition.SharedComponents.Resources

@*
    "Mis enlaces" (Plan J). Componente único: la ruta del API entra por parámetro, así sirve al
    BizCenter y a cualquier portal que exponga el mismo endpoint. Los enlaces los construye el
    servidor con el slug del miembro del token; aquí solo se pintan y se copian.
*@

<div class="card bg-white border-0 rounded-3 mb-4 my-links">
    <div class="card-body p-4">
        <h5 class="fw-semibold mb-3"><i class="ri-links-line me-2 text-primary"></i>@L["Sidebar.MyLinks"]</h5>

        @if (_loading)
        {
            <LoadingSpinner IsLoading="true" />
        }
        else if (_error || _data is null)
        {
            <div class="alert alert-danger my-links-error">@L["MyLinks.LoadFailed"]</div>
        }
        else
        {
            <h6 class="fw-semibold mt-2">@L["MyLinks.Personal"]</h6>
            @if (_data.SlugMissing)
            {
                <div class="alert alert-warning my-links-noslug">
                    @L["MyLinks.NoSlug"]
                    <a href="@ProfileUrl" class="alert-link ms-1">@L["MyLinks.GoToProfile"]</a>
                </div>
            }
            else
            {
                @RenderList(_data.Personal)
            }

            <h6 class="fw-semibold mt-4">@L["MyLinks.Corporate"]</h6>
            @RenderList(_data.Corporate)
        }
    </div>
</div>

@code {
    /// <summary>Raíz del API; el componente llama a <c>{ApiBaseUrl}/my-links</c>.</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = "api/v1/bizcenter";

    /// <summary>Dónde se configura el slug (la tarjeta WebsiteSlugCard del perfil).</summary>
    [Parameter] public string ProfileUrl { get; set; } = "/profile";

    private bool      _loading = true;
    private bool      _error;
    private LinksDto? _data;
    private string?   _copiedKey;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiWrapper<LinksDto>>($"{ApiBaseUrl}/my-links");
            if (resp?.Success == true && resp.Data is not null) _data = resp.Data;
            else _error = true;
        }
        catch (Exception)
        {
            _error = true;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CopyAsync(LinkDto link)
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", link.Url);
            _copiedKey = link.Key;
        }
        catch (JSException)
        {
            _copiedKey = null;
        }
    }

    private RenderFragment RenderList(List<LinkDto> links) => __builder =>
    {
        <ul class="list-group list-group-flush">
            @foreach (var link in links)
            {
                <li class="list-group-item d-flex align-items-center gap-2 px-0 my-link" data-key="@link.Key">
                    <div class="flex-grow-1 text-truncate">
                        <div class="fw-medium">@link.Label</div>
                        <a href="@link.Url" target="_blank" rel="noopener noreferrer" class="small text-break">@link.Url</a>
                    </div>
                    <button type="button" class="btn btn-outline-primary btn-sm my-link-copy"
                            aria-label="@($"{L["MyLinks.Copy"]} {link.Label}")"
                            @onclick="() => CopyAsync(link)">
                        @(_copiedKey == link.Key ? L["MyLinks.Copied"] : L["MyLinks.Copy"])
                    </button>
                </li>
            }
        </ul>
    };

    private sealed class ApiWrapper<T> { public bool Success { get; set; } public T? Data { get; set; } }

    private sealed class LinksDto
    {
        public bool          SlugMissing { get; set; }
        public List<LinkDto> Personal    { get; set; } = new();
        public List<LinkDto> Corporate   { get; set; } = new();
    }

    private sealed class LinkDto
    {
        public string Key   { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Url   { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter MyLinksCardTests -nologo`
Expected: PASS (5 pruebas).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Profile/MyLinksCard.razor \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MyLinksCardTests.cs
git commit -m "Mis enlaces: tarjeta compartida con enlaces personales y corporativos, boton de copiar al portapapeles, aviso con enlace al perfil si falta el slug y ruta del API por parametro"
```

---

### Task 6: Página, menú y textos de *Mis enlaces*

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/MyLinks.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor:123-129` (tras la entrada de *Marketing*)
- Modify: los nueve `MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources*.resx`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Xml.Linq;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Las claves del Plan J existen y no están vacías en los nueve idiomas. Cada bloque añade las
/// suyas a <see cref="Claves"/>.
/// </summary>
public class TextosDeReportesTests
{
    private static readonly string[] Culturas = { "", ".es", ".pt", ".fr", ".it", ".de", ".ko", ".zh", ".ka" };

    public static readonly string[] Claves =
    {
        // Bloque 1 — Mis enlaces
        "Sidebar.MyLinks", "PageTitle.MyLinks", "MyLinks.Personal", "MyLinks.Corporate",
        "MyLinks.Copy", "MyLinks.Copied", "MyLinks.NoSlug", "MyLinks.GoToProfile", "MyLinks.LoadFailed",
        // (claves de los bloques siguientes)
    };

    public static IEnumerable<object[]> Archivos() => Culturas.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Cada_idioma_tiene_las_claves_del_plan_J_y_no_estan_vacias(string cultura)
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(), "MLMConquerorGlobalEdition.SharedComponents",
            "Resources", $"SharedResources{cultura}.resx");
        var datos = XDocument.Load(ruta).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

        Claves.Where(k => !datos.ContainsKey(k)).Should().BeEmpty($"SharedResources{cultura}.resx tiene que traerlas");
        Claves.Where(k => datos.TryGetValue(k, out var v) && string.IsNullOrWhiteSpace(v)).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: FAIL (9 casos: faltan las claves de *Mis enlaces*).

- [ ] **Step 3: Añadir las claves a los nueve `.resx`**

En cada archivo, justo antes de `</root>`, una línea por clave con la forma `<data name="CLAVE" xml:space="preserve"><value>TEXTO</value></data>`:

| Clave | `SharedResources.resx` (en) | `.es` | `.pt` | `.fr` | `.it` | `.de` | `.ko` | `.zh` | `.ka` |
|---|---|---|---|---|---|---|---|---|---|
| `Sidebar.MyLinks` | My Links | Mis enlaces | Meus links | Mes liens | I miei link | Meine Links | 내 링크 | 我的链接 | ჩემი ბმულები |
| `PageTitle.MyLinks` | My Links | Mis enlaces | Meus links | Mes liens | I miei link | Meine Links | 내 링크 | 我的链接 | ჩემი ბმულები |
| `MyLinks.Personal` | Your personal links | Tus enlaces personales | Seus links pessoais | Vos liens personnels | I tuoi link personali | Ihre persönlichen Links | 내 개인 링크 | 您的个人链接 | თქვენი პირადი ბმულები |
| `MyLinks.Corporate` | Corporate sites | Sitios corporativos | Sites corporativos | Sites de l'entreprise | Siti aziendali | Unternehmensseiten | 회사 사이트 | 公司网站 | კორპორატიული საიტები |
| `MyLinks.Copy` | Copy | Copiar | Copiar | Copier | Copia | Kopieren | 복사 | 复制 | კოპირება |
| `MyLinks.Copied` | Copied | Copiado | Copiado | Copié | Copiato | Kopiert | 복사됨 | 已复制 | დაკოპირდა |
| `MyLinks.NoSlug` | Set up your website name in your profile to get your personal links. | Configura el nombre de tu sitio web en tu perfil para obtener tus enlaces personales. | Configure o nome do seu site no seu perfil para obter seus links pessoais. | Configurez le nom de votre site web dans votre profil pour obtenir vos liens personnels. | Configura il nome del tuo sito web nel profilo per ottenere i tuoi link personali. | Legen Sie in Ihrem Profil den Namen Ihrer Website fest, um Ihre persönlichen Links zu erhalten. | 개인 링크를 받으려면 프로필에서 웹사이트 이름을 설정하세요. | 请在个人资料中设置您的网站名称以获取个人链接。 | პირადი ბმულების მისაღებად მიუთითეთ თქვენი ვებსაიტის სახელი პროფილში. |
| `MyLinks.GoToProfile` | Go to my profile | Ir a mi perfil | Ir para meu perfil | Aller à mon profil | Vai al mio profilo | Zu meinem Profil | 내 프로필로 이동 | 前往我的资料 | ჩემს პროფილზე გადასვლა |
| `MyLinks.LoadFailed` | We couldn't load your links. Please try again. | No pudimos cargar tus enlaces. Inténtalo de nuevo. | Não foi possível carregar seus links. Tente novamente. | Impossible de charger vos liens. Veuillez réessayer. | Impossibile caricare i tuoi link. Riprova. | Ihre Links konnten nicht geladen werden. Bitte versuchen Sie es erneut. | 링크를 불러오지 못했습니다. 다시 시도해 주세요. | 无法加载您的链接，请重试。 | ბმულების ჩატვირთვა ვერ მოხერხდა. სცადეთ ხელახლა. |

Ejemplo de las dos primeras líneas en `SharedResources.es.resx`:

```xml
  <data name="Sidebar.MyLinks" xml:space="preserve"><value>Mis enlaces</value></data>
  <data name="PageTitle.MyLinks" xml:space="preserve"><value>Mis enlaces</value></data>
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: PASS (9 casos).

- [ ] **Step 5: Crear la página `MyLinks.razor`**

```razor
@page "/my-links"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject Microsoft.Extensions.Localization.IStringLocalizer<MLMConquerorGlobalEdition.SharedComponents.Resources.SharedResources> L

<PageTitle>@L["PageTitle.MyLinks"]</PageTitle>

<MyLinksCard />
```

- [ ] **Step 6: Añadir la entrada al menú en `SidebarMenu.razor`**

Sustituir:

```razor
        <!-- Marketing -->
        <li class="menu-item @IsActive("/marketing")">
            <a href="/marketing" class="menu-link">
                <span class="material-symbols-outlined menu-icon">campaign</span>
                <span class="title">@L["Sidebar.Marketing"]</span>
            </a>
        </li>
```

por:

```razor
        <!-- Marketing -->
        <li class="menu-item @IsActive("/marketing")">
            <a href="/marketing" class="menu-link">
                <span class="material-symbols-outlined menu-icon">campaign</span>
                <span class="title">@L["Sidebar.Marketing"]</span>
            </a>
        </li>

        <!-- Mis enlaces (Plan J) -->
        <li class="menu-item @IsActive("/my-links")">
            <a href="/my-links" class="menu-link">
                <span class="material-symbols-outlined menu-icon">link</span>
                <span class="title">@L["Sidebar.MyLinks"]</span>
            </a>
        </li>
```

- [ ] **Step 7: Compilar la web**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenterWeb -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/MyLinks.razor \
        MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs
git commit -m "Mis enlaces: pagina /my-links que monta la tarjeta compartida, entrada en el menu tras Marketing y textos en los 9 idiomas, con prueba de que el plan J no deja claves sin traducir"
```

**Fin del Bloque 1.** Revisable y fusionable: no depende de nada posterior y no toca la base.

---

# Bloque 2 — Residuales diarios

### Task 7: El menú abre *Dual Team* en `/team/residuals`

`IsDualTeamActive()` busca `/team/dual` (`SidebarMenu.razor:198`), así que la página de residuales (`/team/residuals`) deja el grupo cerrado. La decisión sale a una clase que se puede probar.

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarRoutes.cs`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor:195-202`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SidebarRoutesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.SharedComponents.Components.Layout;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class SidebarRoutesTests
{
    [Theory]
    [InlineData("https://bc.test/team/residuals")]
    [InlineData("https://bc.test/team/dual-myteam")]
    [InlineData("https://bc.test/team/dual-tree")]
    [InlineData("https://bc.test/team/dual-changes")]
    public void Las_paginas_del_dual_team_abren_su_grupo(string uri)
        => SidebarRoutes.IsDualTeam(uri).Should().BeTrue();

    [Theory]
    [InlineData("https://bc.test/team/enrollment/my-team")]
    [InlineData("https://bc.test/team/enrollment/customers")]
    [InlineData("https://bc.test/team/enrollment/customer-changes")]
    [InlineData("https://bc.test/team/enrollment/activity")]
    public void Las_paginas_de_inscripcion_abren_su_grupo_y_no_el_dual(string uri)
    {
        SidebarRoutes.IsEnrollmentTeam(uri).Should().BeTrue();
        SidebarRoutes.IsDualTeam(uri).Should().BeFalse();
    }

    [Fact]
    public void Residuales_no_abre_inscripcion_ni_comisiones()
    {
        SidebarRoutes.IsEnrollmentTeam("https://bc.test/team/residuals").Should().BeFalse();
        SidebarRoutes.IsCommissions("https://bc.test/team/residuals").Should().BeFalse();
    }

    [Fact]
    public void Comisiones_abre_su_grupo()
        => SidebarRoutes.IsCommissions("https://bc.test/commissions/boost").Should().BeTrue();
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SidebarRoutesTests -nologo`
Expected: FAIL de compilación (`SidebarRoutes` no existe).

- [ ] **Step 3: Crear `SidebarRoutes.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SharedComponents.Components.Layout;

/// <summary>
/// Qué grupo del menú lateral se abre para una URL. Vive fuera del .razor para poder probarlo:
/// el grupo Dual Team buscaba solo "/team/dual" y la página de residuales (/team/residuals)
/// lo dejaba cerrado.
/// </summary>
public static class SidebarRoutes
{
    public static bool IsEnrollmentTeam(string uri) => Has(uri, "/team/enrollment");

    public static bool IsDualTeam(string uri) => Has(uri, "/team/dual") || Has(uri, "/team/residuals");

    public static bool IsCommissions(string uri) => Has(uri, "/commissions");

    private static bool Has(string uri, string fragment) =>
        uri.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Usarla en `SidebarMenu.razor`**

Sustituir:

```csharp
    private bool IsEnrollmentActive()
        => Nav.Uri.Contains("/team/enrollment", StringComparison.OrdinalIgnoreCase);

    private bool IsDualTeamActive()
        => Nav.Uri.Contains("/team/dual", StringComparison.OrdinalIgnoreCase);

    private bool IsCommissionsActive()
        => Nav.Uri.Contains("/commissions", StringComparison.OrdinalIgnoreCase);
```

por:

```csharp
    private bool IsEnrollmentActive() => SidebarRoutes.IsEnrollmentTeam(Nav.Uri);

    private bool IsDualTeamActive() => SidebarRoutes.IsDualTeam(Nav.Uri);

    private bool IsCommissionsActive() => SidebarRoutes.IsCommissions(Nav.Uri);
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SidebarRoutesTests -nologo`
Expected: PASS (10 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarRoutes.cs \
        MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SidebarRoutesTests.cs
git commit -m "Menu lateral: el grupo Dual Team se abre tambien en /team/residuals; que grupo se abre lo decide SidebarRoutes, con pruebas"
```

---

### Task 8: Índice `DailyResidualEarnings(BeneficiaryMemberId, EarnedDate)`

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/Commission/DailyResidualEarningConfiguration.cs:35-37`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceResidualesPorBeneficiarioYFecha.cs` (vía `dotnet ef`)
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Los índices que piden las consultas del Plan J están declarados en el modelo. EF InMemory no los
/// usa; que existan en la base lo comprueban las pruebas [SqlServerFact] de cada bloque.
/// </summary>
public class IndicesDeReportesTests
{
    private static bool TieneIndice(Type entidad, params string[] columnas)
    {
        using var db = ReportesDb.Nueva();
        return db.Model.FindEntityType(entidad)!.GetIndexes()
            .Any(i => i.Properties.Select(p => p.Name).SequenceEqual(columnas));
    }

    [Fact]
    public void Residuales_diarios_por_beneficiario_y_fecha()
        => TieneIndice(typeof(DailyResidualEarning), "BeneficiaryMemberId", "EarnedDate").Should().BeTrue();

    // (índices de los bloques siguientes)
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: FAIL (`Expected ... to be true, but found False`).

- [ ] **Step 3: Declarar el índice**

En `DailyResidualEarningConfiguration.cs`, sustituir:

```csharp
        // Spec: indexes on BeneficiaryMemberId + Status, and EarnedDate
        builder.HasIndex(x => new { x.BeneficiaryMemberId, x.Status });
        builder.HasIndex(x => x.EarnedDate);
```

por:

```csharp
        // Spec: indexes on BeneficiaryMemberId + Status, and EarnedDate
        builder.HasIndex(x => new { x.BeneficiaryMemberId, x.Status });
        builder.HasIndex(x => x.EarnedDate);

        // Plan J: el historial de residuales del BizCenter y su gráfico leen por beneficiario y
        // rango de fechas; con (BeneficiaryMemberId, Status) había que recorrer todas sus filas.
        builder.HasIndex(x => new { x.BeneficiaryMemberId, x.EarnedDate });
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 5: Crear la migración**

Con el comando de la cabecera y el nombre `IndiceResidualesPorBeneficiarioYFecha`. `Up` debe tener solo:

```csharp
migrationBuilder.CreateIndex(
    name: "IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate",
    table: "DailyResidualEarnings",
    columns: new[] { "BeneficiaryMemberId", "EarnedDate" });
```

y `Down`, el `DropIndex` correspondiente.

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceResidualesPorBeneficiarioYFecha.cs`
Expected: `0`

- [ ] **Step 6: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Configurations/Commission/DailyResidualEarningConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceResidualesPorBeneficiarioYFecha.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceResidualesPorBeneficiarioYFecha.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs
git commit -m "Indice de DailyResidualEarnings por beneficiario y fecha de devengo para el historial de residuales del BizCenter; migracion IndiceResidualesPorBeneficiarioYFecha"
```

- [ ] **Step 8: Aplicar la migración a la base local**

Con el comando de la cabecera (`dotnet ef database update … --connection "…MLMConquerorGlobalEditionDb…"`).
Expected: `Applying migration '…_IndiceResidualesPorBeneficiarioYFecha'.` y `Done.`

---

### Task 9: Servicio de residuales diarios

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Commissions/DailyResidualDtos.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Commissions/IDailyResidualReportService.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Commissions/DailyResidualReportService.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/DailyResidualReportServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El historial de residuales lee DailyResidualEarnings (lo que escribe el motor), no
/// CommissionEarnings de la categoría 3, que mezcla débitos y consolidaciones.
/// </summary>
public class DailyResidualReportServiceTests
{
    private const string Yo = "AMB-000001";
    private static readonly RelojFijo Reloj = new(new DateTime(2025, 6, 15, 12, 0, 0));

    internal static DailyResidualEarning Residual(
        string beneficiario, DateTime fecha, decimal importe,
        CommissionEarningStatus estado = CommissionEarningStatus.Pending,
        int? dt = null, int? et = null, DateTime? pagado = null, string? comentario = null) => new()
    {
        BeneficiaryMemberId          = beneficiario,
        EarnedDate                   = fecha,
        Amount                       = importe,
        Status                       = estado,
        EligibleDualTeamPoints       = dt,
        EligibleEnrollmentTeamPoints = et,
        PaymentDate                  = pagado,
        PaymentComment               = comentario,
        CreatedBy                    = "prueba"
    };

    private static DailyResidualReportService Servicio(AppDbContext db) => new(db, Reloj);

    [Fact]
    public async Task Solo_lee_los_residuales_diarios_del_miembro()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m),
            Residual(Yo, new DateTime(2025, 5, 4), 5m),
            Residual("AMB-000002", new DateTime(2025, 5, 4), 50m));
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 999m,
            Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2025, 5, 5), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.TotalCount.Should().Be(2);
        vista.PeriodTotal.Should().Be(15m);
    }

    [Fact]
    public async Task Filtra_por_mes_y_totaliza_pagado_y_pendiente()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m, CommissionEarningStatus.Paid, pagado: new DateTime(2025, 5, 12)),
            Residual(Yo, new DateTime(2025, 5, 4), 5m),
            Residual(Yo, new DateTime(2025, 4, 30), 70m));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.Year.Should().Be(2025);
        vista.Month.Should().Be(5);
        vista.Items.Should().HaveCount(2);
        vista.PeriodPaid.Should().Be(10m);
        vista.PeriodPending.Should().Be(5m);
        vista.PeriodTotal.Should().Be(15m);
    }

    [Fact]
    public async Task Sin_anio_usa_el_del_reloj_de_servidor_y_sin_mes_todo_el_anio()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 1, 2), 1m),
            Residual(Yo, new DateTime(2025, 12, 31), 2m),
            Residual(Yo, new DateTime(2026, 3, 1), 4m));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, null, null, 1, 10);

        vista.Year.Should().Be(2025);
        vista.Month.Should().BeNull();
        vista.PeriodTotal.Should().Be(3m);
    }

    [Fact]
    public async Task Un_anio_fuera_de_los_disponibles_cae_en_el_actual()
    {
        await using var db = ReportesDb.Nueva();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 1990, 13, 1, 10);

        vista.Year.Should().Be(2025);
        vista.Month.Should().BeNull();
    }

    [Fact]
    public async Task Excluye_los_cancelados_de_filas_y_totales()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m),
            Residual(Yo, new DateTime(2025, 5, 4), 99m, CommissionEarningStatus.Cancelled));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.Items.Should().ContainSingle();
        vista.PeriodTotal.Should().Be(10m);
    }

    [Fact]
    public async Task Los_puntos_elegibles_suman_los_dos_ejes_y_son_null_sin_dato()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m, dt: 0, et: 120),
            Residual(Yo, new DateTime(2025, 5, 4), 10m));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.Items.Single(i => i.EarnedDate.Day == 3).EligiblePoints.Should().Be(120);
        vista.Items.Single(i => i.EarnedDate.Day == 4).EligiblePoints.Should().BeNull();
    }

    [Fact]
    public async Task Los_anios_disponibles_van_desde_el_alta_hasta_hoy()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, Yo, alta: new DateTime(2023, 2, 1));
        await db.SaveChangesAsync();

        (await Servicio(db).GetHistoryAsync(Yo, null, null, 1, 10))
            .AvailableYears.Should().Equal(2025, 2024, 2023);
        (await Servicio(db).GetHistoryAsync("AMB-999999", null, null, 1, 10))
            .AvailableYears.Should().Equal(2025);
    }

    [Fact]
    public async Task Pagina_por_fecha_descendente_con_tope_de_cien()
    {
        await using var db = ReportesDb.Nueva();
        for (var d = 1; d <= 25; d++)
            db.DailyResidualEarnings.Add(Residual(Yo, new DateTime(2025, 5, d), 1m));
        await db.SaveChangesAsync();

        var segunda = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 2, 10);
        segunda.Items.Should().HaveCount(10);
        segunda.Items.First().EarnedDate.Day.Should().Be(15);
        segunda.Items.Select(i => i.EarnedDate).Should().BeInDescendingOrder();

        (await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 500)).PageSize.Should().Be(100);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter DailyResidualReportServiceTests -nologo`
Expected: FAIL de compilación (`DailyResidualReportService` no existe).

- [ ] **Step 3: Crear `DailyResidualDtos.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <summary>Una fila de DailyResidualEarnings tal como la ve el miembro.</summary>
public sealed class DailyResidualRowView
{
    public long      Id             { get; set; }
    public DateTime  EarnedDate     { get; set; }
    public decimal   Amount         { get; set; }
    /// <summary>"Paid" o "Pending" (los "Cancelled" no salen).</summary>
    public string    Status         { get; set; } = string.Empty;
    public DateTime? PaymentDate    { get; set; }
    /// <summary>Puntos del eje que calificó (DT o ET; el otro es 0). Null en filas migradas sin dato.</summary>
    public int?      EligiblePoints { get; set; }
    public string?   PaymentComment { get; set; }
}

/// <summary>Una página del historial de un periodo (un mes o un año) y sus totales.</summary>
public sealed class DailyResidualHistoryView
{
    public List<DailyResidualRowView> Items { get; set; } = new();
    public int       TotalCount     { get; set; }
    public int       Page           { get; set; }
    public int       PageSize       { get; set; }
    /// <summary>Año del periodo que se devolvió (el pedido o, si no vale, el del reloj del servidor).</summary>
    public int       Year           { get; set; }
    /// <summary>Mes del periodo; null = el año entero.</summary>
    public int?      Month          { get; set; }
    public decimal   PeriodTotal    { get; set; }
    public decimal   PeriodPaid     { get; set; }
    public decimal   PeriodPending  { get; set; }
    /// <summary>Años que se pueden elegir, del más reciente al más antiguo.</summary>
    public List<int> AvailableYears { get; set; } = new();
}
```

- [ ] **Step 4: Crear `IDailyResidualReportService.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <summary>
/// Historial de residuales diarios (DailyResidualEarnings) de un miembro. Lo comparten BizCenter
/// (con el miembro del token) y Admin (con el miembro de la ruta).
/// </summary>
public interface IDailyResidualReportService
{
    Task<DailyResidualHistoryView> GetHistoryAsync(
        string memberId, int? year, int? month, int page, int pageSize, CancellationToken ct = default);
}
```

- [ ] **Step 5: Crear `DailyResidualReportService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <inheritdoc />
public sealed class DailyResidualReportService : IDailyResidualReportService
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize     = 100;
    public const int MaxYears        = 15;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public DailyResidualReportService(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<DailyResidualHistoryView> GetHistoryAsync(
        string memberId, int? year, int? month, int page, int pageSize, CancellationToken ct = default)
    {
        var now   = _clock.Now;
        var years = await AvailableYearsAsync(memberId, now, ct);

        var y    = year is int requested && years.Contains(requested) ? requested : now.Year;
        int? m   = month is >= 1 and <= 12 ? month : null;
        var from = new DateTime(y, m ?? 1, 1);
        var to   = m is null ? from.AddYears(1) : from.AddMonths(1);

        pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);
        page     = Math.Max(1, page);

        var query = _db.DailyResidualEarnings.AsNoTracking()
            .Where(e => e.BeneficiaryMemberId == memberId
                     && e.EarnedDate >= from
                     && e.EarnedDate <  to
                     && e.Status     != CommissionEarningStatus.Cancelled);

        var sums = await query
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var items = await query
            .OrderByDescending(e => e.EarnedDate).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new DailyResidualRowView
            {
                Id             = e.Id,
                EarnedDate     = e.EarnedDate,
                Amount         = e.Amount,
                Status         = e.Status.ToString(),
                PaymentDate    = e.PaymentDate,
                EligiblePoints = e.EligibleDualTeamPoints == null && e.EligibleEnrollmentTeamPoints == null
                    ? null
                    : (int?)((e.EligibleDualTeamPoints ?? 0) + (e.EligibleEnrollmentTeamPoints ?? 0)),
                PaymentComment = e.PaymentComment
            })
            .ToListAsync(ct);

        var paid    = sums.Where(s => s.Status == CommissionEarningStatus.Paid).Sum(s => s.Amount);
        var pending = sums.Where(s => s.Status == CommissionEarningStatus.Pending).Sum(s => s.Amount);

        return new DailyResidualHistoryView
        {
            Items          = items,
            TotalCount     = sums.Sum(s => s.Count),
            Page           = page,
            PageSize       = pageSize,
            Year           = y,
            Month          = m,
            PeriodPaid     = paid,
            PeriodPending  = pending,
            PeriodTotal    = paid + pending,
            AvailableYears = years
        };
    }

    /// <summary>Del año de alta del miembro (15 como mucho) al año del reloj del servidor, descendente.</summary>
    private async Task<List<int>> AvailableYearsAsync(string memberId, DateTime now, CancellationToken ct)
    {
        var enrolled = await _db.MemberProfiles.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => (DateTime?)p.EnrollDate)
            .FirstOrDefaultAsync(ct);

        var start = Math.Clamp(enrolled?.Year ?? now.Year, now.Year - MaxYears + 1, now.Year);
        return Enumerable.Range(start, now.Year - start + 1).Reverse().ToList();
    }
}
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter DailyResidualReportServiceTests -nologo`
Expected: PASS (8 pruebas).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Commissions/DailyResidualDtos.cs \
        MLMConquerorGlobalEdition.Repository/Services/Commissions/IDailyResidualReportService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Commissions/DailyResidualReportService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/DailyResidualReportServiceTests.cs
git commit -m "Residuales diarios: servicio sobre DailyResidualEarnings por mes o ano con totales pagado y pendiente, sin cancelados, puntos elegibles, anos desde el alta y ano por defecto del reloj del servidor"
```

---

### Task 10: `CommissionsService` con hora de servidor y gráfico sobre `DailyResidualEarnings`

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Commissions/CommissionsService.cs:1-26,32,289-316,338,562,714`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/CommissionHandlersTests.cs` (13 construcciones)
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CommissionsServiceRelojTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// CommissionsService tomaba el "ahora" de DateTime.UtcNow: el año en curso, la semana del bono
/// boost y el mes del bono del coche cambiaban de día a deshora. Ahora sale del reloj del servidor.
/// </summary>
public class CommissionsServiceRelojTests
{
    private const string Yo = "AMB-000001";
    private static readonly RelojFijo Reloj = new(new DateTime(2025, 6, 18, 12, 0, 0)); // miércoles

    [Fact]
    public async Task El_grafico_suma_residuales_diarios_por_mes_con_la_hora_de_servidor()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2025, 5, 3), 10m),
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2025, 5, 20), 5m, CommissionEarningStatus.Paid),
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2025, 6, 7), 7m, CommissionEarningStatus.Cancelled),
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2026, 5, 1), 80m));
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 999m,
            Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2025, 5, 5), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var serie = await new CommissionsService(db, Reloj).GetDualResidualChartAsync(Yo, 3);

        serie.Select(p => (p.Year, p.Month)).Should().Equal((2025, 4), (2025, 5), (2025, 6));
        serie.Select(p => p.Amount).Should().Equal(0m, 15m, 0m);
    }

    [Fact]
    public async Task El_resumen_toma_el_anio_del_reloj()
    {
        await using var db = ReportesDb.Nueva();
        db.CommissionEarnings.AddRange(
            new CommissionEarning { BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 100m, Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2025, 2, 1), CreatedBy = "prueba" },
            new CommissionEarning { BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 50m,  Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2026, 2, 1), CreatedBy = "prueba" });
        await db.SaveChangesAsync();

        var resumen = await new CommissionsService(db, Reloj).GetSummaryAsync(Yo);

        resumen.CurrentYearTotal.Should().Be(100m);
    }

    [Fact]
    public async Task La_semana_del_bono_boost_sale_del_reloj()
    {
        await using var db = ReportesDb.Nueva();

        var semana = await new CommissionsService(db, Reloj).GetBoostBonusWeekStatsAsync(Yo);

        semana.WeekLabel.Should().Be($"Week of {new DateTime(2025, 6, 16):MMM d} - {new DateTime(2025, 6, 22):MMM d}");
    }

    [Fact]
    public async Task El_mes_del_bono_del_coche_sale_del_reloj()
    {
        await using var db = ReportesDb.Nueva();

        var coche = await new CommissionsService(db, Reloj).GetCarBonusStatsAsync(Yo);

        coche.MonthLabel.Should().Be(new DateTime(2025, 6, 18).ToString("MMMM yyyy"));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter CommissionsServiceRelojTests -nologo`
Expected: FAIL de compilación (`CommissionsService` no tiene un constructor de dos argumentos).

- [ ] **Step 3: Constructor con reloj en `CommissionsService.cs`**

Sustituir:

```csharp
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;
```

por:

```csharp
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;
```

y sustituir:

```csharp
    private readonly AppDbContext _db;

    public CommissionsService(AppDbContext db) => _db = db;
```

por:

```csharp
    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    /// <param name="clock">Hora de servidor (Plan J): el "ahora" de todo el servicio sale de aquí.</param>
    public CommissionsService(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }
```

- [ ] **Step 4: Resumen (línea 32)**

Sustituir `        var currentYear = DateTime.UtcNow.Year;` por `        var currentYear = _clock.Now.Year;`

- [ ] **Step 5: Gráfico sobre `DailyResidualEarnings` (líneas 289-316)**

Sustituir:

```csharp
        if (months <= 0) months = 6;

        // Anchor on the first day of the current month (UTC). Build the
        // window backwards so the user always sees the current month as the
        // last bar even when there has been no recent activity.
        var anchor    = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var rangeFrom = anchor.AddMonths(-(months - 1));
        var rangeTo   = anchor.AddMonths(1);    // exclusive upper bound

        var raw = await _db.CommissionEarnings.AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.Status              != CommissionEarningStatus.Cancelled
                     && c.EarnedDate          >= rangeFrom
                     && c.EarnedDate          <  rangeTo)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == DualTeamResidualCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, _) => new { c.EarnedDate, c.Amount })
            .ToListAsync(ct);

        var bucketed = raw
            .GroupBy(r => new { r.EarnedDate.Year, r.EarnedDate.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Sum(x => x.Amount));
```

por:

```csharp
        if (months <= 0) months = 6;
        months = Math.Min(months, 24);

        // Plan J: el gráfico lee DailyResidualEarnings, que es lo que escribe el motor de residuales.
        // CommissionEarnings de la categoría 3 mezcla débitos negativos y consolidaciones.
        // Ancla en el día 1 del mes en curso según la hora del SERVIDOR; la ventana va hacia atrás
        // para que el último punto sea siempre el mes en curso, aunque no haya actividad.
        var now       = _clock.Now;
        var anchor    = new DateTime(now.Year, now.Month, 1);
        var rangeFrom = anchor.AddMonths(-(months - 1));
        var rangeTo   = anchor.AddMonths(1);    // exclusive upper bound

        var raw = await _db.DailyResidualEarnings.AsNoTracking()
            .Where(e => e.BeneficiaryMemberId == memberId
                     && e.Status              != CommissionEarningStatus.Cancelled
                     && e.EarnedDate          >= rangeFrom
                     && e.EarnedDate          <  rangeTo)
            .GroupBy(e => new { e.EarnedDate.Year, e.EarnedDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var bucketed = raw.ToDictionary(r => (r.Year, r.Month), r => r.Amount);
```

(El bucle que construye `series` no cambia.)

- [ ] **Step 6: Las otras tres horas**

- Línea 338: sustituir `        var now = DateTime.Now;` por `        var now = _clock.Now;`
- Línea 562: sustituir `        var today      = DateTime.UtcNow;` por `        var today      = _clock.Now;`
- Línea 714: sustituir `        var today = DateTime.UtcNow;` por `        var today = _clock.Now;`

Comprobar que no queda ninguna:

Run: `grep -n "DateTime.UtcNow\|DateTime.Now" MLMConquerorGlobalEdition.Repository/Services/Commissions/CommissionsService.cs`
Expected: sin salida.

- [ ] **Step 7: Actualizar `CommissionHandlersTests.cs`**

Añadir a los `using` del archivo:

```csharp
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
```

y sustituir **todas** las apariciones (13) de `new CommissionsService(_db)` por `new CommissionsService(_db, new RelojFijo(DateTime.Now))`.

Run: `grep -c "new CommissionsService(_db)" MLMConquerorGlobalEdition.BizCenter.Tests/CommissionHandlersTests.cs`
Expected: `0`

- [ ] **Step 8: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "CommissionsServiceRelojTests|CommissionHandlersTests" -nologo`
Expected: PASS (todas).

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Commissions/CommissionsService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/CommissionHandlersTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CommissionsServiceRelojTests.cs
git commit -m "CommissionsService toma la hora del servidor de IDateTimeProvider (resumen anual, bono FSB, semana del boost y mes del bono del coche) y el grafico de residuales suma DailyResidualEarnings por mes, con tope de 24 meses"
```

---

### Task 11: Endpoint `dual-residual/daily` en BizCenter y Admin

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter/Controllers/CommissionsController.cs:152` (tras `GetDualResidualChart`)
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (registro)
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCommissionsController.cs:102` (tras `GetDualResidualChart`)
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs:85` (registro)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ResidualesDiariosEndpointTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class ResidualesDiariosEndpointTests
{
    [Fact]
    public async Task El_BizCenter_pide_el_historial_del_miembro_del_token()
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        var servicio = new Mock<IDailyResidualReportService>();
        servicio.Setup(s => s.GetHistoryAsync("AMB-000777", 2026, 3, 2, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DailyResidualHistoryView());
        var control = new CommissionsController(new Mock<IMediator>().Object, new Mock<IDownlineGuard>().Object);

        var respuesta = await control.GetDailyResiduals(servicio.Object, usuario.Object, 2026, 3, 2, 10, CancellationToken.None);

        respuesta.Should().BeOfType<OkObjectResult>();
        servicio.Verify(s => s.GetHistoryAsync("AMB-000777", 2026, 3, 2, 10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

Y en `MiembroDelTokenTests.cs`, sustituir:

```csharp
        (typeof(MyLinksController), nameof(MyLinksController.Get)),
        // (acciones de los bloques siguientes)
```

por:

```csharp
        (typeof(MyLinksController), nameof(MyLinksController.Get)),
        (typeof(CommissionsController), nameof(CommissionsController.GetDailyResiduals)),
        // (acciones de los bloques siguientes)
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "ResidualesDiariosEndpointTests|MiembroDelTokenTests" -nologo`
Expected: FAIL de compilación (`GetDailyResiduals` no existe).

- [ ] **Step 3: Acción en el BizCenter**

En `BizCenter/Controllers/CommissionsController.cs`, justo después del cierre de `GetDualResidualChart` (la línea `    }` que sigue a `return Ok(ApiResponse<List<…MonthlyAmountView>>.Ok(data));`), añadir:

```csharp

    /// <summary>
    /// GET /api/v1/bizcenter/commissions/dual-residual/daily?year=&amp;month=&amp;page=&amp;pageSize= —
    /// historial de residuales diarios (DailyResidualEarnings) del miembro del token, con totales
    /// del periodo. Sin año, el del reloj del servidor; sin mes, el año entero.
    /// </summary>
    [HttpGet("dual-residual/daily")]
    public async Task<IActionResult> GetDailyResiduals(
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Commissions.IDailyResidualReportService service,
        [FromServices] ICurrentUserService currentUser,
        [FromQuery] int? year     = null,
        [FromQuery] int? month    = null,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 10,
        CancellationToken ct = default)
    {
        var data = await service.GetHistoryAsync(currentUser.MemberId, year, month, page, pageSize, ct);
        return Ok(ApiResponse<MLMConquerorGlobalEdition.Repository.Services.Commissions.DailyResidualHistoryView>.Ok(data));
    }
```

(`ICurrentUserService` es el de `BizCenter.Services`, que el archivo ya importa.)

- [ ] **Step 4: Registro en el BizCenter**

En `BizCenter/Program.cs`, sustituir:

```csharp
// "Mis enlaces" (Plan J): plantillas de appsettings validadas al arrancar. Con una plantilla
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.IDailyResidualReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.DailyResidualReportService>();

// "Mis enlaces" (Plan J): plantillas de appsettings validadas al arrancar. Con una plantilla
```

- [ ] **Step 5: Acción y registro en el Admin**

En `AdminAPI/Controllers/AdminMemberCommissionsController.cs`, sustituir:

```csharp
    // ─── Fast Start Bonus ─────────────────────────────────────────────────
```

por:

```csharp
    /// <summary>GET /api/v1/admin/members/{memberId}/commissions/dual-residual/daily — el mismo
    /// historial que ve el miembro (DailyResidualEarnings), para la pestaña de residuales del perfil.</summary>
    [HttpGet("dual-residual/daily")]
    public async Task<IActionResult> GetDailyResiduals(
        string memberId,
        [FromServices] IDailyResidualReportService residuals,
        [FromQuery] int? year     = null,
        [FromQuery] int? month    = null,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 10,
        CancellationToken ct = default)
        => Ok(ApiResponse<DailyResidualHistoryView>.Ok(
            await residuals.GetHistoryAsync(memberId, year, month, page, pageSize, ct)));

    // ─── Fast Start Bonus ─────────────────────────────────────────────────
```

En `AdminAPI/Program.cs`, sustituir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.CommissionsService>();
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.CommissionsService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.IDailyResidualReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.DailyResidualReportService>();
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "ResidualesDiariosEndpointTests|MiembroDelTokenTests" -nologo`
Expected: PASS.

- [ ] **Step 7: Compilar los dos anfitriones**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenter -v q -nologo` y `dotnet build MLMConquerorGlobalEdition.AdminAPI -v q -nologo`
Expected: `0 Error(s)` en los dos.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Controllers/CommissionsController.cs \
        MLMConquerorGlobalEdition.BizCenter/Program.cs \
        MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCommissionsController.cs \
        MLMConquerorGlobalEdition.AdminAPI/Program.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ResidualesDiariosEndpointTests.cs
git commit -m "Residuales diarios: GET commissions/dual-residual/daily en el BizCenter con el miembro del token y en el perfil de miembro del Admin, mismo servicio en los dos"
```

---

### Task 12: Componente `DailyResidualHistory`

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Commissions/DailyResidualHistory.razor`
- Modify: los nueve `SharedResources*.resx`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/DailyResidualHistoryTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Commissions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class DailyResidualHistoryTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(f(r));
    }

    private const string Mayo = """
        {"success":true,"data":{"items":[
          {"id":2,"earnedDate":"2025-05-04T00:00:00","amount":5,"status":"Pending","paymentDate":null,"eligiblePoints":null,"paymentComment":null},
          {"id":1,"earnedDate":"2025-05-03T00:00:00","amount":10,"status":"Paid","paymentDate":"2025-05-12T00:00:00","eligiblePoints":120,"paymentComment":"Consolidated"}],
          "totalCount":2,"page":1,"pageSize":10,"year":2025,"month":null,
          "periodTotal":15,"periodPaid":10,"periodPending":5,"availableYears":[2025,2024]}}
        """;

    private readonly List<Uri> _peticiones = new();

    private IRenderedComponent<DailyResidualHistory> Montar(string json, HttpStatusCode estado = HttpStatusCode.OK)
    {
        Services.AddSingleton(new HttpClient(new Manejador(r =>
        {
            _peticiones.Add(r.RequestUri!);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        return Render<DailyResidualHistory>(p => p.Add(c => c.ApiBaseUrl, "api/v1/bizcenter"));
    }

    [Fact]
    public void Pinta_filas_y_totales_del_periodo()
    {
        var c = Montar(Mayo);

        c.WaitForAssertion(() => c.FindAll(".dr-row").Should().HaveCount(2));
        c.Find(".dr-total").TextContent.Should().Be("$15.00");
        c.Find(".dr-paid").TextContent.Should().Be("$10.00");
        c.Find(".dr-pending").TextContent.Should().Be("$5.00");
        c.Markup.Should().Contain("Consolidated").And.Contain("120");
    }

    [Fact]
    public void Pide_el_endpoint_diario_y_no_el_de_comisiones()
    {
        var c = Montar(Mayo);

        c.WaitForAssertion(() => _peticiones.Should().NotBeEmpty());
        _peticiones[0].AbsolutePath.Should().Be("/api/v1/bizcenter/commissions/dual-residual/daily");
    }

    [Fact]
    public void Los_anios_son_los_que_da_el_servidor_y_cambiar_de_mes_vuelve_a_pedir()
    {
        var c = Montar(Mayo);
        c.WaitForAssertion(() => c.FindAll("#dr-year option").Select(o => o.TextContent).Should().Equal("2025", "2024"));

        c.Find("#dr-month").Change("5");

        c.WaitForAssertion(() => _peticiones.Last().Query.Should().Contain("year=2025").And.Contain("month=5"));
    }

    [Fact]
    public void Un_error_de_la_api_se_ve()
    {
        var c = Montar("""{"success":false}""", HttpStatusCode.InternalServerError);

        c.WaitForAssertion(() => c.Find(".daily-residuals-error").TextContent.Should().Contain("Team.LoadFailedResiduals"));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter DailyResidualHistoryTests -nologo`
Expected: FAIL de compilación (`DailyResidualHistory` no existe).

- [ ] **Step 3: Crear `DailyResidualHistory.razor`**

```razor
@inject HttpClient Http
@inject IStringLocalizer<SharedResources> L
@using System.Globalization
@using System.Net.Http.Json
@using Microsoft.Extensions.Localization
@using MLMConquerorGlobalEdition.SharedComponents.Resources

@*
    Historial de residuales diarios (Plan J). Lee {ApiBaseUrl}/commissions/dual-residual/daily,
    que sirve DailyResidualEarnings: en el BizCenter con el miembro del token y en el Admin con el
    de la ruta. Año por defecto y años disponibles los da el servidor (hora de servidor).
*@

<div class="card bg-white border-0 rounded-3 mb-4 daily-residuals">
    <div class="card-body p-0">
        <div class="d-flex justify-content-between align-items-end flex-wrap gap-2 p-4">
            <h5 class="fw-semibold mb-0">@L["Sidebar.Residuals"]</h5>
            <div class="d-flex gap-2 flex-wrap">
                <div>
                    <label class="form-label fs-12 mb-1 fw-medium" for="dr-year">@L["Common.Year"]</label>
                    <select id="dr-year" class="form-select form-select-sm" value="@_year" @onchange="OnYearChange">
                        @foreach (var y in _years)
                        {
                            <option value="@y">@y</option>
                        }
                    </select>
                </div>
                <div>
                    <label class="form-label fs-12 mb-1 fw-medium" for="dr-month">@L["Common.Month"]</label>
                    <select id="dr-month" class="form-select form-select-sm" value="@_month" @onchange="OnMonthChange">
                        <option value="0">@L["Common.AllMonths"]</option>
                        @for (var m = 1; m <= 12; m++)
                        {
                            var mo = m;
                            <option value="@mo">@MonthName(mo)</option>
                        }
                    </select>
                </div>
            </div>
        </div>

        @if (_loading)
        {
            <LoadingSpinner IsLoading="true" />
        }
        else if (_error || _data is null)
        {
            <div class="alert alert-danger m-3 daily-residuals-error">@L["Team.LoadFailedResiduals"]</div>
        }
        else
        {
            <div class="d-flex flex-wrap gap-4 px-4 pb-3">
                <div><span class="text-muted fs-12 d-block">@L["Reports.PeriodTotal"]</span><span class="fw-semibold dr-total">@Money.Usd(_data.PeriodTotal)</span></div>
                <div><span class="text-muted fs-12 d-block">@L["Reports.Paid"]</span><span class="fw-semibold text-success dr-paid">@Money.Usd(_data.PeriodPaid)</span></div>
                <div><span class="text-muted fs-12 d-block">@L["Reports.Pending"]</span><span class="fw-semibold text-warning dr-pending">@Money.Usd(_data.PeriodPending)</span></div>
            </div>

            <div class="default-table-area style-two default-table-width">
                <div class="table-responsive">
                    <table class="table align-middle">
                        <thead>
                            <tr>
                                <th>@L["Reports.EarnedDate"]</th>
                                <th>@L["Common.Amount"]</th>
                                <th>@L["Common.Status"]</th>
                                <th>@L["Reports.PaymentDate"]</th>
                                <th>@L["Reports.EligiblePoints"]</th>
                                <th>@L["Reports.Comment"]</th>
                            </tr>
                        </thead>
                        <tbody>
                            @if (_data.Items.Count == 0)
                            {
                                <tr><td colspan="6" class="text-center text-muted py-4">@L["Team.NoResidualEarnings"]</td></tr>
                            }
                            @foreach (var r in _data.Items)
                            {
                                <tr class="dr-row">
                                    <td>@r.EarnedDate.ToString("MMM dd, yyyy")</td>
                                    <td class="fw-semibold">@Money.Usd(r.Amount)</td>
                                    <td><span class="badge @BadgeClass(r.Status) p-2 fs-12 fw-normal">@StatusText(r.Status)</span></td>
                                    <td>@(r.PaymentDate?.ToString("MMM dd, yyyy") ?? "—")</td>
                                    <td>@(r.EligiblePoints?.ToString("N0") ?? "—")</td>
                                    <td class="small text-muted">@(r.PaymentComment ?? "—")</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>

                <div class="p-4 d-flex justify-content-between align-items-center flex-wrap gap-2">
                    <span class="fs-12 fw-medium">@L["Reports.Showing"] @_data.Items.Count @L["Common.Of"] @_data.TotalCount @L["Reports.Results"]</span>
                    @if (TotalPages > 1)
                    {
                        <div class="btn-group">
                            <button class="btn btn-outline-secondary btn-sm dr-prev" disabled="@(_page <= 1)" @onclick="() => LoadAsync(_page - 1)">@L["Common.Previous"]</button>
                            <button class="btn btn-outline-secondary btn-sm dr-next" disabled="@(_page >= TotalPages)" @onclick="() => LoadAsync(_page + 1)">@L["Common.Next"]</button>
                        </div>
                    }
                </div>
            </div>
        }
    </div>
</div>

@code {
    /// <summary>Raíz del API: "api/v1/bizcenter" o "api/v1/admin/members/{id}".</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = "api/v1/bizcenter";

    [Parameter] public int PageSize { get; set; } = 10;

    private bool        _loading = true;
    private bool        _error;
    private HistoryDto? _data;
    private List<int>   _years = new();
    private int         _year;      // 0 = que decida el servidor
    private int         _month;     // 0 = todo el año
    private int         _page = 1;

    private int TotalPages => _data is null || _data.PageSize <= 0
        ? 0
        : (int)Math.Ceiling(_data.TotalCount / (double)_data.PageSize);

    protected override Task OnParametersSetAsync() => LoadAsync(1);

    private async Task LoadAsync(int page)
    {
        _loading = true;
        _error   = false;
        try
        {
            var url = $"{ApiBaseUrl}/commissions/dual-residual/daily?page={page}&pageSize={PageSize}";
            if (_year > 0) url += $"&year={_year}";
            if (_month is >= 1 and <= 12) url += $"&month={_month}";

            var resp = await Http.GetFromJsonAsync<ApiWrapper<HistoryDto>>(url);
            if (resp?.Success == true && resp.Data is not null)
            {
                _data  = resp.Data;
                _page  = resp.Data.Page;
                _year  = resp.Data.Year;
                _month = resp.Data.Month ?? 0;
                _years = resp.Data.AvailableYears.Count > 0 ? resp.Data.AvailableYears : new List<int> { resp.Data.Year };
            }
            else
            {
                _error = true;
            }
        }
        catch (Exception)
        {
            _error = true;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OnYearChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var y)) _year = y;
        await LoadAsync(1);
    }

    private async Task OnMonthChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var m)) _month = m;
        await LoadAsync(1);
    }

    private string StatusText(string status) => status switch
    {
        "Paid"    => L["Reports.Paid"],
        "Pending" => L["Reports.Pending"],
        _         => status
    };

    private static string BadgeClass(string status) => status switch
    {
        "Paid"    => "bg-success bg-opacity-10 text-success",
        "Pending" => "bg-warning bg-opacity-10 text-warning",
        _         => "bg-secondary bg-opacity-10 text-secondary"
    };

    /// <summary>Algunas culturas (es, fr, it…) dan el mes en minúscula: se capitaliza la primera letra.</summary>
    private static string MonthName(int month)
    {
        var name = CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(month);
        return name.Length == 0 ? name : char.ToUpper(name[0]) + name[1..];
    }

    private sealed class ApiWrapper<T> { public bool Success { get; set; } public T? Data { get; set; } }

    private sealed class HistoryDto
    {
        public List<RowDto> Items          { get; set; } = new();
        public int          TotalCount     { get; set; }
        public int          Page           { get; set; }
        public int          PageSize       { get; set; }
        public int          Year           { get; set; }
        public int?         Month          { get; set; }
        public decimal      PeriodTotal    { get; set; }
        public decimal      PeriodPaid     { get; set; }
        public decimal      PeriodPending  { get; set; }
        public List<int>    AvailableYears { get; set; } = new();
    }

    private sealed class RowDto
    {
        public long      Id             { get; set; }
        public DateTime  EarnedDate     { get; set; }
        public decimal   Amount         { get; set; }
        public string    Status         { get; set; } = string.Empty;
        public DateTime? PaymentDate    { get; set; }
        public int?      EligiblePoints { get; set; }
        public string?   PaymentComment { get; set; }
    }
}
```

- [ ] **Step 4: Textos nuevos**

En `TextosDeReportesTests.cs`, sustituir:

```csharp
        // (claves de los bloques siguientes)
```

por:

```csharp
        // Bloque 2 — Residuales diarios
        "Reports.PeriodTotal", "Reports.Comment",
        // (claves de los bloques siguientes)
```

y añadir a los nueve `.resx` (antes de `</root>`):

| Clave | en | es | pt | fr | it | de | ko | zh | ka |
|---|---|---|---|---|---|---|---|---|---|
| `Reports.PeriodTotal` | Period total | Total del periodo | Total do período | Total de la période | Totale del periodo | Summe des Zeitraums | 기간 합계 | 期间合计 | პერიოდის ჯამი |
| `Reports.Comment` | Comment | Comentario | Comentário | Commentaire | Commento | Kommentar | 메모 | 备注 | კომენტარი |

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "DailyResidualHistoryTests|TextosDeReportesTests" -nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Commissions/DailyResidualHistory.razor \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/DailyResidualHistoryTests.cs
git commit -m "Residuales diarios: componente compartido con ano y mes del servidor, filas con fecha, importe, estado, fecha de pago, puntos elegibles y comentario, totales del periodo en USD y paginacion; textos en los 9 idiomas"
```

---

### Task 13: `DualTeamResiduals` usa el componente; fuera la sección 4 vieja, su `UtcNow` y `DualResidualReport.razor`

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Team/DualTeamResiduals.razor` (líneas 8-9, 417-536, 544-547, 562-573, 599-611, 627, 699-762, 865-869, 924-935, 959, 1018-1022)
- Modify: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor:681`
- Delete: `MLMConquerorGlobalEdition.SharedComponents/Components/Commissions/DualResidualReport.razor`

- [ ] **Step 1: Importar el componente**

Sustituir:

```razor
@using MLMConquerorGlobalEdition.SharedComponents.Resources
@implements IAsyncDisposable
```

por:

```razor
@using MLMConquerorGlobalEdition.SharedComponents.Resources
@using MLMConquerorGlobalEdition.SharedComponents.Components.Commissions
@implements IAsyncDisposable
```

- [ ] **Step 2: Sustituir el marcado de la sección 4**

Borrar desde el `@* ═══…` que abre el comentario `SECTION 4 — Daily Residuals table with date filter` hasta el `</div>` que cierra esa tarjeta, justo antes de la línea en blanco y `@code {`, los dos incluidos (en el archivo original, líneas 417 a 536; tras el paso 1, una más), y poner en su lugar:

```razor
@* ═══════════════════════════════════════════════════════════════════════════
   SECTION 4 — Residuales diarios (Plan J): componente propio sobre
   DailyResidualEarnings. Año, mes, totales y paginación viven allí.
   ══════════════════════════════════════════════════════════════════════════ *@
<DailyResidualHistory ApiBaseUrl="@ApiBaseUrl" />
```

- [ ] **Step 3: Quitar el parámetro `EnrollDate`**

Sustituir:

```csharp
    [Parameter] public bool      ShowBreadcrumb { get; set; } = true;

    /// <summary>Member's enrollment date — drives the lower bound of the year
    /// dropdown so the user can only pick years where they could have earned.
    /// Falls back to (CurrentYear - 5) when the parent did not pass it.</summary>
    [Parameter] public DateTime? EnrollDate     { get; set; }
```

por:

```csharp
    [Parameter] public bool      ShowBreadcrumb { get; set; } = true;
```

- [ ] **Step 4: Quitar el estado de la sección 4**

Borrar el bloque completo:

```csharp
    // ── Section 4 state — residuals table ────────────────────────────────────
    private bool              _residualsLoading   = true;
    private string?           _residualsError;
    private List<DualResidualDto> _residuals      = new();
    private string?           _residualSearch;
    private int               _selectedYear;
    private int               _selectedMonth;     // 0 = all months
    private List<int>         _yearOptions        = new();
    private int               _residualPage       = 1;
    private int               _residualTotal;
    private int               _residualTotalPages;
    private const int         ResidualPageSize    = 10;

```

- [ ] **Step 5: Quitar los años calculados con `DateTime.UtcNow` en `OnParametersSetAsync`**

Sustituir:

```csharp
        if (string.IsNullOrEmpty(MemberProfileId)) return;

        // Build year dropdown range once: from the member's enrollment year
        // (or CurrentYear - 5 fallback) up to the current year. Default the
        // selection to the current year + "all months" so the table reflects
        // the most recent activity on first paint.
        var nowYear   = DateTime.UtcNow.Year;
        var startYear = EnrollDate?.Year is int ey && ey <= nowYear ? ey : nowYear - 5;
        if (startYear > nowYear) startYear = nowYear;

        var newOptions = Enumerable.Range(startYear, nowYear - startYear + 1).Reverse().ToList();
        if (!_yearOptions.SequenceEqual(newOptions)) _yearOptions = newOptions;
        if (_selectedYear == 0)  _selectedYear  = nowYear;
        if (_selectedMonth < 0)  _selectedMonth = 0;

```

por:

```csharp
        if (string.IsNullOrEmpty(MemberProfileId)) return;

```

y en el `Task.WhenAll` de más abajo, borrar la línea `            LoadResidualsAsync(1),`.

- [ ] **Step 6: Quitar la carga y los filtros viejos**

Borrar desde `    private async Task LoadResidualsAsync(int page)` hasta el `    }` que cierra `OnMonthChange` (incluido el bloque `// ── Search / filter handlers` y el campo `_searchCts`), es decir, todo lo que hay entre el final de `LoadMembersAsync` y la línea `    // ── Chart loader + JSInterop ─────…`.

- [ ] **Step 7: Quitar los ayudantes que ya no usa nadie**

Borrar:

```csharp
    /// <summary>Some cultures (es, fr, it...) report month names lowercase.
    /// Capitalize the first letter so the dropdown reads consistently across
    /// languages.</summary>
    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

```

el método completo `private RenderFragment RenderResidualStatusBadge(string status) => __builder => { … };`, la línea

```csharp
    private class PagedData<T>  { public List<T> Items { get; set; } = new(); public int TotalCount { get; set; } }
```

y el registro del final:

```csharp

    private record DualResidualDto(
        DateTime EarnedDate,
        decimal  Amount,
        string   Status,
        int      EligiblePoints);
```

- [ ] **Step 8: Comprobar que no queda nada de la sección vieja**

Run: `grep -n "_residuals\|_residualSearch\|_selectedYear\|_selectedMonth\|_yearOptions\|EnrollDate\|UtcNow\|DualResidualDto\|CapitalizeFirst\|RenderResidualStatusBadge\|PagedData\|LoadResidualsAsync" MLMConquerorGlobalEdition.SharedComponents/Components/Team/DualTeamResiduals.razor`
Expected: sin salida. (`_residualLoading` y `_residualSummary`, de la sección 2, se quedan: el patrón no los encuentra.)

- [ ] **Step 9: El Admin deja de pasar `EnrollDate`**

En `AdminWeb/Components/Pages/AdminMemberProfile.razor`, sustituir:

```razor
            <DualTeamResiduals MemberProfileId="@MemberId"
                               ApiBaseUrl="@($"api/v1/admin/members/{MemberId}")"
                               EnrollDate="@_member.EnrollDate"
                               ShowBreadcrumb="false" />
```

por:

```razor
            <DualTeamResiduals MemberProfileId="@MemberId"
                               ApiBaseUrl="@($"api/v1/admin/members/{MemberId}")"
                               ShowBreadcrumb="false" />
```

- [ ] **Step 10: Borrar `DualResidualReport.razor` (huérfano)**

Comprobar primero que nadie lo usa:

Run: `grep -rn "DualResidualReport" --include=*.razor --include=*.cs --include=*.cshtml . | grep -v "/obj/" | grep -v "/bin/"`
Expected: solo líneas de `MLMConquerorGlobalEdition.SharedComponents/Components/Commissions/DualResidualReport.razor` (el propio archivo). Si aparece cualquier otro archivo, **no borrar** y avisar.

Run: `git rm MLMConquerorGlobalEdition.SharedComponents/Components/Commissions/DualResidualReport.razor`

- [ ] **Step 11: Compilar las dos webs y pasar las pruebas del BizCenter**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenterWeb -v q -nologo` y `dotnet build MLMConquerorGlobalEdition.AdminWeb -v q -nologo`
Expected: `0 Error(s)` en las dos.

Run: `grep -rn "EnrollDate=" --include=*.razor MLMConquerorGlobalEdition.BizCenterApp MLMConquerorGlobalEdition.BizCenterWeb MLMConquerorGlobalEdition.AdminWeb | grep DualTeamResiduals`
Expected: sin salida (la MAUI, `BizCenterApp/Components/Pages/DualTeamResidualsPage.razor`, ya lo montaba sin `EnrollDate`; no se compila aquí porque necesita las cargas de trabajo de MAUI).

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`
Expected: PASS.

- [ ] **Step 12: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Team/DualTeamResiduals.razor \
        MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor
git commit -m "Residuales del Dual Team: la seccion 4 pasa a DailyResidualHistory sobre DailyResidualEarnings; fuera la tabla vieja sobre CommissionEarnings, su busqueda ignorada, sus anos con DateTime.UtcNow y el parametro EnrollDate; se borra DualResidualReport.razor, que nadie montaba"
```

(El `git rm` del paso 10 ya dejó el borrado preparado: entra en este commit.)

---

### Task 14: Pruebas contra SQL Server de residuales

**Files:**
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerResidualesTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Lo que InMemory no prueba: que el GroupBy por estado, el enum.ToString() de la proyección y el
/// GroupBy por año y mes del gráfico se traducen a SQL, y que el índice existe en la base.
/// </summary>
public class SqlServerResidualesTests
{
    [SqlServerFact]
    public async Task El_historial_y_el_grafico_se_traducen_a_SQL()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        db.DailyResidualEarnings.AddRange(
            DailyResidualReportServiceTests.Residual(yo, new DateTime(2025, 5, 3), 10m, CommissionEarningStatus.Paid,
                dt: 100, et: 0, pagado: new DateTime(2025, 5, 10), comentario: "Consolidated"),
            DailyResidualReportServiceTests.Residual(yo, new DateTime(2025, 5, 4), 5m),
            DailyResidualReportServiceTests.Residual(yo, new DateTime(2025, 6, 1), 7m, CommissionEarningStatus.Cancelled));
        await db.SaveChangesAsync();
        var reloj = new RelojFijo(new DateTime(2025, 6, 15, 12, 0, 0));

        var historial = await new DailyResidualReportService(db, reloj).GetHistoryAsync(yo, 2025, 5, 1, 10);
        var grafico   = await new CommissionsService(db, reloj).GetDualResidualChartAsync(yo, 2);

        historial.TotalCount.Should().Be(2);
        historial.PeriodPaid.Should().Be(10m);
        historial.PeriodPending.Should().Be(5m);
        historial.Items[0].Status.Should().Be("Pending");
        historial.Items[0].EligiblePoints.Should().BeNull();
        historial.Items[1].EligiblePoints.Should().Be(100);
        grafico.Select(p => p.Amount).Should().Equal(15m, 0m);

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_indice_por_beneficiario_y_fecha_existe()
    {
        await using var db = SqlServerReportes.Db();

        var n = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE name = 'IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate'
              AND object_id = OBJECT_ID('DailyResidualEarnings')
            """).SingleAsync();

        n.Should().Be(1, "la migración IndiceResidualesPorBeneficiarioYFecha tiene que estar aplicada en local (Tarea 8)");
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerResidualesTests -nologo`
Expected: PASS (2 pruebas) con SQL Server local y la migración de la Tarea 8 aplicada; `Skipped` si no hay SQL Server.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerResidualesTests.cs
git commit -m "Pruebas contra SQL Server del historial y el grafico de residuales diarios y del indice por beneficiario y fecha"
```

**Fin del Bloque 2.** Lleva una migración (`IndiceResidualesPorBeneficiarioYFecha`); el resto no toca la base.

---

# Bloque 3 — Clientes del equipo

### Task 15: Servicio de clientes con alcance, nivel y contacto solo de los personales

`BuildCustomerQueryable` (`EnrollmentTeamService.cs:557-575`) devuelve todo el subárbol con correo y teléfono de cualquier nivel, y `EnrollmentCustomerView` no tiene `Level` aunque el componente lo pinta (`EnrollmentDtos.cs:90-104`).

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/CustomerScope.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentDtos.cs:90-104`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/IEnrollmentTeamService.cs:33-35`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs:1-9,505-575`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Controllers/TeamController.cs:111`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberEnrollmentController.cs:92`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ClientesDelEquipoTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Clientes del equipo: alcance Personales o Equipo, nivel relativo y datos de contacto solo de los
/// clientes personales. El contacto se anula en la proyección, así que tampoco se puede buscar.
/// </summary>
public class ClientesDelEquipoTests
{
    private const string Yo = "AMB-000001";

    internal static EnrollmentTeamService Servicio(AppDbContext db) => new(
        db,
        new RankComputationService(db, new RankQualificationService(
            db, new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db))),
        new EnrollmentTeamPointsService(db));

    /// <summary>
    /// YO ─┬─ C1 (cliente personal de YO)
    ///     └─ A (embajador) ── C2 (cliente de A)
    /// OTRO ── FUERA (cliente de otro equipo)
    /// </summary>
    private static async Task<AppDbContext> EquipoAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, Yo);
        ReportesDb.Miembro(db, "AMB-000002", Yo);
        ReportesDb.Miembro(db, "MBR-000001", Yo, MemberType.ExternalMember, email: "c1@prueba.test", telefono: "111");
        ReportesDb.Miembro(db, "MBR-000002", "AMB-000002", MemberType.ExternalMember, email: "c2@prueba.test", telefono: "222");
        ReportesDb.Miembro(db, "MBR-000009", "AMB-000099", MemberType.ExternalMember);
        ReportesDb.Nodo(db, Yo,           null,         ReportesDb.Ruta(Yo), 1);
        ReportesDb.Nodo(db, "AMB-000002", Yo,           ReportesDb.Ruta(Yo, "AMB-000002"), 2);
        ReportesDb.Nodo(db, "MBR-000001", Yo,           ReportesDb.Ruta(Yo, "MBR-000001"), 2);
        ReportesDb.Nodo(db, "MBR-000002", "AMB-000002", ReportesDb.Ruta(Yo, "AMB-000002", "MBR-000002"), 3);
        ReportesDb.Nodo(db, "MBR-000009", "AMB-000099", ReportesDb.Ruta("AMB-000099", "MBR-000009"), 2);
        await db.SaveChangesAsync();
        return db;
    }

    private static GridDataRequest Pagina(string? buscar = null) => new() { Page = 1, PageSize = 50, Search = buscar };

    [Fact]
    public async Task Equipo_lista_los_clientes_del_subarbol_con_su_nivel_relativo()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Team);

        r.Items.Select(c => (c.MemberId, c.Level)).Should().BeEquivalentTo(new[]
        {
            ("MBR-000001", 1),
            ("MBR-000002", 2)
        });
    }

    [Fact]
    public async Task Personales_solo_los_que_inscribio_el_que_mira()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Personal);

        r.Items.Should().ContainSingle().Which.MemberId.Should().Be("MBR-000001");
        r.Items.Single().Level.Should().Be(1);
        r.Items.Single().Email.Should().Be("c1@prueba.test");
    }

    [Fact]
    public async Task En_equipo_el_contacto_solo_se_ve_de_los_personales()
    {
        await using var db = await EquipoAsync();

        var r = (await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Team)).Items.ToList();

        var c1 = r.Single(c => c.MemberId == "MBR-000001");
        var c2 = r.Single(c => c.MemberId == "MBR-000002");
        (c1.IsPersonal, c1.Email, c1.Phone).Should().Be((true, "c1@prueba.test", "111"));
        (c2.IsPersonal, c2.Email, c2.Phone).Should().Be((false, (string?)null, (string?)null));
    }

    [Fact]
    public async Task Buscar_por_el_correo_de_un_cliente_ajeno_no_lo_delata()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersGridAsync(Yo, Pagina("c2@prueba"), CustomerScope.Team);

        r.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task El_GET_antiguo_tambien_oculta_el_contacto_ajeno()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersAsync(Yo, 1, 50, null);

        r.Items.Single(c => c.MemberId == "MBR-000002").Email.Should().BeNull();
        r.Items.Single(c => c.MemberId == "MBR-000001").Email.Should().Be("c1@prueba.test");
    }

    [Theory]
    [InlineData("personal", CustomerScope.Personal)]
    [InlineData("PERSONAL", CustomerScope.Personal)]
    [InlineData("team",     CustomerScope.Team)]
    [InlineData(null,       CustomerScope.Team)]
    [InlineData("otra",     CustomerScope.Team)]
    public void El_alcance_de_la_query_por_defecto_es_equipo(string? valor, CustomerScope esperado)
        => CustomerScopes.Parse(valor).Should().Be(esperado);
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter ClientesDelEquipoTests -nologo`
Expected: FAIL de compilación (`CustomerScope` no existe).

- [ ] **Step 3: Crear `CustomerScope.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Qué clientes del equipo de inscripción se listan (Plan J).</summary>
public enum CustomerScope
{
    /// <summary>Solo los que inscribió directamente el que mira (SponsorMemberId == él).</summary>
    Personal = 1,

    /// <summary>Todo el subárbol de inscripción. El contacto solo se ve de los personales.</summary>
    Team = 2
}

public static class CustomerScopes
{
    /// <summary>"personal" (sin distinguir mayúsculas) es Personal; cualquier otra cosa, Team.</summary>
    public static CustomerScope Parse(string? value) =>
        string.Equals(value, "personal", StringComparison.OrdinalIgnoreCase)
            ? CustomerScope.Personal
            : CustomerScope.Team;
}
```

- [ ] **Step 4: Ampliar `EnrollmentCustomerView` en `EnrollmentDtos.cs`**

Sustituir:

```csharp
public class EnrollmentCustomerView
{
    public string   MemberId         { get; set; } = string.Empty;
    public string   FullName         { get; set; } = string.Empty;
    public string   Email            { get; set; } = string.Empty;
    public string?  Phone            { get; set; }
    public string   Country          { get; set; } = string.Empty;
    public DateTime EnrollDate       { get; set; }
```

por:

```csharp
public class EnrollmentCustomerView
{
    public string   MemberId         { get; set; } = string.Empty;
    public string   FullName         { get; set; } = string.Empty;
    /// <summary>Solo de los clientes personales del que mira (Plan J); null para el resto del equipo.</summary>
    public string?  Email            { get; set; }
    /// <summary>Igual que <see cref="Email"/>.</summary>
    public string?  Phone            { get; set; }
    public string   Country          { get; set; } = string.Empty;
    /// <summary>Nivel relativo al que mira: 1 = lo inscribió él.</summary>
    public int      Level            { get; set; }
    /// <summary>Lo inscribió directamente el que mira.</summary>
    public bool     IsPersonal       { get; set; }
    public DateTime EnrollDate       { get; set; }
```

- [ ] **Step 5: Firma de la interfaz en `IEnrollmentTeamService.cs`**

Sustituir:

```csharp
    /// <summary>Server-side grid read over all customers in the viewer's enrollment downline.</summary>
    Task<PagedResult<EnrollmentCustomerView>> GetCustomersGridAsync(
        string memberId, GridDataRequest request, CancellationToken ct = default);
```

por:

```csharp
    /// <summary>
    /// Server-side grid read over the viewer's customers: their personal ones or the whole
    /// enrollment downline. Contact details only for personal customers (Plan J).
    /// </summary>
    Task<PagedResult<EnrollmentCustomerView>> GetCustomersGridAsync(
        string memberId, GridDataRequest request, CustomerScope scope, CancellationToken ct = default);
```

- [ ] **Step 6: Implementación en `EnrollmentTeamService.cs`**

Añadir a los `using` del principio:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
```

Y sustituir desde la línea `    // ─── Customers ───…` hasta el final de `BuildCustomerQueryable` (el `    }` justo antes de `    private async Task EnrichCustomerPageAsync(`), las dos incluidas (en el archivo original, líneas 505 a 575), por:

```csharp
    // ─── Customers ─────────────────────────────────────────────────────────
    public async Task<PagedResult<EnrollmentCustomerView>> GetCustomersAsync(
        string memberId, int page, int pageSize, string? search,
        CancellationToken ct = default)
    {
        var myNode = await MyGenealogyNodeAsync(memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentCustomerView>();

        var q = BuildCustomerQueryable(memberId, myNode.HierarchyPath, myNode.Level, CustomerScope.Team);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(v => v.FullName.ToLower().Contains(s) || v.MemberId.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(v => v.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        await EnrichCustomerPageAsync(items, ct);

        return new PagedResult<EnrollmentCustomerView>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    /// <summary>
    /// Server-side grid read over the viewer's customers — DB-side search/filter/sort/count/page on
    /// a light projection, page-only enrichment. <paramref name="scope"/> chooses personal customers
    /// or the whole enrollment downline (Plan J).
    /// </summary>
    public async Task<PagedResult<EnrollmentCustomerView>> GetCustomersGridAsync(
        string memberId, GridDataRequest request, CustomerScope scope, CancellationToken ct = default)
    {
        var myNode = await MyGenealogyNodeAsync(memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentCustomerView>();

        if (!request.Sorts.Any(s => CustomerSearchableFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)
                                 || string.Equals(s.Field, nameof(EnrollmentCustomerView.EnrollDate), StringComparison.OrdinalIgnoreCase)))
            request.Sorts.Add(new GridSort { Field = nameof(EnrollmentCustomerView.EnrollDate), Direction = "desc" });

        var q      = BuildCustomerQueryable(memberId, myNode.HierarchyPath, myNode.Level, scope);
        var result = await q.ToGridResultAsync(request, CustomerSearchableFields, ct: ct);
        await EnrichCustomerPageAsync(result.Items, ct);
        return result;
    }

    /// <summary>The viewer's most recent genealogy node: a member can have two (see e4af288).</summary>
    private Task<GenealogyEntity?> MyGenealogyNodeAsync(string memberId, CancellationToken ct) =>
        _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Light DB projection of the viewer's external-member customers. Email and phone are nulled
    /// IN THE PROJECTION (a SQL CASE) for customers the viewer did not enroll, so the grid's search
    /// and column filters run on the redacted value and cannot reveal that a non-personal customer
    /// with a given e-mail exists (Plan J, J8). Enriched columns are filled per page by
    /// <see cref="EnrichCustomerPageAsync"/>.
    /// </summary>
    private IQueryable<EnrollmentCustomerView> BuildCustomerQueryable(
        string memberId, string pathPrefix, int rootLevel, CustomerScope scope)
    {
        if (scope == CustomerScope.Personal)
        {
            return
                from m in _db.MemberProfiles.AsNoTracking()
                where m.SponsorMemberId == memberId && m.MemberType == MemberType.ExternalMember
                select new EnrollmentCustomerView
                {
                    MemberId        = m.MemberId,
                    FullName        = m.FirstName + " " + m.LastName,
                    Email           = m.Email,
                    Phone           = m.Phone,
                    Country         = m.Country,
                    Level           = 1,
                    IsPersonal      = true,
                    EnrollDate      = m.EnrollDate,
                    SponsorMemberId = m.SponsorMemberId,
                    AccountStatus   = m.Status.ToString()
                };
        }

        return
            from g in _db.GenealogyTree.AsNoTracking()
            where g.HierarchyPath.StartsWith(pathPrefix) && g.MemberId != memberId
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            where m.MemberType == MemberType.ExternalMember
            select new EnrollmentCustomerView
            {
                MemberId        = m.MemberId,
                FullName        = m.FirstName + " " + m.LastName,
                Email           = m.SponsorMemberId == memberId ? m.Email : null,
                Phone           = m.SponsorMemberId == memberId ? m.Phone : null,
                Country         = m.Country,
                Level           = g.Level - rootLevel,
                IsPersonal      = m.SponsorMemberId == memberId,
                EnrollDate      = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                AccountStatus   = m.Status.ToString()
            };
    }
```

- [ ] **Step 7: Los dos llamadores pasan el alcance de hoy**

En `BizCenter/Controllers/TeamController.cs`, sustituir:

```csharp
        var result = await _enrollment.GetCustomersGridAsync(_currentUser.MemberId, request, ct);
```

por:

```csharp
        var result = await _enrollment.GetCustomersGridAsync(_currentUser.MemberId, request, CustomerScope.Team, ct);
```

En `AdminAPI/Controllers/AdminMemberEnrollmentController.cs`, sustituir:

```csharp
        var result = await _service.GetCustomersGridAsync(memberId, request, ct);
```

por:

```csharp
        var result = await _service.GetCustomersGridAsync(memberId, request, CustomerScope.Team, ct);
```

(Los dos archivos ya importan `MLMConquerorGlobalEdition.Repository.Services.Teams`. La Tarea 16 hace el alcance elegible.)

- [ ] **Step 8: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "ClientesDelEquipoTests|TeamHandlersTests" -nologo`
Expected: PASS.

- [ ] **Step 9: Compilar los anfitriones**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenter -v q -nologo` y `dotnet build MLMConquerorGlobalEdition.AdminAPI -v q -nologo`
Expected: `0 Error(s)` (puede haber avisos de nulabilidad en `GetEnrollmentCustomersHandler`; se resuelven en la Tarea 16).

- [ ] **Step 10: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Teams/CustomerScope.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentDtos.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/IEnrollmentTeamService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs \
        MLMConquerorGlobalEdition.BizCenter/Controllers/TeamController.cs \
        MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberEnrollmentController.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ClientesDelEquipoTests.cs
git commit -m "Clientes del equipo: alcance Personales o Equipo, nivel relativo al que mira y correo y telefono solo de los clientes personales, anulados en la proyeccion SQL para que la busqueda no delate a los ajenos; el nodo del que mira es el mas reciente"
```

---

### Task 16: El alcance llega por la query, en BizCenter y Admin

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter/Controllers/TeamController.cs:106-113`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberEnrollmentController.cs:85-94`
- Modify: `MLMConquerorGlobalEdition.BizCenter/DTOs/Teams/EnrollmentCustomerDto.cs:7`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Teams/GetEnrollmentCustomers/GetEnrollmentCustomersHandler.cs:33-37`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ClientesEndpointTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class ClientesEndpointTests
{
    [Theory]
    [InlineData("personal", CustomerScope.Personal)]
    [InlineData(null,       CustomerScope.Team)]
    public async Task La_rejilla_pide_los_clientes_del_miembro_del_token_con_el_alcance_de_la_query(
        string? scope, CustomerScope esperado)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        var inscripcion = new Mock<IEnrollmentTeamService>();
        inscripcion.Setup(s => s.GetCustomersGridAsync("AMB-000777", It.IsAny<GridDataRequest>(), esperado, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new PagedResult<EnrollmentCustomerView>());
        var control = new TeamController(new Mock<IMediator>().Object, new Mock<IDualTeamService>().Object,
            inscripcion.Object, usuario.Object, new Mock<IDownlineGuard>().Object);

        var respuesta = await control.GetEnrollmentCustomersGrid(new GridDataRequest(), scope, CancellationToken.None);

        respuesta.Should().BeOfType<OkObjectResult>();
        inscripcion.Verify(s => s.GetCustomersGridAsync("AMB-000777", It.IsAny<GridDataRequest>(), esperado, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

Y en `MiembroDelTokenTests.cs`, sustituir:

```csharp
        // (acciones de los bloques siguientes)
```

por:

```csharp
        (typeof(TeamController), nameof(TeamController.GetEnrollmentCustomersGrid)),
        // (acciones de los bloques siguientes)
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter ClientesEndpointTests -nologo`
Expected: FAIL de compilación (`GetEnrollmentCustomersGrid` no tiene el parámetro `scope`).

- [ ] **Step 3: BizCenter**

En `TeamController.cs`, sustituir:

```csharp
    /// <summary>POST /api/v1/bizcenter/team/enrollment/customers/grid</summary>
    [HttpPost("enrollment/customers/grid")]
    [ReadOnlySafe]
    public async Task<IActionResult> GetEnrollmentCustomersGrid([FromBody] GridDataRequest request, CancellationToken ct = default)
    {
        var result = await _enrollment.GetCustomersGridAsync(_currentUser.MemberId, request, CustomerScope.Team, ct);
```

por:

```csharp
    /// <summary>POST /api/v1/bizcenter/team/enrollment/customers/grid?scope=personal|team</summary>
    /// <remarks>
    /// Plan J: <c>scope=personal</c> lista solo los clientes que inscribió el miembro del token;
    /// cualquier otro valor, todo su equipo de inscripción. El contacto solo sale de los personales.
    /// </remarks>
    [HttpPost("enrollment/customers/grid")]
    [ReadOnlySafe]
    public async Task<IActionResult> GetEnrollmentCustomersGrid(
        [FromBody] GridDataRequest request, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        var result = await _enrollment.GetCustomersGridAsync(
            _currentUser.MemberId, request, CustomerScopes.Parse(scope), ct);
```

- [ ] **Step 4: Admin**

En `AdminMemberEnrollmentController.cs`, sustituir:

```csharp
    [HttpPost("customers/grid")]
    public async Task<IActionResult> GetCustomersGrid(
        string memberId,
        [FromBody] GridDataRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.GetCustomersGridAsync(memberId, request, CustomerScope.Team, ct);
```

por:

```csharp
    [HttpPost("customers/grid")]
    public async Task<IActionResult> GetCustomersGrid(
        string memberId,
        [FromBody] GridDataRequest request,
        [FromQuery] string? scope = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetCustomersGridAsync(memberId, request, CustomerScopes.Parse(scope), ct);
```

- [ ] **Step 5: El DTO y el manejador del `GET` antiguo**

En `BizCenter/DTOs/Teams/EnrollmentCustomerDto.cs`, sustituir `    public string    Email            { get; set; } = string.Empty;` por `    public string?   Email            { get; set; }`.

En `GetEnrollmentCustomersHandler.cs`, sustituir:

```csharp
                Phone            = c.Phone,
                Country          = c.Country,
                EnrollDate       = c.EnrollDate,
```

por:

```csharp
                Phone            = c.Phone,
                Country          = c.Country,
                Level            = c.Level,
                EnrollDate       = c.EnrollDate,
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "ClientesEndpointTests|MiembroDelTokenTests|PropiedadEnElCentroDeNegociosTests" -nologo`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Controllers/TeamController.cs \
        MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberEnrollmentController.cs \
        MLMConquerorGlobalEdition.BizCenter/DTOs/Teams/EnrollmentCustomerDto.cs \
        MLMConquerorGlobalEdition.BizCenter/Features/Teams/GetEnrollmentCustomers/GetEnrollmentCustomersHandler.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ClientesEndpointTests.cs
git commit -m "Clientes del equipo: la rejilla del BizCenter y la del Admin aceptan scope=personal|team en la query con el miembro del token o de la ruta; el GET antiguo devuelve el nivel y el correo anulable"
```

---

### Task 17: Pantalla de clientes con Personales/Equipo

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Team/EnrollmentTeamCustomers.razor:27-33,40-45,63-65,140-141,170`
- Modify: los nueve `SharedResources*.resx`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla (textos)**

En `TextosDeReportesTests.cs`, sustituir:

```csharp
        // (claves de los bloques siguientes)
```

por:

```csharp
        // Bloque 3 — Clientes del equipo
        "Team.CustomersScopePersonal", "Team.CustomersScopeTeam", "Team.ContactOnlyPersonal",
        // (claves de los bloques siguientes)
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: FAIL (faltan las tres claves).

- [ ] **Step 2: Añadir las claves a los nueve `.resx`**

| Clave | en | es | pt | fr | it | de | ko | zh | ka |
|---|---|---|---|---|---|---|---|---|---|
| `Team.CustomersScopePersonal` | Personal | Personales | Pessoais | Personnels | Personali | Persönlich | 개인 | 个人 | პირადი |
| `Team.CustomersScopeTeam` | Whole team | Todo el equipo | Toda a equipe | Toute l'équipe | Tutto il team | Gesamtes Team | 전체 팀 | 整个团队 | მთელი გუნდი |
| `Team.ContactOnlyPersonal` | Contact details are shown only for your personal customers. | Los datos de contacto solo se muestran de tus clientes personales. | Os dados de contato só são exibidos para seus clientes pessoais. | Les coordonnées ne sont affichées que pour vos clients personnels. | I dati di contatto sono mostrati solo per i tuoi clienti personali. | Kontaktdaten werden nur für Ihre persönlichen Kunden angezeigt. | 연락처 정보는 개인 고객에게만 표시됩니다. | 仅显示您个人客户的联系方式。 | საკონტაქტო მონაცემები ჩანს მხოლოდ თქვენი პირადი კლიენტებისთვის. |

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 3: Botones de alcance y aviso**

En `EnrollmentTeamCustomers.razor`, sustituir:

```razor
                <button class="btn btn-outline-secondary btn-sm" @onclick="ClearAsync">
                    <i class="ri-refresh-line"></i>
                </button>
            </div>
        </div>
```

por:

```razor
                <button class="btn btn-outline-secondary btn-sm" @onclick="ClearAsync">
                    <i class="ri-refresh-line"></i>
                </button>
            </div>
            <div class="btn-group btn-group-sm" role="group" aria-label="@L["Sidebar.Customers"]">
                <button type="button"
                        class="btn @(_scope == ScopePersonal ? "btn-primary" : "btn-outline-primary") customers-scope-personal"
                        aria-pressed="@(_scope == ScopePersonal)"
                        @onclick="() => SetScopeAsync(ScopePersonal)">@L["Team.CustomersScopePersonal"]</button>
                <button type="button"
                        class="btn @(_scope == ScopeTeam ? "btn-primary" : "btn-outline-primary") customers-scope-team"
                        aria-pressed="@(_scope == ScopeTeam)"
                        @onclick="() => SetScopeAsync(ScopeTeam)">@L["Team.CustomersScopeTeam"]</button>
            </div>
        </div>
        <p class="small text-muted px-3 mb-2">@L["Team.ContactOnlyPersonal"]</p>
```

- [ ] **Step 4: El alcance viaja en cada lectura**

Sustituir:

```razor
                <ServerGridAdaptor TValue="CustomerDto"
                                   Http="Http"
                                   Endpoint="@($"{ApiBaseUrl}/customers/grid")"
                                   AccessTokenProvider="GetTokenAsync" />
```

por:

```razor
                <ServerGridAdaptor TValue="CustomerDto"
                                   Http="Http"
                                   Endpoint="@($"{ApiBaseUrl}/customers/grid")"
                                   QueryStringProvider="ScopeQuery"
                                   AccessTokenProvider="GetTokenAsync" />
```

- [ ] **Step 5: Correo vacío como «—»**

Sustituir `                    <Template>@(((CustomerDto)context).Email)</Template>` por `                    <Template>@(((CustomerDto)context).Email ?? "—")</Template>`.

- [ ] **Step 6: Estado del alcance**

Sustituir:

```csharp
    private SfGrid<CustomerDto>? _grid;
    private string? _search;
```

por:

```csharp
    private SfGrid<CustomerDto>? _grid;
    private string? _search;

    // Plan J: alcance de la lista. Por defecto todo el equipo, como antes.
    private const string ScopePersonal = "personal";
    private const string ScopeTeam     = "team";
    private string _scope = ScopeTeam;

    /// <summary>Se añade a la query de cada lectura de la rejilla.</summary>
    private string? ScopeQuery() => $"scope={_scope}";

    private async Task SetScopeAsync(string scope)
    {
        if (_scope == scope) return;
        _scope = scope;
        if (_grid is not null) await _grid.Refresh();
    }
```

Y en la clase `CustomerDto`, sustituir `        public string   Email            { get; set; } = string.Empty;` por `        public string?  Email            { get; set; }`.

- [ ] **Step 7: Compilar las webs**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenterWeb -v q -nologo` y `dotnet build MLMConquerorGlobalEdition.AdminWeb -v q -nologo`
Expected: `0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Team/EnrollmentTeamCustomers.razor \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs
git commit -m "Clientes del equipo: botones Personales y Todo el equipo que viajan en la query de la rejilla, aviso de que el contacto solo es de los personales y correo vacio como guion; textos en los 9 idiomas"
```

---

### Task 18: Índice `MemberProfiles(SponsorMemberId, EnrollDate)`

> **Corrección de Nick (longitud).** `SponsorMemberId` va a **`nvarchar(450)`**, no a `nvarchar(20)` (J22): en el cliente hay patrocinadores de 50 caracteres. La guarda es `LEN > 450`. Las migraciones `IndicePatrocinadorYFechaDeAlta` e `IndiceFechaDeAlta` se rehicieron (BD local revertida, migraciones quitadas, snapshot restaurado y regeneradas en orden). **Aviso abierto:** `MemberProfiles.MemberId` sigue en `nvarchar(20)` (y `MembershipSubscriptions.MemberId`, `MemberStatisticHistories.MemberId` y `WalletApiLogs.MemberId` también en 20); si los ids del cliente miden 50, esas columnas tampoco los admiten. Fuera de este plan: decidirlo con Nick.

`SponsorMemberId` es `nvarchar(max)` (snapshot, `MemberProfile` sin `HasMaxLength`) y no puede ser clave de índice: pasa a `nvarchar(20)`, como `MemberId`. En local mide 10 como mucho.

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs:13-14`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_IndicePatrocinadorYFechaDeAlta.cs` (vía `dotnet ef`, con guarda a mano)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClientesTests.cs`

- [ ] **Step 1: Escribir la prueba de modelo que falla**

En `IndicesDeReportesTests.cs`, añadir `using MLMConquerorGlobalEdition.Domain.Entities.Member;` y sustituir:

```csharp
    // (índices de los bloques siguientes)
```

por:

```csharp
    [Fact]
    public void Perfiles_por_patrocinador_y_fecha_de_alta_con_patrocinador_de_veinte()
    {
        TieneIndice(typeof(MemberProfile), "SponsorMemberId", "EnrollDate").Should().BeTrue();
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(typeof(MemberProfile))!.FindProperty("SponsorMemberId")!
            .GetMaxLength().Should().Be(20);
    }

    // (índices de los bloques siguientes)
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: FAIL.

- [ ] **Step 2: Configuración**

En `MemberProfileConfiguration.cs`, sustituir:

```csharp
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.MemberId).IsUnique();
```

por:

```csharp
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.MemberId).IsUnique();

        // Plan J: el patrocinador es un MemberId (20) y se indexa con la fecha de alta para los
        // clientes personales y la actividad por patrocinador. nvarchar(max) no podía ser clave.
        builder.Property(x => x.SponsorMemberId).HasMaxLength(20);
        builder.HasIndex(x => new { x.SponsorMemberId, x.EnrollDate });
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 3: Crear la migración y añadir la guarda**

Con el comando de la cabecera y el nombre `IndicePatrocinadorYFechaDeAlta`. `Up` debe traer solo un `AlterColumn` de `SponsorMemberId` (`nvarchar(max)` → `nvarchar(20)`, anulable) y el `CreateIndex` de `IX_MemberProfiles_SponsorMemberId_EnrollDate`. Añadir **al principio de `Up`**:

```csharp
        // Plan J (R2): no estrechar la columna si algún valor no cabe; mejor fallar el despliegue
        // que truncar un patrocinador.
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MemberProfiles] WHERE LEN([SponsorMemberId]) > 20)
    THROW 51001, N'MemberProfiles.SponsorMemberId tiene valores de mas de 20 caracteres: revisar antes de migrar (Plan J, Tarea 18).', 1;");
```

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_IndicePatrocinadorYFechaDeAlta.cs`
Expected: `0`

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 4: Aplicar la migración a la base local**

Con el comando de la cabecera. Expected: `Applying migration '…_IndicePatrocinadorYFechaDeAlta'.` y `Done.`

- [ ] **Step 5: Escribir las pruebas contra SQL Server**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>El CASE del contacto y los dos alcances se traducen a SQL; el índice existe.</summary>
public class SqlServerClientesTests
{
    [SqlServerFact]
    public async Task Los_dos_alcances_y_el_contacto_anulado_funcionan_en_SQL()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        var a  = SqlServerReportes.NuevoId();
        var c1 = SqlServerReportes.NuevoId("TC");
        var c2 = SqlServerReportes.NuevoId("TC");
        ReportesDb.Miembro(db, yo);
        ReportesDb.Miembro(db, a, yo);
        ReportesDb.Miembro(db, c1, yo, MemberType.ExternalMember, email: $"{c1}@prueba.test".ToLowerInvariant(), telefono: "111");
        ReportesDb.Miembro(db, c2, a,  MemberType.ExternalMember, email: $"{c2}@prueba.test".ToLowerInvariant(), telefono: "222");
        ReportesDb.Nodo(db, yo, null, ReportesDb.Ruta(yo), 1);
        ReportesDb.Nodo(db, a,  yo,   ReportesDb.Ruta(yo, a), 2);
        ReportesDb.Nodo(db, c1, yo,   ReportesDb.Ruta(yo, c1), 2);
        ReportesDb.Nodo(db, c2, a,    ReportesDb.Ruta(yo, a, c2), 3);
        await db.SaveChangesAsync();
        var servicio = ClientesDelEquipoTests.Servicio(db);

        var equipo     = (await servicio.GetCustomersGridAsync(yo, new GridDataRequest { PageSize = 50 }, CustomerScope.Team)).Items.ToList();
        var personales = (await servicio.GetCustomersGridAsync(yo, new GridDataRequest { PageSize = 50 }, CustomerScope.Personal)).Items.ToList();
        var busqueda   = await servicio.GetCustomersGridAsync(yo, new GridDataRequest { PageSize = 50, Search = c2.ToLowerInvariant() + "@" }, CustomerScope.Team);

        equipo.Should().HaveCount(2);
        equipo.Single(c => c.MemberId == c2).Email.Should().BeNull();
        equipo.Single(c => c.MemberId == c2).Level.Should().Be(2);
        personales.Should().ContainSingle().Which.MemberId.Should().Be(c1);
        busqueda.TotalCount.Should().Be(0, "el correo del cliente ajeno está anulado en la proyección");

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_indice_de_patrocinador_y_fecha_existe()
    {
        await using var db = SqlServerReportes.Db();

        var n = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE name = 'IX_MemberProfiles_SponsorMemberId_EnrollDate'
              AND object_id = OBJECT_ID('MemberProfiles')
            """).SingleAsync();

        n.Should().Be(1, "la migración IndicePatrocinadorYFechaDeAlta tiene que estar aplicada en local");
    }
}
```

- [ ] **Step 6: Ejecutar**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerClientesTests -nologo`
Expected: PASS (2), o `Skipped` sin SQL Server.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndicePatrocinadorYFechaDeAlta.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndicePatrocinadorYFechaDeAlta.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClientesTests.cs
git commit -m "MemberProfiles.SponsorMemberId pasa a nvarchar(20) con indice (SponsorMemberId, EnrollDate); la migracion IndicePatrocinadorYFechaDeAlta se niega a truncar; pruebas contra SQL Server de los dos alcances de clientes y del contacto anulado"
```

---

### Task 19: Guarda de profundidad en el alta (`GENEALOGY_TOO_DEEP`)

> **FUERA (corrección de Nick N1).** No se implementa: sin cambio de `HierarchyPath` no hay límite de 450 que proteger. Se conserva el texto como referencia para la mejora de rendimiento futura.

Antes de poner límite a la columna (Tarea 20) hay que impedir que el alta escriba una ruta que no cabría. Solo escriben rutas de genealogía `SignupAmbassadorHandler.cs:224-235` y `SignupMemberHandler.cs:164-175` (y la semilla de la raíz).

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Tree/GenealogyPath.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs:224-230`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs:164-170`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Domain/GenealogyPathTests.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupMemberHandlerTests.cs` (prueba nueva al final)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupAmbassadorHandlerTests.cs` (prueba nueva al final)

- [ ] **Step 1: Escribir las pruebas que fallan**

`GenealogyPathTests.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Domain;

/// <summary>
/// La ruta de patrocinio mide 1 + 11 por nivel con ids de 10 caracteres: 450 dan 40 niveles. Una
/// ruta que no cabe no se crea (Plan J, J21).
/// </summary>
public class GenealogyPathTests
{
    private static string RutaDe(int niveles) =>
        "/" + string.Concat(Enumerable.Range(1, niveles).Select(i => $"AMB-{i:000000}/"));

    [Fact]
    public void La_raiz_lleva_barra_al_principio_y_al_final()
        => GenealogyPath.Root("AMB-000001").Should().Be("/AMB-000001/");

    [Fact]
    public void El_hijo_cuelga_de_la_ruta_del_padre()
    {
        GenealogyPath.TryChild("/AMB-000001/", "AMB-000002", out var ruta).Should().BeTrue();
        ruta.Should().Be("/AMB-000001/AMB-000002/");
    }

    [Fact]
    public void Caben_cuarenta_niveles_y_el_cuarenta_y_uno_no()
    {
        RutaDe(40).Length.Should().Be(441);
        GenealogyPath.TryChild(RutaDe(39), "AMB-999999", out _).Should().BeTrue();
        GenealogyPath.TryChild(RutaDe(40), "AMB-999999", out _).Should().BeFalse();
    }

    [Fact]
    public void El_limite_es_el_de_la_columna()
        => GenealogyPath.MaxLength.Should().Be(450);
}
```

Al final de la clase `SignupMemberHandlerTests` (antes de su última `}`), añadir:

```csharp
    [Fact]
    public async Task Handle_CuandoElPatrocinadorEstaDemasiadoProfundo_DevuelveGenealogyTooDeepSinCrearNada()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.MemberProfiles.AddAsync(BuildSponsor("AMB-000001", "pedro-site"));
        // 40 niveles: 441 caracteres. El hijo mediría 452 y no cabe en nvarchar(450).
        var ruta = "/" + string.Concat(Enumerable.Range(1, 39).Select(i => $"AMB-{i + 100:000000}/")) + "AMB-000001/";
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId = "AMB-000001", HierarchyPath = ruta, Level = 40,
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new SignupMemberCommand(BuildRequest(sponsorSlug: "pedro-site")), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("GENEALOGY_TOO_DEEP");
        (await db.MemberProfiles.CountAsync()).Should().Be(1, "solo el patrocinador: el alta no creó nada");
        (await db.GenealogyTree.CountAsync()).Should().Be(1);
    }
```

Al final de la clase `SignupAmbassadorHandlerTests` (antes de su última `}`), añadir:

```csharp
    [Fact]
    public async Task Handle_CuandoElPatrocinadorEstaDemasiadoProfundo_DevuelveGenealogyTooDeepSinCrearNada()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.MemberProfiles.AddAsync(BuildSponsor("AMB-000001", "maria-site"));
        var ruta = "/" + string.Concat(Enumerable.Range(1, 39).Select(i => $"AMB-{i + 100:000000}/")) + "AMB-000001/";
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId = "AMB-000001", HierarchyPath = ruta, Level = 40,
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new SignupAmbassadorCommand(BuildRequest(sponsorSlug: "maria-site", slug: "deep-site")), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("GENEALOGY_TOO_DEEP");
        (await db.MemberProfiles.CountAsync()).Should().Be(1);
        (await db.GenealogyTree.CountAsync()).Should().Be(1);
    }
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "GenealogyPathTests|GenealogyTooDeep" -nologo`
Expected: FAIL de compilación (`GenealogyPath` no existe).

- [ ] **Step 3: Crear `GenealogyPath.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

/// <summary>
/// Rutas del árbol de inscripción ("/A/B/C/"). Solo las escribe el alta; GenealogyTree.HierarchyPath
/// es nvarchar(450) con índice (Plan J, J21): con ids de 10 caracteres caben 40 niveles.
/// </summary>
public static class GenealogyPath
{
    public const int MaxLength = 450;

    public static string Root(string memberId) => $"/{memberId}/";

    /// <summary>
    /// La ruta del hijo bajo <paramref name="parentPath"/>. Devuelve false si no cabría en
    /// <see cref="MaxLength"/>: quien llama no debe crear el nodo.
    /// </summary>
    public static bool TryChild(string parentPath, string memberId, out string path)
    {
        path = $"{parentPath}{memberId}/";
        return path.Length <= MaxLength;
    }
}
```

- [ ] **Step 4: Usarla en `SignupAmbassadorHandler.cs`**

Sustituir:

```csharp
        var genealogyNode = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = sponsorMemberId,
            HierarchyPath  = sponsorNode is not null
                ? $"{sponsorNode.HierarchyPath}{memberId}/"
                : $"/{memberId}/",
```

por:

```csharp
        // Plan J: la ruta de patrocinio tiene límite (nvarchar(450), 40 niveles). Si no cabe, el
        // alta se rechaza ANTES de añadir nada al contexto, en vez de romper al guardar.
        var genealogyPath = GenealogyPath.Root(memberId);
        if (sponsorNode is not null && !GenealogyPath.TryChild(sponsorNode.HierarchyPath, memberId, out genealogyPath))
            return Result<SignupResponse>.Failure(
                "GENEALOGY_TOO_DEEP",
                "The sponsor's enrollment tree is too deep to add new members. Please contact support.");

        var genealogyNode = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = sponsorMemberId,
            HierarchyPath  = genealogyPath,
```

- [ ] **Step 5: Usarla en `SignupMemberHandler.cs`**

Exactamente la misma sustitución que en el paso 4 (el bloque de origen es idéntico en las dos clases).

- [ ] **Step 6: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "GenealogyPathTests|SignupMemberHandlerTests|SignupAmbassadorHandlerTests" -nologo`
Expected: PASS (todas, también las anteriores).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Tree/GenealogyPath.cs \
        MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs \
        MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Domain/GenealogyPathTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupMemberHandlerTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupAmbassadorHandlerTests.cs
git commit -m "Alta: la ruta de patrocinio se construye con GenealogyPath y un alta cuya ruta pasaria de 450 caracteres (40 niveles) se rechaza con GENEALOGY_TOO_DEEP antes de crear nada"
```

---

### Task 20: `GenealogyTree.HierarchyPath` a `nvarchar(450)` con índice (migración propia)

> **FUERA (corrección de Nick N1).** `GenealogyTree.HierarchyPath` no se toca. Pasarla a `nvarchar(450)` con índice queda como mejora de rendimiento futura; las consultas siguen con `StartsWith` sobre `nvarchar(max)`. Ninguna prueba de otro bloque puede exigir `IX_GenealogyTree_HierarchyPath`.

**Esta tarea se puede revertir sola** si R1 lo exige: nada del plan depende de ella, solo es más rápido con ella.

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/GenealogyConfiguration.cs:12-14`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceRutaDeGenealogia.cs` (vía `dotnet ef`, con guarda a mano)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerRutaDeGenealogiaTests.cs`

- [ ] **Step 1: Escribir la prueba de modelo que falla**

En `IndicesDeReportesTests.cs`, añadir `using MLMConquerorGlobalEdition.Domain.Entities.Tree;` y sustituir:

```csharp
    // (índices de los bloques siguientes)
```

por:

```csharp
    [Fact]
    public void La_ruta_de_genealogia_es_indexable_y_mide_lo_que_dice_GenealogyPath()
    {
        TieneIndice(typeof(GenealogyEntity), "HierarchyPath").Should().BeTrue();
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(typeof(GenealogyEntity))!.FindProperty("HierarchyPath")!
            .GetMaxLength().Should().Be(GenealogyPath.MaxLength);
    }

    // (índices de los bloques siguientes)
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: FAIL.

- [ ] **Step 2: Configuración**

En `GenealogyConfiguration.cs`, sustituir:

```csharp
        // Unbounded materialized path — see DualTeamConfiguration for why it is not a
        // keyed index (1700-byte nonclustered-index limit vs. deep spillover chains).
        builder.Property(x => x.HierarchyPath).IsRequired();
```

por:

```csharp
        // Plan J (J21): la ruta de PATROCINIO sí se indexa. Mide 1 + 11 por nivel con ids de 10
        // caracteres: 450 dan 40 niveles (900 bytes, bajo el límite de 1700 de un índice). Las altas
        // que no cabrían se rechazan con GENEALOGY_TOO_DEEP (GenealogyPath.TryChild) y la migración
        // IndiceRutaDeGenealogia se niega a truncar. DualTeamTree NO: allí las rutas pasan de 10.000
        // caracteres (ver DualTeamConfiguration).
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(GenealogyPath.MaxLength);
        builder.HasIndex(x => x.HierarchyPath);
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 3: Crear la migración y añadir la guarda**

Con el comando de la cabecera y el nombre `IndiceRutaDeGenealogia`. `Up` debe traer solo el `AlterColumn` de `GenealogyTree.HierarchyPath` (`nvarchar(max)` → `nvarchar(450)`, no anulable) y el `CreateIndex` de `IX_GenealogyTree_HierarchyPath`. Añadir **al principio de `Up`**:

```csharp
        // Plan J (R1): si alguna ruta no cabe, la migración falla en vez de truncarla. Antes de
        // desplegar: SELECT MAX(LEN(HierarchyPath)), MAX(Level) FROM GenealogyTree en producción.
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [GenealogyTree] WHERE LEN([HierarchyPath]) > 450)
    THROW 51002, N'GenealogyTree.HierarchyPath tiene rutas de mas de 450 caracteres: no se puede indexar. Ver Plan J, R1.', 1;");
```

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceRutaDeGenealogia.cs`
Expected: `0`

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 4: Aplicar la migración a la base local**

Antes, comprobar la longitud real:

Run: `sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -W -Q "SET NOCOUNT ON; SELECT MAX(LEN(HierarchyPath)) AS MaxLen, MAX(Level) AS MaxLevel FROM GenealogyTree"`
Expected: `89` y `8` (o en todo caso `MaxLen` ≤ 450).

Después, el comando de aplicar de la cabecera. Expected: `Applying migration '…_IndiceRutaDeGenealogia'.` y `Done.`

- [ ] **Step 5: Comprobar que el prefijo usa el índice**

Crear en el *scratchpad* (fuera del repositorio) un `plan-ruta.sql`:

```sql
SET SHOWPLAN_TEXT ON;
GO
DECLARE @p nvarchar(450) = N'/AMB-700829/AMB-211134/%';
SELECT COUNT(*) FROM [GenealogyTree] WHERE [HierarchyPath] LIKE @p ESCAPE N'\' AND [IsDeleted] = 0;
GO
```

Run: `sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -i <ruta-del-scratchpad>/plan-ruta.sql`
Expected: una línea `Index Seek(OBJECT:([MLMConquerorGlobalEditionDb].[dbo].[GenealogyTree].[IX_GenealogyTree_HierarchyPath])…` (antes del índice era `Clustered Index Scan`). Es la forma en que EF 10 traduce `StartsWith` con parámetro (`LIKE @p ESCAPE N'\'`).

- [ ] **Step 6: Escribir la prueba contra SQL Server**

```csharp
using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class SqlServerRutaDeGenealogiaTests
{
    [SqlServerFact]
    public async Task La_ruta_es_nvarchar_450_con_indice()
    {
        await using var db = SqlServerReportes.Db();

        var bytes = await db.Database.SqlQuery<int>($"""
            SELECT CAST(c.max_length AS int) AS [Value] FROM sys.columns c
            WHERE c.object_id = OBJECT_ID('GenealogyTree') AND c.name = 'HierarchyPath'
            """).SingleAsync();
        var indices = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE name = 'IX_GenealogyTree_HierarchyPath' AND object_id = OBJECT_ID('GenealogyTree')
            """).SingleAsync();

        bytes.Should().Be(900, "nvarchar(450) son 900 bytes; -1 sería nvarchar(max)");
        indices.Should().Be(1);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerRutaDeGenealogiaTests -nologo`
Expected: PASS (o `Skipped` sin SQL Server).

- [ ] **Step 7: Commit (solo esta tarea, para poder revertirla sola)**

```bash
git add MLMConquerorGlobalEdition.Repository/Configurations/GenealogyConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceRutaDeGenealogia.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceRutaDeGenealogia.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerRutaDeGenealogiaTests.cs
git commit -m "GenealogyTree.HierarchyPath pasa a nvarchar(450) con indice para que el prefijo del equipo de inscripcion sea un seek; la migracion IndiceRutaDeGenealogia falla si alguna ruta no cabe; condicion de despliegue: medir la ruta mas larga en produccion"
```

**Fin del Bloque 3.** Dos migraciones (`IndicePatrocinadorYFechaDeAlta` e `IndiceRutaDeGenealogia`); la segunda va en su propio commit y se puede dejar fuera del despliegue (R1).

---

# Bloque 4 — Actividad por fechas

### Task 21: Rango de fechas común (`ReportDateRange`)

> **Corrección de Nick N4 (solo Actividad).** `ReportDateRange` conserva la regla de J12 (366 días, `REPORT_RANGE_INVALID`/`REPORT_RANGE_TOO_LONG`) para *Cambios de estado*. Se añade `TryCreateForActivity(from, to, now)`, que aplica además `ActivityLookbackMonths = 3`: inicio no anterior a `hoy − 3 meses` (`REPORT_RANGE_TOO_OLD`) y fin no posterior a hoy (`REPORT_RANGE_IN_FUTURE`); `hoy − 3 meses` exacto vale y un día antes no. `SetupFor(now)` devuelve también `ActivityMinFrom`.

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/ReportDateRange.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportDateRangeTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class ReportDateRangeTests
{
    [Fact]
    public void Un_solo_dia_va_de_su_medianoche_a_la_siguiente_e_ignora_la_hora()
    {
        ReportDateRange.TryCreate(new DateTime(2026, 3, 5, 18, 0, 0), new DateTime(2026, 3, 5, 9, 0, 0), out var r, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        r.Start.Should().Be(new DateTime(2026, 3, 5));
        r.End.Should().Be(new DateTime(2026, 3, 6));
    }

    [Fact]
    public void Hasta_antes_que_desde_no_vale()
    {
        ReportDateRange.TryCreate(new DateTime(2026, 3, 5), new DateTime(2026, 3, 4), out _, out var error).Should().BeFalse();
        error.Should().Be(ReportDateRange.InvalidCode);
    }

    [Fact]
    public void Sin_fechas_no_vale()
    {
        ReportDateRange.TryCreate(default, new DateTime(2026, 3, 4), out _, out var error).Should().BeFalse();
        error.Should().Be(ReportDateRange.InvalidCode);
    }

    [Fact]
    public void Caben_366_dias_y_no_367()
    {
        ReportDateRange.TryCreate(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), out _, out _).Should().BeTrue();
        ReportDateRange.TryCreate(new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), out _, out var error).Should().BeFalse();
        error.Should().Be(ReportDateRange.TooLongCode);
    }

    [Fact]
    public void Por_defecto_del_dia_uno_del_mes_a_hoy_segun_el_servidor()
    {
        var setup = ReportDateRange.SetupFor(new DateTime(2026, 10, 16, 15, 30, 0));

        setup.DefaultFrom.Should().Be(new DateTime(2026, 10, 1));
        setup.DefaultTo.Should().Be(new DateTime(2026, 10, 16));
        setup.MaxRangeDays.Should().Be(366);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter ReportDateRangeTests -nologo`
Expected: FAIL de compilación (`ReportDateRange` no existe).

- [ ] **Step 3: Crear `ReportDateRange.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Rango de fechas de los reportes del equipo (Plan J, J12): los dos extremos son días inclusivos y
/// se convierten a [desde, hasta + 1 día). Como mucho <see cref="MaxDays"/> días, que es lo que
/// acota el trabajo de las consultas por fecha.
/// </summary>
public readonly record struct ReportDateRange(DateTime Start, DateTime End)
{
    public const int    MaxDays     = 366;
    public const string InvalidCode = "REPORT_RANGE_INVALID";
    public const string TooLongCode = "REPORT_RANGE_TOO_LONG";

    public static bool TryCreate(DateTime from, DateTime to, out ReportDateRange range, out string? errorCode)
    {
        range     = default;
        errorCode = null;

        if (from == default || to == default)
        {
            errorCode = InvalidCode;
            return false;
        }

        var start = from.Date;
        var end   = to.Date.AddDays(1);

        if (end <= start)
        {
            errorCode = InvalidCode;
            return false;
        }

        if ((end - start).TotalDays > MaxDays)
        {
            errorCode = TooLongCode;
            return false;
        }

        range = new ReportDateRange(start, end);
        return true;
    }

    /// <summary>Valores por defecto según la hora del SERVIDOR: del día 1 del mes en curso a hoy.</summary>
    public static ReportSetupView SetupFor(DateTime now) => new()
    {
        DefaultFrom  = new DateTime(now.Year, now.Month, 1),
        DefaultTo    = now.Date,
        MaxRangeDays = MaxDays
    };
}

/// <summary>Lo que necesita la pantalla para pintar el filtro de fechas sin mirar el reloj del navegador.</summary>
public sealed class ReportSetupView
{
    public DateTime DefaultFrom  { get; set; }
    public DateTime DefaultTo    { get; set; }
    public int      MaxRangeDays { get; set; }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter ReportDateRangeTests -nologo`
Expected: PASS (5 pruebas).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Teams/ReportDateRange.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportDateRangeTests.cs
git commit -m "Reportes del equipo: rango de fechas comun con dias inclusivos, maximo 366 dias, codigos REPORT_RANGE_INVALID y REPORT_RANGE_TOO_LONG y valores por defecto del reloj del servidor"
```

---

### Task 22: Servicio de actividad por fechas

> **Corrección de Nick N2 y N4.** La columna de nivel sale de `OrderMembershipLevelQueries` (nivel más alto de los productos de la **orden de alta** del inscrito: su primera orden con productos de membresía, por `OrderDate` y desempate por `CreationDate`), no de la primera suscripción; sin esa orden, «Otros». Para no hacer un `Contains` de miles de ids, se añade a `OrderMembershipLevelQueries` una variante por lote que recibe un `IQueryable<string>` de ids de orden y agrupa con `g.Max()` en SQL. La prueba `Un_rango_de_mas_de_366_dias_se_rechaza` pasa a ser la de N4 (inicio anterior a hoy − 3 meses y fin en el futuro); el servicio recibe `IDateTimeProvider` y llama a `ReportDateRange.TryCreateForActivity`.

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityDtos.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/IEnrollmentActivityService.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityService.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/EnrollmentActivityServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Una fila por miembro del equipo que inscribió a alguien en el rango, con cuántos embajadores y
/// clientes, cuántos de cada nivel (el de la PRIMERA suscripción del inscrito) y sus puntos de
/// equipo de inscripción actuales.
/// </summary>
public class EnrollmentActivityServiceTests
{
    private const string Yo = "AMB-000001";
    private const string A  = "AMB-000002";
    private static readonly DateTime Desde = new(2026, 3, 1);
    private static readonly DateTime Hasta = new(2026, 3, 31);

    /// <summary>
    /// YO (500 pts) inscribe en marzo a N1 (embajador; Lifestyle y luego Elite) y N2 (cliente, VIP);
    /// en abril a N3. A (40 pts, de YO) inscribe a N4 (cliente, Turbo) y N5 (embajador sin
    /// suscripción). OTRO, fuera del equipo, inscribe a N6.
    /// </summary>
    internal static async Task<AppDbContext> EquipoAsync(AppDbContext db, string yo, string a, string otro, string prefijo)
    {
        ReportesDb.Miembro(db, yo);
        ReportesDb.Miembro(db, a, yo);
        ReportesDb.Miembro(db, otro);
        ReportesDb.Nodo(db, yo,   null, ReportesDb.Ruta(yo), 1);
        ReportesDb.Nodo(db, a,    yo,   ReportesDb.Ruta(yo, a), 2);
        ReportesDb.Nodo(db, otro, null, ReportesDb.Ruta(otro), 1);
        ReportesDb.Puntos(db, yo, 500);
        ReportesDb.Puntos(db, a, 40);

        ReportesDb.Miembro(db, $"{prefijo}1", yo, alta: new DateTime(2026, 3, 5));
        ReportesDb.Suscripcion(db, $"{prefijo}1", 1, MembershipStatus.Expired, new DateTime(2026, 3, 5));
        ReportesDb.Suscripcion(db, $"{prefijo}1", 3, MembershipStatus.Active,  new DateTime(2026, 3, 25));
        ReportesDb.Miembro(db, $"{prefijo}2", yo, MemberType.ExternalMember, alta: new DateTime(2026, 3, 31, 23, 0, 0));
        ReportesDb.Suscripcion(db, $"{prefijo}2", 2, MembershipStatus.Active, new DateTime(2026, 3, 31));
        ReportesDb.Miembro(db, $"{prefijo}3", yo, alta: new DateTime(2026, 4, 1));
        ReportesDb.Miembro(db, $"{prefijo}4", a, MemberType.ExternalMember, alta: new DateTime(2026, 3, 20));
        ReportesDb.Suscripcion(db, $"{prefijo}4", 4, MembershipStatus.Active, new DateTime(2026, 3, 20));
        ReportesDb.Miembro(db, $"{prefijo}5", a, alta: new DateTime(2026, 3, 21));
        ReportesDb.Miembro(db, $"{prefijo}6", otro, alta: new DateTime(2026, 3, 15));
        await db.SaveChangesAsync();
        return db;
    }

    private static Task<AppDbContext> EquipoAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        return EquipoAsync(db, Yo, A, "AMB-000099", "AMB-90000");
    }

    private static GridDataRequest Pagina(string? buscar = null) => new() { Page = 1, PageSize = 50, Search = buscar };

    [Fact]
    public async Task Cuenta_por_patrocinador_tipo_y_nivel_de_la_primera_suscripcion()
    {
        await using var db = await EquipoAsync();

        var r = await new EnrollmentActivityService(db).GetGridAsync(Yo, Desde, Hasta, Pagina());

        r.IsSuccess.Should().BeTrue();
        var filas = r.Value!.Items.ToDictionary(f => f.MemberId);
        filas.Keys.Should().BeEquivalentTo(new[] { Yo, A });

        var yo = filas[Yo];
        (yo.Level, yo.Ambassadors, yo.Customers, yo.Level1, yo.Level2, yo.Level3, yo.Level4, yo.OtherLevel, yo.EnrollmentTeamPoints)
            .Should().Be((0, 1, 1, 1, 1, 0, 0, 0, 500));

        var a = filas[A];
        (a.Level, a.Ambassadors, a.Customers, a.Level1, a.Level2, a.Level3, a.Level4, a.OtherLevel, a.EnrollmentTeamPoints)
            .Should().Be((1, 1, 1, 0, 0, 0, 1, 1, 40));
    }

    [Fact]
    public async Task Fuera_del_rango_o_del_equipo_no_cuenta()
    {
        await using var db = await EquipoAsync();

        var r = await new EnrollmentActivityService(db).GetGridAsync(Yo, Desde, Hasta, Pagina());

        r.Value!.Items.Sum(f => f.Ambassadors + f.Customers).Should().Be(4, "N3 es de abril y N6 de otro equipo");
    }

    [Fact]
    public async Task Busca_por_nombre_y_ordena_por_cualquier_columna()
    {
        await using var db = await EquipoAsync();
        var pagina = Pagina(A.ToLowerInvariant());
        var porNivel4 = Pagina();
        porNivel4.Sorts.Add(new GridSort { Field = nameof(EnrollmentActivityRowView.Level4), Direction = "desc" });

        (await new EnrollmentActivityService(db).GetGridAsync(Yo, Desde, Hasta, pagina))
            .Value!.Items.Should().ContainSingle().Which.MemberId.Should().Be(A);
        (await new EnrollmentActivityService(db).GetGridAsync(Yo, Desde, Hasta, porNivel4))
            .Value!.Items.First().MemberId.Should().Be(A);
    }

    [Fact]
    public async Task Un_rango_de_mas_de_366_dias_se_rechaza()
    {
        await using var db = await EquipoAsync();

        var r = await new EnrollmentActivityService(db).GetGridAsync(Yo, new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), Pagina());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(ReportDateRange.TooLongCode);
    }

    [Fact]
    public async Task Sin_nodo_de_genealogia_no_hay_filas()
    {
        await using var db = await EquipoAsync();

        var r = await new EnrollmentActivityService(db).GetGridAsync("AMB-555555", Desde, Hasta, Pagina());

        r.IsSuccess.Should().BeTrue();
        r.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Las_columnas_son_los_niveles_1_a_4_con_su_nombre()
    {
        await using var db = await EquipoAsync();

        var columnas = await new EnrollmentActivityService(db).GetColumnsAsync();

        columnas.Select(c => (c.LevelId, c.Name)).Should().Equal(
            (1, "Lifestyle Ambassador"), (2, "Travel Advantage - VIP"),
            (3, "Travel Advantage - Elite"), (4, "Travel Advantage - Turbo"));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter EnrollmentActivityServiceTests -nologo`
Expected: FAIL de compilación (`EnrollmentActivityService` no existe).

- [ ] **Step 3: Crear `EnrollmentActivityDtos.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Lo que inscribió un miembro del equipo en el rango. Los niveles son los ids reales de
/// MembershipLevels (1 Lifestyle Ambassador, 2 VIP, 3 Elite, 4 Turbo); "Plus" no existe aquí y
/// cualquier otro nivel, o ninguno, va a <see cref="OtherLevel"/> (Plan J, J10).
/// </summary>
public sealed class EnrollmentActivityRowView
{
    public string MemberId             { get; set; } = string.Empty;
    public string FullName             { get; set; } = string.Empty;
    /// <summary>Nivel relativo al que mira (0 = él mismo).</summary>
    public int    Level                { get; set; }
    public int    Ambassadors          { get; set; }
    public int    Customers            { get; set; }
    public int    Level1               { get; set; }
    public int    Level2               { get; set; }
    public int    Level3               { get; set; }
    public int    Level4               { get; set; }
    public int    OtherLevel           { get; set; }
    /// <summary>MemberStatistics.EnrollmentPoints actuales, como el legado.</summary>
    public int    EnrollmentTeamPoints { get; set; }
}

/// <summary>Cabecera de una columna de nivel.</summary>
public sealed class EnrollmentActivityColumnView
{
    public int    LevelId { get; set; }
    public string Name    { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Crear `IEnrollmentActivityService.cs`**

```csharp
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Actividad por fechas del equipo de inscripción (Plan J, bloque 4).</summary>
public interface IEnrollmentActivityService
{
    Task<List<EnrollmentActivityColumnView>> GetColumnsAsync(CancellationToken ct = default);

    /// <summary>
    /// <paramref name="from"/> y <paramref name="to"/> son días inclusivos (<see cref="ReportDateRange"/>).
    /// Falla con REPORT_RANGE_INVALID o REPORT_RANGE_TOO_LONG.
    /// </summary>
    Task<Result<PagedResult<EnrollmentActivityRowView>>> GetGridAsync(
        string memberId, DateTime from, DateTime to, GridDataRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 5: Crear `EnrollmentActivityService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
/// <remarks>
/// En SQL: las altas del rango cuyo patrocinador está en el equipo (prefijo de genealogía del que
/// mira, él incluido) y la primera suscripción de cada alta, unida por JOIN y no por Contains. En
/// memoria: el pivote por tipo y nivel, la búsqueda, el orden y la página (R5: el rango de 366 días
/// como mucho acota cuántas altas se traen).
/// </remarks>
public sealed class EnrollmentActivityService : IEnrollmentActivityService
{
    public static readonly int[] LevelColumns = { 1, 2, 3, 4 };

    private static readonly string[] SearchableFields =
    {
        nameof(EnrollmentActivityRowView.MemberId),
        nameof(EnrollmentActivityRowView.FullName)
    };

    private readonly AppDbContext _db;

    public EnrollmentActivityService(AppDbContext db) => _db = db;

    public async Task<List<EnrollmentActivityColumnView>> GetColumnsAsync(CancellationToken ct = default)
    {
        var names = await _db.MembershipLevels.AsNoTracking()
            .Where(l => LevelColumns.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        return LevelColumns
            .Select(id => new EnrollmentActivityColumnView { LevelId = id, Name = names.GetValueOrDefault(id, $"#{id}") })
            .ToList();
    }

    public async Task<Result<PagedResult<EnrollmentActivityRowView>>> GetGridAsync(
        string memberId, DateTime from, DateTime to, GridDataRequest request, CancellationToken ct = default)
    {
        if (!ReportDateRange.TryCreate(from, to, out var range, out var error))
            return Result<PagedResult<EnrollmentActivityRowView>>.Failure(error!, "Invalid date range.");

        // El nodo más reciente del que mira: un miembro puede tener dos (R8).
        var me = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => new { g.HierarchyPath, g.Level })
            .FirstOrDefaultAsync(ct);
        if (me is null) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));

        var start  = range.Start;
        var end    = range.End;
        var prefix = me.HierarchyPath;

        var enrolled =
            from m in _db.MemberProfiles.AsNoTracking()
            where m.EnrollDate >= start && m.EnrollDate < end && m.SponsorMemberId != null
            join s in _db.GenealogyTree.AsNoTracking() on m.SponsorMemberId equals s.MemberId
            where s.HierarchyPath.StartsWith(prefix)
            select new { m.MemberId, SponsorMemberId = m.SponsorMemberId!, m.MemberType, SponsorLevel = s.Level };

        var rows = (await enrolled.ToListAsync(ct)).DistinctBy(r => r.MemberId).ToList();
        if (rows.Count == 0) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));

        // Nivel de la PRIMERA suscripción de cada inscrito (J10): una subida posterior no lo mueve.
        var firstLevel = (await (
                from sub in _db.MembershipSubscriptions.AsNoTracking()
                join e in enrolled on sub.MemberId equals e.MemberId
                select new { sub.MemberId, sub.MembershipLevelId, sub.StartDate, sub.CreationDate })
            .ToListAsync(ct))
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key,
                          g => g.OrderBy(x => x.StartDate).ThenBy(x => x.CreationDate).First().MembershipLevelId);

        var sponsorIds = enrolled.Select(e => e.SponsorMemberId).Distinct();

        var names = await (
                from p in _db.MemberProfiles.AsNoTracking()
                join id in sponsorIds on p.MemberId equals id
                select new { p.MemberId, FullName = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.FullName, ct);

        var points = (await (
                from st in _db.MemberStatistics.AsNoTracking()
                join id in sponsorIds on st.MemberId equals id
                select new { st.MemberId, st.EnrollmentPoints })
            .ToListAsync(ct))
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.First().EnrollmentPoints);

        var result = rows
            .GroupBy(r => r.SponsorMemberId)
            .Select(g =>
            {
                var row = new EnrollmentActivityRowView
                {
                    MemberId             = g.Key,
                    FullName             = names.GetValueOrDefault(g.Key, g.Key),
                    Level                = g.Min(x => x.SponsorLevel) - me.Level,
                    EnrollmentTeamPoints = points.GetValueOrDefault(g.Key)
                };

                foreach (var e in g)
                {
                    if (e.MemberType == MemberType.ExternalMember) row.Customers++;
                    else row.Ambassadors++;

                    switch (firstLevel.TryGetValue(e.MemberId, out var level) ? level : 0)
                    {
                        case 1:  row.Level1++;     break;
                        case 2:  row.Level2++;     break;
                        case 3:  row.Level3++;     break;
                        case 4:  row.Level4++;     break;
                        default: row.OtherLevel++; break;
                    }
                }

                return row;
            })
            .ToList();

        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(EnrollmentActivityRowView.Ambassadors), Direction = "desc" });

        return Result<PagedResult<EnrollmentActivityRowView>>.Success(result.ToGridResult(request, SearchableFields));
    }

    private static PagedResult<EnrollmentActivityRowView> Empty(GridDataRequest request) => new()
    {
        Page     = Math.Max(1, request.Page),
        PageSize = request.PageSize <= 0 ? 20 : request.PageSize
    };
}
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter EnrollmentActivityServiceTests -nologo`
Expected: PASS (6 pruebas).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityDtos.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/IEnrollmentActivityService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/EnrollmentActivityServiceTests.cs
git commit -m "Actividad por fechas: por cada miembro del equipo de inscripcion que inscribio en el rango, embajadores, clientes, columnas por los niveles reales 1 a 4 segun la primera suscripcion, Otros y puntos de inscripcion actuales; rango de 366 dias como mucho"
```

---

### Task 23: Índice `MemberProfiles(EnrollDate)`

*Actividad* filtra por fecha de alta sin un patrocinador concreto: el índice compuesto de la Tarea 18 empieza por el patrocinador y no sirve para eso (J22).

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceFechaDeAlta.cs` (vía `dotnet ef`)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

En `IndicesDeReportesTests.cs`, sustituir:

```csharp
    // (índices de los bloques siguientes)
```

por:

```csharp
    [Fact]
    public void Perfiles_por_fecha_de_alta()
        => TieneIndice(typeof(MemberProfile), "EnrollDate").Should().BeTrue();

    // (índices de los bloques siguientes)
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: FAIL.

- [ ] **Step 2: Configuración**

En `MemberProfileConfiguration.cs`, sustituir:

```csharp
        builder.HasIndex(x => new { x.SponsorMemberId, x.EnrollDate });
```

por:

```csharp
        builder.HasIndex(x => new { x.SponsorMemberId, x.EnrollDate });
        // Plan J: la actividad por fechas reduce primero por fecha de alta, sin patrocinador concreto.
        builder.HasIndex(x => x.EnrollDate);
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 3: Crear la migración**

Con el comando de la cabecera y el nombre `IndiceFechaDeAlta`. `Up` debe traer solo `CreateIndex` de `IX_MemberProfiles_EnrollDate`.

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceFechaDeAlta.cs`
Expected: `0`

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceFechaDeAlta.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_IndiceFechaDeAlta.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs
git commit -m "Indice de MemberProfiles por fecha de alta para la actividad por fechas; migracion IndiceFechaDeAlta"
```

- [ ] **Step 5: Aplicar la migración a la base local**

Con el comando de la cabecera. Expected: `Applying migration '…_IndiceFechaDeAlta'.` y `Done.`

---

### Task 24: Endpoints de actividad (`TeamReportsController`)

> **Corrección de Nick N4.** `GET team/reports/setup` devuelve `DefaultFrom`, `DefaultTo`, `MaxRangeDays` (para *Cambios de estado*) y `ActivityMinFrom`; los errores de rango de *Actividad* son los de N4.

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter/Controllers/TeamReportsController.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (registro)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamReportsControllerTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class TeamReportsControllerTests
{
    private static readonly DateTime Desde = new(2026, 3, 1);
    private static readonly DateTime Hasta = new(2026, 3, 31);

    internal static TeamReportsController Control(string memberId = "AMB-000777")
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns(memberId);
        return new TeamReportsController(usuario.Object, new RelojFijo(new DateTime(2026, 10, 16, 15, 0, 0)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public void El_setup_sale_del_reloj_del_servidor()
    {
        var r = Control().GetSetup().Should().BeOfType<OkObjectResult>().Subject;

        var setup = ((ApiResponse<ReportSetupView>)r.Value!).Data!;
        setup.DefaultFrom.Should().Be(new DateTime(2026, 10, 1));
        setup.DefaultTo.Should().Be(new DateTime(2026, 10, 16));
    }

    [Fact]
    public async Task La_actividad_es_la_del_miembro_del_token()
    {
        var actividad = new Mock<IEnrollmentActivityService>();
        actividad.Setup(s => s.GetGridAsync("AMB-000777", Desde, Hasta, It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PagedResult<EnrollmentActivityRowView>>.Success(new PagedResult<EnrollmentActivityRowView>()));

        var r = await Control().GetActivityGrid(new GridDataRequest(), Desde, Hasta, actividad.Object, CancellationToken.None);

        r.Should().BeOfType<OkObjectResult>();
        actividad.VerifyAll();
    }

    [Fact]
    public async Task Un_rango_invalido_es_400_con_su_codigo()
    {
        var actividad = new Mock<IEnrollmentActivityService>();
        actividad.Setup(s => s.GetGridAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PagedResult<EnrollmentActivityRowView>>.Failure(ReportDateRange.TooLongCode, "Invalid date range."));

        var r = await Control().GetActivityGrid(new GridDataRequest(), Desde, Hasta, actividad.Object, CancellationToken.None);

        var bad = r.Should().BeOfType<BadRequestObjectResult>().Subject;
        ((ApiResponse<PagedResult<EnrollmentActivityRowView>>)bad.Value!).ErrorCode.Should().Be(ReportDateRange.TooLongCode);
    }
}
```

Y en `MiembroDelTokenTests.cs`, sustituir:

```csharp
        // (acciones de los bloques siguientes)
```

por:

```csharp
        (typeof(TeamReportsController), nameof(TeamReportsController.GetSetup)),
        (typeof(TeamReportsController), nameof(TeamReportsController.GetActivityColumns)),
        (typeof(TeamReportsController), nameof(TeamReportsController.GetActivityGrid)),
        // (acciones de los bloques siguientes)
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "TeamReportsControllerTests|MiembroDelTokenTests" -nologo`
Expected: FAIL de compilación (`TeamReportsController` no existe).

- [ ] **Step 3: Crear `TeamReportsController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// Reportes del equipo del Plan J (actividad por fechas y, en el bloque 5, cambios de estado).
/// Ninguna ruta lleva sujeto: el miembro sale SIEMPRE del token (ICurrentUserService.MemberId) y
/// el alcance lo pone el servicio (prefijo de genealogía o del árbol binario). Comparte el prefijo
/// de ruta con <see cref="TeamController"/> sin pisar ninguna de sus rutas.
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/team")]
[Authorize]
public class TeamReportsController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public TeamReportsController(ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _currentUser = currentUser;
        _clock       = clock;
    }

    /// <summary>GET /api/v1/bizcenter/team/reports/setup — rango por defecto y máximo, con la hora del servidor.</summary>
    [HttpGet("reports/setup")]
    public IActionResult GetSetup() =>
        Ok(ApiResponse<ReportSetupView>.Ok(ReportDateRange.SetupFor(_clock.Now)));

    /// <summary>GET /api/v1/bizcenter/team/enrollment/activity/columns — nombres de los niveles 1 a 4.</summary>
    [HttpGet("enrollment/activity/columns")]
    public async Task<IActionResult> GetActivityColumns(
        [FromServices] IEnrollmentActivityService activity, CancellationToken ct = default) =>
        Ok(ApiResponse<List<EnrollmentActivityColumnView>>.Ok(await activity.GetColumnsAsync(ct)));

    /// <summary>POST /api/v1/bizcenter/team/enrollment/activity/grid?from=yyyy-MM-dd&amp;to=yyyy-MM-dd</summary>
    /// <remarks>POST por el cuerpo de la rejilla, pero no escribe: ReadOnlySafe, como las de TeamController.</remarks>
    [HttpPost("enrollment/activity/grid")]
    [ReadOnlySafe]
    public async Task<IActionResult> GetActivityGrid(
        [FromBody]  GridDataRequest request,
        [FromQuery] DateTime        from,
        [FromQuery] DateTime        to,
        [FromServices] IEnrollmentActivityService activity,
        CancellationToken ct = default)
    {
        var result = await activity.GetGridAsync(_currentUser.MemberId, from, to, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<EnrollmentActivityRowView>>.Ok(result.Value!))
            : Invalid(result);
    }

    private IActionResult Invalid<T>(Result<T> result) =>
        BadRequest(ApiResponse<T>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
}
```

- [ ] **Step 4: Registro**

En `BizCenter/Program.cs`, sustituir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.IDailyResidualReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.DailyResidualReportService>();
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.IDailyResidualReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.DailyResidualReportService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IEnrollmentActivityService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.EnrollmentActivityService>();
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "TeamReportsControllerTests|MiembroDelTokenTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.BizCenter -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Controllers/TeamReportsController.cs \
        MLMConquerorGlobalEdition.BizCenter/Program.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamReportsControllerTests.cs
git commit -m "Actividad por fechas: GET team/reports/setup con la hora del servidor, GET team/enrollment/activity/columns y POST team/enrollment/activity/grid con el miembro del token y 400 con el codigo del rango"
```

---

### Task 25: Pantalla de actividad, página, menú y textos

> **Corrección de Nick N4.** La pantalla de *Actividad* limita el selector de fechas a `[ActivityMinFrom, hoy]` con los valores del *setup*; `ReportRangeCheck` tiene dos comprobaciones: la de *Actividad* (`Reports.RangeTooOld`, `Reports.RangeInFuture`) y la común de 366 días (`Reports.RangeTooLong`), que usará *Cambios de estado*.

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Team/ReportRangeCheck.cs`
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Team/EnrollmentTeamActivity.razor`
- Create: `MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/EnrollmentActivity.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor:35,47-49`
- Modify: los nueve `SharedResources*.resx`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportRangeCheckTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

`ReportRangeCheckTests.cs`:

```csharp
using MLMConquerorGlobalEdition.SharedComponents.Components.Team;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>La pantalla avisa antes de pedir un rango que el servidor rechazaría.</summary>
public class ReportRangeCheckTests
{
    [Fact]
    public void Un_rango_valido_no_tiene_problema()
        => ReportRangeCheck.Problem(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), 366).Should().BeNull();

    [Fact]
    public void Hasta_antes_que_desde()
        => ReportRangeCheck.Problem(new DateTime(2026, 3, 2), new DateTime(2026, 3, 1), 366)
            .Should().Be(("Reports.RangeInvalid", (object?)null));

    [Fact]
    public void Mas_dias_de_los_permitidos()
        => ReportRangeCheck.Problem(new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), 366)
            .Should().Be(("Reports.RangeTooLong", (object?)366));
}
```

Y en `TextosDeReportesTests.cs`, sustituir:

```csharp
        // (claves de los bloques siguientes)
```

por:

```csharp
        // Bloque 4 — Actividad por fechas
        "Sidebar.Activity", "PageTitle.EnrollmentActivity", "Activity.OtherLevel",
        "Reports.RangeInvalid", "Reports.RangeTooLong", "Reports.Apply",
        // (claves de los bloques siguientes)
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "ReportRangeCheckTests|TextosDeReportesTests" -nologo`
Expected: FAIL (compilación de `ReportRangeCheck` y claves que faltan).

- [ ] **Step 3: Crear `ReportRangeCheck.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SharedComponents.Components.Team;

/// <summary>
/// La misma regla que ReportDateRange del servidor (días inclusivos, tope de días), para avisar en
/// la pantalla sin hacer la petición. El servidor la vuelve a comprobar siempre.
/// </summary>
public static class ReportRangeCheck
{
    /// <summary>null si el rango vale; si no, la clave de texto y su argumento.</summary>
    public static (string Key, object? Arg)? Problem(DateTime from, DateTime to, int maxDays)
    {
        if (to.Date < from.Date) return ("Reports.RangeInvalid", null);
        if ((to.Date - from.Date).TotalDays + 1 > maxDays) return ("Reports.RangeTooLong", maxDays);
        return null;
    }
}
```

- [ ] **Step 4: Añadir las claves a los nueve `.resx`**

| Clave | en | es | pt | fr | it | de | ko | zh | ka |
|---|---|---|---|---|---|---|---|---|---|
| `Sidebar.Activity` | Activity | Actividad | Atividade | Activité | Attività | Aktivität | 활동 | 注册动态 | აქტივობა |
| `PageTitle.EnrollmentActivity` | Enrollment activity | Actividad de inscripción | Atividade de inscrição | Activité d'inscription | Attività di iscrizione | Anmeldeaktivität | 등록 활동 | 注册动态 | რეგისტრაციის აქტივობა |
| `Activity.OtherLevel` | Other | Otros | Outros | Autres | Altri | Andere | 기타 | 其他 | სხვა |
| `Reports.RangeInvalid` | The end date must be on or after the start date. | La fecha final tiene que ser igual o posterior a la inicial. | A data final deve ser igual ou posterior à inicial. | La date de fin doit être égale ou postérieure à la date de début. | La data finale deve essere uguale o successiva a quella iniziale. | Das Enddatum muss am oder nach dem Startdatum liegen. | 종료일은 시작일과 같거나 이후여야 합니다. | 结束日期必须等于或晚于开始日期。 | დასრულების თარიღი უნდა იყოს დაწყების თარიღის ტოლი ან მის შემდეგ. |
| `Reports.RangeTooLong` | The date range can't exceed {0} days. | El rango de fechas no puede superar {0} días. | O intervalo de datas não pode exceder {0} dias. | La période ne peut pas dépasser {0} jours. | L'intervallo di date non può superare {0} giorni. | Der Zeitraum darf {0} Tage nicht überschreiten. | 기간은 {0}일을 초과할 수 없습니다. | 日期范围不能超过 {0} 天。 | თარიღების დიაპაზონი არ უნდა აღემატებოდეს {0} დღეს. |
| `Reports.Apply` | Apply | Aplicar | Aplicar | Appliquer | Applica | Anwenden | 적용 | 应用 | გამოყენება |

- [ ] **Step 5: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "ReportRangeCheckTests|TextosDeReportesTests" -nologo`
Expected: PASS.

- [ ] **Step 6: Crear `EnrollmentTeamActivity.razor`**

```razor
@inject HttpClient Http
@inject IStringLocalizer<SharedResources> L

@using System.Globalization
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.Extensions.Localization
@using MLMConquerorGlobalEdition.SharedComponents.Resources
@using MLMConquerorGlobalEdition.SharedComponents.Components.Grid
@using Syncfusion.Blazor
@using Syncfusion.Blazor.Grids
@using Syncfusion.Blazor.Data

@*
    Actividad por fechas del equipo de inscripción (Plan J). Componente único: la ruta del API
    entra por parámetro. El rango por defecto, el tope de días y los nombres de las columnas de
    nivel los da el servidor; aquí solo se avisa antes de pedir un rango que se rechazaría.
*@

@if (ShowBreadcrumb) { <BreadcrumbNav Items="@_breadcrumb" /> }

<div class="card bg-white border-0 rounded-3 mb-4 enrollment-activity">
    <div class="card-body p-0">
        <div class="d-flex align-items-end flex-wrap gap-2 p-3">
            <div>
                <label class="form-label fs-12 mb-1 fw-medium" for="act-from">@L["Common.From"]</label>
                <input id="act-from" type="date" class="form-control form-control-sm" @bind="_from" />
            </div>
            <div>
                <label class="form-label fs-12 mb-1 fw-medium" for="act-to">@L["Common.To"]</label>
                <input id="act-to" type="date" class="form-control form-control-sm" @bind="_to" />
            </div>
            <button class="btn btn-primary btn-sm" disabled="@(!_ready)" @onclick="ApplyAsync">@L["Reports.Apply"]</button>
        </div>

        @if (_message is not null)
        {
            <div class="alert alert-warning mx-3">@_message</div>
        }

        @if (_ready)
        {
            <SfGrid TValue="ActivityDto" @ref="_grid"
                    AllowPaging="true" AllowSorting="true" AllowResizing="true"
                    AllowFiltering="true" GridLines="GridLine.Horizontal">
                <SfDataManager Adaptor="Adaptors.CustomAdaptor">
                    <ServerGridAdaptor TValue="ActivityDto"
                                       Http="Http"
                                       Endpoint="@($"{ApiBaseUrl}/enrollment/activity/grid")"
                                       QueryStringProvider="RangeQuery"
                                       AccessTokenProvider="GetTokenAsync" />
                </SfDataManager>
                <GridPageSettings PageSize="20" PageSizes="@(new int[]{20,50,100})" />
                <GridFilterSettings Type="Syncfusion.Blazor.Grids.FilterType.Excel" />
                <GridColumns>
                    <GridColumn Field="@nameof(ActivityDto.FullName)" HeaderText="@L["Reports.Name"]" Width="180" Type="ColumnType.String" />
                    <GridColumn Field="@nameof(ActivityDto.MemberId)" HeaderText="@L["Reports.Id"]" Width="120" Type="ColumnType.String" />
                    <GridColumn Field="@nameof(ActivityDto.Level)" HeaderText="@L["Reports.Level"]" Width="80" TextAlign="TextAlign.Center" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.Ambassadors)" HeaderText="@L["Reports.Ambassadors"]" Width="110" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.Customers)" HeaderText="@L["Common.Customers"]" Width="100" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.Level1)" HeaderText="@ColumnName(1)" Width="140" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.Level2)" HeaderText="@ColumnName(2)" Width="140" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.Level3)" HeaderText="@ColumnName(3)" Width="140" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.Level4)" HeaderText="@ColumnName(4)" Width="140" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.OtherLevel)" HeaderText="@L["Activity.OtherLevel"]" Width="90" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(ActivityDto.EnrollmentTeamPoints)" HeaderText="@L["Reports.EnrollTeamPoints"]" Width="130" TextAlign="TextAlign.Right" Format="N0" Type="ColumnType.Integer" />
                </GridColumns>
            </SfGrid>
        }
    </div>
</div>

@code {
    /// <summary>Raíz del API de equipo, p. ej. "api/v1/bizcenter/team".</summary>
    [Parameter] public string ApiBaseUrl     { get; set; } = "api/v1/bizcenter/team";
    [Parameter] public bool   ShowBreadcrumb { get; set; } = true;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private SfGrid<ActivityDto>? _grid;
    private DateTime _from;
    private DateTime _to;
    private DateTime _appliedFrom;
    private DateTime _appliedTo;
    private int      _maxDays = 366;
    private bool     _ready;
    private string?  _message;
    private Dictionary<int, string> _columns = new();

    private List<BreadcrumbNav.BreadcrumbItem> _breadcrumb => new()
    {
        new(L["Nav.Dashboard"], "/dashboard"),
        new(L["Sidebar.EnrollmentTeam"]),
        new(L["Sidebar.Activity"])
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var setup = await Http.GetFromJsonAsync<ApiWrapper<SetupDto>>($"{ApiBaseUrl}/reports/setup");
            var cols  = await Http.GetFromJsonAsync<ApiWrapper<List<ColumnDto>>>($"{ApiBaseUrl}/enrollment/activity/columns");

            if (setup?.Success == true && setup.Data is not null)
            {
                _from    = _appliedFrom = setup.Data.DefaultFrom;
                _to      = _appliedTo   = setup.Data.DefaultTo;
                _maxDays = setup.Data.MaxRangeDays;
                _ready   = true;
            }
            else
            {
                _message = L["Common.Error"];
            }

            if (cols?.Data is not null) _columns = cols.Data.ToDictionary(c => c.LevelId, c => c.Name);
        }
        catch (Exception)
        {
            _message = L["Common.Error"];
        }
    }

    private string ColumnName(int levelId) => _columns.TryGetValue(levelId, out var name) ? name : $"#{levelId}";

    private string? RangeQuery() =>
        $"from={_appliedFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={_appliedTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    private async Task ApplyAsync()
    {
        if (ReportRangeCheck.Problem(_from, _to, _maxDays) is { } problem)
        {
            _message = problem.Arg is null ? L[problem.Key] : L[problem.Key, problem.Arg];
            return;
        }

        _message     = null;
        _appliedFrom = _from;
        _appliedTo   = _to;
        if (_grid is not null) await _grid.Refresh();
    }

    // Igual que EnrollmentTeamCustomers: el Bearer para la MAUI; en las webs lo pone el manejador.
    private async Task<string?> GetTokenAsync()
    {
        if (AuthState is null) return null;
        var state = await AuthState;
        return state.User.FindFirst("access_token")?.Value;
    }

    private sealed class ApiWrapper<T> { public bool Success { get; set; } public T? Data { get; set; } }

    private sealed class SetupDto
    {
        public DateTime DefaultFrom  { get; set; }
        public DateTime DefaultTo    { get; set; }
        public int      MaxRangeDays { get; set; }
    }

    private sealed class ColumnDto
    {
        public int    LevelId { get; set; }
        public string Name    { get; set; } = string.Empty;
    }

    public sealed class ActivityDto
    {
        public string MemberId             { get; set; } = string.Empty;
        public string FullName             { get; set; } = string.Empty;
        public int    Level                { get; set; }
        public int    Ambassadors          { get; set; }
        public int    Customers            { get; set; }
        public int    Level1               { get; set; }
        public int    Level2               { get; set; }
        public int    Level3               { get; set; }
        public int    Level4               { get; set; }
        public int    OtherLevel           { get; set; }
        public int    EnrollmentTeamPoints { get; set; }
    }
}
```

- [ ] **Step 7: Crear la página `EnrollmentActivity.razor`**

```razor
@page "/team/enrollment/activity"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject Microsoft.Extensions.Localization.IStringLocalizer<MLMConquerorGlobalEdition.SharedComponents.Resources.SharedResources> L

<PageTitle>@L["PageTitle.EnrollmentActivity"]</PageTitle>

<EnrollmentTeamActivity />
```

- [ ] **Step 8: Menú**

En `SidebarMenu.razor`, dentro del grupo *Equipo de inscripción*, sustituir `                <span class="count">4</span>` (la del grupo de inscripción, que es la primera del archivo) por `                <span class="count">5</span>`, y sustituir:

```razor
                <li class="menu-item @IsActive("/team/enrollment/customers")">
                    <a href="/team/enrollment/customers" class="menu-link">@L["Sidebar.Customers"]</a>
                </li>
```

por:

```razor
                <li class="menu-item @IsActive("/team/enrollment/customers")">
                    <a href="/team/enrollment/customers" class="menu-link">@L["Sidebar.Customers"]</a>
                </li>
                <li class="menu-item @IsActive("/team/enrollment/activity")">
                    <a href="/team/enrollment/activity" class="menu-link">@L["Sidebar.Activity"]</a>
                </li>
```

- [ ] **Step 9: Compilar la web**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenterWeb -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 10: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Team/ReportRangeCheck.cs \
        MLMConquerorGlobalEdition.SharedComponents/Components/Team/EnrollmentTeamActivity.razor \
        MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/EnrollmentActivity.razor \
        MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportRangeCheckTests.cs
git commit -m "Actividad por fechas: componente compartido con rango del servidor, aviso de rango invalido antes de pedir, columnas de nivel con sus nombres reales y rejilla de servidor; pagina /team/enrollment/activity, entrada en el menu de inscripcion y textos en los 9 idiomas"
```

---

### Task 26: Pruebas contra SQL Server de la actividad

> **Hecho (Bloques 3 y 4):** la raíz local con 3 meses tarda **6,2 s** medida en `ReadUncommitted`: pasa del umbral de 3 s, ver R16. Las páginas de *Mis enlaces* y *Actividad* también se añadieron a `BizCenterApp` (MAUI) como envoltorios finos, para que el menú compartido no lleve a una página inexistente.
>
> **Corrección de Nick N1, N2 y N4.** La medición de la raíz es sobre 3 meses, no 366 días; sin índice de `HierarchyPath` (N1). La prueba de nivel siembra una orden de alta con dos productos (niveles 1 y 3) y espera la columna del 3.

**Files:**
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerActividadTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class SqlServerActividadTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerActividadTests(ITestOutputHelper salida) => _salida = salida;

    [SqlServerFact]
    public async Task La_actividad_se_traduce_a_SQL_y_cuenta_lo_mismo_que_en_memoria()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        var a  = SqlServerReportes.NuevoId();
        await EnrollmentActivityServiceTests.EquipoAsync(db, yo, a, SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId("TN")[..13]);

        var r = await new EnrollmentActivityService(db).GetGridAsync(
            yo, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), new GridDataRequest { PageSize = 50 });

        var filas = r.Value!.Items.ToDictionary(f => f.MemberId);
        (filas[yo].Ambassadors, filas[yo].Customers, filas[yo].Level1, filas[yo].Level2).Should().Be((1, 1, 1, 1));
        (filas[a].Level4, filas[a].OtherLevel, filas[a].EnrollmentTeamPoints).Should().Be((1, 1, 40));

        await tx.RollbackAsync();
    }

    /// <summary>R5: mide la raíz real con un año. Si pasa de 3 s, abrir la tarea de agregar en SQL.</summary>
    [SqlServerFact]
    public async Task La_raiz_con_un_anio_cabe_en_tiempo()
    {
        await using var db = SqlServerReportes.Db();
        var raiz = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == null).OrderBy(g => g.Level).Select(g => g.MemberId).FirstAsync();
        var hoy = DateTime.Now.Date;

        var reloj = Stopwatch.StartNew();
        var r = await new EnrollmentActivityService(db).GetGridAsync(raiz, hoy.AddDays(-365), hoy, new GridDataRequest { PageSize = 20 });
        reloj.Stop();

        _salida.WriteLine($"Actividad de {raiz} en 366 días: {r.Value!.TotalCount} filas en {reloj.ElapsedMilliseconds} ms");
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }
}
```

(El sufijo de los inscritos se recorta a 13 caracteres porque `EquipoAsync` le añade un dígito y el `MemberId` admite 20. `DateTime.Now` aquí es solo el extremo de un rango de medición, no lógica de negocio.)

- [ ] **Step 2: Ejecutar**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerActividadTests -nologo --logger "console;verbosity=detailed"`
Expected: PASS (2) o `Skipped`. Anotar el tiempo que imprime la segunda; si pasa de 3.000 ms, abrir la tarea de R5.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerActividadTests.cs
git commit -m "Pruebas contra SQL Server de la actividad por fechas: misma cuenta que en memoria y medicion de la raiz con un ano"
```

**Fin del Bloque 4.** Una migración (`IndiceFechaDeAlta`).

---

# Bloque 5 — Cambios de estado

Las Tareas 27 y 28 (modelo e interceptor) son lo primero que conviene fusionar y desplegar: desde ese momento el historial empieza a acumularse (R4), aunque la pantalla llegue después.

### Task 27: Modelo del historial y migración `HistorialDeEstados`

> **Corrección de Nick N3.** Esta tarea añade además la marca `MemberProfile.KeepQualified` (`bool`, por defecto `false`) en **su propia migración pequeña**, `MarcaMantenerCalificado`, separada de `HistorialDeEstados`. Editarla desde el Admin queda fuera del plan.

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Membership/MembershipStatusHistory.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/MemberStatusHistoryConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/MembershipStatusHistoryConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:52`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_HistorialDeEstados.cs` (vía `dotnet ef`, con guarda a mano)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

En `IndicesDeReportesTests.cs`, añadir `using MLMConquerorGlobalEdition.Domain.Entities.Membership;` y sustituir:

```csharp
    // (índices de los bloques siguientes)
```

por:

```csharp
    [Theory]
    [InlineData(typeof(MemberStatusHistory))]
    [InlineData(typeof(MembershipStatusHistory))]
    public void Historiales_por_miembro_y_fecha_y_por_fecha(Type historial)
    {
        TieneIndice(historial, "MemberId", "ChangedAt").Should().BeTrue();
        TieneIndice(historial, "ChangedAt").Should().BeTrue();
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(historial)!.FindProperty("MemberId")!.GetMaxLength().Should().Be(450);
    }

    // (índices de los bloques siguientes)
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: FAIL de compilación (`MembershipStatusHistory` no existe).

- [ ] **Step 2: Crear `MembershipStatusHistory.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Membership;

/// <summary>
/// Un cambio de estado de una suscripción (Plan J, J13). Hermana de MemberStatusHistory, que es
/// el estado de la CUENTA: aquí va el de la MEMBRESÍA. La escribe StatusHistoryInterceptor al
/// guardar; nadie la escribe a mano.
/// </summary>
public class MembershipStatusHistory : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;

    /// <summary>MembershipSubscription.Id: una subida de nivel crea otra suscripción.</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    public int MembershipLevelId { get; set; }

    /// <summary>Null cuando la fila registra la creación de la suscripción.</summary>
    public MembershipStatus? OldStatus { get; set; }

    public MembershipStatus NewStatus { get; set; }

    /// <summary>Hora de servidor.</summary>
    public DateTime ChangedAt { get; set; }
}
```

- [ ] **Step 3: Crear `MemberStatusHistoryConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberStatusHistoryConfiguration : IEntityTypeConfiguration<MemberStatusHistory>
{
    public void Configure(EntityTypeBuilder<MemberStatusHistory> builder)
    {
        builder.HasKey(x => x.Id);

        // Plan J: era nvarchar(max) sin índice. El reporte de cambios busca por fecha y la última
        // fecha de cada miembro.
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => new { x.MemberId, x.ChangedAt });
        builder.HasIndex(x => x.ChangedAt);
    }
}
```

- [ ] **Step 4: Crear `MembershipStatusHistoryConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MembershipStatusHistoryConfiguration : IEntityTypeConfiguration<MembershipStatusHistory>
{
    public void Configure(EntityTypeBuilder<MembershipStatusHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.SubscriptionId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => new { x.MemberId, x.ChangedAt });
        builder.HasIndex(x => x.ChangedAt);
    }
}
```

- [ ] **Step 5: `DbSet` en `AppDbContext.cs`**

Sustituir:

```csharp
    public DbSet<MemberStatusHistory> MemberStatusHistories => Set<MemberStatusHistory>();
```

por:

```csharp
    public DbSet<MemberStatusHistory> MemberStatusHistories => Set<MemberStatusHistory>();
    public DbSet<MembershipStatusHistory> MembershipStatusHistories => Set<MembershipStatusHistory>();
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 7: Crear la migración y añadir la guarda**

Con el comando de la cabecera y el nombre `HistorialDeEstados`. `Up` debe traer solo: `AlterColumn` de `MemberStatusHistories.MemberId` (`nvarchar(max)` → `nvarchar(450)`), los dos `CreateIndex` de `MemberStatusHistories`, `CreateTable("MembershipStatusHistories")` y sus dos índices. Añadir **al principio de `Up`**:

```csharp
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MemberStatusHistories] WHERE LEN([MemberId]) > 450)
    THROW 51003, N'MemberStatusHistories.MemberId tiene valores de mas de 450 caracteres: revisar antes de migrar (Plan J, Tarea 27).', 1;");
```

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_HistorialDeEstados.cs`
Expected: `0`

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Membership/MembershipStatusHistory.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/MemberStatusHistoryConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/MembershipStatusHistoryConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_HistorialDeEstados.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_HistorialDeEstados.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs
git commit -m "Historial de estados: MemberStatusHistories.MemberId a nvarchar(450) con indices (MemberId, ChangedAt) y (ChangedAt), y tabla hermana MembershipStatusHistories para la membresia; migracion HistorialDeEstados con guarda contra truncado"
```

- [ ] **Step 9: Aplicar la migración a la base local**

Con el comando de la cabecera. Expected: `Applying migration '…_HistorialDeEstados'.` y `Done.`

---

### Task 28: Interceptor del historial y guarda contra escrituras que se lo saltan

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Interceptors/StatusHistoryInterceptor.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:189-194`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Domain/StatusHistoryInterceptorTests.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Domain/EstadosSinInterceptorTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

`StatusHistoryInterceptorTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Domain;

/// <summary>
/// El historial de estados se escribe al guardar, como SecurityFactorsInterceptor sella los
/// factores: así lo cubren también los que no lo escribían (activación del alta, facturación
/// recurrente, subida y bajada de nivel). Pasa por el AppDbContext real, que registra el
/// interceptor él mismo.
/// </summary>
public class StatusHistoryInterceptorTests
{
    private const string MemberId = "AMB-000001";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_dbName).Options);

    /// <summary>Perfil Pending y suscripción Pending, como deja el alta. Devuelve el id de la suscripción.</summary>
    private async Task<string> SeedAsync()
    {
        await using var db = NewContext();
        db.MemberProfiles.Add(new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = MemberId, FirstName = "Ana", LastName = "Ruiz",
            Country = "US", Status = MemberAccountStatus.Pending, CreatedBy = "seed"
        });
        var suscripcion = new MembershipSubscription
        {
            MemberId = MemberId, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Pending,
            StartDate = new DateTime(2026, 3, 1), CreatedBy = "seed"
        };
        db.MembershipSubscriptions.Add(suscripcion);
        await db.SaveChangesAsync();
        return suscripcion.Id;
    }

    [Fact]
    public async Task Crear_el_perfil_no_es_un_cambio_de_estado_de_la_cuenta()
    {
        await SeedAsync();

        await using var db = NewContext();
        (await db.MemberStatusHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Crear_una_suscripcion_deja_su_alta_sin_estado_anterior()
    {
        var id = await SeedAsync();

        await using var db = NewContext();
        var fila = await db.MembershipStatusHistories.SingleAsync();
        (fila.MemberId, fila.SubscriptionId, fila.MembershipLevelId, fila.OldStatus, fila.NewStatus)
            .Should().Be((MemberId, id, 3, (MembershipStatus?)null, MembershipStatus.Pending));
        fila.CreatedBy.Should().Be("seed");
    }

    [Fact]
    public async Task Cambiar_el_estado_de_la_cuenta_deja_una_fila_con_la_hora_de_servidor()
    {
        await SeedAsync();
        var antes = DateTime.Now;

        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.Status       = MemberAccountStatus.Active;
            perfil.LastUpdateBy = "admin-001";
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        var fila = await lectura.MemberStatusHistories.SingleAsync();
        (fila.MemberId, fila.OldStatus, fila.NewStatus, fila.CreatedBy)
            .Should().Be((MemberId, MemberAccountStatus.Pending, MemberAccountStatus.Active, "admin-001"));
        fila.ChangedAt.Should().BeOnOrAfter(antes).And.BeOnOrBefore(DateTime.Now);
        fila.ChangedAt.Kind.Should().NotBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Guardar_sin_cambiar_el_estado_no_deja_fila_aunque_todo_se_marque_modificado()
    {
        await SeedAsync();

        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.FirstName = "Ana María";
            db.Update(perfil);
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        (await lectura.MemberStatusHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Si_el_manejador_ya_escribio_la_fila_con_su_motivo_no_se_duplica()
    {
        await SeedAsync();

        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.Status = MemberAccountStatus.Active;
            db.MemberStatusHistories.Add(new MemberStatusHistory
            {
                MemberId = MemberId, OldStatus = MemberAccountStatus.Pending, NewStatus = MemberAccountStatus.Active,
                Reason = "Pago verificado a mano", ChangedAt = DateTime.Now, CreatedBy = "admin-001"
            });
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        (await lectura.MemberStatusHistories.SingleAsync()).Reason.Should().Be("Pago verificado a mano");
    }

    [Fact]
    public async Task Cambiar_el_estado_de_la_suscripcion_deja_fila_con_suscripcion_y_nivel()
    {
        var id = await SeedAsync();

        await using (var db = NewContext())
        {
            var suscripcion = await db.MembershipSubscriptions.SingleAsync();
            suscripcion.SubscriptionStatus = MembershipStatus.Active;
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        var fila = await lectura.MembershipStatusHistories.SingleAsync(h => h.OldStatus != null);
        (fila.SubscriptionId, fila.MembershipLevelId, fila.OldStatus, fila.NewStatus)
            .Should().Be((id, 3, (MembershipStatus?)MembershipStatus.Pending, MembershipStatus.Active));
    }
}
```

`EstadosSinInterceptorTests.cs`:

```csharp
using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Domain;

/// <summary>
/// El historial de estados lo escribe StatusHistoryInterceptor al guardar. ExecuteUpdate,
/// ExecuteDelete y el SQL crudo no pasan por el seguimiento de cambios y se lo saltan (Plan J, R3).
/// Esta prueba falla si alguien cambia estados de perfiles o suscripciones así.
/// </summary>
public class EstadosSinInterceptorTests
{
    private static readonly Regex UpdateSql = new(
        @"UPDATE\s+(\[?dbo\]?\.)?\[?(MemberProfiles|MembershipSubscriptions)\]?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Nadie_cambia_estados_por_fuera_del_seguimiento_de_cambios()
    {
        var raiz      = RaizDelRepositorio();
        var sep       = Path.DirectorySeparatorChar;
        var ofensores = new List<string>();

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}"))
                continue;

            var texto = File.ReadAllText(archivo);
            foreach (var sentencia in texto.Split(';'))
            {
                var masiva = sentencia.Contains("ExecuteUpdate") || sentencia.Contains("ExecuteDelete")
                             || sentencia.Contains("ExecuteSql");
                var tabla  = sentencia.Contains("MemberProfiles") || sentencia.Contains("MembershipSubscriptions");
                if (masiva && tabla && sentencia.Contains("Status"))
                    ofensores.Add(Path.GetRelativePath(raiz, archivo));
            }

            if (UpdateSql.IsMatch(texto))
                ofensores.Add(Path.GetRelativePath(raiz, archivo) + " (UPDATE en SQL)");

            // Update() de una entidad que no viene del contexto: el valor original sería el nuevo y
            // el interceptor no vería el cambio (IUnitOfWork.Members / Subscriptions).
            if (texto.Contains("Members.Update(") || texto.Contains("Subscriptions.Update("))
                ofensores.Add(Path.GetRelativePath(raiz, archivo) + " (Update de entidad sin seguir)");
        }

        ofensores.Distinct().Should().BeEmpty(
            "un cambio de estado hecho así no deja historial. Si de verdad hace falta, escribir la fila " +
            "de historial a mano en el mismo sitio y documentar aquí la excepción");
    }

    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null &&
               !File.Exists(Path.Combine(directorio.FullName, "MLMConquerorGlobalEdition.slnx")))
        {
            directorio = directorio.Parent;
        }

        directorio.Should().NotBeNull("la prueba tiene que poder localizar la raíz del repositorio");
        return directorio!.FullName;
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "StatusHistoryInterceptorTests|EstadosSinInterceptorTests" -nologo`
Expected: `EstadosSinInterceptorTests` PASS ya (hoy nadie lo hace) y `StatusHistoryInterceptorTests` FAIL en 3 de 6: las que esperan una fila automática (alta de suscripción, cambio de cuenta, cambio de suscripción). Las otras tres pasan ya porque no esperan filas nuevas.

- [ ] **Step 3: Crear `StatusHistoryInterceptor.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Escribe el historial de estados al guardar (Plan J, J14): cada cambio de
/// <see cref="MemberProfile.Status"/> va a MemberStatusHistories y cada alta o cambio de
/// <see cref="MembershipSubscription.SubscriptionStatus"/> a MembershipStatusHistories.
/// </summary>
/// <remarks>
/// POR QUÉ AQUÍ. Tres sitios escribían el historial a mano (actualizar estado desde el Admin,
/// cancelar membresía y el job de cancelaciones programadas) y otros cuatro cambiaban estados sin
/// dejar rastro (activación del alta, facturación recurrente, subida y bajada de nivel). Al
/// guardar no se escapa ninguno, tampoco el que se escriba mañana.
///
/// COMPARA VALORES, NO <c>IsModified</c>: un <c>Update(entidad)</c> marca todas las columnas como
/// modificadas aunque no cambien (mismo criterio que SecurityFactorsInterceptor).
///
/// NO DUPLICA: si el mismo guardado ya trae una MemberStatusHistory añadida para ese miembro y ese
/// estado nuevo, es la del manejador, con su motivo, y se respeta.
///
/// NO VE ExecuteUpdate NI SQL CRUDO: lo vigila EstadosSinInterceptorTests.
///
/// HORA: DateTime.Now, hora de servidor, como AuditInterceptor; el interceptor se crea en
/// AppDbContext.OnConfiguring, sin contenedor (J25).
/// </remarks>
public sealed class StatusHistoryInterceptor : SaveChangesInterceptor
{
    internal const string SystemActor = "system";
    private const int ActorMaxLength = 100;

    private readonly Func<DateTime> _now;

    /// <param name="now">Solo para pruebas. Por defecto <see cref="DateTime.Now"/>.</param>
    public StatusHistoryInterceptor(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) Record(context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Record(context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Record(DbContext context)
    {
        // Se materializa antes de añadir filas: no se puede tocar el ChangeTracker mientras se recorre.
        var entries = context.ChangeTracker.Entries().ToList();
        var now     = _now();

        var writtenByHandler = entries
            .Where(e => e.State == EntityState.Added && e.Entity is MemberStatusHistory)
            .Select(e => (MemberStatusHistory)e.Entity)
            .Select(h => (h.MemberId, h.NewStatus))
            .ToHashSet();

        var accountRows    = new List<MemberStatusHistory>();
        var membershipRows = new List<MembershipStatusHistory>();

        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case MemberProfile profile when entry.State == EntityState.Modified:
                {
                    var status = entry.Property(nameof(MemberProfile.Status));
                    if (!ValueChanged(status) || writtenByHandler.Contains((profile.MemberId, profile.Status)))
                        break;

                    accountRows.Add(new MemberStatusHistory
                    {
                        MemberId     = profile.MemberId,
                        OldStatus    = (MemberAccountStatus)status.OriginalValue!,
                        NewStatus    = profile.Status,
                        ChangedAt    = now,
                        CreationDate = now,
                        CreatedBy    = Actor(profile.LastUpdateBy)
                    });
                    break;
                }

                case MembershipSubscription subscription when entry.State == EntityState.Added:
                    membershipRows.Add(Row(subscription, null, now));
                    break;

                case MembershipSubscription subscription when entry.State == EntityState.Modified:
                {
                    var status = entry.Property(nameof(MembershipSubscription.SubscriptionStatus));
                    if (!ValueChanged(status)) break;

                    membershipRows.Add(Row(subscription, (MembershipStatus)status.OriginalValue!, now));
                    break;
                }
            }
        }

        if (accountRows.Count > 0)    context.Set<MemberStatusHistory>().AddRange(accountRows);
        if (membershipRows.Count > 0) context.Set<MembershipStatusHistory>().AddRange(membershipRows);
    }

    private static MembershipStatusHistory Row(MembershipSubscription s, MembershipStatus? oldStatus, DateTime now) => new()
    {
        MemberId          = s.MemberId,
        SubscriptionId    = s.Id,
        MembershipLevelId = s.MembershipLevelId,
        OldStatus         = oldStatus,
        NewStatus         = s.SubscriptionStatus,
        ChangedAt         = now,
        CreationDate      = now,
        CreatedBy         = Actor(s.LastUpdateBy ?? s.CreatedBy)
    };

    private static bool ValueChanged(PropertyEntry property) =>
        property.IsModified && !Equals(property.OriginalValue, property.CurrentValue);

    private static string Actor(string? who)
    {
        var actor = string.IsNullOrWhiteSpace(who) ? SystemActor : who.Trim();
        return actor.Length <= ActorMaxLength ? actor : actor[..ActorMaxLength];
    }
}
```

- [ ] **Step 4: Registrarlo en `AppDbContext.OnConfiguring`**

Sustituir:

```csharp
        // SecurityFactorsInterceptor va aquí y no en el registro de cada anfitrión por la misma
        // razón que existe: un anfitrión que se olvidara de añadirlo cambiaría factores sin sellar.
        optionsBuilder.AddInterceptors(new AuditInterceptor(), new SecurityFactorsInterceptor());
```

por:

```csharp
        // SecurityFactorsInterceptor va aquí y no en el registro de cada anfitrión por la misma
        // razón que existe: un anfitrión que se olvidara de añadirlo cambiaría factores sin sellar.
        // StatusHistoryInterceptor (Plan J), por lo mismo: un anfitrión sin él cambiaría estados
        // sin dejar historial.
        optionsBuilder.AddInterceptors(
            new AuditInterceptor(), new SecurityFactorsInterceptor(), new StatusHistoryInterceptor());
```

- [ ] **Step 5: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "StatusHistoryInterceptorTests|EstadosSinInterceptorTests|CancelMembershipHandlerTests" -nologo`
Expected: PASS.

- [ ] **Step 6: Las suites que tocan estados siguen verdes**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo`, `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests -nologo` y `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS. (`ActivacionManualSinPagoEnVueloTests` y `UpdateMemberStatusHandlerTests` esperan una sola fila con el motivo del manejador: la deduplicación la mantiene.) Si alguna prueba falla porque contaba lo que devuelve `SaveChanges` o todas las entidades del contexto, es el historial que ahora se añade: ajustar la prueba a lo que de verdad comprueba (sin quitar el interceptor) e incluirla en este commit.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Interceptors/StatusHistoryInterceptor.cs \
        MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Domain/StatusHistoryInterceptorTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Domain/EstadosSinInterceptorTests.cs
git commit -m "Historial de estados al guardar: StatusHistoryInterceptor registra cada cambio de estado de la cuenta y cada alta o cambio de estado de una suscripcion con hora de servidor, sin duplicar la fila que ya escribe un manejador; prueba que vigila que nadie cambie estados con ExecuteUpdate o SQL crudo"
```

---

### Task 29: Qué es «Calificado» (`QualificationTimeline`)

> **SUSTITUIDA (corrección de Nick N3).** En lugar de `QualificationTimeline` se crea un servicio compartido `IQualificationService` / `QualificationService` en `Repository/Services/Teams` con `IsQualifiedAsync(memberId)` y una versión por lote para una página de miembros: **membresía activa** (suscripción actual `Active`) **Y** (a) `EnrollDate > Now − 12 meses` (hora de servidor, `IDateTimeProvider`), **o** (b) `IAnnualFeeStatus.HasPaidCurrentAnnualFeeAsync` (punto de extensión; la única implementación, `AnnualFeeNotModeled`, devuelve `false` hasta el Plan K y lo dice en su `<summary>`), **o** (c) `MemberProfile.KeepQualified`. Pruebas: cada rama por separado, el borde exacto de los 12 meses, y que sin membresía activa nada califica. `EnrollmentTeamService` y `DualTeamService` pasan a usarlo para su `IsQualified`. No hay «Calificado desde».

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/QualificationTimeline.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/QualificationTimelineTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Cambio = MLMConquerorGlobalEdition.Repository.Services.Teams.QualificationTimeline.Change;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Calificado = tiene alguna suscripción Active. "Calificado desde/hasta" = el último instante en
/// que eso cambió, mirando cada guardado entero: una subida de nivel no es perder y recuperar.
/// </summary>
public class QualificationTimelineTests
{
    private static readonly DateTime T0 = new(2026, 1, 10);
    private static readonly DateTime T1 = new(2026, 2, 1);
    private static readonly DateTime T2 = new(2026, 3, 5);

    [Fact]
    public void Sin_historial_no_hay_fecha()
        => QualificationTimeline.LastFlip(Array.Empty<Cambio>()).Should().BeNull();

    [Fact]
    public void La_activacion_del_alta_es_calificarse()
        => QualificationTimeline.LastFlip(new[]
        {
            new Cambio("S1", null, MembershipStatus.Pending, T0),
            new Cambio("S1", MembershipStatus.Pending, MembershipStatus.Active, T1)
        }).Should().Be(T1);

    [Fact]
    public void Pasar_a_HoldByBilling_es_dejar_de_estar_calificado()
        => QualificationTimeline.LastFlip(new[]
        {
            new Cambio("S1", MembershipStatus.Active, MembershipStatus.HoldByBilling, T1)
        }).Should().Be(T1);

    [Fact]
    public void Recuperarla_despues_cuenta_la_ultima()
        => QualificationTimeline.LastFlip(new[]
        {
            new Cambio("S1", MembershipStatus.Active, MembershipStatus.HoldByBilling, T1),
            new Cambio("S1", MembershipStatus.HoldByBilling, MembershipStatus.Active, T2)
        }).Should().Be(T2);

    [Fact]
    public void Una_subida_de_nivel_en_el_mismo_guardado_no_es_un_cambio()
        => QualificationTimeline.LastFlip(new[]
        {
            new Cambio("S1", MembershipStatus.Pending, MembershipStatus.Active, T1),
            new Cambio("S1", MembershipStatus.Active, MembershipStatus.Expired, T2),
            new Cambio("S2", null, MembershipStatus.Active, T2)
        }).Should().Be(T1);

    [Fact]
    public void Un_cambio_que_no_toca_la_calificacion_no_cuenta()
        => QualificationTimeline.LastFlip(new[]
        {
            new Cambio("S1", MembershipStatus.Pending, MembershipStatus.Cancelled, T1)
        }).Should().BeNull();

    [Theory]
    [InlineData(MembershipStatus.OnHold,        "2026-02-02")]
    [InlineData(MembershipStatus.HoldByBilling, "2026-02-02")]
    [InlineData(MembershipStatus.Cancelled,     "2026-02-03")]
    [InlineData(MembershipStatus.Expired,       "2026-02-04")]
    [InlineData(MembershipStatus.Active,        "2026-01-01")]
    [InlineData(MembershipStatus.Pending,       "2026-01-01")]
    public void Sin_historial_la_fecha_es_la_propia_del_estado(MembershipStatus estado, string esperada)
    {
        var s = new MembershipSubscription
        {
            SubscriptionStatus = estado,
            StartDate          = new DateTime(2026, 1, 1),
            HoldDate           = new DateTime(2026, 2, 2),
            CancellationDate   = new DateTime(2026, 2, 3),
            EndDate            = new DateTime(2026, 2, 4)
        };

        QualificationTimeline.FallbackDate(s).Should().Be(DateTime.Parse(esperada));
    }

    [Fact]
    public void Sin_fecha_de_inicio_no_hay_fecha_de_reserva()
        => QualificationTimeline.FallbackDate(new MembershipSubscription { SubscriptionStatus = MembershipStatus.Active })
            .Should().BeNull();
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter QualificationTimelineTests -nologo`
Expected: FAIL de compilación (`QualificationTimeline` no existe).

- [ ] **Step 3: Crear `QualificationTimeline.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// "Calificado" (Plan J, J15): el miembro tiene alguna suscripción Active — la misma definición que
/// EnrollmentTeamService usa para IsQualified. Este tipo calcula CUÁNDO cambió por última vez esa
/// condición a partir de MembershipStatusHistories.
/// </summary>
/// <remarks>
/// Los cambios se aplican por INSTANTE (todas las filas con el mismo ChangedAt, que salen del mismo
/// guardado). Una subida de nivel deja la vieja en Expired y crea la nueva Active a la vez: antes y
/// después del instante el miembro está calificado, así que no cuenta como cambio. Una suscripción
/// que aparece por primera vez aporta su OldStatus como estado "de antes".
/// </remarks>
public static class QualificationTimeline
{
    public sealed record Change(string SubscriptionId, MembershipStatus? OldStatus, MembershipStatus NewStatus, DateTime ChangedAt);

    /// <summary>Último instante en que cambió la calificación, o null si no consta ninguno.</summary>
    public static DateTime? LastFlip(IEnumerable<Change> changes)
    {
        var state = new Dictionary<string, MembershipStatus>(StringComparer.Ordinal);
        DateTime? last = null;

        foreach (var instant in changes.GroupBy(c => c.ChangedAt).OrderBy(g => g.Key))
        {
            var before = new Dictionary<string, MembershipStatus>(state, StringComparer.Ordinal);
            foreach (var change in instant)
            {
                if (!before.ContainsKey(change.SubscriptionId) && change.OldStatus is { } old)
                    before[change.SubscriptionId] = old;
            }

            foreach (var change in instant)
                state[change.SubscriptionId] = change.NewStatus;

            if (IsQualified(before) != IsQualified(state))
                last = instant.Key;
        }

        return last;
    }

    /// <summary>
    /// Fecha que se enseña cuando no hay historial (todo lo anterior al Bloque 5): la columna propia
    /// del estado de la suscripción. Null si no hay ninguna.
    /// </summary>
    public static DateTime? FallbackDate(MembershipSubscription subscription)
    {
        DateTime? date = subscription.SubscriptionStatus switch
        {
            MembershipStatus.OnHold or MembershipStatus.HoldByBilling => subscription.HoldDate ?? subscription.StartDate,
            MembershipStatus.Cancelled                                => subscription.CancellationDate ?? subscription.EndDate,
            MembershipStatus.Expired                                  => subscription.EndDate,
            _                                                         => subscription.StartDate
        };

        return date is null || date.Value == default ? null : date;
    }

    private static bool IsQualified(Dictionary<string, MembershipStatus> state) =>
        state.Values.Any(s => s == MembershipStatus.Active);
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter QualificationTimelineTests -nologo`
Expected: PASS (13 pruebas).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Teams/QualificationTimeline.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/QualificationTimelineTests.cs
git commit -m "Calificado es tener una suscripcion Active; su ultima fecha de cambio se calcula del historial de membresia por instante, para que una subida de nivel no cuente como perder y recuperar, con la fecha propia del estado como reserva"
```

---

### Task 30: Servicio de cambios de estado (tres vistas)

> **Corrección de Nick N2, N3 y N4.** `IsQualified` sale de `IQualificationService`; se quitan `QualifiedChangedAt` y el uso de `QualificationTimeline`. `MembershipLevelName` sale del nivel más alto de los productos de la orden de la suscripción actual (`LastOrderId`, vía `OrderMembershipLevelQueries`); sin orden, el `MembershipLevelId` de la suscripción. El rango sigue J12 (366 días; N4 es solo para *Actividad*).

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/StatusChangesScope.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/TeamStatusChangeDtos.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/ITeamStatusChangesService.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Teams/TeamStatusChangesService.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamStatusChangesServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Cambios de estado: los miembros del alcance con un cambio registrado en el rango, con su estado
/// ACTUAL y la fecha del ÚLTIMO cambio. Inscripción y clientes por prefijo de genealogía; Dual Team
/// reduciendo primero por fecha y comprobando después la pertenencia al árbol binario.
/// </summary>
public class TeamStatusChangesServiceTests
{
    internal sealed record Ids(string Yo, string E1, string E2, string E3, string E4, string R1, string Fuera, string Otro)
    {
        public static readonly Ids Fijos = new("AMB-000001", "AMB-000011", "MBR-000012", "AMB-000013",
                                               "AMB-000014", "AMB-000021", "AMB-000031", "AMB-000099");

        public IEnumerable<string> Todos => new[] { Yo, E1, E2, E3, E4, R1, Fuera, Otro };
    }

    internal static readonly DateTime Desde = new(2026, 3, 1);
    internal static readonly DateTime Hasta = new(2026, 3, 31);

    /// <summary>
    /// Genealogía: YO ─ E1 ─ E3; YO ─ E2 (cliente); YO ─ E4. OTRO ─ R1; OTRO ─ FUERA.
    /// Binario: YO ─izq─ E1 ─der─ E3; YO ─der─ R1. FUERA cuelga de OTRO.
    /// Cambios en marzo: E1 (cuenta y membresía), E2 (membresía a HoldByBilling), E3 (cuenta a
    /// Suspended), R1 (membresía) y FUERA (cuenta). E4 cambió en enero.
    /// </summary>
    internal static async Task EscenarioAsync(AppDbContext db, Ids id)
    {
        ReportesDb.Miembro(db, id.Yo);
        ReportesDb.Miembro(db, id.E1, id.Yo);
        ReportesDb.Miembro(db, id.E2, id.Yo, MemberType.ExternalMember);
        ReportesDb.Miembro(db, id.E3, id.E1, estado: MemberAccountStatus.Suspended);
        ReportesDb.Miembro(db, id.E4, id.Yo);
        ReportesDb.Miembro(db, id.Otro);
        ReportesDb.Miembro(db, id.R1, id.Otro);
        ReportesDb.Miembro(db, id.Fuera, id.Otro);

        ReportesDb.Nodo(db, id.Yo,    null,    ReportesDb.Ruta(id.Yo), 1);
        ReportesDb.Nodo(db, id.E1,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E1), 2);
        ReportesDb.Nodo(db, id.E2,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E2), 2);
        ReportesDb.Nodo(db, id.E3,    id.E1,   ReportesDb.Ruta(id.Yo, id.E1, id.E3), 3);
        ReportesDb.Nodo(db, id.E4,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E4), 2);
        ReportesDb.Nodo(db, id.Otro,  null,    ReportesDb.Ruta(id.Otro), 1);
        ReportesDb.Nodo(db, id.R1,    id.Otro, ReportesDb.Ruta(id.Otro, id.R1), 2);
        ReportesDb.Nodo(db, id.Fuera, id.Otro, ReportesDb.Ruta(id.Otro, id.Fuera), 2);

        ReportesDb.NodoBinario(db, id.Yo,    null,    ReportesDb.Ruta(id.Yo), TreeSide.Left);
        ReportesDb.NodoBinario(db, id.E1,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E1), TreeSide.Left);
        ReportesDb.NodoBinario(db, id.E3,    id.E1,   ReportesDb.Ruta(id.Yo, id.E1, id.E3), TreeSide.Right);
        ReportesDb.NodoBinario(db, id.R1,    id.Yo,   ReportesDb.Ruta(id.Yo, id.R1), TreeSide.Right);
        ReportesDb.NodoBinario(db, id.Otro,  null,    ReportesDb.Ruta(id.Otro), TreeSide.Left);
        ReportesDb.NodoBinario(db, id.Fuera, id.Otro, ReportesDb.Ruta(id.Otro, id.Fuera), TreeSide.Left);

        var sE1 = ReportesDb.Suscripcion(db, id.E1, 3, MembershipStatus.Active, new DateTime(2026, 1, 10));
        var sE2 = ReportesDb.Suscripcion(db, id.E2, 2, MembershipStatus.HoldByBilling, new DateTime(2025, 11, 1));
        sE2.HoldDate = new DateTime(2026, 3, 10);
        var sE3 = ReportesDb.Suscripcion(db, id.E3, 1, MembershipStatus.OnHold, new DateTime(2025, 6, 1));
        sE3.HoldDate = new DateTime(2026, 2, 2);
        var sR1 = ReportesDb.Suscripcion(db, id.R1, 1, MembershipStatus.OnHold, new DateTime(2025, 6, 1));
        await db.SaveChangesAsync();

        // Lo que el interceptor apuntó al sembrar (altas de suscripción con la hora real) fuera:
        // el escenario escribe su propio historial.
        var todos = id.Todos.ToList();
        db.MembershipStatusHistories.RemoveRange(db.MembershipStatusHistories.Where(h => todos.Contains(h.MemberId)));
        db.MemberStatusHistories.RemoveRange(db.MemberStatusHistories.Where(h => todos.Contains(h.MemberId)));
        await db.SaveChangesAsync();

        db.MemberStatusHistories.AddRange(
            Cuenta(id.E1,    MemberAccountStatus.Pending, MemberAccountStatus.Active,    new DateTime(2026, 3, 5)),
            Cuenta(id.E3,    MemberAccountStatus.Active,  MemberAccountStatus.Suspended, new DateTime(2026, 3, 12)),
            Cuenta(id.Fuera, MemberAccountStatus.Active,  MemberAccountStatus.Inactive,  new DateTime(2026, 3, 15)),
            Cuenta(id.E4,    MemberAccountStatus.Active,  MemberAccountStatus.Inactive,  new DateTime(2026, 1, 1)));
        db.MembershipStatusHistories.AddRange(
            Membresia(id.E1, sE1.Id, 3, null, MembershipStatus.Pending, new DateTime(2026, 1, 10)),
            Membresia(id.E1, sE1.Id, 3, MembershipStatus.Pending, MembershipStatus.Active, new DateTime(2026, 3, 5)),
            Membresia(id.E2, sE2.Id, 2, MembershipStatus.Active, MembershipStatus.HoldByBilling, new DateTime(2026, 3, 10)),
            Membresia(id.R1, sR1.Id, 1, MembershipStatus.Active, MembershipStatus.OnHold, new DateTime(2026, 3, 15)));
        await db.SaveChangesAsync();
    }

    private static MemberStatusHistory Cuenta(string m, MemberAccountStatus de, MemberAccountStatus a, DateTime cuando) => new()
    {
        MemberId = m, OldStatus = de, NewStatus = a, ChangedAt = cuando, CreatedBy = "prueba"
    };

    private static MembershipStatusHistory Membresia(string m, string s, int nivel, MembershipStatus? de, MembershipStatus a, DateTime cuando) => new()
    {
        MemberId = m, SubscriptionId = s, MembershipLevelId = nivel, OldStatus = de, NewStatus = a, ChangedAt = cuando, CreatedBy = "prueba"
    };

    private static async Task<AppDbContext> BaseAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        await EscenarioAsync(db, Ids.Fijos);
        return db;
    }

    private static GridDataRequest Pagina(string? buscar = null) => new() { Page = 1, PageSize = 50, Search = buscar };

    private static async Task<List<TeamStatusChangeView>> FilasAsync(AppDbContext db, StatusChangesScope alcance, GridDataRequest? pagina = null)
    {
        var r = await new TeamStatusChangesService(db).GetGridAsync(Ids.Fijos.Yo, alcance, Desde, Hasta, pagina ?? Pagina());
        r.IsSuccess.Should().BeTrue();
        return r.Value!.Items.ToList();
    }

    [Fact]
    public async Task Inscripcion_lista_el_equipo_con_cambios_en_el_rango()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        (await FilasAsync(db, StatusChangesScope.Enrollment)).Select(f => f.MemberId)
            .Should().BeEquivalentTo(new[] { id.E1, id.E2, id.E3 }, "R1 y FUERA no son del equipo de inscripción y E4 cambió en enero");
    }

    [Fact]
    public async Task Clientes_solo_los_clientes_del_equipo()
    {
        await using var db = await BaseAsync();

        (await FilasAsync(db, StatusChangesScope.Customers)).Select(f => f.MemberId)
            .Should().Equal(Ids.Fijos.E2);
    }

    [Fact]
    public async Task Dual_team_va_por_el_arbol_binario_con_su_pierna()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var filas = (await FilasAsync(db, StatusChangesScope.DualTeam)).ToDictionary(f => f.MemberId);

        filas.Keys.Should().BeEquivalentTo(new[] { id.E1, id.E3, id.R1 }, "E2 no está en el binario y FUERA cuelga de otro");
        filas[id.E1].Leg.Should().Be("Left");
        filas[id.E3].Leg.Should().Be("Left", "la pierna es la del hijo directo de YO en la ruta");
        filas[id.R1].Leg.Should().Be("Right");
        filas[id.E3].DualUplineMemberId.Should().Be(id.E1);
    }

    [Fact]
    public async Task Cada_fila_trae_el_estado_actual_y_la_fecha_del_ultimo_cambio()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var filas = (await FilasAsync(db, StatusChangesScope.Enrollment)).ToDictionary(f => f.MemberId);

        var e1 = filas[id.E1];
        (e1.AccountStatus, e1.AccountStatusChangedAt, e1.MembershipStatus, e1.MembershipLevelName,
         e1.MembershipStatusChangedAt, e1.IsQualified, e1.QualifiedChangedAt, e1.SponsorMemberId)
            .Should().Be(("Active", (DateTime?)new DateTime(2026, 3, 5), "Active", "Travel Advantage - Elite",
                          (DateTime?)new DateTime(2026, 3, 5), true, (DateTime?)new DateTime(2026, 3, 5), id.Yo));
        e1.SponsorFullName.Should().Be($"Nombre {id.Yo}");

        var e2 = filas[id.E2];
        (e2.MembershipStatus, e2.MembershipStatusChangedAt, e2.IsQualified, e2.QualifiedChangedAt)
            .Should().Be(("HoldByBilling", (DateTime?)new DateTime(2026, 3, 10), false, (DateTime?)new DateTime(2026, 3, 10)));
    }

    [Fact]
    public async Task Sin_historial_de_membresia_usa_la_fecha_propia_del_estado()
    {
        await using var db = await BaseAsync();

        var e3 = (await FilasAsync(db, StatusChangesScope.Enrollment)).Single(f => f.MemberId == Ids.Fijos.E3);

        (e3.AccountStatus, e3.AccountStatusChangedAt, e3.MembershipStatus, e3.MembershipStatusChangedAt, e3.IsQualified, e3.QualifiedChangedAt)
            .Should().Be(("Suspended", (DateTime?)new DateTime(2026, 3, 12), "OnHold", (DateTime?)new DateTime(2026, 2, 2), false, (DateTime?)new DateTime(2026, 2, 2)));
    }

    [Fact]
    public async Task La_ruta_binaria_no_sale_del_servidor_y_se_busca_por_nombre()
    {
        await using var db = await BaseAsync();

        var filas = await FilasAsync(db, StatusChangesScope.DualTeam, Pagina(Ids.Fijos.R1.ToLowerInvariant()));

        filas.Should().ContainSingle().Which.DualPath.Should().BeNull();
    }

    [Fact]
    public async Task Un_rango_de_mas_de_366_dias_se_rechaza()
    {
        await using var db = await BaseAsync();

        var r = await new TeamStatusChangesService(db).GetGridAsync(
            Ids.Fijos.Yo, StatusChangesScope.Enrollment, new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), Pagina());

        r.ErrorCode.Should().Be(ReportDateRange.TooLongCode);
    }

    [Theory]
    [InlineData("enrollment", StatusChangesScope.Enrollment)]
    [InlineData("DUAL",       StatusChangesScope.DualTeam)]
    [InlineData("customers",  StatusChangesScope.Customers)]
    public void Las_tres_vistas_se_leen_de_la_ruta(string valor, StatusChangesScope esperado)
    {
        StatusChangesScopes.TryParse(valor, out var alcance).Should().BeTrue();
        alcance.Should().Be(esperado);
    }

    [Fact]
    public void Otra_vista_no_existe()
        => StatusChangesScopes.TryParse("binary", out _).Should().BeFalse();
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TeamStatusChangesServiceTests -nologo`
Expected: FAIL de compilación (`TeamStatusChangesService` no existe).

- [ ] **Step 3: Crear `StatusChangesScope.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Las tres vistas del reporte de cambios de estado (Plan J).</summary>
public enum StatusChangesScope
{
    /// <summary>Equipo de inscripción (prefijo de genealogía).</summary>
    Enrollment = 1,

    /// <summary>Árbol binario (se reduce por fecha y luego se comprueba la pertenencia).</summary>
    DualTeam = 2,

    /// <summary>Clientes (ExternalMember) del equipo de inscripción.</summary>
    Customers = 3
}

public static class StatusChangesScopes
{
    /// <summary>"enrollment", "dual" o "customers", sin distinguir mayúsculas.</summary>
    public static bool TryParse(string? value, out StatusChangesScope scope)
    {
        switch (value?.ToLowerInvariant())
        {
            case "enrollment": scope = StatusChangesScope.Enrollment; return true;
            case "dual":       scope = StatusChangesScope.DualTeam;   return true;
            case "customers":  scope = StatusChangesScope.Customers;  return true;
            default:           scope = default;                       return false;
        }
    }
}
```

- [ ] **Step 4: Crear `TeamStatusChangeDtos.cs`**

```csharp
using System.Text.Json.Serialization;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Una fila del reporte de cambios de estado: estado ACTUAL de la cuenta y de la membresía, con la
/// fecha de su último cambio, y si está calificado (J15). Las columnas de MemberProfiles se buscan,
/// filtran y ordenan en la base; el resto se rellena por página.
/// </summary>
public sealed class TeamStatusChangeView
{
    public string    MemberId                  { get; set; } = string.Empty;
    public string    FirstName                 { get; set; } = string.Empty;
    public string    LastName                  { get; set; } = string.Empty;
    public DateTime  EnrollDate                { get; set; }
    public string    AccountStatus             { get; set; } = string.Empty;
    public DateTime? AccountStatusChangedAt    { get; set; }
    public string    MembershipStatus          { get; set; } = "None";
    public string?   MembershipLevelName       { get; set; }
    public DateTime? MembershipStatusChangedAt { get; set; }
    public bool      IsQualified               { get; set; }
    public DateTime? QualifiedChangedAt        { get; set; }
    public string?   SponsorMemberId           { get; set; }
    public string?   SponsorFullName           { get; set; }
    public string?   DualUplineMemberId        { get; set; }
    public string?   DualUplineFullName        { get; set; }
    /// <summary>"Left" o "Right"; solo en la vista Dual Team.</summary>
    public string?   Leg                       { get; set; }

    /// <summary>Ruta binaria, solo para calcular la pierna en el servidor. Nunca sale (se anula y no se serializa).</summary>
    [JsonIgnore]
    public string?   DualPath                  { get; set; }
}
```

- [ ] **Step 5: Crear `ITeamStatusChangesService.cs`**

```csharp
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Reporte de cambios de estado del equipo (Plan J, bloque 5).</summary>
public interface ITeamStatusChangesService
{
    /// <summary>
    /// Miembros del alcance con algún cambio registrado entre <paramref name="from"/> y
    /// <paramref name="to"/> (días inclusivos, <see cref="ReportDateRange"/>).
    /// </summary>
    Task<Result<PagedResult<TeamStatusChangeView>>> GetGridAsync(
        string memberId, StatusChangesScope scope, DateTime from, DateTime to,
        GridDataRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 6: Crear `TeamStatusChangesService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
/// <remarks>
/// En SQL: el UNION de los MemberId con cambios en el rango (los dos historiales, por su índice de
/// ChangedAt), cruzado con el alcance y proyectado a columnas de MemberProfiles, con búsqueda,
/// filtro, orden, cuenta y página. Por página: suscripción actual, últimas fechas, calificación,
/// nombres y pierna.
/// </remarks>
public sealed class TeamStatusChangesService : ITeamStatusChangesService
{
    private static readonly string[] DbFields =
    {
        nameof(TeamStatusChangeView.MemberId),
        nameof(TeamStatusChangeView.FirstName),
        nameof(TeamStatusChangeView.LastName),
        nameof(TeamStatusChangeView.EnrollDate),
        nameof(TeamStatusChangeView.AccountStatus),
        nameof(TeamStatusChangeView.SponsorMemberId)
    };

    private static readonly string[] SearchableFields =
    {
        nameof(TeamStatusChangeView.MemberId),
        nameof(TeamStatusChangeView.FirstName),
        nameof(TeamStatusChangeView.LastName)
    };

    private readonly AppDbContext _db;

    public TeamStatusChangesService(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<TeamStatusChangeView>>> GetGridAsync(
        string memberId, StatusChangesScope scope, DateTime from, DateTime to,
        GridDataRequest request, CancellationToken ct = default)
    {
        if (!ReportDateRange.TryCreate(from, to, out var range, out var error))
            return Result<PagedResult<TeamStatusChangeView>>.Failure(error!, "Invalid date range.");

        Sanitize(request);

        var start = range.Start;
        var end   = range.End;

        // Primero se reduce por fecha (J17): solo quien tiene un cambio registrado en el rango.
        var changedIds = _db.MemberStatusHistories.AsNoTracking()
            .Where(h => h.ChangedAt >= start && h.ChangedAt < end)
            .Select(h => h.MemberId)
            .Union(_db.MembershipStatusHistories.AsNoTracking()
                .Where(h => h.ChangedAt >= start && h.ChangedAt < end)
                .Select(h => h.MemberId));

        var query = scope == StatusChangesScope.DualTeam
            ? await DualTeamQueryAsync(memberId, changedIds, ct)
            : await EnrollmentQueryAsync(memberId, changedIds, scope == StatusChangesScope.Customers, ct);

        if (query is null)
            return Result<PagedResult<TeamStatusChangeView>>.Success(new PagedResult<TeamStatusChangeView>
            {
                Page = Math.Max(1, request.Page), PageSize = request.PageSize <= 0 ? 20 : request.PageSize
            });

        var page = await query.ToGridResultAsync(request, SearchableFields, ct: ct);
        var rows = page.Items as List<TeamStatusChangeView> ?? page.Items.ToList();
        await EnrichAsync(rows, memberId, scope, ct);
        page.Items = rows;
        return Result<PagedResult<TeamStatusChangeView>>.Success(page);
    }

    /// <summary>Inscripción y clientes: prefijo de la genealogía (nodo más reciente del que mira, R8).</summary>
    private async Task<IQueryable<TeamStatusChangeView>?> EnrollmentQueryAsync(
        string memberId, IQueryable<string> changedIds, bool customersOnly, CancellationToken ct)
    {
        var prefix = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        if (prefix is null) return null;

        var teamIds = _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(prefix) && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .Distinct();

        return
            from id in changedIds
            join t in teamIds on id equals t
            join m in _db.MemberProfiles.AsNoTracking() on id equals m.MemberId
            where !customersOnly || m.MemberType == MemberType.ExternalMember
            select new TeamStatusChangeView
            {
                MemberId        = m.MemberId,
                FirstName       = m.FirstName,
                LastName        = m.LastName,
                EnrollDate      = m.EnrollDate,
                AccountStatus   = m.Status.ToString(),
                SponsorMemberId = m.SponsorMemberId
            };
    }

    /// <summary>
    /// Dual Team: los MemberId del rango se cruzan con DualTeamTree por MemberId (índice) y solo
    /// sobre esas filas se comprueba el prefijo binario, que no es indexable (J17).
    /// </summary>
    private async Task<IQueryable<TeamStatusChangeView>?> DualTeamQueryAsync(
        string memberId, IQueryable<string> changedIds, CancellationToken ct)
    {
        var prefix = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.MemberId == memberId)
            .Select(d => d.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        if (prefix is null) return null;

        return
            from id in changedIds
            join d in _db.DualTeamTree.AsNoTracking() on id equals d.MemberId
            where d.HierarchyPath.StartsWith(prefix) && d.MemberId != memberId
            join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
            select new TeamStatusChangeView
            {
                MemberId        = m.MemberId,
                FirstName       = m.FirstName,
                LastName        = m.LastName,
                EnrollDate      = m.EnrollDate,
                AccountStatus   = m.Status.ToString(),
                SponsorMemberId = m.SponsorMemberId,
                DualPath        = d.HierarchyPath
            };
    }

    private async Task EnrichAsync(List<TeamStatusChangeView> rows, string viewerId, StatusChangesScope scope, CancellationToken ct)
    {
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.MemberId).ToList();

        var current = (await _db.MembershipSubscriptions.AsNoTracking()
                .Include(s => s.MembershipLevel)
                .Where(s => ids.Contains(s.MemberId))
                .ToListAsync(ct))
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartDate).ThenByDescending(s => s.CreationDate).First());

        var accountLast = await _db.MemberStatusHistories.AsNoTracking()
            .Where(h => ids.Contains(h.MemberId))
            .GroupBy(h => h.MemberId)
            .Select(g => new { MemberId = g.Key, Last = g.Max(h => h.ChangedAt) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Last, ct);

        var membershipHistory = (await _db.MembershipStatusHistories.AsNoTracking()
                .Where(h => ids.Contains(h.MemberId))
                .Select(h => new { h.MemberId, h.SubscriptionId, h.OldStatus, h.NewStatus, h.ChangedAt })
                .ToListAsync(ct))
            .GroupBy(h => h.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var uplines = (await _db.DualTeamTree.AsNoTracking()
                .Where(d => ids.Contains(d.MemberId))
                .Select(d => new { d.MemberId, d.ParentMemberId })
                .ToListAsync(ct))
            .GroupBy(d => d.MemberId)
            .ToDictionary(g => g.Key, g => g.First().ParentMemberId);

        var nameIds = rows.Where(r => r.SponsorMemberId != null).Select(r => r.SponsorMemberId!)
            .Concat(uplines.Values.Where(v => v != null).Select(v => v!))
            .Distinct().ToList();
        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(m => nameIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        var legs = scope == StatusChangesScope.DualTeam
            ? await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId == viewerId)
                .ToDictionaryAsync(d => d.MemberId, d => d.Side, ct)
            : new Dictionary<string, TreeSide>();

        foreach (var row in rows)
        {
            current.TryGetValue(row.MemberId, out var sub);
            var history = membershipHistory.TryGetValue(row.MemberId, out var h) ? h : new();

            row.AccountStatusChangedAt = accountLast.TryGetValue(row.MemberId, out var last) ? last : null;
            row.MembershipStatus       = sub?.SubscriptionStatus.ToString() ?? "None";
            row.MembershipLevelName    = sub?.MembershipLevel?.Name;
            row.IsQualified            = sub?.SubscriptionStatus == MembershipStatus.Active;

            row.MembershipStatusChangedAt = sub is null
                ? null
                : history.Where(x => x.SubscriptionId == sub.Id).Select(x => (DateTime?)x.ChangedAt).Max()
                  ?? QualificationTimeline.FallbackDate(sub);

            row.QualifiedChangedAt =
                QualificationTimeline.LastFlip(history.Select(x =>
                    new QualificationTimeline.Change(x.SubscriptionId, x.OldStatus, x.NewStatus, x.ChangedAt)))
                ?? (sub is null ? null : QualificationTimeline.FallbackDate(sub));

            row.SponsorFullName    = names.GetValueOrDefault(row.SponsorMemberId ?? string.Empty);
            row.DualUplineMemberId = uplines.GetValueOrDefault(row.MemberId);
            row.DualUplineFullName = names.GetValueOrDefault(row.DualUplineMemberId ?? string.Empty);

            if (scope == StatusChangesScope.DualTeam && row.DualPath is not null)
            {
                var segments = row.DualPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var at       = Array.IndexOf(segments, viewerId);
                var gateway  = at >= 0 && at + 1 < segments.Length ? segments[at + 1] : null;
                row.Leg      = gateway is not null && legs.TryGetValue(gateway, out var side) ? side.ToString() : null;
            }

            row.DualPath = null; // nunca sale del servidor
        }
    }

    /// <summary>Solo se ordena y filtra en la base por columnas de la proyección ligera (R6).</summary>
    private static void Sanitize(GridDataRequest request)
    {
        request.Sorts   = request.Sorts.Where(s => DbFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        request.Filters = request.Filters.Where(f => DbFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(TeamStatusChangeView.LastName), Direction = "asc" });
    }
}
```

- [ ] **Step 7: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TeamStatusChangesServiceTests -nologo`
Expected: PASS (11 pruebas).

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Teams/StatusChangesScope.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/TeamStatusChangeDtos.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/ITeamStatusChangesService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/TeamStatusChangesService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamStatusChangesServiceTests.cs
git commit -m "Cambios de estado: servicio con tres vistas (inscripcion y clientes por prefijo de genealogia, Dual Team reduciendo primero por fecha y comprobando despues el arbol binario) que da el estado actual de cuenta y membresia con la fecha de su ultimo cambio, si esta calificado y desde cuando, patrocinador, upline y pierna"
```

---

### Task 31: Endpoint de cambios de estado

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter/Controllers/TeamReportsController.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (registro)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamReportsControllerTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

Al final de la clase `TeamReportsControllerTests` (antes de su última `}`), añadir:

```csharp
    [Fact]
    public async Task Los_cambios_son_los_del_miembro_del_token_con_la_vista_de_la_ruta()
    {
        var cambios = new Mock<ITeamStatusChangesService>();
        cambios.Setup(s => s.GetGridAsync("AMB-000777", StatusChangesScope.DualTeam, Desde, Hasta, It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<PagedResult<TeamStatusChangeView>>.Success(new PagedResult<TeamStatusChangeView>()));

        var r = await Control().GetStatusChangesGrid("dual", new GridDataRequest(), Desde, Hasta, cambios.Object, CancellationToken.None);

        r.Should().BeOfType<OkObjectResult>();
        cambios.VerifyAll();
    }

    [Fact]
    public async Task Una_vista_que_no_existe_es_404_sin_consultar()
    {
        var cambios = new Mock<ITeamStatusChangesService>(MockBehavior.Strict);

        var r = await Control().GetStatusChangesGrid("binary", new GridDataRequest(), Desde, Hasta, cambios.Object, CancellationToken.None);

        r.Should().BeOfType<NotFoundObjectResult>();
    }
```

Y en `MiembroDelTokenTests.cs`, sustituir:

```csharp
        // (acciones de los bloques siguientes)
```

por:

```csharp
        (typeof(TeamReportsController), nameof(TeamReportsController.GetStatusChangesGrid)),
        // (acciones de los bloques siguientes)
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "TeamReportsControllerTests|MiembroDelTokenTests" -nologo`
Expected: FAIL de compilación (`GetStatusChangesGrid` no existe).

- [ ] **Step 3: Acción en `TeamReportsController.cs`**

Sustituir:

```csharp
    private IActionResult Invalid<T>(Result<T> result) =>
```

por:

```csharp
    /// <summary>
    /// POST /api/v1/bizcenter/team/changes/{scope}/grid?from=yyyy-MM-dd&amp;to=yyyy-MM-dd, con
    /// scope = enrollment | dual | customers. Miembros del equipo del token con un cambio de estado
    /// registrado en el rango, con su estado actual y la fecha de su último cambio.
    /// </summary>
    [HttpPost("changes/{scope}/grid")]
    [ReadOnlySafe]
    public async Task<IActionResult> GetStatusChangesGrid(
        string scope,
        [FromBody]  GridDataRequest request,
        [FromQuery] DateTime        from,
        [FromQuery] DateTime        to,
        [FromServices] ITeamStatusChangesService changes,
        CancellationToken ct = default)
    {
        if (!StatusChangesScopes.TryParse(scope, out var parsed))
            return NotFound(ApiResponse<PagedResult<TeamStatusChangeView>>.Fail(
                "REPORT_SCOPE_NOT_FOUND", "Unknown report.", HttpContext.TraceIdentifier));

        var result = await changes.GetGridAsync(_currentUser.MemberId, parsed, from, to, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<TeamStatusChangeView>>.Ok(result.Value!))
            : Invalid(result);
    }

    private IActionResult Invalid<T>(Result<T> result) =>
```

- [ ] **Step 4: Registro**

En `BizCenter/Program.cs`, sustituir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IEnrollmentActivityService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.EnrollmentActivityService>();
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IEnrollmentActivityService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.EnrollmentActivityService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.ITeamStatusChangesService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.TeamStatusChangesService>();
```

- [ ] **Step 5: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "TeamReportsControllerTests|MiembroDelTokenTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.BizCenter -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Controllers/TeamReportsController.cs \
        MLMConquerorGlobalEdition.BizCenter/Program.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamReportsControllerTests.cs
git commit -m "Cambios de estado: POST team/changes/{enrollment|dual|customers}/grid con el miembro del token, 404 REPORT_SCOPE_NOT_FOUND para otra vista y 400 con el codigo del rango"
```

---

### Task 32: Componente único con tres vistas, páginas, menú y textos

> **Corrección de Nick N3 y N4.** Sin columna «Calificado desde»; la columna «Calificado» es el valor actual de `IQualificationService`. El selector de fechas sigue J12 (366 días con `_maxDays` del *setup*); N4 es solo para *Actividad*.

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Team/TeamStatusChangesGrid.razor`
- Create: `MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/EnrollmentChanges.razor`
- Create: `MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/DualTeamChanges.razor`
- Create: `MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/CustomerChanges.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor`
- Modify: los nueve `SharedResources*.resx`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla (textos)**

En `TextosDeReportesTests.cs`, sustituir:

```csharp
        // (claves de los bloques siguientes)
```

por:

```csharp
        // Bloque 5 — Cambios de estado
        "Sidebar.StatusChanges", "Sidebar.CustomerChanges", "PageTitle.EnrollmentChanges",
        "PageTitle.DualTeamChanges", "PageTitle.CustomerChanges", "Changes.FirstName",
        "Changes.LastName", "Changes.ChangedOn", "Changes.NoHistory",
        // (claves de los bloques siguientes)
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: FAIL.

- [ ] **Step 2: Añadir las claves a los nueve `.resx`**

| Clave | en | es | pt | fr | it | de | ko | zh | ka |
|---|---|---|---|---|---|---|---|---|---|
| `Sidebar.StatusChanges` | Status changes | Cambios de estado | Mudanças de status | Changements de statut | Cambi di stato | Statusänderungen | 상태 변경 | 状态变更 | სტატუსის ცვლილებები |
| `Sidebar.CustomerChanges` | Customer changes | Cambios de clientes | Mudanças de clientes | Changements des clients | Cambi dei clienti | Kundenänderungen | 고객 변경 | 客户变更 | კლიენტების ცვლილებები |
| `PageTitle.EnrollmentChanges` | Enrollment team status changes | Cambios de estado del equipo de inscripción | Mudanças de status da equipe de inscrição | Changements de statut de l'équipe d'inscription | Cambi di stato del team di iscrizione | Statusänderungen im Anmeldeteam | 등록 팀 상태 변경 | 注册团队状态变更 | რეგისტრაციის გუნდის სტატუსის ცვლილებები |
| `PageTitle.DualTeamChanges` | Dual Team status changes | Cambios de estado del Dual Team | Mudanças de status do Dual Team | Changements de statut de la Dual Team | Cambi di stato del Dual Team | Statusänderungen im Dual Team | 듀얼 팀 상태 변경 | 双轨团队状态变更 | Dual Team-ის სტატუსის ცვლილებები |
| `PageTitle.CustomerChanges` | Customer status changes | Cambios de estado de clientes | Mudanças de status de clientes | Changements de statut des clients | Cambi di stato dei clienti | Statusänderungen bei Kunden | 고객 상태 변경 | 客户状态变更 | კლიენტების სტატუსის ცვლილებები |
| `Changes.FirstName` | First name | Nombre | Nome | Prénom | Nome | Vorname | 이름 | 名 | სახელი |
| `Changes.LastName` | Last name | Apellido | Sobrenome | Nom | Cognome | Nachname | 성 | 姓 | გვარი |
| `Changes.ChangedOn` | Changed on {0} | Cambió el {0} | Alterado em {0} | Modifié le {0} | Modificato il {0} | Geändert am {0} | {0}에 변경됨 | 变更于 {0} | შეიცვალა {0} |
| `Changes.NoHistory` | No change recorded yet | Aún no hay cambios registrados | Ainda não há mudanças registradas | Aucun changement enregistré pour l'instant | Nessuna modifica registrata finora | Noch keine Änderung erfasst | 아직 기록된 변경이 없습니다 | 暂无变更记录 | ცვლილება ჯერ არ დაფიქსირებულა |

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 3: Crear `TeamStatusChangesGrid.razor`**

```razor
@inject HttpClient Http
@inject IStringLocalizer<SharedResources> L

@using System.Globalization
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.Extensions.Localization
@using MLMConquerorGlobalEdition.SharedComponents.Resources
@using MLMConquerorGlobalEdition.SharedComponents.Components.Grid
@using Syncfusion.Blazor
@using Syncfusion.Blazor.Grids
@using Syncfusion.Blazor.Data

@*
    Cambios de estado (Plan J). UN componente para las tres vistas: lo que cambia entre ellas
    (qué endpoint, migas, columna de pierna) sale del parámetro Scope. Estado actual y fecha del
    último cambio; las columnas enriquecidas no se ordenan ni se filtran en el servidor (R6).
*@

@if (ShowBreadcrumb) { <BreadcrumbNav Items="@Breadcrumb" /> }

<div class="card bg-white border-0 rounded-3 mb-4 status-changes" data-scope="@Scope">
    <div class="card-body p-0">
        <div class="d-flex align-items-end flex-wrap gap-2 p-3">
            <div>
                <label class="form-label fs-12 mb-1 fw-medium" for="chg-from">@L["Common.From"]</label>
                <input id="chg-from" type="date" class="form-control form-control-sm" @bind="_from" />
            </div>
            <div>
                <label class="form-label fs-12 mb-1 fw-medium" for="chg-to">@L["Common.To"]</label>
                <input id="chg-to" type="date" class="form-control form-control-sm" @bind="_to" />
            </div>
            <button class="btn btn-primary btn-sm" disabled="@(!_ready)" @onclick="ApplyAsync">@L["Reports.Apply"]</button>
        </div>

        @if (_message is not null)
        {
            <div class="alert alert-warning mx-3">@_message</div>
        }

        @if (_ready)
        {
            <SfGrid TValue="ChangeDto" @ref="_grid"
                    AllowPaging="true" AllowSorting="true" AllowResizing="true"
                    AllowFiltering="true" GridLines="GridLine.Horizontal">
                <SfDataManager Adaptor="Adaptors.CustomAdaptor">
                    <ServerGridAdaptor TValue="ChangeDto"
                                       Http="Http"
                                       Endpoint="@($"{ApiBaseUrl}/changes/{Scope}/grid")"
                                       QueryStringProvider="RangeQuery"
                                       AccessTokenProvider="GetTokenAsync" />
                </SfDataManager>
                <GridPageSettings PageSize="20" PageSizes="@(new int[]{20,50,100})" />
                <GridFilterSettings Type="Syncfusion.Blazor.Grids.FilterType.Excel" />
                <GridColumns>
                    <GridColumn Field="@nameof(ChangeDto.FirstName)" HeaderText="@L["Changes.FirstName"]" Width="130" Type="ColumnType.String" />
                    <GridColumn Field="@nameof(ChangeDto.LastName)" HeaderText="@L["Changes.LastName"]" Width="130" Type="ColumnType.String" />
                    <GridColumn Field="@nameof(ChangeDto.MemberId)" HeaderText="@L["Reports.Id"]" Width="120" Type="ColumnType.String" />

                    <GridColumn Field="@nameof(ChangeDto.AccountStatus)" HeaderText="@L["Reports.BizCenterStatus"]" Width="150" Type="ColumnType.String">
                        <Template>
                            @{ var c = (ChangeDto)context; }
                            <StatusBadge Status="@c.AccountStatus" />
                            <div class="fs-12 text-muted">@ChangedOn(c.AccountStatusChangedAt)</div>
                        </Template>
                    </GridColumn>

                    <GridColumn Field="@nameof(ChangeDto.MembershipStatus)" HeaderText="@L["Reports.MembershipStatus"]" Width="190"
                                AllowSorting="false" AllowFiltering="false">
                        <Template>
                            @{ var c = (ChangeDto)context; }
                            <StatusBadge Status="@c.MembershipStatus" />
                            <div class="fs-12">@(c.MembershipLevelName ?? "—")</div>
                            <div class="fs-12 text-muted">@ChangedOn(c.MembershipStatusChangedAt)</div>
                        </Template>
                    </GridColumn>

                    <GridColumn Field="@nameof(ChangeDto.IsQualified)" HeaderText="@L["Reports.Qualified"]" Width="140"
                                AllowSorting="false" AllowFiltering="false">
                        <Template>
                            @{ var c = (ChangeDto)context; }
                            <span class="badge @(c.IsQualified ? "bg-success" : "bg-secondary")">@(c.IsQualified ? L["Common.Yes"] : L["Common.No"])</span>
                            <div class="fs-12 text-muted">@ChangedOn(c.QualifiedChangedAt)</div>
                        </Template>
                    </GridColumn>

                    <GridColumn Field="@nameof(ChangeDto.SponsorMemberId)" HeaderText="@L["Reports.Sponsor"]" Width="160" Type="ColumnType.String">
                        <Template>
                            @{ var c = (ChangeDto)context; }
                            @(c.SponsorFullName ?? c.SponsorMemberId ?? "—")
                        </Template>
                    </GridColumn>

                    <GridColumn Field="@nameof(ChangeDto.DualUplineMemberId)" HeaderText="@L["Reports.DualTeamUpline"]" Width="160"
                                AllowSorting="false" AllowFiltering="false">
                        <Template>
                            @{ var c = (ChangeDto)context; }
                            @(c.DualUplineFullName ?? c.DualUplineMemberId ?? "—")
                        </Template>
                    </GridColumn>

                    @if (IsDual)
                    {
                        <GridColumn Field="@nameof(ChangeDto.Leg)" HeaderText="@L["Reports.Leg"]" Width="90" TextAlign="TextAlign.Center"
                                    AllowSorting="false" AllowFiltering="false">
                            <Template>@(((ChangeDto)context).Leg == "Left" ? L["Reports.Left"] : ((ChangeDto)context).Leg == "Right" ? L["Reports.Right"] : "—")</Template>
                        </GridColumn>
                    }
                </GridColumns>
            </SfGrid>
        }
    </div>
</div>

@code {
    /// <summary>"enrollment", "dual" o "customers": la vista y el segmento de la ruta del API.</summary>
    [Parameter, EditorRequired] public string Scope { get; set; } = "enrollment";

    /// <summary>Raíz del API de equipo, p. ej. "api/v1/bizcenter/team".</summary>
    [Parameter] public string ApiBaseUrl     { get; set; } = "api/v1/bizcenter/team";
    [Parameter] public bool   ShowBreadcrumb { get; set; } = true;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private SfGrid<ChangeDto>? _grid;
    private DateTime _from;
    private DateTime _to;
    private DateTime _appliedFrom;
    private DateTime _appliedTo;
    private int      _maxDays = 366;
    private bool     _ready;
    private string?  _message;

    private bool IsDual => string.Equals(Scope, "dual", StringComparison.OrdinalIgnoreCase);

    private List<BreadcrumbNav.BreadcrumbItem> Breadcrumb => Scope.ToLowerInvariant() switch
    {
        "dual"      => new() { new(L["Nav.Dashboard"], "/dashboard"), new(L["Sidebar.DualTeam"]),       new(L["Sidebar.StatusChanges"]) },
        "customers" => new() { new(L["Nav.Dashboard"], "/dashboard"), new(L["Sidebar.EnrollmentTeam"]), new(L["Sidebar.CustomerChanges"]) },
        _           => new() { new(L["Nav.Dashboard"], "/dashboard"), new(L["Sidebar.EnrollmentTeam"]), new(L["Sidebar.StatusChanges"]) }
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var setup = await Http.GetFromJsonAsync<ApiWrapper<SetupDto>>($"{ApiBaseUrl}/reports/setup");
            if (setup?.Success == true && setup.Data is not null)
            {
                _from    = _appliedFrom = setup.Data.DefaultFrom;
                _to      = _appliedTo   = setup.Data.DefaultTo;
                _maxDays = setup.Data.MaxRangeDays;
                _ready   = true;
            }
            else
            {
                _message = L["Common.Error"];
            }
        }
        catch (Exception)
        {
            _message = L["Common.Error"];
        }
    }

    private string ChangedOn(DateTime? at) =>
        at is { } d ? L["Changes.ChangedOn", d.ToString("d", CultureInfo.CurrentUICulture)] : L["Changes.NoHistory"];

    private string? RangeQuery() =>
        $"from={_appliedFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={_appliedTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    private async Task ApplyAsync()
    {
        if (ReportRangeCheck.Problem(_from, _to, _maxDays) is { } problem)
        {
            _message = problem.Arg is null ? L[problem.Key] : L[problem.Key, problem.Arg];
            return;
        }

        _message     = null;
        _appliedFrom = _from;
        _appliedTo   = _to;
        if (_grid is not null) await _grid.Refresh();
    }

    private async Task<string?> GetTokenAsync()
    {
        if (AuthState is null) return null;
        var state = await AuthState;
        return state.User.FindFirst("access_token")?.Value;
    }

    private sealed class ApiWrapper<T> { public bool Success { get; set; } public T? Data { get; set; } }

    private sealed class SetupDto
    {
        public DateTime DefaultFrom  { get; set; }
        public DateTime DefaultTo    { get; set; }
        public int      MaxRangeDays { get; set; }
    }

    public sealed class ChangeDto
    {
        public string    MemberId                  { get; set; } = string.Empty;
        public string    FirstName                 { get; set; } = string.Empty;
        public string    LastName                  { get; set; } = string.Empty;
        public DateTime  EnrollDate                { get; set; }
        public string    AccountStatus             { get; set; } = string.Empty;
        public DateTime? AccountStatusChangedAt    { get; set; }
        public string    MembershipStatus          { get; set; } = string.Empty;
        public string?   MembershipLevelName       { get; set; }
        public DateTime? MembershipStatusChangedAt { get; set; }
        public bool      IsQualified               { get; set; }
        public DateTime? QualifiedChangedAt        { get; set; }
        public string?   SponsorMemberId           { get; set; }
        public string?   SponsorFullName           { get; set; }
        public string?   DualUplineMemberId        { get; set; }
        public string?   DualUplineFullName        { get; set; }
        public string?   Leg                       { get; set; }
    }
}
```

- [ ] **Step 4: Las tres páginas finas**

`EnrollmentChanges.razor`:

```razor
@page "/team/enrollment/changes"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject Microsoft.Extensions.Localization.IStringLocalizer<MLMConquerorGlobalEdition.SharedComponents.Resources.SharedResources> L

<PageTitle>@L["PageTitle.EnrollmentChanges"]</PageTitle>

<TeamStatusChangesGrid Scope="enrollment" />
```

`DualTeamChanges.razor`:

```razor
@page "/team/dual-changes"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject Microsoft.Extensions.Localization.IStringLocalizer<MLMConquerorGlobalEdition.SharedComponents.Resources.SharedResources> L

<PageTitle>@L["PageTitle.DualTeamChanges"]</PageTitle>

<TeamStatusChangesGrid Scope="dual" />
```

`CustomerChanges.razor`:

```razor
@page "/team/enrollment/customer-changes"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject Microsoft.Extensions.Localization.IStringLocalizer<MLMConquerorGlobalEdition.SharedComponents.Resources.SharedResources> L

<PageTitle>@L["PageTitle.CustomerChanges"]</PageTitle>

<TeamStatusChangesGrid Scope="customers" />
```

- [ ] **Step 5: Menú**

En `SidebarMenu.razor`, grupo *Equipo de inscripción*: sustituir `                <span class="count">5</span>` por `                <span class="count">7</span>`, y sustituir:

```razor
                <li class="menu-item @IsActive("/team/enrollment/customers")">
                    <a href="/team/enrollment/customers" class="menu-link">@L["Sidebar.Customers"]</a>
                </li>
                <li class="menu-item @IsActive("/team/enrollment/activity")">
                    <a href="/team/enrollment/activity" class="menu-link">@L["Sidebar.Activity"]</a>
                </li>
```

por:

```razor
                <li class="menu-item @IsActive("/team/enrollment/customers")">
                    <a href="/team/enrollment/customers" class="menu-link">@L["Sidebar.Customers"]</a>
                </li>
                <li class="menu-item @IsActive("/team/enrollment/customer-changes")">
                    <a href="/team/enrollment/customer-changes" class="menu-link">@L["Sidebar.CustomerChanges"]</a>
                </li>
                <li class="menu-item @IsActive("/team/enrollment/activity")">
                    <a href="/team/enrollment/activity" class="menu-link">@L["Sidebar.Activity"]</a>
                </li>
                <li class="menu-item @IsActive("/team/enrollment/changes")">
                    <a href="/team/enrollment/changes" class="menu-link">@L["Sidebar.StatusChanges"]</a>
                </li>
```

Grupo *Dual Team*: sustituir `                    <span class="count">3</span>` por `                    <span class="count">4</span>`, y sustituir:

```razor
                    <li class="menu-item @IsActive("/team/residuals")">
                        <a href="/team/residuals" class="menu-link">@L["Sidebar.Residuals"]</a>
                    </li>
```

por:

```razor
                    <li class="menu-item @IsActive("/team/residuals")">
                        <a href="/team/residuals" class="menu-link">@L["Sidebar.Residuals"]</a>
                    </li>
                    <li class="menu-item @IsActive("/team/dual-changes")">
                        <a href="/team/dual-changes" class="menu-link">@L["Sidebar.StatusChanges"]</a>
                    </li>
```

- [ ] **Step 6: Compilar la web**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenterWeb -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Team/TeamStatusChangesGrid.razor \
        MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/EnrollmentChanges.razor \
        MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/DualTeamChanges.razor \
        MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/CustomerChanges.razor \
        MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs
git commit -m "Cambios de estado: un componente compartido para las vistas de inscripcion, Dual Team y clientes con estado actual y fecha del ultimo cambio, tres paginas finas, entradas en el menu y textos en los 9 idiomas"
```

---

### Task 33: Pruebas contra SQL Server de los cambios de estado

> **Corrección de Nick N3.** Las pruebas SQL Server del servicio de calificación sustituyen a las de `QualificationTimeline`.

**Files:**
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerCambiosDeEstadoTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El UNION de los dos historiales, los JOIN con el alcance y el prefijo binario se traducen a SQL y
/// dan lo mismo que en memoria; el interceptor escribe de verdad en las tablas nuevas.
/// </summary>
public class SqlServerCambiosDeEstadoTests
{
    private static TeamStatusChangesServiceTests.Ids Nuevos() => new(
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId("TC"),
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId());

    [SqlServerFact]
    public async Task Las_tres_vistas_se_traducen_a_SQL()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamStatusChangesServiceTests.EscenarioAsync(db, id);
        var servicio = new TeamStatusChangesService(db);
        async Task<List<string>> Filas(StatusChangesScope alcance) =>
            (await servicio.GetGridAsync(id.Yo, alcance, TeamStatusChangesServiceTests.Desde, TeamStatusChangesServiceTests.Hasta,
                new GridDataRequest { PageSize = 50 })).Value!.Items.Select(f => f.MemberId).ToList();

        (await Filas(StatusChangesScope.Enrollment)).Should().BeEquivalentTo(new[] { id.E1, id.E2, id.E3 });
        (await Filas(StatusChangesScope.Customers)).Should().Equal(id.E2);
        (await Filas(StatusChangesScope.DualTeam)).Should().BeEquivalentTo(new[] { id.E1, id.E3, id.R1 });

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_interceptor_escribe_en_las_tablas_de_historial()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        var perfil = ReportesDb.Miembro(db, yo, estado: MemberAccountStatus.Pending);
        ReportesDb.Suscripcion(db, yo, 3, MembershipStatus.Pending, new DateTime(2026, 3, 1));
        await db.SaveChangesAsync();

        perfil.Status = MemberAccountStatus.Active;
        await db.SaveChangesAsync();

        (await db.MemberStatusHistories.CountAsync(h => h.MemberId == yo)).Should().Be(1);
        (await db.MembershipStatusHistories.CountAsync(h => h.MemberId == yo)).Should().Be(1);

        await tx.RollbackAsync();
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerCambiosDeEstadoTests -nologo`
Expected: PASS (2) con la migración `HistorialDeEstados` aplicada; `Skipped` sin SQL Server.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerCambiosDeEstadoTests.cs
git commit -m "Pruebas contra SQL Server de las tres vistas de cambios de estado y de que el interceptor escribe en los dos historiales"
```

**Fin del Bloque 5.** Una migración (`HistorialDeEstados`). Las Tareas 27 y 28 se pueden fusionar y desplegar antes que el resto del bloque (R4).

---

# Bloque 6 — Eventos del equipo

### Task 34: Entidad de inscripción a evento y migración `InscripcionesAEventos`

> **Corrección de Nick N5.** Se mantiene: la entidad es la fuente del reporte. No hay importador en este plan; el índice único `(EventId, ExternalRef)` se crea para el importador futuro.

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Events/CorporateEventRegistration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/CorporateEventRegistrationConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:110`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_InscripcionesAEventos.cs` (vía `dotnet ef`)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

En `IndicesDeReportesTests.cs`, añadir `using MLMConquerorGlobalEdition.Domain.Entities.Events;` y sustituir:

```csharp
    // (índices de los bloques siguientes)
```

por:

```csharp
    [Fact]
    public void Inscripciones_a_eventos_unicas_por_evento_y_referencia_externa()
    {
        using var db = ReportesDb.Nueva();
        db.Model.FindEntityType(typeof(CorporateEventRegistration))!.GetIndexes()
            .Should().Contain(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "EventId", "ExternalRef" }));
        TieneIndice(typeof(CorporateEventRegistration), "EventId", "MemberId").Should().BeTrue();
    }

    // (índices de los bloques siguientes)
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: FAIL de compilación (`CorporateEventRegistration` no existe).

- [ ] **Step 2: Crear `CorporateEventRegistration.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

/// <summary>
/// Una compra de entradas de un miembro para un evento corporativo (Plan J, J18). Llegan por
/// importación CSV desde el Admin; la referencia externa es la de la compra en la plataforma de
/// entradas y es única por evento, así que reimportar el mismo archivo no duplica nada.
/// </summary>
public class CorporateEventRegistration : AuditChangesLongKey
{
    public const string CsvImportSource = "CsvImport";

    public string    EventId      { get; set; } = string.Empty;
    public string    MemberId     { get; set; } = string.Empty;
    public DateTime  RegisteredAt { get; set; }
    public int       Tickets      { get; set; }
    public decimal?  Price        { get; set; }
    public string    Source       { get; set; } = CsvImportSource;
    public string    ExternalRef  { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Crear `CorporateEventRegistrationConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CorporateEventRegistrationConfiguration : IEntityTypeConfiguration<CorporateEventRegistration>
{
    public void Configure(EntityTypeBuilder<CorporateEventRegistration> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ExternalRef).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasOne<CorporateEvent>().WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany()
            .HasForeignKey(x => x.MemberId)
            .HasPrincipalKey(m => m.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // La idempotencia de la importación la garantiza la base: una compra (referencia externa)
        // entra una sola vez por evento, aunque se importe el mismo archivo dos veces a la vez.
        builder.HasIndex(x => new { x.EventId, x.ExternalRef }).IsUnique();
        builder.HasIndex(x => new { x.EventId, x.MemberId });
    }
}
```

- [ ] **Step 4: `DbSet` en `AppDbContext.cs`**

Sustituir:

```csharp
    public DbSet<CorporateEvent> CorporateEvents => Set<CorporateEvent>();
```

por:

```csharp
    public DbSet<CorporateEvent> CorporateEvents => Set<CorporateEvent>();
    public DbSet<CorporateEventRegistration> CorporateEventRegistrations => Set<CorporateEventRegistration>();
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter IndicesDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 6: Crear la migración**

Con el comando de la cabecera y el nombre `InscripcionesAEventos`. `Up` debe traer solo `CreateTable("CorporateEventRegistrations")` con sus dos claves foráneas (`CorporateEvents.Id` y `MemberProfiles.MemberId`, `Restrict`) y los índices `IX_CorporateEventRegistrations_EventId_ExternalRef` (único), `IX_CorporateEventRegistrations_EventId_MemberId` y el de `MemberId` que EF crea para la clave foránea.

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_InscripcionesAEventos.cs`
Expected: `0`

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Events/CorporateEventRegistration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/CorporateEventRegistrationConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_InscripcionesAEventos.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_InscripcionesAEventos.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/IndicesDeReportesTests.cs
git commit -m "Inscripciones a eventos corporativos: entidad con evento, miembro, fecha, entradas, precio, origen y referencia externa unica por evento; migracion InscripcionesAEventos"
```

- [ ] **Step 8: Aplicar la migración a la base local**

Con el comando de la cabecera. Expected: `Applying migration '…_InscripcionesAEventos'.` y `Done.`

---

### Task 35: Lector de CSV y neutralización de fórmulas (`CsvLite`)

> **FUERA (corrección de Nick N5).** Sin importación CSV en este plan.

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Events/CsvLite.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CorporateEvents/CsvLiteTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Events;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CorporateEvents;

public class CsvLiteTests
{
    private static List<CsvRecord> Leer(string texto) => CsvLite.Parse(new StringReader(texto)).ToList();

    [Fact]
    public void Separa_campos_y_registros_con_CRLF_o_LF()
    {
        var r = Leer("a,b\r\nc,d\ne,f");

        r.Select(x => string.Join("|", x.Fields)).Should().Equal("a|b", "c|d", "e|f");
        r.Select(x => x.Line).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Las_comillas_admiten_comas_y_comillas_dobles()
        => Leer("\"a,b\",\"c\"\"d\"").Single().Fields.Should().Equal("a,b", "c\"d");

    [Fact]
    public void Un_salto_dentro_de_comillas_no_parte_el_registro_y_las_lineas_siguen_contando()
    {
        var r = Leer("x,\"l1\nl2\"\ny,z");

        r.Should().HaveCount(2);
        r[0].Fields.Should().Equal("x", "l1\nl2");
        r[1].Line.Should().Be(3);
    }

    [Fact]
    public void Una_comilla_sin_cerrar_es_un_registro_malformado()
        => Leer("a,\"sin cerrar").Single().Malformed.Should().BeTrue();

    [Fact]
    public void Una_linea_final_vacia_no_es_un_registro()
        => Leer("a,b\n").Should().ContainSingle();

    [Theory]
    [InlineData("=1+1", true)]
    [InlineData("+1", true)]
    [InlineData("-1", true)]
    [InlineData("@SUM(A1)", true)]
    [InlineData("\tx", true)]
    [InlineData("\rx", true)]
    [InlineData("a=b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Detecta_lo_que_una_hoja_de_calculo_ejecutaria(string? valor, bool esFormula)
        => CsvLite.LooksLikeFormula(valor).Should().Be(esFormula);

    [Fact]
    public void Neutraliza_y_recorta_lo_que_devuelve()
    {
        CsvLite.Neutralize("=HYPERLINK(\"http://x\")").Should().Be("'=HYPERLINK(\"http://x\")");
        CsvLite.Neutralize("REF-1").Should().Be("REF-1");
        CsvLite.Neutralize(new string('x', 150)).Should().HaveLength(100);
        CsvLite.Neutralize(null).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter CsvLiteTests -nologo`
Expected: FAIL de compilación (`CsvLite` no existe).

- [ ] **Step 3: Crear `CsvLite.cs`**

```csharp
using System.Text;

namespace MLMConquerorGlobalEdition.Repository.Services.Events;

/// <summary>Un registro del CSV: la línea donde empieza (para el informe), sus campos y si quedó una comilla abierta.</summary>
public sealed record CsvRecord(int Line, IReadOnlyList<string> Fields, bool Malformed);

/// <summary>
/// Lector de CSV RFC 4180 (coma, comillas dobles, "" como comilla, saltos dentro de comillas) sin
/// dependencias, y defensa contra la inyección de fórmulas (Plan J, J19): un campo que empieza por
/// = + - @, tabulador o retorno lo ejecutaría una hoja de cálculo al abrir el archivo.
/// </summary>
public static class CsvLite
{
    private static readonly char[] FormulaPrefixes = { '=', '+', '-', '@', '\t', '\r' };

    public static bool LooksLikeFormula(string? value) =>
        !string.IsNullOrEmpty(value) && Array.IndexOf(FormulaPrefixes, value[0]) >= 0;

    /// <summary>
    /// Lo que se devuelve de un archivo ajeno (informe de rechazos): recortado y, si parece una
    /// fórmula, con un apóstrofo delante para que una hoja de cálculo lo trate como texto.
    /// </summary>
    public static string Neutralize(string? value, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var text = value.Length > maxLength ? value[..maxLength] : value;
        return LooksLikeFormula(text) ? "'" + text : text;
    }

    public static IEnumerable<CsvRecord> Parse(TextReader reader)
    {
        var fields     = new List<string>();
        var field      = new StringBuilder();
        var inQuotes   = false;
        var pending    = false;
        var line       = 1;
        var recordLine = 1;
        int read;

        while ((read = reader.Read()) != -1)
        {
            var ch = (char)read;
            pending = true;

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"') { field.Append('"'); reader.Read(); }
                    else inQuotes = false;
                }
                else
                {
                    if (ch == '\n') line++;
                    field.Append(ch);
                }
                continue;
            }

            if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
                continue;
            }

            if (ch == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                fields.Add(field.ToString());
                field.Clear();
                yield return new CsvRecord(recordLine, fields, Malformed: false);

                fields     = new List<string>();
                line++;
                recordLine = line;
                pending    = false;
                continue;
            }

            field.Append(ch);
        }

        if (pending || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return new CsvRecord(recordLine, fields, Malformed: inQuotes);
        }
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter CsvLiteTests -nologo`
Expected: PASS (15 pruebas).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Events/CsvLite.cs \
        MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CorporateEvents/CsvLiteTests.cs
git commit -m "Lector de CSV RFC 4180 sin dependencias con linea de inicio de cada registro y deteccion de comillas sin cerrar, y neutralizacion de valores que una hoja de calculo ejecutaria como formula"
```

---

### Task 36: Importador de inscripciones

> **FUERA (corrección de Nick N5).** Sin importación CSV en este plan.

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Events/EventRegistrationImportDtos.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Events/IEventRegistrationImporter.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Events/EventRegistrationImporter.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CorporateEvents/EventRegistrationImporterTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Codes = MLMConquerorGlobalEdition.Repository.Services.Events.EventRegistrationImportCodes;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CorporateEvents;

/// <summary>
/// Importación CSV de inscripciones: valida cada fila, informa de las rechazadas con línea y motivo,
/// es idempotente por referencia externa y no deja entrar fórmulas.
/// </summary>
public class EventRegistrationImporterTests
{
    private const string Evento   = "EV-1";
    private const string Cabecera = "MemberId,RegisteredAt,Tickets,Price,ExternalRef";

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now    => new(2026, 10, 16, 12, 0, 0);
        public DateTime UtcNow => Now.ToUniversalTime();
    }

    internal static async Task<AppDbContext> BaseAsync()
    {
        var db = InMemoryDbHelper.Create();
        db.CorporateEvents.Add(new CorporateEvent { Id = Evento, Title = "Convención", EventDate = new DateTime(2026, 11, 20), CreatedBy = "seed" });
        foreach (var id in new[] { "AMB-000001", "AMB-000002" })
            db.MemberProfiles.Add(new MemberProfile { UserId = Guid.NewGuid(), MemberId = id, FirstName = "N", LastName = id, Country = "US", CreatedBy = "seed" });
        await db.SaveChangesAsync();
        return db;
    }

    private static Task<MLMConquerorGlobalEdition.SharedKernel.Result<EventRegistrationImportResult>> Importar(
        AppDbContext db, string csv, string evento = Evento)
        => new EventRegistrationImporter(db, new Reloj()).ImportAsync(evento, new StringReader(csv), "admin@prueba.test");

    private static string Csv(params string[] filas) => Cabecera + "\n" + string.Join("\n", filas);

    [Fact]
    public async Task Importa_las_filas_validas_con_el_miembro_canonico()
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, Csv("amb-000001,2026-03-01,2,,REF-1", "AMB-000002,2026-03-01 18:30,1,49.99,REF-2"));

        r.IsSuccess.Should().BeTrue();
        (r.Value!.TotalRows, r.Value.Imported, r.Value.AlreadyImported, r.Value.Rejected).Should().Be((2, 2, 0, 0));
        var filas = await db.CorporateEventRegistrations.OrderBy(x => x.ExternalRef).ToListAsync();
        filas[0].MemberId.Should().Be("AMB-000001");
        (filas[1].RegisteredAt, filas[1].Price, filas[1].Tickets).Should().Be((new DateTime(2026, 3, 1, 18, 30, 0), (decimal?)49.99m, 1));
        filas.Should().OnlyContain(f => f.Source == CorporateEventRegistration.CsvImportSource && f.CreatedBy == "admin@prueba.test");
    }

    [Fact]
    public async Task Reimportar_el_mismo_archivo_no_duplica()
    {
        await using var db = await BaseAsync();
        var csv = Csv("AMB-000001,2026-03-01,2,,REF-1", "AMB-000002,2026-03-01,1,,REF-2");

        await Importar(db, csv);
        var segunda = await Importar(db, csv);

        (segunda.Value!.Imported, segunda.Value.AlreadyImported).Should().Be((0, 2));
        (await db.CorporateEventRegistrations.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("AMB-000001,2026-03-01,0,,REF-1",                    Codes.InvalidTickets)]
    [InlineData("AMB-000001,2026-03-01,101,,REF-1",                  Codes.InvalidTickets)]
    [InlineData("AMB-000001,01/03/2026,1,,REF-1",                    Codes.InvalidDate)]
    [InlineData("AMB-000001,2026-10-18,1,,REF-1",                    Codes.InvalidDate)]
    [InlineData("AMB-000001,2026-03-01,1,abc,REF-1",                 Codes.InvalidPrice)]
    [InlineData("AMB-000001,2026-03-01,1,,REF 1",                    Codes.InvalidExternalRef)]
    [InlineData("AMB 1,2026-03-01,1,,REF-1",                         Codes.InvalidMemberId)]
    [InlineData("AMB-999999,2026-03-01,1,,REF-1",                    Codes.MemberNotFound)]
    [InlineData("AMB-000001,2026-03-01,1,,REF-1,sobra",              Codes.ColumnCount)]
    [InlineData("=HYPERLINK(\"http://x\"),2026-03-01,1,,REF-1",      Codes.FormulaNotAllowed)]
    [InlineData("AMB-000001,2026-03-01,1,,@SUM(A1)",                 Codes.FormulaNotAllowed)]
    [InlineData("AMB-000001,2026-03-01,-1,,REF-1",                   Codes.FormulaNotAllowed)]
    [InlineData("AMB-000001,2026-03-01,1,,\"REF-1",                  Codes.MalformedRow)]
    public async Task Rechaza_cada_fila_mala_con_su_motivo_y_su_linea(string fila, string codigo)
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, Csv(fila));

        r.IsSuccess.Should().BeTrue();
        r.Value!.Rejected.Should().Be(1);
        r.Value.Rejections.Single().Should().BeEquivalentTo(new { Line = 2, Code = codigo }, o => o.ExcludingMissingMembers());
        (await db.CorporateEventRegistrations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Lo_que_devuelve_el_informe_va_neutralizado()
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, Csv("=cmd|' /C calc'!A0,2026-03-01,1,,REF-1"));

        r.Value!.Rejections.Single().MemberId.Should().StartWith("'=");
    }

    [Fact]
    public async Task Una_referencia_repetida_en_el_archivo_entra_una_vez()
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, Csv("AMB-000001,2026-03-01,1,,REF-1", "AMB-000002,2026-03-01,1,,ref-1"));

        (r.Value!.Imported, r.Value.Rejected).Should().Be((1, 1));
        r.Value.Rejections.Single().Should().BeEquivalentTo(new { Line = 3, Code = Codes.DuplicateInFile }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Campos_entre_comillas_valen()
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, Csv("\"AMB-000001\",\"2026-03-01\",\"3\",\"10.00\",\"REF-9\""));

        r.Value!.Imported.Should().Be(1);
    }

    [Fact]
    public async Task La_cabecera_admite_BOM_y_mayusculas_distintas()
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, "﻿memberid,REGISTEREDAT,tickets,price,externalref\nAMB-000001,2026-03-01,1,,REF-1");

        r.Value!.Imported.Should().Be(1);
    }

    [Fact]
    public async Task Otra_cabecera_no_importa_nada()
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, "Member,Fecha\nAMB-000001,2026-03-01");

        r.ErrorCode.Should().Be(Codes.InvalidHeader);
        (await db.CorporateEventRegistrations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Mas_de_diez_mil_filas_no_importa_nada()
    {
        await using var db = await BaseAsync();
        var filas = Enumerable.Range(1, EventRegistrationImporter.MaxRows + 1).Select(i => $"AMB-000001,2026-03-01,1,,REF-{i}");

        var r = await Importar(db, Csv(filas.ToArray()));

        r.ErrorCode.Should().Be(Codes.TooManyRows);
        (await db.CorporateEventRegistrations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_evento_que_no_existe_es_EVENT_NOT_FOUND()
    {
        await using var db = await BaseAsync();

        (await Importar(db, Csv("AMB-000001,2026-03-01,1,,REF-1"), "EV-NO")).ErrorCode.Should().Be(Codes.EventNotFound);
    }

    [Fact]
    public async Task Lista_quinientos_rechazos_pero_los_cuenta_todos()
    {
        await using var db = await BaseAsync();
        var malas = Enumerable.Range(1, 600).Select(i => $"AMB-999999,2026-03-01,1,,REF-{i}");

        var r = await Importar(db, Csv(malas.ToArray()));

        r.Value!.Rejected.Should().Be(600);
        r.Value.Rejections.Should().HaveCount(EventRegistrationImporter.MaxListedRejections);
    }

    [Fact]
    public async Task Las_lineas_en_blanco_no_cuentan()
    {
        await using var db = await BaseAsync();

        var r = await Importar(db, Cabecera + "\n\nAMB-000001,2026-03-01,1,,REF-1\n\n");

        (r.Value!.TotalRows, r.Value.Imported).Should().Be((1, 1));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter EventRegistrationImporterTests -nologo`
Expected: FAIL de compilación (`EventRegistrationImporter` no existe).

- [ ] **Step 3: Crear `EventRegistrationImportDtos.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Events;

/// <summary>Códigos de la importación CSV de inscripciones (Plan J, J19).</summary>
public static class EventRegistrationImportCodes
{
    // Del archivo entero (no se importa nada)
    public const string EventNotFound   = "EVENT_NOT_FOUND";
    public const string InvalidHeader   = "INVALID_HEADER";
    public const string TooManyRows     = "TOO_MANY_ROWS";
    public const string ImportConflict  = "IMPORT_CONFLICT";

    // De una fila (esa fila se rechaza y el resto sigue)
    public const string MalformedRow       = "MALFORMED_ROW";
    public const string ColumnCount        = "COLUMN_COUNT";
    public const string FormulaNotAllowed  = "FORMULA_NOT_ALLOWED";
    public const string InvalidMemberId    = "INVALID_MEMBER_ID";
    public const string MemberNotFound     = "MEMBER_NOT_FOUND";
    public const string InvalidDate        = "INVALID_DATE";
    public const string InvalidTickets     = "INVALID_TICKETS";
    public const string InvalidPrice       = "INVALID_PRICE";
    public const string InvalidExternalRef = "INVALID_EXTERNAL_REF";
    public const string DuplicateInFile    = "DUPLICATE_IN_FILE";
}

/// <summary>Una fila rechazada. Los textos vienen del archivo y van neutralizados (CsvLite.Neutralize).</summary>
public sealed class EventRegistrationRejection
{
    public int    Line        { get; set; }
    public string Code        { get; set; } = string.Empty;
    public string MemberId    { get; set; } = string.Empty;
    public string ExternalRef { get; set; } = string.Empty;
}

public sealed class EventRegistrationImportResult
{
    /// <summary>Filas de datos (sin la cabecera ni las líneas en blanco).</summary>
    public int TotalRows       { get; set; }
    public int Imported        { get; set; }
    /// <summary>Referencias que ya estaban importadas para este evento: no se tocan.</summary>
    public int AlreadyImported { get; set; }
    public int Rejected        { get; set; }
    /// <summary>Las primeras 500 rechazadas, con su línea y su motivo.</summary>
    public List<EventRegistrationRejection> Rejections { get; set; } = new();
}
```

- [ ] **Step 4: Crear `IEventRegistrationImporter.cs`**

```csharp
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Events;

public interface IEventRegistrationImporter
{
    /// <summary>
    /// Importa un CSV (cabecera MemberId,RegisteredAt,Tickets,Price,ExternalRef) para un evento.
    /// Idempotente por (evento, referencia externa). <paramref name="actor"/> queda en CreatedBy.
    /// </summary>
    Task<Result<EventRegistrationImportResult>> ImportAsync(
        string eventId, TextReader csv, string actor, CancellationToken ct = default);
}
```

- [ ] **Step 5: Crear `EventRegistrationImporter.cs`**

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Codes = MLMConquerorGlobalEdition.Repository.Services.Events.EventRegistrationImportCodes;

namespace MLMConquerorGlobalEdition.Repository.Services.Events;

/// <inheritdoc />
/// <remarks>
/// Todo o nada por archivo solo para lo que invalida el archivo (evento, cabecera, tamaño); por
/// fila, lo malo se rechaza con línea y motivo y lo bueno entra. Las referencias se comparan sin
/// distinguir mayúsculas, como la intercalación de la base que guarda el índice único.
/// </remarks>
public sealed class EventRegistrationImporter : IEventRegistrationImporter
{
    public const int MaxRows             = 10_000;
    public const int MaxListedRejections = 500;
    private const int ChunkSize          = 1_000;

    private static readonly string[] Header = { "MemberId", "RegisteredAt", "Tickets", "Price", "ExternalRef" };

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"
    };

    private static readonly Regex MemberIdPattern    = new(@"^[A-Za-z0-9-]{1,20}$", RegexOptions.Compiled);
    private static readonly Regex ExternalRefPattern = new(@"^[A-Za-z0-9._:/#-]{1,100}$", RegexOptions.Compiled);

    private sealed record Row(string MemberId, DateTime RegisteredAt, int Tickets, decimal? Price, string ExternalRef);

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public EventRegistrationImporter(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<Result<EventRegistrationImportResult>> ImportAsync(
        string eventId, TextReader csv, string actor, CancellationToken ct = default)
    {
        if (!await _db.CorporateEvents.AsNoTracking().AnyAsync(e => e.Id == eventId && !e.IsDeleted, ct))
            return Fail(Codes.EventNotFound, "Event not found.");

        var records = new List<CsvRecord>();
        foreach (var record in CsvLite.Parse(csv))
        {
            if (record.Fields.Count == 1 && record.Fields[0].Trim().Length == 0) continue; // línea en blanco
            records.Add(record);
            if (records.Count > MaxRows + 1)
                return Fail(Codes.TooManyRows, $"At most {MaxRows} rows per file.");
        }

        if (records.Count == 0 || !IsHeader(records[0]))
            return Fail(Codes.InvalidHeader, "The first line must be: " + string.Join(",", Header));

        var now    = _clock.Now;
        var result = new EventRegistrationImportResult { TotalRows = records.Count - 1 };
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valid  = new List<(CsvRecord Record, Row Row)>();

        foreach (var record in records.Skip(1))
        {
            var code = Validate(record, now, out var row);
            if (code is null && !seen.Add(row!.ExternalRef)) code = Codes.DuplicateInFile;
            if (code is not null)
            {
                Reject(result, record, code);
                continue;
            }
            valid.Add((record, row!));
        }

        // Miembros que existen, con su MemberId tal como está en la base.
        var members = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in valid.Select(v => v.Row.MemberId).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(ChunkSize))
        {
            var found = await _db.MemberProfiles.AsNoTracking()
                .Where(m => chunk.Contains(m.MemberId)).Select(m => m.MemberId).ToListAsync(ct);
            foreach (var id in found) members[id] = id;
        }

        // Referencias ya importadas para este evento: idempotencia.
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in valid.Select(v => v.Row.ExternalRef).Chunk(ChunkSize))
        {
            existing.UnionWith(await _db.CorporateEventRegistrations.AsNoTracking()
                .Where(r => r.EventId == eventId && chunk.Contains(r.ExternalRef))
                .Select(r => r.ExternalRef).ToListAsync(ct));
        }

        foreach (var (record, row) in valid)
        {
            if (!members.TryGetValue(row.MemberId, out var memberId))
            {
                Reject(result, record, Codes.MemberNotFound);
                continue;
            }

            if (existing.Contains(row.ExternalRef))
            {
                result.AlreadyImported++;
                continue;
            }

            _db.CorporateEventRegistrations.Add(new CorporateEventRegistration
            {
                EventId      = eventId,
                MemberId     = memberId,
                RegisteredAt = row.RegisteredAt,
                Tickets      = row.Tickets,
                Price        = row.Price,
                ExternalRef  = row.ExternalRef,
                Source       = CorporateEventRegistration.CsvImportSource,
                CreatedBy    = actor,
                CreationDate = now
            });
            result.Imported++;
        }

        if (result.Imported > 0)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Otra importación del mismo evento guardó a la vez alguna de estas referencias y el
                // índice único lo paró: no entra nada de esta y repetirla es seguro.
                _db.ChangeTracker.Clear();
                return Fail(Codes.ImportConflict,
                    "Another import of this event saved some of these rows at the same time. Run it again: it is idempotent.");
            }
        }

        return Result<EventRegistrationImportResult>.Success(result);
    }

    private static string? Validate(CsvRecord record, DateTime now, out Row? row)
    {
        row = null;
        if (record.Malformed) return Codes.MalformedRow;
        if (record.Fields.Count != Header.Length) return Codes.ColumnCount;

        var f = record.Fields.Select(x => x.Trim()).ToArray();
        if (record.Fields.Any(CsvLite.LooksLikeFormula) || f.Any(CsvLite.LooksLikeFormula))
            return Codes.FormulaNotAllowed;

        if (!MemberIdPattern.IsMatch(f[0])) return Codes.InvalidMemberId;

        if (!DateTime.TryParseExact(f[1], DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            || at > now.AddDays(1))
            return Codes.InvalidDate;

        if (!int.TryParse(f[2], NumberStyles.None, CultureInfo.InvariantCulture, out var tickets)
            || tickets < 1 || tickets > 100)
            return Codes.InvalidTickets;

        decimal? price = null;
        if (f[3].Length > 0)
        {
            if (!decimal.TryParse(f[3], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var p)
                || p > 100_000m)
                return Codes.InvalidPrice;
            price = p;
        }

        if (!ExternalRefPattern.IsMatch(f[4])) return Codes.InvalidExternalRef;

        // Los MemberId se generan en mayúsculas (AMB-######, MBR-######): se normaliza para que
        // "amb-000001" encuentre al miembro igual en SQL Server que en cualquier otro proveedor.
        row = new Row(f[0].ToUpperInvariant(), at, tickets, price, f[4]);
        return null;
    }

    private static bool IsHeader(CsvRecord record) =>
        record.Fields.Count == Header.Length &&
        record.Fields.Select((field, i) => (field, i)).All(x =>
            string.Equals(x.field.Trim().TrimStart('﻿'), Header[x.i], StringComparison.OrdinalIgnoreCase));

    private static void Reject(EventRegistrationImportResult result, CsvRecord record, string code)
    {
        result.Rejected++;
        if (result.Rejections.Count >= MaxListedRejections) return;

        result.Rejections.Add(new EventRegistrationRejection
        {
            Line        = record.Line,
            Code        = code,
            MemberId    = CsvLite.Neutralize(record.Fields.ElementAtOrDefault(0)),
            ExternalRef = CsvLite.Neutralize(record.Fields.ElementAtOrDefault(4))
        });
    }

    private static Result<EventRegistrationImportResult> Fail(string code, string message) =>
        Result<EventRegistrationImportResult>.Failure(code, message);
}
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter EventRegistrationImporterTests -nologo`
Expected: PASS (25 pruebas).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Events/EventRegistrationImportDtos.cs \
        MLMConquerorGlobalEdition.Repository/Services/Events/IEventRegistrationImporter.cs \
        MLMConquerorGlobalEdition.Repository/Services/Events/EventRegistrationImporter.cs \
        MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CorporateEvents/EventRegistrationImporterTests.cs
git commit -m "Importacion CSV de inscripciones a eventos: cabecera fija, hasta 10.000 filas, validacion por fila con linea y motivo, rechazo de formulas, idempotencia por referencia externa, informe de hasta 500 rechazos neutralizado y conflicto de importaciones simultaneas sin duplicados"
```

---

### Task 37: Endpoint de importación en el Admin

> **FUERA (corrección de Nick N5).** Sin endpoint de importación en el Admin.

**Files:**
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/CorporateEventsController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs` (registro)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CorporateEvents/ImportarInscripcionesEndpointTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Reflection;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CorporateEvents;

public class ImportarInscripcionesEndpointTests
{
    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now    => new(2026, 10, 16, 12, 0, 0);
        public DateTime UtcNow => Now.ToUniversalTime();
    }

    private static IFormFile Archivo(byte[] contenido, string nombre = "inscripciones.csv") =>
        new FormFile(new MemoryStream(contenido), 0, contenido.Length, "file", nombre);

    private static IFormFile Csv(string texto) => Archivo(Encoding.UTF8.GetBytes(texto));

    private static CorporateEventsController Control() => new(new Mock<IMediator>().Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static ICurrentUserService Admin()
    {
        var u = new Mock<ICurrentUserService>();
        u.Setup(x => x.Email).Returns("admin@prueba.test");
        u.Setup(x => x.UserId).Returns("admin-001");
        return u.Object;
    }

    private static string Codigo(IActionResult r) =>
        ((ApiResponse<EventRegistrationImportResult>)((ObjectResult)r).Value!).ErrorCode!;

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
        => typeof(CorporateEventsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");

    [Fact]
    public async Task Sin_archivo_es_400()
    {
        var r = await Control().ImportRegistrations("EV-1", null, Mock.Of<IEventRegistrationImporter>(), Admin());

        r.Should().BeOfType<BadRequestObjectResult>();
        Codigo(r).Should().Be("INVALID_FILE");
    }

    [Fact]
    public async Task Otra_extension_es_400()
    {
        var r = await Control().ImportRegistrations("EV-1", Archivo(new byte[] { 1, 2 }, "datos.xlsx"), Mock.Of<IEventRegistrationImporter>(), Admin());

        Codigo(r).Should().Be("INVALID_FILE_TYPE");
    }

    [Fact]
    public async Task Mas_de_dos_megas_es_400()
    {
        var r = await Control().ImportRegistrations("EV-1", Archivo(new byte[CorporateEventsController.MaxImportBytes + 1]), Mock.Of<IEventRegistrationImporter>(), Admin());

        Codigo(r).Should().Be("FILE_TOO_LARGE");
    }

    [Fact]
    public async Task Bytes_que_no_son_UTF8_es_400()
    {
        await using var db = await EventRegistrationImporterTests.BaseAsync();
        var bytes = Encoding.ASCII.GetBytes("MemberId,RegisteredAt,Tickets,Price,ExternalRef\nAMB-000001,2026-03-01,1,,REF-")
            .Concat(new byte[] { 0xC3, 0x28 }).ToArray();

        var r = await Control().ImportRegistrations("EV-1", Archivo(bytes), new EventRegistrationImporter(db, new Reloj()), Admin());

        Codigo(r).Should().Be("INVALID_ENCODING");
    }

    [Fact]
    public async Task Evento_inexistente_es_404()
    {
        await using var db = await EventRegistrationImporterTests.BaseAsync();

        var r = await Control().ImportRegistrations("EV-NO", Csv("MemberId,RegisteredAt,Tickets,Price,ExternalRef\n"),
            new EventRegistrationImporter(db, new Reloj()), Admin());

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Bien_devuelve_el_informe_y_firma_con_el_administrador()
    {
        await using var db = await EventRegistrationImporterTests.BaseAsync();

        var r = await Control().ImportRegistrations("EV-1",
            Csv("MemberId,RegisteredAt,Tickets,Price,ExternalRef\nAMB-000001,2026-03-01,2,,REF-1\nAMB-999999,2026-03-01,1,,REF-2"),
            new EventRegistrationImporter(db, new Reloj()), Admin());

        var ok = r.Should().BeOfType<OkObjectResult>().Subject;
        var informe = ((ApiResponse<EventRegistrationImportResult>)ok.Value!).Data!;
        (informe.Imported, informe.Rejected).Should().Be((1, 1));
        db.CorporateEventRegistrations.Single().CreatedBy.Should().Be("admin@prueba.test");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter ImportarInscripcionesEndpointTests -nologo`
Expected: FAIL de compilación (`ImportRegistrations` no existe).

- [ ] **Step 3: Acción en `CorporateEventsController.cs`**

Añadir a los `using`:

```csharp
using System.Text;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
```

Y sustituir:

```csharp
    [HttpDelete("{eventId}")]
```

por:

```csharp
    /// <summary>Tamaño máximo del CSV de inscripciones (Plan J, J19).</summary>
    public const long MaxImportBytes = 2_000_000;

    /// <summary>
    /// POST /api/v1/admin/corporate-events/{eventId}/registrations/import — multipart con el campo
    /// <c>file</c> (.csv, UTF-8, 2 MB como mucho). Devuelve el informe: importadas, ya importadas y
    /// rechazadas con línea y motivo. Reimportar el mismo archivo es seguro.
    /// </summary>
    [HttpPost("{eventId}/registrations/import")]
    [RequestSizeLimit(MaxImportBytes + 64_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes + 64_000)]
    public async Task<IActionResult> ImportRegistrations(
        string eventId,
        IFormFile? file,
        [FromServices] IEventRegistrationImporter importer,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return ImportFail(StatusCodes.Status400BadRequest, "INVALID_FILE", "No file provided.");
        if (file.Length > MaxImportBytes)
            return ImportFail(StatusCodes.Status400BadRequest, "FILE_TOO_LARGE", "The file must be at most 2 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return ImportFail(StatusCodes.Status400BadRequest, "INVALID_FILE_TYPE", "Only .csv files are allowed.");

        var actor = !string.IsNullOrWhiteSpace(currentUser.Email) ? currentUser.Email : currentUser.UserId;
        if (actor.Length > 100) actor = actor[..100];

        Result<EventRegistrationImportResult> result;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            result = await importer.ImportAsync(eventId, reader, actor, ct);
        }
        catch (DecoderFallbackException)
        {
            return ImportFail(StatusCodes.Status400BadRequest, "INVALID_ENCODING", "The file must be UTF-8.");
        }

        if (result.IsSuccess)
            return Ok(ApiResponse<EventRegistrationImportResult>.Ok(result.Value!));

        return result.ErrorCode switch
        {
            EventRegistrationImportCodes.EventNotFound  => ImportFail(StatusCodes.Status404NotFound, result.ErrorCode!, result.Error!),
            EventRegistrationImportCodes.ImportConflict => ImportFail(StatusCodes.Status409Conflict, result.ErrorCode!, result.Error!),
            _                                           => ImportFail(StatusCodes.Status400BadRequest, result.ErrorCode!, result.Error!)
        };
    }

    private ObjectResult ImportFail(int status, string code, string message)
    {
        var body = ApiResponse<EventRegistrationImportResult>.Fail(code, message, HttpContext.TraceIdentifier);
        return status switch
        {
            StatusCodes.Status404NotFound => NotFound(body),
            StatusCodes.Status409Conflict => Conflict(body),
            _                             => BadRequest(body)
        };
    }

    [HttpDelete("{eventId}")]
```

- [ ] **Step 4: Registro**

En `AdminAPI/Program.cs`, sustituir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.IDailyResidualReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.DailyResidualReportService>();
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.IDailyResidualReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.DailyResidualReportService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Events.IEventRegistrationImporter,
                            MLMConquerorGlobalEdition.Repository.Services.Events.EventRegistrationImporter>();
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "ImportarInscripcionesEndpointTests|PuertaUnicaDe2FATests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.AdminAPI -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Controllers/CorporateEventsController.cs \
        MLMConquerorGlobalEdition.AdminAPI/Program.cs \
        MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CorporateEvents/ImportarInscripcionesEndpointTests.cs
git commit -m "Admin: POST corporate-events/{id}/registrations/import para SuperAdmin y Admin, solo .csv UTF-8 de 2 MB como mucho, 404 si el evento no existe, 409 en conflicto de importaciones simultaneas y el informe de la importacion firmado con el administrador"
```

---

### Task 38: Pantalla de importación en el Admin

> **FUERA (corrección de Nick N5).** Sin pantalla de importación en el Admin.

**Files:**
- Modify: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminCorporateEvents.razor:78-84,185,328`

- [ ] **Step 1: Botón de importar en cada evento**

Sustituir `                    <GridColumn HeaderText="Actions" Width="130" TextAlign="TextAlign.Center">` por `                    <GridColumn HeaderText="Actions" Width="170" TextAlign="TextAlign.Center">`, y sustituir:

```razor
                            <button class="btn btn-sm btn-outline-primary py-0 me-1"
                                    @onclick="() => OpenEdit((CorporateEventDto)context)">
                                <i class="ri-edit-line"></i>
                            </button>
```

por:

```razor
                            <button class="btn btn-sm btn-outline-secondary py-0 me-1" title="Import registrations (CSV)"
                                    @onclick="() => OpenImport((CorporateEventDto)context)">
                                <i class="ri-upload-2-line"></i>
                            </button>
                            <button class="btn btn-sm btn-outline-primary py-0 me-1"
                                    @onclick="() => OpenEdit((CorporateEventDto)context)">
                                <i class="ri-edit-line"></i>
                            </button>
```

- [ ] **Step 2: Ventana de importación**

Sustituir:

```razor
@code {
    private static readonly List<ToolbarItemModel> _rteToolbar = new()
```

por:

```razor
@if (_importTarget is not null)
{
    <div class="modal d-block" tabindex="-1" style="background:rgba(0,0,0,.45)">
        <div class="modal-dialog modal-lg modal-dialog-scrollable">
            <div class="modal-content border-0 rounded-3 shadow">
                <div class="modal-header border-bottom">
                    <h5 class="modal-title fw-bold">Import registrations — @_importTarget.Title</h5>
                    <button type="button" class="btn-close" @onclick="CloseImport"></button>
                </div>
                <div class="modal-body">
                    <p class="small text-muted mb-2">CSV in UTF-8, comma-separated, up to 2 MB and 10,000 rows. The first line must be exactly:</p>
                    <pre class="small bg-light p-2 rounded mb-2">MemberId,RegisteredAt,Tickets,Price,ExternalRef</pre>
                    <p class="small text-muted">
                        RegisteredAt as yyyy-MM-dd or yyyy-MM-dd HH:mm. Tickets from 1 to 100. Price is optional, with a dot as decimal separator.
                        ExternalRef is the ticket platform's purchase id: importing the same file again skips what is already there.
                        Cells that start with =, +, -, @ or a tab are rejected.
                    </p>
                    <InputFile OnChange="OnImportFileSelected" accept=".csv" class="form-control form-control-sm mb-3" />

                    @if (_importResult is not null)
                    {
                        <div class="alert alert-info small mb-2">
                            Rows: @_importResult.TotalRows — imported: @_importResult.Imported — already imported: @_importResult.AlreadyImported — rejected: @_importResult.Rejected
                        </div>
                        @if (_importResult.Rejections.Count > 0)
                        {
                            <div class="table-responsive" style="max-height:300px">
                                <table class="table table-sm small">
                                    <thead><tr><th>Line</th><th>Reason</th><th>MemberId</th><th>ExternalRef</th></tr></thead>
                                    <tbody>
                                        @foreach (var rechazo in _importResult.Rejections)
                                        {
                                            <tr><td>@rechazo.Line</td><td><code>@rechazo.Code</code></td><td>@rechazo.MemberId</td><td>@rechazo.ExternalRef</td></tr>
                                        }
                                    </tbody>
                                </table>
                            </div>
                            @if (_importResult.Rejected > _importResult.Rejections.Count)
                            {
                                <p class="small text-muted">Showing the first @_importResult.Rejections.Count of @_importResult.Rejected rejected rows.</p>
                            }
                        }
                    }
                    @if (_importError is not null)
                    {
                        <div class="alert alert-danger small">@_importError</div>
                    }
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="CloseImport">Close</button>
                    <button class="btn btn-primary" disabled="@(_importFile is null || _importing)" @onclick="ImportAsync">
                        @if (_importing) { <span class="spinner-border spinner-border-sm me-1"></span> }
                        Import
                    </button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    private static readonly List<ToolbarItemModel> _rteToolbar = new()
```

- [ ] **Step 3: Estado y llamada**

Sustituir:

```csharp
    private class CorporateEventFormModel
```

por:

```csharp
    // ── Importación de inscripciones (Plan J) ─────────────────────────────────
    private const long MaxImportBytes = 2_000_000;
    private CorporateEventDto? _importTarget;
    private IBrowserFile?      _importFile;
    private bool               _importing;
    private ImportResultDto?   _importResult;
    private string?            _importError;

    private void OpenImport(CorporateEventDto ev)
    {
        _importTarget = ev;
        _importFile   = null;
        _importResult = null;
        _importError  = null;
    }

    private void CloseImport()
    {
        _importTarget = null;
        _importFile   = null;
        _importResult = null;
        _importError  = null;
    }

    private void OnImportFileSelected(InputFileChangeEventArgs e)
    {
        _importFile   = e.File;
        _importResult = null;
        _importError  = null;
    }

    private async Task ImportAsync()
    {
        if (_importTarget is null || _importFile is null) return;
        if (_importFile.Size > MaxImportBytes)
        {
            _importError = "The file is larger than 2 MB.";
            return;
        }

        _importing   = true;
        _importError = null;
        try
        {
            await using var source = _importFile.OpenReadStream(MaxImportBytes);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            buffer.Position = 0;

            using var content = new MultipartFormDataContent();
            var part = new StreamContent(buffer);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            content.Add(part, "file", _importFile.Name);

            var response = await Http.PostAsync(
                $"api/v1/admin/corporate-events/{Uri.EscapeDataString(_importTarget.Id)}/registrations/import", content);
            var body = await response.Content.ReadFromJsonAsync<ImportResp>();

            if (response.IsSuccessStatusCode && body?.Data is not null)
                _importResult = body.Data;
            else
                _importError = $"{body?.ErrorCode ?? response.StatusCode.ToString()}: {body?.Errors?.FirstOrDefault() ?? "Import failed."}";
        }
        catch (Exception ex)
        {
            _importError = $"Error: {ex.Message}";
        }
        finally
        {
            _importing = false;
        }
    }

    private sealed record ImportRejectionDto(int Line, string Code, string? MemberId, string? ExternalRef);
    private sealed record ImportResultDto(int TotalRows, int Imported, int AlreadyImported, int Rejected, List<ImportRejectionDto> Rejections);
    private sealed record ImportResp(bool Success, ImportResultDto? Data, string? ErrorCode, List<string>? Errors);

    private class CorporateEventFormModel
```

- [ ] **Step 4: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminCorporateEvents.razor
git commit -m "Admin, eventos corporativos: boton de importar inscripciones por evento con instrucciones del formato, subida del CSV e informe con importadas, ya importadas y rechazadas con linea y motivo"
```

---

### Task 39: Servicio de eventos del equipo (BizCenter)

> **Corrección de Nick N3 y N5.** Si la fila enseña «Calificado», sale de `IQualificationService`. Hasta que haya importación, el reporte sale vacío.

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventDtos.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Events/ITeamEventRegistrationsService.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventRegistrationsService.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamEventRegistrationsServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Inscripciones a un evento dentro del equipo de inscripción del que mira (él incluido), con las
/// entradas sumadas, la primera fecha, si está calificado y su rango; y los totales del equipo.
/// </summary>
public class TeamEventRegistrationsServiceTests
{
    internal sealed record Ids(string Yo, string A, string B, string Fuera, string Otro, string Evento, string Inactivo)
    {
        public static readonly Ids Fijos = new("AMB-000001", "AMB-000002", "AMB-000003", "AMB-000031", "AMB-000099", "EV-1", "EV-OFF");
    }

    /// <summary>
    /// YO ─ A, YO ─ B; OTRO ─ FUERA. En EV-1: YO 2 entradas (1 mar), A dos compras (1 entrada el
    /// 3 mar y 3 el 2 mar), FUERA 5 entradas. EV-OFF está desactivado.
    /// </summary>
    internal static async Task EscenarioAsync(AppDbContext db, Ids id, bool sembrarRango = true)
    {
        ReportesDb.Miembro(db, id.Yo);
        ReportesDb.Miembro(db, id.A, id.Yo);
        ReportesDb.Miembro(db, id.B, id.Yo);
        ReportesDb.Miembro(db, id.Otro);
        ReportesDb.Miembro(db, id.Fuera, id.Otro);
        ReportesDb.Nodo(db, id.Yo,    null,    ReportesDb.Ruta(id.Yo), 1);
        ReportesDb.Nodo(db, id.A,     id.Yo,   ReportesDb.Ruta(id.Yo, id.A), 2);
        ReportesDb.Nodo(db, id.B,     id.Yo,   ReportesDb.Ruta(id.Yo, id.B), 2);
        ReportesDb.Nodo(db, id.Otro,  null,    ReportesDb.Ruta(id.Otro), 1);
        ReportesDb.Nodo(db, id.Fuera, id.Otro, ReportesDb.Ruta(id.Otro, id.Fuera), 2);
        ReportesDb.Suscripcion(db, id.A, 3, MembershipStatus.Active, new DateTime(2026, 1, 1));

        db.CorporateEvents.AddRange(
            new CorporateEvent { Id = id.Evento,   Title = "Convención", EventDate = new DateTime(2026, 11, 20), IsActive = true,  CreatedBy = "prueba" },
            new CorporateEvent { Id = id.Inactivo, Title = "Pasado",     EventDate = new DateTime(2025, 11, 20), IsActive = false, CreatedBy = "prueba" });

        if (sembrarRango)
        {
            db.RankDefinitions.Add(new RankDefinition { Id = 1, Name = "Bronze", SortOrder = 1, CreatedBy = "prueba" });
            db.MemberRankHistories.Add(new MemberRankHistory { MemberId = id.A, RankDefinitionId = 1, AchievedAt = new DateTime(2026, 2, 1), CreatedBy = "prueba" });
        }
        await db.SaveChangesAsync();

        db.CorporateEventRegistrations.AddRange(
            Inscripcion(id.Evento, id.Yo,    2, new DateTime(2026, 3, 1), $"{id.Evento}-R1"),
            Inscripcion(id.Evento, id.A,     1, new DateTime(2026, 3, 3), $"{id.Evento}-R2"),
            Inscripcion(id.Evento, id.A,     3, new DateTime(2026, 3, 2), $"{id.Evento}-R3"),
            Inscripcion(id.Evento, id.Fuera, 5, new DateTime(2026, 3, 1), $"{id.Evento}-R4"));
        await db.SaveChangesAsync();
    }

    internal static CorporateEventRegistration Inscripcion(string evento, string miembro, int entradas, DateTime cuando, string referencia) => new()
    {
        EventId = evento, MemberId = miembro, Tickets = entradas, RegisteredAt = cuando, ExternalRef = referencia, CreatedBy = "prueba"
    };

    private static async Task<AppDbContext> BaseAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        await EscenarioAsync(db, Ids.Fijos);
        return db;
    }

    private static GridDataRequest Pagina() => new() { Page = 1, PageSize = 50 };

    [Fact]
    public async Task Solo_inscritos_del_equipo_con_las_entradas_sumadas_y_la_primera_fecha()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var r = await new TeamEventRegistrationsService(db).GetGridAsync(id.Yo, id.Evento, registeredOnly: true, Pagina());

        var filas = r.Value!.Items.ToDictionary(f => f.MemberId);
        filas.Keys.Should().BeEquivalentTo(new[] { id.Yo, id.A }, "B no compró y FUERA no es del equipo");
        (filas[id.A].Tickets, filas[id.A].RegisteredAt, filas[id.A].Registered).Should().Be((4, (DateTime?)new DateTime(2026, 3, 2), true));
        filas[id.Yo].Tickets.Should().Be(2);
    }

    [Fact]
    public async Task Todo_el_equipo_con_No_para_quien_no_se_inscribio()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var r = await new TeamEventRegistrationsService(db).GetGridAsync(id.Yo, id.Evento, registeredOnly: false, Pagina());

        var b = r.Value!.Items.Single(f => f.MemberId == id.B);
        (b.Registered, b.Tickets, b.RegisteredAt).Should().Be((false, 0, (DateTime?)null));
        r.Value.Items.Select(f => f.MemberId).Should().NotContain(id.Fuera);
    }

    [Fact]
    public async Task Los_totales_son_solo_del_equipo()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var t = await new TeamEventRegistrationsService(db).GetTotalsAsync(id.Yo, id.Evento);

        (t.Value!.Registered, t.Value.Tickets).Should().Be((2, 6));
    }

    [Fact]
    public async Task Cada_fila_dice_si_esta_calificado_y_su_rango()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var a = (await new TeamEventRegistrationsService(db).GetGridAsync(id.Yo, id.Evento, true, Pagina()))
            .Value!.Items.Single(f => f.MemberId == id.A);

        (a.IsQualified, a.RankName).Should().Be((true, "Bronze"));
    }

    [Fact]
    public async Task Un_evento_desactivado_o_inexistente_es_EVENT_NOT_FOUND()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;
        var servicio = new TeamEventRegistrationsService(db);

        (await servicio.GetGridAsync(id.Yo, id.Inactivo, true, Pagina())).ErrorCode.Should().Be(TeamEventErrors.EventNotFound);
        (await servicio.GetTotalsAsync(id.Yo, "EV-NO")).ErrorCode.Should().Be(TeamEventErrors.EventNotFound);
    }

    [Fact]
    public async Task Solo_se_listan_los_eventos_activos()
    {
        await using var db = await BaseAsync();

        (await new TeamEventRegistrationsService(db).GetActiveEventsAsync())
            .Select(e => e.Id).Should().Equal(Ids.Fijos.Evento);
    }

    [Fact]
    public async Task Se_ordena_por_entradas()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;
        var pagina = Pagina();
        pagina.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.Tickets), Direction = "desc" });

        (await new TeamEventRegistrationsService(db).GetGridAsync(id.Yo, id.Evento, true, pagina))
            .Value!.Items.First().MemberId.Should().Be(id.A);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TeamEventRegistrationsServiceTests -nologo`
Expected: FAIL de compilación (`TeamEventRegistrationsService` no existe).

- [ ] **Step 3: Crear `TeamEventDtos.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Events;

public static class TeamEventErrors
{
    public const string EventNotFound = "EVENT_NOT_FOUND";
}

public sealed class TeamEventView
{
    public string   Id        { get; set; } = string.Empty;
    public string   Title     { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string?  Location  { get; set; }
}

/// <summary>Un miembro del equipo y su inscripción (o no) a un evento.</summary>
public sealed class TeamEventRegistrationView
{
    public string    MemberId     { get; set; } = string.Empty;
    public string    FirstName    { get; set; } = string.Empty;
    public string    LastName     { get; set; } = string.Empty;
    public DateTime  EnrollDate   { get; set; }
    /// <summary>Suscripción actual Active (J15).</summary>
    public bool      IsQualified  { get; set; }
    public string?   RankName     { get; set; }
    public bool      Registered   { get; set; }
    /// <summary>La primera de sus compras para este evento.</summary>
    public DateTime? RegisteredAt { get; set; }
    /// <summary>Suma de las entradas de todas sus compras para este evento.</summary>
    public int       Tickets      { get; set; }
}

public sealed class TeamEventTotalsView
{
    /// <summary>Miembros del equipo con al menos una compra.</summary>
    public int Registered { get; set; }
    public int Tickets    { get; set; }
}
```

- [ ] **Step 4: Crear `ITeamEventRegistrationsService.cs`**

```csharp
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Events;

/// <summary>Eventos del equipo de inscripción (Plan J, bloque 6). Quien llama pasa el miembro del token.</summary>
public interface ITeamEventRegistrationsService
{
    /// <summary>Eventos activos (IsActive y no borrados), del más reciente al más antiguo, 50 como mucho.</summary>
    Task<List<TeamEventView>> GetActiveEventsAsync(CancellationToken ct = default);

    Task<Result<PagedResult<TeamEventRegistrationView>>> GetGridAsync(
        string memberId, string eventId, bool registeredOnly, GridDataRequest request, CancellationToken ct = default);

    Task<Result<TeamEventTotalsView>> GetTotalsAsync(string memberId, string eventId, CancellationToken ct = default);
}
```

- [ ] **Step 5: Crear `TeamEventRegistrationsService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Events;

/// <inheritdoc />
/// <remarks>
/// El equipo es el subárbol de genealogía del que mira, ÉL INCLUIDO (J20), como conjunto Distinct de
/// MemberId (R8). Las entradas y la primera fecha son subconsultas correlacionadas sobre
/// (EventId, MemberId), que el índice de esa pareja resuelve con un seek por fila de la página.
/// </remarks>
public sealed class TeamEventRegistrationsService : ITeamEventRegistrationsService
{
    public const int MaxEvents = 50;

    private static readonly string[] DbFields =
    {
        nameof(TeamEventRegistrationView.MemberId),
        nameof(TeamEventRegistrationView.FirstName),
        nameof(TeamEventRegistrationView.LastName),
        nameof(TeamEventRegistrationView.EnrollDate),
        nameof(TeamEventRegistrationView.Registered),
        nameof(TeamEventRegistrationView.RegisteredAt),
        nameof(TeamEventRegistrationView.Tickets)
    };

    private static readonly string[] SearchableFields =
    {
        nameof(TeamEventRegistrationView.MemberId),
        nameof(TeamEventRegistrationView.FirstName),
        nameof(TeamEventRegistrationView.LastName)
    };

    private readonly AppDbContext _db;

    public TeamEventRegistrationsService(AppDbContext db) => _db = db;

    public Task<List<TeamEventView>> GetActiveEventsAsync(CancellationToken ct = default) =>
        _db.CorporateEvents.AsNoTracking()
            .Where(e => e.IsActive && !e.IsDeleted)
            .OrderByDescending(e => e.EventDate)
            .Take(MaxEvents)
            .Select(e => new TeamEventView { Id = e.Id, Title = e.Title, EventDate = e.EventDate, Location = e.Location })
            .ToListAsync(ct);

    public async Task<Result<PagedResult<TeamEventRegistrationView>>> GetGridAsync(
        string memberId, string eventId, bool registeredOnly, GridDataRequest request, CancellationToken ct = default)
    {
        if (!await IsVisibleAsync(eventId, ct))
            return Result<PagedResult<TeamEventRegistrationView>>.Failure(TeamEventErrors.EventNotFound, "Event not found.");

        var teamIds = await TeamIdsAsync(memberId, ct);
        if (teamIds is null)
            return Result<PagedResult<TeamEventRegistrationView>>.Success(new PagedResult<TeamEventRegistrationView>
            {
                Page = Math.Max(1, request.Page), PageSize = request.PageSize <= 0 ? 20 : request.PageSize
            });

        Sanitize(request);

        var regs = _db.CorporateEventRegistrations.AsNoTracking().Where(r => r.EventId == eventId);

        var query =
            from id in teamIds
            join m in _db.MemberProfiles.AsNoTracking() on id equals m.MemberId
            where !registeredOnly || regs.Any(r => r.MemberId == m.MemberId)
            select new TeamEventRegistrationView
            {
                MemberId     = m.MemberId,
                FirstName    = m.FirstName,
                LastName     = m.LastName,
                EnrollDate   = m.EnrollDate,
                Registered   = regs.Any(r => r.MemberId == m.MemberId),
                RegisteredAt = regs.Where(r => r.MemberId == m.MemberId).Min(r => (DateTime?)r.RegisteredAt),
                Tickets      = regs.Where(r => r.MemberId == m.MemberId).Sum(r => (int?)r.Tickets) ?? 0
            };

        var page = await query.ToGridResultAsync(request, SearchableFields, ct: ct);
        var rows = page.Items as List<TeamEventRegistrationView> ?? page.Items.ToList();
        await EnrichAsync(rows, ct);
        page.Items = rows;
        return Result<PagedResult<TeamEventRegistrationView>>.Success(page);
    }

    public async Task<Result<TeamEventTotalsView>> GetTotalsAsync(string memberId, string eventId, CancellationToken ct = default)
    {
        if (!await IsVisibleAsync(eventId, ct))
            return Result<TeamEventTotalsView>.Failure(TeamEventErrors.EventNotFound, "Event not found.");

        var teamIds = await TeamIdsAsync(memberId, ct);
        if (teamIds is null) return Result<TeamEventTotalsView>.Success(new TeamEventTotalsView());

        var inTeam =
            from r in _db.CorporateEventRegistrations.AsNoTracking()
            where r.EventId == eventId
            join id in teamIds on r.MemberId equals id
            select r;

        return Result<TeamEventTotalsView>.Success(new TeamEventTotalsView
        {
            Registered = await inTeam.Select(r => r.MemberId).Distinct().CountAsync(ct),
            Tickets    = await inTeam.SumAsync(r => (int?)r.Tickets, ct) ?? 0
        });
    }

    private Task<bool> IsVisibleAsync(string eventId, CancellationToken ct) =>
        _db.CorporateEvents.AsNoTracking().AnyAsync(e => e.Id == eventId && e.IsActive && !e.IsDeleted, ct);

    /// <summary>MemberId del equipo de inscripción (prefijo del nodo más reciente del que mira, él incluido).</summary>
    private async Task<IQueryable<string>?> TeamIdsAsync(string memberId, CancellationToken ct)
    {
        var prefix = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        if (prefix is null) return null;

        return _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(prefix))
            .Select(g => g.MemberId)
            .Distinct();
    }

    private async Task EnrichAsync(List<TeamEventRegistrationView> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.MemberId).ToList();

        var status = (await _db.MembershipSubscriptions.AsNoTracking()
                .Where(s => ids.Contains(s.MemberId))
                .Select(s => new { s.MemberId, s.SubscriptionStatus, s.StartDate, s.CreationDate })
                .ToListAsync(ct))
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartDate).ThenByDescending(s => s.CreationDate).First().SubscriptionStatus);

        var ranks = (await _db.MemberRankHistories.AsNoTracking()
                .Include(r => r.RankDefinition)
                .Where(r => ids.Contains(r.MemberId))
                .ToListAsync(ct))
            .GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First().RankDefinition?.Name);

        foreach (var row in rows)
        {
            row.IsQualified = status.TryGetValue(row.MemberId, out var s) && s == MembershipStatus.Active;
            row.RankName    = ranks.GetValueOrDefault(row.MemberId);
        }
    }

    private static void Sanitize(GridDataRequest request)
    {
        request.Sorts   = request.Sorts.Where(s => DbFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        request.Filters = request.Filters.Where(f => DbFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.LastName), Direction = "asc" });
    }
}
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TeamEventRegistrationsServiceTests -nologo`
Expected: PASS (7 pruebas).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventDtos.cs \
        MLMConquerorGlobalEdition.Repository/Services/Events/ITeamEventRegistrationsService.cs \
        MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventRegistrationsService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamEventRegistrationsServiceTests.cs
git commit -m "Eventos del equipo: eventos activos, inscripciones del equipo de inscripcion del que mira con entradas sumadas, primera fecha, calificado y rango, solo inscritos o todo el equipo, y totales del equipo; un evento desactivado es EVENT_NOT_FOUND"
```

---

### Task 40: Endpoints de eventos en el BizCenter

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter/Controllers/TeamEventsController.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (registro)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamEventsControllerTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class TeamEventsControllerTests
{
    private static TeamEventsController Control(ITeamEventRegistrationsService servicio)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        return new TeamEventsController(servicio, usuario.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task La_rejilla_es_del_equipo_del_token()
    {
        var servicio = new Mock<ITeamEventRegistrationsService>();
        servicio.Setup(s => s.GetGridAsync("AMB-000777", "EV-1", false, It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PagedResult<TeamEventRegistrationView>>.Success(new PagedResult<TeamEventRegistrationView>()));

        var r = await Control(servicio.Object).GetTeamRegistrationsGrid("EV-1", new GridDataRequest(), registeredOnly: false);

        r.Should().BeOfType<OkObjectResult>();
        servicio.VerifyAll();
    }

    [Fact]
    public async Task Un_evento_que_no_se_ve_es_404()
    {
        var servicio = new Mock<ITeamEventRegistrationsService>();
        servicio.Setup(s => s.GetTotalsAsync("AMB-000777", "EV-OFF", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TeamEventTotalsView>.Failure(TeamEventErrors.EventNotFound, "Event not found."));

        var r = await Control(servicio.Object).GetTeamRegistrationsTotals("EV-OFF");

        r.Should().BeOfType<NotFoundObjectResult>();
    }
}
```

Y en `MiembroDelTokenTests.cs`, sustituir:

```csharp
        // (acciones de los bloques siguientes)
```

por:

```csharp
        (typeof(TeamEventsController), nameof(TeamEventsController.GetActive)),
        (typeof(TeamEventsController), nameof(TeamEventsController.GetTeamRegistrationsGrid)),
        (typeof(TeamEventsController), nameof(TeamEventsController.GetTeamRegistrationsTotals)),
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "TeamEventsControllerTests|MiembroDelTokenTests" -nologo`
Expected: FAIL de compilación (`TeamEventsController` no existe).

- [ ] **Step 3: Crear `TeamEventsController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// Eventos corporativos y las inscripciones del equipo del que llama (Plan J). El sujeto de la ruta
/// es el EVENTO, nunca un miembro: el equipo sale del token.
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/events")]
[Authorize]
public class TeamEventsController : ControllerBase
{
    private readonly ITeamEventRegistrationsService _events;
    private readonly ICurrentUserService            _currentUser;

    public TeamEventsController(ITeamEventRegistrationsService events, ICurrentUserService currentUser)
    {
        _events      = events;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/v1/bizcenter/events — eventos activos.</summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct = default) =>
        Ok(ApiResponse<List<TeamEventView>>.Ok(await _events.GetActiveEventsAsync(ct)));

    /// <summary>POST /api/v1/bizcenter/events/{eventId}/team-registrations/grid?registeredOnly=true</summary>
    [HttpPost("{eventId}/team-registrations/grid")]
    [ReadOnlySafe]
    public async Task<IActionResult> GetTeamRegistrationsGrid(
        string eventId,
        [FromBody]  GridDataRequest request,
        [FromQuery] bool registeredOnly = true,
        CancellationToken ct = default)
    {
        var result = await _events.GetGridAsync(_currentUser.MemberId, eventId, registeredOnly, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<TeamEventRegistrationView>>.Ok(result.Value!))
            : NotFound(ApiResponse<PagedResult<TeamEventRegistrationView>>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    /// <summary>GET /api/v1/bizcenter/events/{eventId}/team-registrations/totals</summary>
    [HttpGet("{eventId}/team-registrations/totals")]
    public async Task<IActionResult> GetTeamRegistrationsTotals(string eventId, CancellationToken ct = default)
    {
        var result = await _events.GetTotalsAsync(_currentUser.MemberId, eventId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<TeamEventTotalsView>.Ok(result.Value!))
            : NotFound(ApiResponse<TeamEventTotalsView>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }
}
```

- [ ] **Step 4: Registro**

En `BizCenter/Program.cs`, sustituir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.ITeamStatusChangesService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.TeamStatusChangesService>();
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.ITeamStatusChangesService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.TeamStatusChangesService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Events.ITeamEventRegistrationsService,
                            MLMConquerorGlobalEdition.Repository.Services.Events.TeamEventRegistrationsService>();
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "TeamEventsControllerTests|MiembroDelTokenTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.BizCenter -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Controllers/TeamEventsController.cs \
        MLMConquerorGlobalEdition.BizCenter/Program.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamEventsControllerTests.cs
git commit -m "Eventos del equipo: GET bizcenter/events, POST events/{id}/team-registrations/grid y GET events/{id}/team-registrations/totals con el equipo del token y 404 EVENT_NOT_FOUND para un evento que no se ve"
```

---

### Task 41: Pantalla de eventos del equipo, página, menú y textos

> **Corrección de Nick N5.** La página se maqueta con el selector de evento y la tabla; sin inscripciones importadas se ve el estado vacío.

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Team/TeamEventRegistrations.razor`
- Create: `MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/TeamEvents.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor`
- Modify: los nueve `SharedResources*.resx`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs`

- [ ] **Step 1: Escribir la prueba que falla (textos)**

En `TextosDeReportesTests.cs`, sustituir:

```csharp
        // (claves de los bloques siguientes)
```

por:

```csharp
        // Bloque 6 — Eventos del equipo
        "Sidebar.Events", "PageTitle.TeamEvents", "Events.Select", "Events.None",
        "Events.RegisteredOnly", "Events.Registered", "Events.Tickets", "Events.RegistrationDate",
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: FAIL.

- [ ] **Step 2: Añadir las claves a los nueve `.resx`**

| Clave | en | es | pt | fr | it | de | ko | zh | ka |
|---|---|---|---|---|---|---|---|---|---|
| `Sidebar.Events` | Events | Eventos | Eventos | Événements | Eventi | Veranstaltungen | 이벤트 | 活动 | ღონისძიებები |
| `PageTitle.TeamEvents` | Team events | Eventos del equipo | Eventos da equipe | Événements de l'équipe | Eventi del team | Team-Veranstaltungen | 팀 이벤트 | 团队活动 | გუნდის ღონისძიებები |
| `Events.Select` | Event | Evento | Evento | Événement | Evento | Veranstaltung | 이벤트 | 活动 | ღონისძიება |
| `Events.None` | There are no active events. | No hay eventos activos. | Não há eventos ativos. | Aucun événement actif. | Non ci sono eventi attivi. | Es gibt keine aktiven Veranstaltungen. | 진행 중인 이벤트가 없습니다. | 没有进行中的活动。 | აქტიური ღონისძიებები არ არის. |
| `Events.RegisteredOnly` | Registered only | Solo inscritos | Somente inscritos | Inscrits uniquement | Solo iscritti | Nur angemeldete | 등록자만 | 仅已报名 | მხოლოდ რეგისტრირებულები |
| `Events.Registered` | Registered | Inscritos | Inscritos | Inscrits | Iscritti | Angemeldet | 등록자 | 已报名 | რეგისტრირებული |
| `Events.Tickets` | Tickets | Entradas | Ingressos | Billets | Biglietti | Tickets | 티켓 | 门票 | ბილეთები |
| `Events.RegistrationDate` | Registration date | Fecha de registro | Data de inscrição | Date d'inscription | Data di iscrizione | Anmeldedatum | 등록일 | 报名日期 | რეგისტრაციის თარიღი |

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter TextosDeReportesTests -nologo`
Expected: PASS.

- [ ] **Step 3: Crear `TeamEventRegistrations.razor`**

```razor
@inject HttpClient Http
@inject IStringLocalizer<SharedResources> L

@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.Extensions.Localization
@using MLMConquerorGlobalEdition.SharedComponents.Resources
@using MLMConquerorGlobalEdition.SharedComponents.Components.Grid
@using Syncfusion.Blazor
@using Syncfusion.Blazor.Grids
@using Syncfusion.Blazor.Data

@*
    Eventos del equipo (Plan J). Componente único: la ruta del API entra por parámetro. El equipo
    lo decide el servidor con el token; aquí se elige el evento y si se ve solo a los inscritos.
*@

@if (ShowBreadcrumb) { <BreadcrumbNav Items="@_breadcrumb" /> }

<div class="card bg-white border-0 rounded-3 mb-4 team-events">
    <div class="card-body p-0">
        @if (_loading)
        {
            <LoadingSpinner IsLoading="true" />
        }
        else if (_events.Count == 0)
        {
            <div class="text-center text-muted py-5 team-events-none">@L["Events.None"]</div>
        }
        else
        {
            <div class="d-flex align-items-end flex-wrap gap-3 p-3">
                <div>
                    <label class="form-label fs-12 mb-1 fw-medium" for="ev-select">@L["Events.Select"]</label>
                    <select id="ev-select" class="form-select form-select-sm" value="@_eventId" @onchange="OnEventChange">
                        @foreach (var ev in _events)
                        {
                            <option value="@ev.Id">@ev.Title — @ev.EventDate.ToString("d")</option>
                        }
                    </select>
                </div>
                <div class="form-check mb-1">
                    <input id="ev-registered-only" class="form-check-input" type="checkbox"
                           checked="@_registeredOnly" @onchange="OnRegisteredOnlyChange" />
                    <label class="form-check-label" for="ev-registered-only">@L["Events.RegisteredOnly"]</label>
                </div>
                <div class="ms-auto d-flex gap-4">
                    <div><span class="text-muted fs-12 d-block">@L["Events.Registered"]</span><span class="fw-semibold">@(_totals?.Registered.ToString("N0") ?? "—")</span></div>
                    <div><span class="text-muted fs-12 d-block">@L["Events.Tickets"]</span><span class="fw-semibold">@(_totals?.Tickets.ToString("N0") ?? "—")</span></div>
                </div>
            </div>

            <SfGrid @key="_eventId" TValue="RegistrationDto" @ref="_grid"
                    AllowPaging="true" AllowSorting="true" AllowResizing="true"
                    AllowFiltering="true" GridLines="GridLine.Horizontal">
                <SfDataManager Adaptor="Adaptors.CustomAdaptor">
                    <ServerGridAdaptor TValue="RegistrationDto"
                                       Http="Http"
                                       Endpoint="@($"{ApiBaseUrl}/events/{Uri.EscapeDataString(_eventId)}/team-registrations/grid")"
                                       QueryStringProvider="FilterQuery"
                                       AccessTokenProvider="GetTokenAsync" />
                </SfDataManager>
                <GridPageSettings PageSize="20" PageSizes="@(new int[]{20,50,100})" />
                <GridFilterSettings Type="Syncfusion.Blazor.Grids.FilterType.Excel" />
                <GridColumns>
                    <GridColumn Field="@nameof(RegistrationDto.FirstName)" HeaderText="@L["Changes.FirstName"]" Width="130" Type="ColumnType.String" />
                    <GridColumn Field="@nameof(RegistrationDto.LastName)" HeaderText="@L["Changes.LastName"]" Width="130" Type="ColumnType.String" />
                    <GridColumn Field="@nameof(RegistrationDto.EnrollDate)" HeaderText="@L["Reports.EnrollmentDate"]" Width="120" Format="d" Type="ColumnType.Date" />
                    <GridColumn Field="@nameof(RegistrationDto.IsQualified)" HeaderText="@L["Reports.Qualified"]" Width="100" TextAlign="TextAlign.Center"
                                AllowSorting="false" AllowFiltering="false">
                        <Template>@(((RegistrationDto)context).IsQualified ? L["Common.Yes"] : L["Common.No"])</Template>
                    </GridColumn>
                    <GridColumn Field="@nameof(RegistrationDto.RankName)" HeaderText="@L["Reports.Rank"]" Width="130"
                                AllowSorting="false" AllowFiltering="false">
                        <Template>@(((RegistrationDto)context).RankName ?? "—")</Template>
                    </GridColumn>
                    <GridColumn Field="@nameof(RegistrationDto.RegisteredAt)" HeaderText="@L["Events.RegistrationDate"]" Width="130" Format="d" Type="ColumnType.Date" />
                    <GridColumn Field="@nameof(RegistrationDto.Tickets)" HeaderText="@L["Events.Tickets"]" Width="90" TextAlign="TextAlign.Right" Type="ColumnType.Integer" />
                    <GridColumn Field="@nameof(RegistrationDto.Registered)" HeaderText="@L["Events.Registered"]" Width="100" TextAlign="TextAlign.Center" Type="ColumnType.Boolean">
                        <Template>@(((RegistrationDto)context).Registered ? L["Common.Yes"] : L["Common.No"])</Template>
                    </GridColumn>
                </GridColumns>
            </SfGrid>
        }
    </div>
</div>

@code {
    /// <summary>Raíz del API, p. ej. "api/v1/bizcenter".</summary>
    [Parameter] public string ApiBaseUrl     { get; set; } = "api/v1/bizcenter";
    [Parameter] public bool   ShowBreadcrumb { get; set; } = true;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private SfGrid<RegistrationDto>? _grid;
    private bool             _loading = true;
    private List<EventDto>   _events  = new();
    private string           _eventId = string.Empty;
    private bool             _registeredOnly = true;
    private TotalsDto?       _totals;

    private List<BreadcrumbNav.BreadcrumbItem> _breadcrumb => new()
    {
        new(L["Nav.Dashboard"], "/dashboard"),
        new(L["Sidebar.Events"])
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiWrapper<List<EventDto>>>($"{ApiBaseUrl}/events");
            _events = resp?.Data ?? new();
            if (_events.Count > 0)
            {
                _eventId = _events[0].Id;
                await LoadTotalsAsync();
            }
        }
        catch (Exception)
        {
            _events = new();
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadTotalsAsync()
    {
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiWrapper<TotalsDto>>(
                $"{ApiBaseUrl}/events/{Uri.EscapeDataString(_eventId)}/team-registrations/totals");
            _totals = resp?.Data;
        }
        catch (Exception)
        {
            _totals = null;
        }
    }

    private string? FilterQuery() => $"registeredOnly={(_registeredOnly ? "true" : "false")}";

    private async Task OnEventChange(ChangeEventArgs e)
    {
        _eventId = e.Value?.ToString() ?? string.Empty;
        _totals  = null;
        await LoadTotalsAsync();   // el @key del grid lo recrea con el endpoint del evento nuevo
    }

    private async Task OnRegisteredOnlyChange(ChangeEventArgs e)
    {
        _registeredOnly = e.Value is bool b && b;
        if (_grid is not null) await _grid.Refresh();
    }

    private async Task<string?> GetTokenAsync()
    {
        if (AuthState is null) return null;
        var state = await AuthState;
        return state.User.FindFirst("access_token")?.Value;
    }

    private sealed class ApiWrapper<T> { public bool Success { get; set; } public T? Data { get; set; } }

    private sealed class EventDto
    {
        public string   Id        { get; set; } = string.Empty;
        public string   Title     { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string?  Location  { get; set; }
    }

    private sealed class TotalsDto
    {
        public int Registered { get; set; }
        public int Tickets    { get; set; }
    }

    public sealed class RegistrationDto
    {
        public string    MemberId     { get; set; } = string.Empty;
        public string    FirstName    { get; set; } = string.Empty;
        public string    LastName     { get; set; } = string.Empty;
        public DateTime  EnrollDate   { get; set; }
        public bool      IsQualified  { get; set; }
        public string?   RankName     { get; set; }
        public bool      Registered   { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public int       Tickets      { get; set; }
    }
}
```

- [ ] **Step 4: Crear la página `TeamEvents.razor`**

```razor
@page "/events"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject Microsoft.Extensions.Localization.IStringLocalizer<MLMConquerorGlobalEdition.SharedComponents.Resources.SharedResources> L

<PageTitle>@L["PageTitle.TeamEvents"]</PageTitle>

<TeamEventRegistrations />
```

- [ ] **Step 5: Menú**

En `SidebarMenu.razor`, sustituir:

```razor
        <!-- Mis enlaces (Plan J) -->
```

por:

```razor
        <!-- Eventos del equipo (Plan J) -->
        <li class="menu-item @IsActive("/events")">
            <a href="/events" class="menu-link">
                <span class="material-symbols-outlined menu-icon">event</span>
                <span class="title">@L["Sidebar.Events"]</span>
            </a>
        </li>

        <!-- Mis enlaces (Plan J) -->
```

- [ ] **Step 6: Compilar la web**

Run: `dotnet build MLMConquerorGlobalEdition.BizCenterWeb -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Team/TeamEventRegistrations.razor \
        MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/TeamEvents.razor \
        MLMConquerorGlobalEdition.SharedComponents/Components/Layout/SidebarMenu.razor \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TextosDeReportesTests.cs
git commit -m "Eventos del equipo: componente compartido con selector de evento activo, solo inscritos o todo el equipo, totales y rejilla de servidor; pagina /events, entrada en el menu y textos en los 9 idiomas"
```

---

### Task 42: Pruebas contra SQL Server de eventos

> **Corrección de Nick N5.** Solo las pruebas del reporte; las del importador y de la neutralización CSV salen con las Tareas 35 y 36.

**Files:**
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerEventosTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Las subconsultas correlacionadas de entradas y fecha, el orden por ellas y los totales se
/// traducen a SQL; el índice único (EventId, ExternalRef) existe y no distingue mayúsculas, que es
/// lo que hace segura una importación repetida o simultánea.
/// </summary>
public class SqlServerEventosTests
{
    private static TeamEventRegistrationsServiceTests.Ids Nuevos() => new(
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
        "TJ-EV-" + Guid.NewGuid().ToString("N")[..12], "TJ-EV-" + Guid.NewGuid().ToString("N")[..12]);

    [SqlServerFact]
    public async Task La_rejilla_y_los_totales_se_traducen_a_SQL()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);
        var servicio = new TeamEventRegistrationsService(db);
        var porEntradas = new GridDataRequest { PageSize = 50 };
        porEntradas.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.Tickets), Direction = "desc" });

        var inscritos = (await servicio.GetGridAsync(id.Yo, id.Evento, true, porEntradas)).Value!.Items.ToList();
        var todos     = (await servicio.GetGridAsync(id.Yo, id.Evento, false, new GridDataRequest { PageSize = 50 })).Value!.Items.ToList();
        var totales   = (await servicio.GetTotalsAsync(id.Yo, id.Evento)).Value!;

        inscritos.Select(f => f.MemberId).Should().Equal(id.A, id.Yo);
        inscritos[0].Tickets.Should().Be(4);
        todos.Should().HaveCount(3);
        (totales.Registered, totales.Tickets).Should().Be((2, 6));

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task La_misma_referencia_no_entra_dos_veces_aunque_cambien_las_mayusculas()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);

        db.CorporateEventRegistrations.Add(
            TeamEventRegistrationsServiceTests.Inscripcion(id.Evento, id.B, 1, new DateTime(2026, 3, 1), $"{id.Evento}-r1".ToLowerInvariant()));
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("ya existe la referencia {0}-R1 para ese evento", id.Evento);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Importar_dos_veces_contra_SQL_es_idempotente()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);
        var csv = $"MemberId,RegisteredAt,Tickets,Price,ExternalRef\n{id.B},2026-03-05,2,25.50,{id.Evento}-NEW";
        var importador = new EventRegistrationImporter(db, new RelojFijo(new DateTime(2026, 10, 16, 12, 0, 0)));

        var primera = await importador.ImportAsync(id.Evento, new StringReader(csv), "prueba");
        var segunda = await importador.ImportAsync(id.Evento, new StringReader(csv), "prueba");

        primera.Value!.Imported.Should().Be(1);
        (segunda.Value!.Imported, segunda.Value.AlreadyImported).Should().Be((0, 1));
        (await db.CorporateEventRegistrations.CountAsync(r => r.EventId == id.Evento)).Should().Be(5);

        await tx.RollbackAsync();
    }
}
```

(Los ids de evento no pasan de 18 caracteres y la referencia sintética `TJ-EV-…-R1` cabe en el patrón de `ExternalRef`.)

- [ ] **Step 2: Ejecutar**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SqlServerEventosTests -nologo`
Expected: PASS (3) con la migración `InscripcionesAEventos` aplicada; `Skipped` sin SQL Server.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerEventosTests.cs
git commit -m "Pruebas contra SQL Server de los eventos del equipo, del indice unico de referencia externa sin distinguir mayusculas y de la importacion repetida"
```

**Fin del Bloque 6.** Una migración (`InscripcionesAEventos`).

---

# Cierre

### Task 43: Pruebas de seguridad, §12

> **Corrección de Nick.** §12 no lleva filas de importación CSV (N5) ni de `GENEALOGY_TOO_DEEP`/`HierarchyPath` (N1); los rangos son los de N4 y «Calificado» el de N3.

**Files:**
- Modify: `docs/deployment/pruebas-de-seguridad.md` (sección nueva al final, después de §10)

La numeración salta a 12 a propósito: §11 la reserva el Plan I (3DS con Spreedly).

- [ ] **Step 1: Añadir la sección al final del archivo**

````markdown

## 12. Reportes del BizCenter (Plan J)

Seis reportes nuevos o arreglados en el BizCenter: *Mis enlaces*, *Residuales diarios*, *Clientes
del equipo* (Personales/Equipo), *Actividad por fechas*, *Cambios de estado* (inscripción, Dual Team
y clientes) y *Eventos del equipo*, más la importación CSV de inscripciones a eventos en el Admin.
Tres reglas que estas pruebas comprueban: **el miembro sale siempre del token** (ningún endpoint
nuevo acepta `memberId`), **el alcance lo pone el servidor** (prefijo de genealogía para el equipo
de inscripción; fecha primero y pertenencia después para el binario) y **nada que venga de un CSV se
ejecuta ni se devuelve sin neutralizar**.

### Condiciones de despliegue

- **Orden de las migraciones** (todas las aplica `MigrateAsync` al arrancar cualquier servicio):
  `IndiceResidualesPorBeneficiarioYFecha`, `IndicePatrocinadorYFechaDeAlta`, `IndiceRutaDeGenealogia`,
  `IndiceFechaDeAlta`, `HistorialDeEstados`, `InscripcionesAEventos`. Alteran o indexan tablas de
  ~120.000 filas: desplegar fuera de horas de tráfico.
- **Antes de `IndiceRutaDeGenealogia`**, en producción:
  `SELECT MAX(LEN(HierarchyPath)) AS MaxLen, MAX(Level) AS MaxLevel FROM GenealogyTree`. Si `MaxLen`
  pasa de 360, **no** desplegar esa migración (revertir su commit, Tarea 20) y reabrir J21. Si pasa
  de 450 la migración se niega (`THROW 51002`) y el servicio no arranca: mejor saberlo antes.
- **Dominios de *Mis enlaces*** confirmados por Nick (pregunta P1) en `MyLinks:Links` del
  `appsettings.json` del BizCenter. Una plantilla inválida impide arrancar el BizCenter.
- **El historial de estados empieza al desplegar el Bloque 5.** Antes de esa fecha *Cambios de
  estado* sale vacío y las fechas de «último cambio» son las de reserva (J15). Anotar aquí la fecha
  del despliegue: ________.
- **Primer ciclo de facturación recurrente tras el Bloque 5**: comparar su duración con la del
  anterior (el interceptor añade una fila por cada cambio de estado, R14).
- **Fuera de este plan, pendiente**: índice de `Orders.OrderNo` (R9 del Plan J).

### Pruebas

Preparación: SignupAPI (`https://localhost:7005`), BizCenter (`https://localhost:7003`),
BizCenterWeb (`https://localhost:7004`), AdminAPI (`https://localhost:7002`) y AdminWeb
(`https://localhost:7001`) arrancados con el perfil `https` contra la base local, con las seis
migraciones aplicadas. Dos embajadores sin relación entre sí (A y B), un descendiente de A con
clientes en dos niveles, y un administrador. `TOKEN_A` es el Bearer de A.

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 12.1 | IDOR en *Mis enlaces* | `GET /api/v1/bizcenter/my-links?memberId={B}` con `TOKEN_A` | los enlaces de A (con el slug de A); el parámetro se ignora | ☐ |
| 12.2 | IDOR en residuales | `GET /api/v1/bizcenter/commissions/dual-residual/daily?memberId={B}&year=2026` con `TOKEN_A` | el historial de A | ☐ |
| 12.3 | IDOR en las rejillas | añadir `?memberId={B}` y `"memberId":"{B}"` al cuerpo de `POST team/enrollment/activity/grid`, `team/changes/enrollment/grid`, `team/changes/dual/grid`, `team/changes/customers/grid`, `team/enrollment/customers/grid` y `events/{id}/team-registrations/grid` con `TOKEN_A` | se ignoran: siempre el equipo de A (lo vigila además `MiembroDelTokenTests`) | ☐ |
| 12.4 | Alcance del equipo de inscripción | con `TOKEN_A`, actividad y cambios de inscripción en un rango donde B y la upline de A tienen altas y cambios | nunca aparecen B, su equipo ni la upline de A; la fila de A en actividad tiene nivel 0 | ☐ |
| 12.5 | Alcance del Dual Team | un miembro del binario de A que no es de su inscripción y otro fuera de los dos, ambos con cambios en el rango | el primero sale en `changes/dual` (con su pierna) y no en `changes/enrollment`; el segundo en ninguna | ☐ |
| 12.6 | Contacto solo de personales | `POST team/enrollment/customers/grid` (sin `scope`) con `TOKEN_A` | `email` y `phone` solo en los clientes de nivel 1; `null` en los demás. Con `?scope=personal`, solo los de nivel 1 | ☐ |
| 12.7 | La búsqueda no delata | el mismo endpoint con `"search"` = parte del correo de un cliente de nivel 2, y con un filtro de columna `Email contains` ese texto | 0 resultados en los dos | ☐ |
| 12.8 | El `GET` antiguo también | `GET /api/v1/bizcenter/team/enrollment/customers` con `TOKEN_A` | mismo anulado que 12.6 | ☐ |
| 12.9 | Rango de fechas | `from=2024-01-01&to=2025-01-01`; `to` anterior a `from`; sin fechas | `400 REPORT_RANGE_TOO_LONG`; `400 REPORT_RANGE_INVALID`; `400 REPORT_RANGE_INVALID` | ☐ |
| 12.10 | Vista desconocida | `POST team/changes/binary/grid` | `404 REPORT_SCOPE_NOT_FOUND` | ☐ |
| 12.11 | La ruta binaria no sale | respuesta de `team/changes/dual/grid` en las herramientas del navegador | sin `dualPath` ni ninguna ruta del árbol | ☐ |
| 12.12 | Evento que no se ve | `events/{id de un evento desactivado}/team-registrations/grid` y `/totals` | `404 EVENT_NOT_FOUND`; `GET events` no lo lista | ☐ |
| 12.13 | Totales del equipo | inscripciones de miembros fuera del equipo de A en el mismo evento | no cuentan ni en filas ni en totales | ☐ |
| 12.14 | Importación solo para Admin | `POST /api/v1/admin/corporate-events/{id}/registrations/import` sin token, con un `SupportLevel1` y con `TOKEN_A` | `401`, `403`, `401`/`403` | ☐ |
| 12.15 | Inyección CSV | filas con `=HYPERLINK("http://evil","x")` en `MemberId`, `+1`, `-1` y `@SUM(A1)` en otros campos, y un campo que empiece por tabulador | cada una rechazada con `FORMULA_NOT_ALLOWED`; en el informe el valor vuelve con `'` delante; nada importado de esas filas | ☐ |
| 12.16 | Archivo que no toca | un `.xlsx`; un CSV guardado en Latin-1 con una «ñ» (bytes que no son UTF-8 válido); uno de más de 2 MB; uno con 10.001 filas; uno con otra cabecera | `400 INVALID_FILE_TYPE`; `400 INVALID_ENCODING`; `400 FILE_TOO_LARGE` (o `413` si lo corta el servidor); `400 TOO_MANY_ROWS`; `400 INVALID_HEADER`. En todos, nada importado | ☐ |
| 12.17 | Idempotencia | importar el mismo CSV dos veces | la segunda: `imported: 0`, `alreadyImported: N`; en SQL una sola fila por `(EventId, ExternalRef)` | ☐ |
| 12.18 | Importaciones simultáneas | lanzar a la vez dos importaciones del mismo archivo | una `200` y la otra `200` con `alreadyImported` o `409 IMPORT_CONFLICT`; nunca filas duplicadas | ☐ |
| 12.19 | Miembro inexistente | fila con un `MemberId` que no existe | `MEMBER_NOT_FOUND` con su línea; el resto del archivo entra | ☐ |
| 12.20 | Suplantación de solo lectura | un administrador suplanta a A con token de solo lectura y abre las seis pantallas | las rejillas `POST` responden `200` (`ReadOnlySafe`) con datos de A; ningún endpoint del plan escribe | ☐ |
| 12.21 | Suplantación y enlaces | durante la suplantación, *Mis enlaces* | el slug del suplantado; copiar no escribe nada | ☐ |
| 12.22 | Suplantación e importación | con el token de suplantación del BizCenter, `POST admin/corporate-events/{id}/registrations/import` | `401`/`403`: la importación es del Admin | ☐ |
| 12.23 | Plantilla de enlace inválida | arrancar BizCenter con `MyLinks__Links__0__Url=http://mwrlife.com/{slug}` | no arranca: `InvalidOperationException` «[MyLinks] … https» | ☐ |
| 12.24 | Slug con caracteres raros | un slug con espacio o acento | sale escapado en la URL (`%20`, `%C3%B3`) | ☐ |
| 12.25 | Residuales sin débitos | comparar el total del mes con `SELECT SUM(Amount) FROM DailyResidualEarnings WHERE BeneficiaryMemberId = @A AND Status <> 3 AND EarnedDate >= @desde AND EarnedDate < @hasta` | coinciden; no aparecen débitos negativos ni consolidaciones de `CommissionEarnings` | ☐ |
| 12.26 | Hora de servidor | cambiar la zona horaria del navegador a UTC+14 y volver a abrir residuales, actividad y cambios | el año por defecto y los rangos por defecto no cambian (son del servidor) | ☐ |
| 12.27 | Historial | cambiar desde el Admin el estado de A con un motivo; activar un alta; forzar que una renovación pase a `HoldByBilling` | una sola fila en `MemberStatusHistories` con el motivo; una fila `Pending → Active`; una fila en `MembershipStatusHistories` | ☐ |
| 12.28 | Profundidad del árbol | en una transacción que se deshace, un nodo de genealogía con ruta de 441 caracteres como patrocinador y un alta bajo él | `400 GENEALOGY_TOO_DEEP` sin perfil, pedido ni nodo nuevos | ☐ |
| 12.29 | Menú | abrir `/team/residuals` y `/team/dual-changes` | el grupo Dual Team queda abierto | ☐ |
| 12.30 | Primer ciclo de facturación tras el Bloque 5 | comparar la duración del ciclo con el anterior | sin degradación apreciable | ☐ |
````

- [ ] **Step 2: Commit**

```bash
git add docs/deployment/pruebas-de-seguridad.md
git commit -m "Pruebas de seguridad, seccion 12: reportes del BizCenter del plan J (IDOR, alcance del equipo de inscripcion y del binario, contacto solo de personales, rangos, eventos, importacion CSV con inyeccion de formulas, idempotencia y concurrencia, suplantacion, plantillas de enlaces, hora de servidor, historial y profundidad del arbol) y sus condiciones de despliegue"
```

---

### Task 44: Verificación completa

**Files:** ninguno (verificación).

- [ ] **Step 1: Compilar la solución**

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 2: Todas las pruebas de los proyectos tocados**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`, `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests -nologo`, `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo` y `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS en los cuatro, también lo anterior al plan. Las `[SqlServerFact]` pasan con SQL Server local y las seis migraciones aplicadas (o salen `Skipped` sin él; no vale como verde para cerrar el plan).

- [ ] **Step 3: Arrancar en local**

Con las migraciones ya aplicadas a mano (pasos de las Tareas 8, 18, 20, 23, 27 y 34), cuatro consolas:

```bash
dotnet run --project MLMConquerorGlobalEdition.SignupAPI --launch-profile https
dotnet run --project MLMConquerorGlobalEdition.BizCenter --launch-profile https
dotnet run --project MLMConquerorGlobalEdition.BizCenterWeb --launch-profile https
dotnet run --project MLMConquerorGlobalEdition.AdminWeb --launch-profile https
```

(y `MLMConquerorGlobalEdition.AdminAPI` en una quinta para el paso 9). Entrar en `https://localhost:7004` con un embajador de prueba que tenga equipo.

- [ ] **Step 4: Bloque 1** — Menú → *Mis enlaces*: salen los cinco personales con el slug del miembro y los cinco corporativos; *Copiar* deja la URL en el portapapeles y el botón dice «Copiado». Con un miembro sin slug: aviso con enlace a *Mi perfil* y solo corporativos.

- [ ] **Step 5: Bloque 2** — `/team/residuals`: el grupo *Dual Team* del menú queda abierto; la sección 4 muestra año (por defecto el del servidor) y mes, filas de `DailyResidualEarnings` con fecha de pago, puntos y comentario, y los totales del periodo; el gráfico suma por mes lo mismo que la consulta de §12.25. En el Admin, perfil de miembro → Dual Team → Residuals: lo mismo para ese miembro.

- [ ] **Step 6: Bloque 3** — `/team/enrollment/customers`: *Todo el equipo* muestra la columna de nivel rellena y el correo solo de los personales («—» en el resto); *Personales* deja solo los de nivel 1. Repetir 12.7 desde la caja de búsqueda.

- [ ] **Step 7: Bloque 4** — `/team/enrollment/activity`: el rango por defecto es del día 1 del mes a hoy; las columnas de nivel tienen los nombres de `MembershipLevels`; con un rango de 400 días sale el aviso sin hacer la petición.

- [ ] **Step 8: Bloque 5** — Desde el Admin, cambiar el estado de un miembro del equipo y volver a `/team/enrollment/changes` con el rango de hoy: aparece con el estado nuevo y la fecha de hoy. `/team/dual-changes` muestra la columna de pierna; `/team/enrollment/customer-changes` solo clientes.

- [ ] **Step 9: Bloque 6** — En AdminWeb (`https://localhost:7001/admin/events`), botón de importar de un evento activo → subir un CSV con dos filas buenas y una con `=1+1`: el informe dice 2 importadas y 1 rechazada (`FORMULA_NOT_ALLOWED`, línea 4). Subirlo otra vez: 0 importadas, 2 ya importadas. En el BizCenter, `/events`: el evento sale en el selector, los dos inscritos del equipo con sus entradas y los totales; desmarcar *Solo inscritos* enseña al resto del equipo con «No».

- [ ] **Step 10: Pruebas de seguridad** — Ejecutar §12 de `docs/deployment/pruebas-de-seguridad.md` y marcar las que pasen con fecha y cómo se probó, como en §10.

- [ ] **Step 11: Cerrar** — Parar los procesos. No hay nada que commitear en esta tarea salvo las marcas de §12, que van en su propio commit:

```bash
git add docs/deployment/pruebas-de-seguridad.md
git commit -m "Pruebas de seguridad, seccion 12: resultado de la verificacion local del plan J"
```

---

## Autorrevisión

- **Cobertura del encargo.**
  - *Mis enlaces* (plantillas en configuración o `GlobalParameters`, decidido en J1; entrada en el menú): Tareas 2–6.
  - *Residuales diarios*: endpoint sobre `DailyResidualEarnings` (9, 11); sección 4 y gráfico (10, 12, 13); `UtcNow` de `CommissionsService` (10) y del componente (13); `IsDualTeamActive` (7); borrado de `DualResidualReport.razor` tras comprobar que nadie lo usa (13).
  - *Clientes del equipo*: alcance Personales/Equipo, `Level` y contacto solo de los personales (15–17).
  - *Actividad por fechas*: columnas por los niveles reales 1–4, sin «Plus» y con Turbo nuevo, documentado en J10; puntos actuales (21–26).
  - *Cambios de estado*: modelo e interceptor primero (27, 28); «Calificado» decidido y documentado (J15, Tarea 29); servicio (30); endpoint (31); componente único con tres vistas y estado actual con fecha del último cambio (32); SQL (33).
  - *Eventos del equipo*: entidad (34); CSV con validación, informe de rechazos, idempotencia y defensa contra inyección (35–38); reporte (39–41); SQL (42).
  - Índices: `MemberProfiles(SponsorMemberId, EnrollDate)` (18), `DailyResidualEarnings(BeneficiaryMemberId, EarnedDate)` (8), `MemberStatusHistories` con `MemberId` a `nvarchar(450)` e índice (27), `HierarchyPath` evaluado y propuesto con código que lo escribe revisado, límite teórico, guarda y riesgo (J21, R1, Tareas 19 y 20), más `MemberProfiles(EnrollDate)` (23).
  - `Orders.OrderNo`: en Riesgos (R9) y Fuera de alcance.
  - Reglas: componentes unificados (componentes en `SharedComponents`, páginas de tres líneas, `ApiBaseUrl`/`Scope` por parámetro); miembro del token (`MiembroDelTokenTests`); alcance por prefijo y binario reducido por fecha (J17); `IDateTimeProvider.Now` (con la excepción documentada del interceptor, J25); TDD con InMemory y `[SqlServerFact]`; migraciones creadas y aplicadas en local con `--connection` como paso explícito; commits en español sin coautoría y con `git add` explícito; §12 (43).
  - Preguntas abiertas: P1–P4 del encargo y P5–P6 propias.
- **Marcadores:** ningún «TBD» ni «implementar después». Los cambios en archivos existentes van como «sustituir X por Y» con el texto literal; los dos borrados por rango de líneas de `DualTeamResiduals.razor` (Tarea 13, pasos 2 y 6) llevan sus anclas de inicio y fin y una comprobación con `grep`.
- **Nombres usados en varias tareas:** `RelojFijo`, `ReportesDb` (`Nueva`, `Ruta`, `Niveles`, `Miembro`, `Nodo`, `NodoBinario`, `Suscripcion`, `Puntos`, `RaizDelRepositorio`), `SqlServerReportes` (`Db`, `NuevoId`), `SqlServerFactAttribute`; `MyLinksOptions`, `MyLinkTemplate`, `MyLinksOptionsGuard`, `IMyLinksService`, `MyLinksService`, `MyLinksView`, `MyLinkView`, `AddMyLinks`; `SidebarRoutes`; `IDailyResidualReportService.GetHistoryAsync`, `DailyResidualHistoryView`, `DailyResidualRowView`; `CustomerScope`, `CustomerScopes.Parse`; `GenealogyPath.Root/TryChild/MaxLength`; `ReportDateRange.TryCreate/SetupFor/InvalidCode/TooLongCode/MaxDays`, `ReportSetupView`, `ReportRangeCheck.Problem`; `IEnrollmentActivityService.GetColumnsAsync/GetGridAsync`, `EnrollmentActivityRowView`, `EnrollmentActivityColumnView`; `TeamReportsController.GetSetup/GetActivityColumns/GetActivityGrid/GetStatusChangesGrid`; `MembershipStatusHistory`, `StatusHistoryInterceptor`; `QualificationTimeline.Change/LastFlip/FallbackDate`; `StatusChangesScope`, `StatusChangesScopes.TryParse`, `ITeamStatusChangesService.GetGridAsync`, `TeamStatusChangeView`; `CorporateEventRegistration`, `CsvLite`, `CsvRecord`, `EventRegistrationImportCodes`, `EventRegistrationImportResult`, `EventRegistrationRejection`, `IEventRegistrationImporter.ImportAsync`, `EventRegistrationImporter.MaxRows/MaxListedRejections`; `ITeamEventRegistrationsService.GetActiveEventsAsync/GetGridAsync/GetTotalsAsync`, `TeamEventView`, `TeamEventRegistrationView`, `TeamEventTotalsView`, `TeamEventErrors`; `TeamEventsController.GetActive/GetTeamRegistrationsGrid/GetTeamRegistrationsTotals`; `CorporateEventsController.ImportRegistrations/MaxImportBytes`.
- **Escenarios compartidos entre pruebas** (InMemory y SQL Server usan el mismo): `EnrollmentActivityServiceTests.EquipoAsync`, `TeamStatusChangesServiceTests.EscenarioAsync` + `Ids`, `TeamEventRegistrationsServiceTests.EscenarioAsync` + `Ids` + `Inscripcion`, `ClientesDelEquipoTests.Servicio`, `DailyResidualReportServiceTests.Residual`, `EventRegistrationImporterTests.BaseAsync`.
