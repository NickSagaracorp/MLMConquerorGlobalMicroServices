# Plan K — Cuota anual del Biz Center

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cobrar la cuota anual de $99 de cada Ambassador como un cobro **aparte** de su membresía, con un segundo estado de cobro anual por miembro anclado en el aniversario de su inscripción, las exclusiones del legado, un único motor de renovación (`RecurringBillingProcessor`), un botón «Pagar ahora» en el BizCenter (comisiones, eWallet o token; nunca tarjeta), la preferencia de pagar las cuotas con el saldo, y la rama «anualidad pagada» de «Calificado» funcionando de verdad.

**Architecture:** La cuota anual es una fila de `SubscriptionBillingState` con `Purpose = AnnualFee` y sin suscripción (`MembershipSubscriptionId` nulo, garantizado por un `CHECK`), una por miembro (índice único filtrado), en el plan que ya existe «Lifestyle Ambassador (Annual)», marcado ahora con `IsAnnualFeePlan`. Un trabajo idempotente da de alta los estados de los Ambassadors existentes por lotes. El planificador, los trabajadores y el barrido de siempre los recogen; `RecurringBillingProcessor` los reconoce y delega en `AnnualFeeChargeService` (Billing), que decide con reglas puras de Repository (`AnnualFeeSchedule`, `AnnualFeeEligibility`, compartidas con el BizCenter), cobra bajo el candado de fondos del miembro (`IMemberFundsGate`) en el orden token → comisiones → eWallet, deja cada pago en `AnnualFeePayments` (idempotencia y conciliación, con un índice único por ciclo que hace imposible el doble cobro en la base) y avanza el estado. «Pagar ahora» entra por el BizCenter con step-up y se reenvía a Billing con el JWT del miembro, como la compra de tokens del Plan F. La pantalla es un componente único (`AnnualFeeCard`) dentro del perfil, de solo lectura en el Admin.

**Tech Stack:** .NET 10, ASP.NET Core MVC, EF Core 10 (SQL Server), Hangfire, Blazor Server (BizCenterWeb, AdminWeb) con un componente sin Syncfusion, xUnit + FluentAssertions + Moq + bUnit, EF InMemory y pruebas `[SqlServerFact]` contra la base local. Rama: `plan-k-cuota-anual`, creada desde `master` antes de la Tarea 1.

**Cinco bloques entregables, en este orden.** Cada uno se revisa y se fusiona por separado; un bloque solo depende de los anteriores.

| Bloque | Tareas | Qué entrega |
|---|---|---|
| 1. Un solo motor | 1–3 | `MembershipAutoRenewalJob` apagado; el débito de comisiones del motor bajo el candado; el motor no da por cobrada una renovación con tarjeta simulada |
| 2. Modelo y alta de estados | 4–8 | Entidades, configuración y migración `CuotaAnual` con guardas; el plan anual no crea estados por suscripción; calendario y exclusiones puros; trabajo de alta de estados |
| 3. El cobro | 9–18 | Contratos, débito de comisiones, consumo de token, cobro automático (token, comisiones, eWallet), «Pagar ahora», conciliación, delegación desde el motor, controlador de Billing, registro y pruebas contra SQL Server. Cobro automático apagado por defecto |
| 4. Calificado | 19 | `BillingStateAnnualFeeStatus` sustituye a `AnnualFeeNotModeled` en BizCenter y AdminAPI |
| 5. BizCenter y pantalla | 20–23 | Resumen y preferencia, endpoints del BizCenter (con step-up) y del Admin (lectura), `AnnualFeeCard` en el perfil y textos en nueve idiomas |
| Cierre | 24–25 | §13 de `pruebas-de-seguridad.md` y verificación completa |

**Reglas del repo que aplican en todo el plan:**

- **Componentes unificados.** Se programa una vez y lo específico se inyecta por parámetro. `AnnualFeeCard` sirve al BizCenter y al Admin con el mismo código; la ruta base la decide `ProfileApiPaths.ProfileBase` y el modo de solo lectura lo pasa el perfil. Las reglas de negocio que deciden qué se puede pagar viven una vez en Repository y las usan Billing (para cobrar) y el BizCenter (para enseñar).
- **Miembro del token.** Ningún endpoint nuevo del BizCenter ni de Billing acepta `memberId` (ni ruta, ni query, ni cuerpo). Lo vigila `MiembroDelTokenTests` (ampliado en la Tarea 21). El Admin lee con el `memberId` de la ruta, como el resto de su perfil de miembro.
- **Dinero.** Todo lo que mueve saldo o tokens del miembro va dentro de `IMemberFundsGate.EnterAsync`. Pagar con saldo (comisiones, eWallet o token) exige step-up y se rechaza bajo suplantación en el BizCenter **y** en Billing. Nunca se cobra con tarjeta: la tarjeta queda para el Plan I (3DS) y su gateway es un stub.
- **Hora de servidor.** `IDateTimeProvider.Now` (el de Billing en Billing; el de SharedKernel en Repository). Las expresiones cron de Hangfire siguen en UTC, como las demás.
- **Ids de miembro (aclarado por Nick el 2026-10-17).** Los 50 caracteres eran del sitio replicado (`ReplicateSiteSlug`, ya `nvarchar(100)`), no del id del miembro: con la migración de datos cada miembro recibirá un `MemberId` de este sistema, tipo `AMB-123456`, de `nvarchar(20)`. Una columna nueva que guarde un `MemberId` con FK hacia `MemberProfiles.MemberId` sigue su longitud; sin FK, `nvarchar(100)` o `nvarchar(450)` valen. Los ids de entidades, `nvarchar(450)`. Nada se estrecha.
- **Pruebas.** TDD con EF InMemory; `[SqlServerFact]` donde InMemory no reproduce la base (índices únicos filtrados, `CHECK`, `sp_getapplock`). Las de SQL Server usan un miembro sintético propio y borran lo que escriben.
- **Migraciones.** Se crean con `dotnet ef migrations add`; **no** se aplican solas. Aplicarlas a la base local es un paso explícito, siempre con `--connection` y siempre contra `MLMConquerorGlobalEditionDb` local. Cualquier servicio que se arranque en local también las aplica (`MigrateAsync` en todos los `Program.cs`): se aplican a mano **antes** de arrancar nada.
- **Textos.** Toda clave nueva va en los **nueve** `.resx` de `SharedComponents/Resources` (en, es, pt, fr, it, de, ko, zh, ka); el español, en tuteo. Lo vigila `TextosDeCuotaAnualTests`.
- **Commits.** Mensaje en español, sin tildes (como el historial), solo la descripción de lo que se hizo: **sin** `Co-Authored-By` ni menciones de herramientas o IA. `git add` con rutas explícitas, nunca `git add .` ni `-A`.
- No se migran datos del legado. No se toca la matriz de paridad (`MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md`).

---

## Lo que dice el código hoy (comprobado el 2026-10-17 en `master`, con los Planes F, H y J y el cierre de la tarjeta)

- **El plan anual existe y no tiene estados.** `RecurringBillingPlans` tiene «Travel Advantage» (Id 1, 30 días) y «Lifestyle Ambassador (Annual)» (Id 2: anual, cadencia `1,1,1,2,2,5,5`, `MarkExpired`, comisiones primero, token 103). El plan 2 enlaza el producto `00000005-prod-…` «Lifestyle Ambassador» (`MembershipLevelId` 1, `AnnualPrice` 99, `SetupFee` 99, `CorporateFee`). En local **no hay ninguna suscripción de nivel 1**: los 119.445 Ambassadors tienen una sola suscripción, casi todas Travel Advantage Elite. Por eso el plan 2 tiene 0 estados, y por eso la cuota no se puede colgar de una suscripción.
- **`SubscriptionBillingState.MembershipSubscriptionId` es obligatorio y único** (`nvarchar(100)`). Los trabajadores, el barrido, el servicio de alta del estado, el procesador y el Admin lo leen.
- **Dos motores.** `MembershipAutoRenewalJob` (Hangfire diario, 6:00 UTC) renueva toda suscripción activa con `StartDate <= hoy − 30` vía `RenewMembershipHandler`, sin candado ni idempotencia, con bucle de `OrderNo` por `AnyAsync`. `RecurringBillingProcessor` (planificador 5:00, trabajadores, barrido 7:00) hace lo mismo con estados. Con los dos, una suscripción puede cobrarse dos veces.
- **El débito de comisiones del motor no toma el candado.** `RecurringBillingProcessor` mira `GetAvailableAsync` (que suma comisiones ya reservadas por un pago y comisiones aún no pagables) y llama a `FundWithCommissionBalanceAsync` sin `IMemberFundsGate`. La compra de tokens (`TokenPurchaseService.PayWithCommissionsAsync`) sí lo toma y usa `GetSpendableAsync`.
- **El motor acepta un éxito simulado de tarjeta.** `SpreedlyCardGatewayService` devuelve `simulated-…` con éxito; `HandleCardChargeAsync` da la renovación por cobrada sin mirar eso. `CardChargeOutcomes.IsSimulated` ya existe (Plan F).
- **La tarjeta está apagada en la compra de tokens y en el alta** (`Billing:TokenPurchase:CardEnabled`, `Signup:CardEnabled`, ambos `false`).
- **Tokens de cuota.** `TokenTypes` 23 «Annual: Biz Center» y 10 «Annual Fee» (categoría Annual) otorgan el producto 5; 24 «Legacy Biz Center Fee» (Other) no otorga nada; 103 «Lifestyle Ambassador Subscription» es el token del plan 2. Los cuatro tienen `IsPurchasable = 1` en local; 23, 10 y 103 se venden en `/tokens` a $99 (`SetupFee + MonthlyFee` del producto 5), 24 no (`TOKEN_TYPE_NO_PRODUCT`). Hoy nadie tiene ninguno.
- **El legado** (`BizCenterFeeMWRLife.cs`): no cobra si la cuenta está cancelada (`Active == 2`), si tiene menos de un año (`days < 365`), si está suspendida (`Suspend` con `SuspendUntil` futuro), si se cobró este año (`BilledRecentlyAnnual`) o si tiene `KeepQualified` (y entonces la marca como activa). Orden: token de cuota propio o asignado → convertir Daily Pay (comisiones) en token → eWallet + Daily Pay → tarjeta. Al fallar marca al agente inactivo («no calificado»). La preferencia `DailyTokenDeduction` («Use commissions to pay for my Travel Advantage membership», `AjaxController.TokenDeductionPreference`) **no se consulta** en la cuota anual: la comprobación está comentada en `BillByToken`, así que el legado convertía comisiones siempre.
- **«Calificado»** (Plan J): `QualificationService` usa `IAnnualFeeStatus`, cuya única implementación, `AnnualFeeNotModeled`, devuelve siempre «nadie pagó». Registrada en `BizCenter/Program.cs:98` y `AdminAPI/Program.cs:85`.
- **Volúmenes locales.** 118.263 estados de Travel Advantage (106.229 `Stopped`); 106.185 suscripciones en `HoldByBilling` (la investigación del 2026-10-16 contaba 92.599); 2 Ambassadors con más de un año (inscritos el 2025-10-16); 40.325 inscritos el 2026-06-05, 37.509 el 2026-06-07 y 31.629 el 2026-06-06 (datos sintéticos).
- **`Admin bill-now` no funciona.** `AdminRecurringBillingController.BillNow` pide `IRecurringBillingProcessor` con `[FromServices]`, pero AdminAPI no lo registra: hoy responde 500. Fuera de este plan (R4).

---

## Riesgos

| # | Riesgo | Mitigación en este plan |
|---|---|---|
| R1 | **Cobro masivo el día del despliegue.** Si los estados se crearan con el primer aniversario aunque ya haya pasado, todo Ambassador inscrito hace más de un año vencería a la vez y el primer ciclo cobraría comisiones y eWallet de golpe. En producción no sabemos cuántos tienen la inscripción antigua. | (1) Al crear el estado, el primer vencimiento es el **primer aniversario que no haya pasado** (`AnnualFeeSchedule.FirstDueOnCreation`, K3): no se cobra hacia atrás. (2) El cobro automático está **apagado por defecto** (`Billing:AnnualFee:AutoChargeEnabled = false`, K13): se despliega, se crean los estados, se revisan y Nick lo enciende. «Pagar ahora» funciona con él apagado. |
| R2 | **Picos de aniversarios.** En local, 109.000 Ambassadors cumplen años entre el 3 y el 8 de junio (datos sintéticos): el 2027-06-05 serían 40.000 cobros en una noche, cada uno con lecturas de comisiones y quizá una llamada a i-Payout. | Los estados anuales entran en el mismo planificador por lotes y trabajadores que Travel Advantage. El cobro con eWallet hace dos llamadas a i-Payout por miembro; con el interruptor apagado no hay cobros hasta que Nick decida. §13 pide medir el primer ciclo con el interruptor encendido en un entorno de prueba. |
| R3 | **Migración al arrancar.** `CuotaAnual` altera `SubscriptionBillingStates` (118.263 filas: columna a anulable, columna nueva, índices nuevos y `CHECK`), añade una columna con valor por defecto a `MemberProfiles` (~119.400 filas) y crea una tabla. Todos los servicios la aplican al arrancar. | Columnas con valor por defecto constante (metadatos en SQL Server). Dos guardas que se niegan a migrar con datos contradictorios: `THROW 51010` (más de un plan «Lifestyle Ambassador (Annual)») y `THROW 51011` (ya hay estados por suscripción en ese plan). Nada se estrecha. Desplegar fuera de horas de tráfico; en local, aplicar a mano antes de arrancar nada. |
| R4 | **Otros caminos que renuevan.** Apagar `MembershipAutoRenewalJob` no borra `POST api/v1/billing/memberships/renew` (`BillingController`), que sigue llamando a `RenewMembershipHandler` para el miembro del token o el personal. El `bill-now` del Admin está roto (no registra el procesador). AdminAPI referencia el proyecto Billing, y MVC puede descubrir sus controladores (también el nuevo `AnnualFeeController`), que allí no tienen sus servicios. | El endpoint manual no cobra cuotas anuales (no conoce los estados anuales) y queda anotado (P4). `bill-now` del Admin: fuera de alcance. §13.16 comprueba que `POST /api/v1/billing/annual-fee/pay` contra AdminAPI no mueve nada (404 o 500 sin efectos). |
| R5 | **Membresías en `HoldByBilling`** (106.185 en local, casi todos los Ambassadors). **Decisión de Nick (2026-10-17): una membresía en `HoldByBilling` queda excluida de la cuota anual; mientras esté así no se cobra** (ni en automático ni con «Pagar ahora»). El riesgo pasa a ser el contrario: el día que una membresía sale de la espera, su cuota puede estar vencida desde hace meses y se cobra en la siguiente pasada; con K4 revisado, el siguiente vencimiento es un año desde ese cobro, así que no se cobra dos veces seguidas. | Se crean sus estados igual (K3) para que estén listos al salir de la espera. `AnnualFeeEligibility` los pospone a diario (`MEMBERSHIP_ON_HOLD`, sin avanzar el vencimiento ni escribir intentos) y «Pagar ahora» responde `ANNUAL_FEE_MEMBERSHIP_ON_HOLD`. La membresía que cuenta es la vigente del miembro: su suscripción no borrada más reciente por `StartDate` y luego `CreationDate`, la misma regla que usa `QualificationService` (`AnnualFeeMemberQueries.FactsAsync`). |
| R6 | **«Al día» sin haber pagado aquí.** Un Ambassador antiguo recibe un estado con vencimiento en su próximo aniversario; hasta entonces su estado está «al día» y cuenta como anualidad pagada en «Calificado», aunque nunca haya pagado en este sistema (lo pagó, o no, en MWR Life). | Es la consecuencia de no migrar datos y de no cobrar hacia atrás (K3, K16). Pregunta P1 con la alternativa (exigir un cobro registrado). Solo afecta a lo que se ve (Plan J: «Calificado» no mueve comisiones ni rangos). |
| R7 | **Cambio de comportamiento en Travel Advantage.** El débito de comisiones del motor pasa a mirar el saldo **gastable** (`GetSpendableAsync`, como la compra de tokens) en lugar del disponible: las comisiones reservadas por un pago o aún no pagables ya no pagan renovaciones. Habrá menos renovaciones pagadas con comisiones y más intentos con tarjeta (que fallan o son simulados). | Es lo correcto: con el disponible, el mismo dinero podía pagar un payout y una renovación (K18). **Aprobado por Nick (2026-10-17)**: el motor usa el saldo gastable. Se anota en §13. |
| R8 | **Token sin consumir en el camino de Travel Advantage.** `FundWithCommissionBalanceAsync` emite un `TokenTransaction` `EarnedRenewal` (sin código) y suma 1 al saldo del token del plan, pero nadie lo consume: cada renovación pagada con comisiones deja un token de regalo en el saldo. | Fuera de alcance (es Travel Advantage); la cuota anual **no** usa ese método (K9, Tarea 10). Pregunta P5. |
| R9 | **eWallet sin el miembro delante.** El cobro automático con eWallet llama a i-Payout de madrugada. Un resultado dudoso queda `NeedsReconciliation`, no se reintenta y lo cierra tesorería por API. | Dos fases bajo el candado y verificación por saldo, como `TokenPurchaseService` (K11). Barrido de `Pending` a los 30 minutos (Tarea 15). No hay pantalla de conciliación en el Admin (P7). |
| R10 | **Rastro del stub.** Cuando el gateway devuelve un éxito simulado, el orquestador ya escribió un `PaymentHistory` `Captured` antes de que el motor lo rechace (Tarea 3). | El pedido se cancela y el estado no avanza; el `PaymentHistory` simulado queda (lo escribe el orquestador, no el motor) y §13 da la consulta para encontrarlos (`GatewayTransactionId LIKE 'simulated-%'`). |
| R11 | **`Orders.OrderNo` sin índice** (`nvarchar(max)`, 289.519 filas en local) y seis bucles de unicidad con `AnyAsync`. | La cuota anual **no** usa ese bucle: genera el número con `OrderNumberHelper.Generate("Annual Fee", now)` (456.976 combinaciones por milisegundo), como la compra de tokens (K20). Apagar `MembershipAutoRenewalJob` saca de la noche uno de los seis bucles (`RenewMembershipHandler`). El índice sigue pendiente de Nick. |
| R12 | ~~Columnas de id estrechas que ya existen.~~ **Deja de ser un riesgo (Nick, 2026-10-17):** los ids de 50 caracteres eran del sitio replicado (`ReplicateSiteSlug`, ya `nvarchar(100)`); los miembros migrados recibirán un `MemberId` de este sistema de `nvarchar(20)`, así que `MemberProfiles.MemberId nvarchar(20)` y `TokenPurchases.MemberId nvarchar(36)` bastan. | Nada que hacer. `AnnualFeePayments.MemberId` queda en `nvarchar(100)`, como `SubscriptionBillingStates.MemberId`: no tiene FK a `MemberProfiles`, así que 100 vale. |
| R13 | **Conciliación de §12 (Plan J).** Hay 33 estados de Travel Advantage `Stopped` con la membresía `Active`. Hoy `MembershipAutoRenewalJob` los sigue renovando por su cuenta (no mira estados). Al apagarlo, nadie los cobra y siguen activos sin pagar. | Antes de desplegar el Bloque 1, ejecutar la consulta de §12 y que Nick decida (§13 lo repite como condición). La consulta sigue valiendo con estados anuales: el `JOIN` con `MembershipSubscriptions` los excluye (su suscripción es nula); §13 le añade `AND s.Purpose = 1` para dejarlo explícito. |
| R14 | InMemory no aplica índices únicos filtrados, `CHECK` ni `sp_getapplock`. | Tarea 18 contra SQL Server: los dos índices únicos, el `CHECK` y dos «Pagar ahora» simultáneos con el candado real. |
| R15 | **Consumo de token frente a distribución.** El BizCenter distribuye y transfiere tokens; un token de cuota podría moverse a la vez que se consume. | Las dos cosas van bajo el mismo candado del miembro (el de distribuir lo toma el BizCenter desde el Plan F). El consumo relee la instancia dentro del candado. |
| R16 | Las exclusiones temporales (suspendida, pendiente de activar, `KeepQualified`) se vuelven a mirar cada día. | Coste pequeño (en local hay 0 `KeepQualified` y 0 suspendidos). Si crece, pasar a `+7 días`. |
| R17 | **Doble cobro con el legado.** Un miembro que pagó su cuota en MWR Life este año puede volver a pagarla aquí en su próximo aniversario, porque no se migran datos. | Aceptado por la regla de no migrar datos. El cobro automático empieza apagado y Nick elige cuándo encenderlo. |

## Decisiones

**De Nick (vinculantes):** cobro aparte de $99 por Ambassador con un segundo estado de cobro anual; ancla en la inscripción + 1 año; exclusiones del legado (cancelada, menos de un año, suspendida, cobrada hace poco, `KeepQualified`); un solo motor (`RecurringBillingProcessor`) y `MembershipAutoRenewalJob` apagado; «Pagar ahora» con comisiones, eWallet o token, sin tarjeta hasta el Plan I y nunca con el stub; preferencia por miembro de pagar las cuotas con tokens, con orden token → comisiones → eWallet; la anualidad cuenta como pagada si el estado anual está al día; miembro del token, step-up y bloqueo bajo suplantación en los pagos con saldo, candado, idempotencia y conciliación, hora de servidor, componentes unificados, ids de al menos `nvarchar(100)`, migraciones pequeñas con guardas, sin migrar datos del legado.

**De Nick, 2026-10-17 (mandan sobre lo de abajo):**

1. **`HoldByBilling` excluida.** Una membresía en `HoldByBilling` queda excluida de la cuota anual: mientras esté así no se cobra (K5, K14, R5, P2).
2. **El saldo paga por defecto.** El cobro automático usa por defecto el saldo: sin token, primero comisiones y después eWallet. El miembro puede desactivarlo (K6, K7, P3).
3. **Pago tardío reancla.** El próximo vencimiento es un año desde la fecha de pago, sin gracia de 30 días que conserve el aniversario. El pago adelantado (desde 30 días antes) suma un año al vencimiento actual para no hacer perder días al miembro (K4).

**Tomadas aquí (con su porqué):**

| # | Decisión | Por qué |
|---|---|---|
| K1 | **El estado anual es un `SubscriptionBillingState` con `Purpose = AnnualFee` y `MembershipSubscriptionId` nulo**, uno por miembro (índice único filtrado `UX_SubscriptionBillingStates_AnnualFeeMember`, `[Purpose] = 2`). Un `CHECK` exige suscripción en los de membresía y la prohíbe en los anuales. | No hay suscripción de nivel 1 a la que colgarlo (ninguna en local). Siendo un `SubscriptionBillingState`, el planificador, los trabajadores y el barrido lo recogen sin cambios: un solo motor. El `CHECK` hace imposible que un estado anual mueva una membresía (el procesador usa la suscripción para renovar) y el índice, que se cree dos veces. |
| K2 | **El plan anual se marca con `RecurringBillingPlan.IsAnnualFeePlan`** (único filtrado). La migración marca «Lifestyle Ambassador (Annual)» y el seeder lo hace al crearlo. El plan marcado **nunca** crea estados por suscripción (`RecurringBillingEnrollmentService` y el barrido lo saltan). | Hoy, una suscripción de nivel 1 crearía un estado de membresía en el plan 2 y la cuota se cobraría dos veces (como membresía y como cuota). Marcar el plan es explícito; buscarlo por nombre en cada sitio, frágil. |
| K3 | **Alta de los estados por un trabajo idempotente** (`AnnualFeeStateEnrollmentJob`, diario a las 4:30 UTC, antes del planificador) por lotes de 1.000, más la creación al vuelo cuando el miembro pulsa «Pagar ahora». **No** una migración de datos ni la creación perezosa al leer. Primer vencimiento = el **primer aniversario de la inscripción que no haya pasado** al crear el estado. Se crean para todo Ambassador no borrado y no `Terminated` (también los `Pending`, los suspendidos y los que tienen la membresía en `HoldByBilling`, a los que no se cobra mientras sigan así: K5). Si el trabajo se registra en Hangfire, el primer despliegue no lo dispara: se lanza a mano (ver Tarea 8). | Una migración de datos corre en cada arranque de ocho servicios y no se puede parar ni reintentar; la creación perezosa al leer haría escribir a un `GET` y no serviría al planificador, que necesita la fila para encontrar lo que vence. El trabajo se puede lanzar a mano desde Hangfire tras el despliegue, se detiene sin daño y se repite sin duplicar (índice único). No cobrar hacia atrás evita R1. |
| K4 | **Ancla y ciclo (revisado por Nick el 2026-10-17).** El ciclo se identifica por su fecha de vencimiento (`CycleDueDate`). Al cobrar: **pago tardío** (cualquier día después del vencimiento, también un reintento de la cadencia al día siguiente) → el siguiente vence **un año desde la fecha de pago**; **pago puntual o adelantado** (el día del vencimiento o antes, dentro de la ventana de «Pagar ahora» de 30 días) → el siguiente vence **un año después del vencimiento actual**. No hay gracia que conserve el aniversario. | Decisión de Nick: quien paga tarde reancla a su fecha de pago (como el legado, `Today.AddYears(1)`). El adelantado suma al vencimiento actual para que pagar antes no haga perder días al miembro (si se reanclara al pago, quien paga 30 días antes perdería 30 días de cuota). La ventana de 30 días (K14) impide prepagar años. |
| K5 | **Exclusiones del cobro automático** (`AnnualFeeEligibility.ForAutomaticCharge`, en este orden): (1) **cancelada** = perfil inexistente o borrado, o no Ambassador, o `Terminated` → el estado pasa a `Stopped` y no se vuelve a mirar; (2) **menos de un año** = hoy antes del primer aniversario → se reprograma al aniversario; (3) **suspendida** (`Suspended`) o **pendiente de activar** (`Pending`) → se mira mañana; (3b) **membresía en `HoldByBilling`** (decisión de Nick del 2026-10-17; la membresía vigente, ver R5) → se mira mañana, sin cobrar y sin avanzar el vencimiento; (4) **cobrada hace poco** = último cobro hace menos de 11 meses (`AnnualFeeRules.RecentlyBilledMonths`) → se reprograma a un año del último cobro; (5) **`KeepQualified`** → se mira mañana, sin cobrar y **sin avanzar** el vencimiento (no cuenta como pagada: califica por la marca). `Inactive` se cobra. Saltar una exclusión no escribe `RecurringBillingAttempts` (no es un intento). | Es la lista de Nick con el orden del legado. «Terminada no reactivable» (decisiones de paridad). Sin `SuspendUntil` en este sistema, una suspensión se vuelve a mirar a diario. No avanzar con `KeepQualified` evita que, al quitar la marca, el miembro parezca pagado sin haber pagado. La exclusión por `HoldByBilling` la añadió Nick el 2026-10-17 (R5): es temporal como la suspensión, así que tampoco avanza el vencimiento. |
| K6 | **Orden del cobro automático: token → comisiones → eWallet.** El token de cuota se usa siempre que exista; comisiones y eWallet solo con la preferencia encendida. Sin nada de eso: intento fallido y cadencia del plan. | Es el orden de Nick y el del legado (que después probaba la tarjeta, aquí fuera). |
| K7 | **Semántica de la preferencia: `MemberProfile.FeesFromBalance`** («usar mis comisiones y mi eWallet para pagar mis cuotas cuando no tenga un token de cuota»), `bit` por defecto **1**. En el legado, «pagar con tokens» era convertir Daily Pay en un token de la cuota y gastarlo (`BizCenterFeeTokenDailyDeduction`): la preferencia `DailyTokenDeduction` autorizaba esa conversión para Travel Advantage, y para la cuota anual ni se miraba. Aquí la preferencia autoriza lo mismo (cobrar con saldo), sin el token intermedio, y **sí** se respeta. Apagada, solo se usan tokens de cuota que el miembro ya tenga. | Por defecto encendida porque es lo que hacía el legado con la cuota (cobraba siempre del saldo) y porque, sin tarjeta, apagarla dejaría sin pagar a casi todos. El token intermedio no aporta nada (se crearía y se gastaría en el mismo guardado) y deja un token suelto si algo falla a mitad. **Confirmado por Nick el 2026-10-17** (P3): el cobro automático usa por defecto el saldo (sin token, primero comisiones y después eWallet) y el miembro puede desactivarlo. |
| K8 | **Tokens que pagan la cuota: 23 «Annual: Biz Center» y luego 10 «Annual Fee»** (los dos de categoría Annual que otorgan el producto 5), el más antiguo primero (`AnnualFeeRules.FeeTokenTypeIds`). **No** el 24 «Legacy Biz Center Fee» (no otorga producto y en el legado era solo de cuentas «legacy», que aquí no existen) ni el 103 (token del plan que emitiría el camino de Travel Advantage). Se pueden comprar en `/tokens` (Plan F, $99) y distribuir a la línea descendente, como en el legado (`AssignedToLCID`). Una instancia distribuida a otro deja de ser de quien la compró y paga la cuota del nuevo dueño. | Los ids son de `HasData` (estables en todos los entornos). El legado gastaba los tokens propios y los asignados. |
| K9 | **Pedido de la cuota sin `OrderDetails`.** Con saldo: pedido de $99 `Paid` y su `PaymentHistory` (`CommissionBalance` o `eWallet`). Con token: pedido de $0 `Paid`, sin `PaymentHistory`, con el código del token en las notas y en el pago. El débito de comisiones usa un método nuevo, `FundAnnualFeeAsync`, que comparte núcleo con `FundTokenPurchaseAsync` (débito + `PaymentHistory`, sin emitir tokens). | Un `OrderDetail` con el producto 5 (nivel 1) haría que el pedido contara como pedido de membresía para el nivel por la orden del Plan J, sumara puntos de rango (se suman de los `OrderDetails`) y pudiera disparar comisiones. Con token no se mueve dinero en el momento: un `PaymentHistory` de $99 mentiría. `FundWithCommissionBalanceAsync` no sirve: emite un token que nadie consume (R8). |
| K10 | **Idempotencia y doble cobro en la base.** Cada cobro es una fila de `AnnualFeePayments` con dos índices únicos filtrados: `(MemberId, IdempotencyKey)` cuando hay clave («Pagar ahora») y `(SubscriptionBillingStateId, CycleDueDate)` para los pagos vivos (`Pending`, `Completed`, `NeedsReconciliation`). | Con el segundo, ni dos instancias de Billing sin candado compartido pueden dejar dos cobros vivos del mismo ciclo. El primero es el contrato de reintento de la pantalla (misma clave = mismo resultado), como `TokenPurchases`. |
| K11 | **eWallet en dos fases, como la compra de tokens:** (1) bajo el candado se reserva el pago `Pending` y el pedido `Processing`; (2) con el candado otra vez, se relee el saldo real, se debita en i-Payout, se verifica por saldo y se cierra `Completed`, `Failed` o `NeedsReconciliation`. Lo dudoso nunca se reintenta. Un barrido pasa a conciliar los `Pending` de más de 30 minutos (`Billing:AnnualFee:StalePendingMinutes`). Tesorería cierra con `POST api/v1/billing/annual-fee/payments/{id}/reconcile` (roles `SuperAdmin`, `Admin`, `BillingManager`). | Mismo patrón probado del Plan F; el candado en la fase 2 es lo que hace fiable la verificación por saldo. |
| K12 | **Fallo sin fondos:** se aplica la cadencia del plan (`1,1,1,2,2,5,5`) y, agotada, el estado queda `Stopped` (`MarkExpired`). **No** se toca la membresía ni `MemberProfile.Status`: la consecuencia es «No calificado» (después del primer año). «Pagar ahora» revive un estado parado. | El legado marcaba al agente inactivo («no calificado»); aquí eso lo calcula `QualificationService`. Tocar la membresía mezclaría la cuota con Travel Advantage (K1). |
| K13 | **Interruptor `Billing:AnnualFee:AutoChargeEnabled`**, apagado por defecto y leído en cada llamada. Apagado, el motor devuelve `Skipped` sin escribir nada; «Pagar ahora» funciona igual. | Desplegar sin cobrar (R1, R2, R5), revisar los estados y encender cuando Nick diga. Leerlo en cada llamada permite apagarlo sin reiniciar. |
| K14 | **«Pagar ahora»:** solo cuentas `Active`, sin `KeepQualified` (`ANNUAL_FEE_WAIVED`), con la membresía vigente fuera de `HoldByBilling` (`ANNUAL_FEE_MEMBERSHIP_ON_HOLD`, R5), y solo si el vencimiento cae dentro de los próximos 30 días o ya pasó (`AnnualFeeRules.PayAheadDays`; si no, `ANNUAL_FEE_NOT_DUE`). **Step-up para los tres métodos**, también el token (`member.annual-fee.pay-from-balance`), y 403 bajo suplantación en el BizCenter y en Billing. Tarjeta: `400 ANNUAL_FEE_METHOD_NOT_SUPPORTED`. | Un token es dinero ya pagado y una sesión robada podría gastarlo. La ventana evita prepagar años; un pago adelantado suma un año al vencimiento actual (K4), así que pagar antes no hace perder días. Con la membresía en `HoldByBilling` no hay cuota que cobrar (decisión de Nick), tampoco a petición del miembro. |
| K15 | **La preferencia se cambia sin step-up**, con 403 bajo suplantación. | Encenderla autoriza a pagar la cuota propia con el saldo propio: el dinero no sale hacia nadie más. Pregunta P9 por si Nick quiere step-up también aquí. |
| K16 | **«Anualidad pagada» = el estado anual tiene `NextBillingDate` posterior a hoy** (`BillingStateAnnualFeeStatus`). Se borra `AnnualFeeNotModeled` y una guarda comprueba que BizCenter y AdminAPI registran la implementación real. | Es «el estado anual al día» de Nick. Un estado `Stopped` o con el ciclo vencido sin pagar no está al día; uno con el pago a conciliar tampoco (el estado no avanzó). |
| K17 | **`MembershipAutoRenewalJob` se apaga con `RecurringJob.RemoveIfExists("membership-auto-renewal")`** (como `commission-payout`); la clase sigue registrada. | `RemoveIfExists` también lo borra de la base de Hangfire si un despliegue anterior lo dejó programado; quitar solo el `AddOrUpdate` lo dejaría vivo allí. |
| K18 | **El débito de comisiones de Travel Advantage en el motor va bajo el candado y con el saldo gastable** (`GetSpendableAsync`). Candado ocupado: se cancela el pedido, el estado no avanza y se reintenta en la siguiente pasada (`MEMBER_FUNDS_BUSY`). Si algo falla dentro, se deshace entero (también el débito que hoy `FundWithCommissionBalanceAsync` escribe antes de fallar por falta de tipo de token). | El encargo lo pide si el plan pasa por ahí, y pasa (el motor es el único que queda). R7. **Saldo gastable aprobado por Nick el 2026-10-17** (en la primera ronda se dejó el disponible a la espera de su confirmación). |
| K19 | **El motor no da por cobrada una renovación con tarjeta simulada** (`CardChargeOutcomes.IsSimulated`): cancela el pedido, no avanza el estado ni la suscripción y registra un `Critical`. | Regla de Nick: nunca se cobra con el stub. Siendo el único motor, renovaría Travel Advantage con dinero inventado. |
| K20 | **Sin bucle de `AnyAsync` para `OrderNo`** en la cuota anual. El del procesador de Travel Advantage se queda: Nick no ha decidido sobre los índices. | R11. |
| K21 | **Pantalla: `AnnualFeeCard`** en la columna derecha del perfil, bajo `MembershipCard`. En el BizCenter muestra estado, importe, vencimiento, último pago, tokens de cuota disponibles, la preferencia y «Pagar ahora» con los tres métodos y los saldos de `wallet/balances`. En el Admin y bajo suplantación, solo lectura. | Componente unificado en el sitio donde el miembro ya mira su membresía; sin página nueva ni entrada de menú. |
| K22 | **Las reglas viven en Repository** (`AnnualFeeSchedule`, `AnnualFeeEligibility`, `AnnualFeePlanQueries`, `AnnualFeePaymentQueries`, `AnnualFeeTokenQueries`): Billing las usa para cobrar y el BizCenter para enseñar si se puede pagar y por qué no. | Una sola definición; la pantalla nunca dice «puedes pagar» cuando Billing va a decir que no. |
| K23 | Columnas nuevas: `AnnualFeePayments.MemberId nvarchar(100)`, `SubscriptionBillingStateId`, `OrderId` y `PaymentHistoryId nvarchar(450)`, `CreatedBy`/`LastUpdateBy nvarchar(256)` (pueden ser un correo). `SubscriptionBillingStates.MembershipSubscriptionId` sigue en `nvarchar(100)`, ahora anulable. | Regla de ids (aclarada por Nick; R12 ya no es un riesgo). |
| K24 | **Los pagos de la cuota quedan en `RecurringBillingAttempts`** (automáticos y «Pagar ahora»), con dos valores nuevos de `RecurringFundingSource`: `EWallet = 3` y `Token = 4`. Un fallo sin fondos se registra con `CommissionBalance` si la preferencia está encendida y `Token` si no. | Soporte ve en un solo registro todo lo que pasó con un estado, también desde el listado del Admin. |

## Fuera de alcance

- **Tarjeta** en la cuota anual (automática o «Pagar ahora»): Plan I (3DS con Spreedly).
- **Avisos** de próximo vencimiento o de fallo (correo, SMS, push). El legado enviaba SMS al primer fallo (P8).
- **Pantalla de conciliación** en el Admin para `AnnualFeePayments` (hoy por API, P7).
- **Editar `KeepQualified`** y la preferencia desde el Admin.
- **Precio por miembro** (`BillingInfo.BizCenterDueAmount` del legado): aquí el importe es el del plan (`FixedAmountOverride`) o el `AnnualPrice` del producto (P6).
- **Migrar** cuotas pagadas, tokens o fechas del legado.
- **Índices de `Orders.OrderNo`, `Orders.MemberId` y `OrderDetails.OrderId`** (R11; Plan J R9 y R16).
- **Retirar o restringir `POST api/v1/billing/memberships/renew`** (R4, P4).
- **El token sin consumir de `FundWithCommissionBalanceAsync`** en Travel Advantage (R8, P5).
- **Registrar `IRecurringBillingProcessor` en AdminAPI** para que funcione el `bill-now` del Admin (R4).
- **Página de la cuota en `BizCenterApp` (MAUI)**: el componente es el mismo y se podrá montar después.
- **La matriz de paridad.**

## Preguntas abiertas para Nick

No bloquean el plan: cada una tiene un valor por defecto que el plan implementa y que se cambia con una línea o con configuración.

| # | Pregunta | Qué hace el plan mientras tanto |
|---|---|---|
| P1 | Un Ambassador antiguo al que se le crea el estado a mitad de ciclo, ¿cuenta como «anualidad pagada» hasta su próximo aniversario aunque no haya pagado en este sistema? | Sí (R6, K16): el estado está al día. La alternativa es exigir `LastSuccessfulBillingDate` no nulo en `BillingStateAnnualFeeStatus` (una condición más en la consulta). |
| P2 | ¿Se cobra la cuota a quien tiene la membresía en `HoldByBilling` (o `Expired`, `Cancelled`)? | **Resuelta (Nick, 2026-10-17): `HoldByBilling` queda excluida**; mientras esté así no se cobra (K5, K14, R5). `Expired` y `Cancelled` siguen sin ser exclusión: Nick solo nombró `HoldByBilling`. |
| P3 | ¿La preferencia «pagar mis cuotas con mi saldo» empieza encendida para todos? | **Resuelta (Nick, 2026-10-17): sí**, encendida por defecto y desactivable por el miembro (K7). |
| P4 | ¿Se retira o se limita a tesorería `POST api/v1/billing/memberships/renew`? | Se deja como está (R4). |
| P5 | `FundWithCommissionBalanceAsync` deja un token de regalo en cada renovación de Travel Advantage pagada con comisiones. ¿Es intencionado? | No se toca (R8). |
| P6 | ¿Hace falta un importe de cuota por miembro (el legado tenía `BizCenterDueAmount`)? | No: el del plan o el del producto. |
| P7 | ¿Se quiere en el Admin una pantalla para cerrar los pagos de cuota a conciliar? | No: se cierran por API (`reconcile`). |
| P8 | ¿Avisos al miembro antes del vencimiento o al fallar? | No hay avisos. |
| P9 | ¿Step-up también al cambiar la preferencia? | No (K15). |
| P10 | ¿Cuándo se enciende `Billing:AnnualFee:AutoChargeEnabled` en producción? | Apagado (K13). |

## Mapa de archivos

Rutas abreviadas: todos los proyectos llevan el prefijo `MLMConquerorGlobalEdition.` (`Billing/…` = `MLMConquerorGlobalEdition.Billing/…`). En los bloques **Files** de cada tarea van completas.

| Archivo | Responsabilidad |
|---|---|
| `Billing/Program.cs`, `Billing/Jobs/MembershipAutoRenewalJob.cs` | Apagar el segundo motor; programar los trabajos de la cuota |
| `Billing/Services/Recurring/RecurringBillingProcessor.cs` | Candado en comisiones, rechazo del éxito simulado, delegación de los estados anuales |
| `Domain/Enums/RecurringBillingEnums.cs`, `Domain/Enums/AnnualFeeEnums.cs` (nuevo) | `BillingStatePurpose`, fuentes nuevas, estados/métodos/origen del pago de cuota |
| `Domain/Entities/Billing/SubscriptionBillingState.cs`, `RecurringBillingPlan.cs`, `AnnualFeePayment.cs` (nuevo), `Domain/Entities/Member/MemberProfile.cs` | Modelo |
| `Repository/Configurations/Billing/SubscriptionBillingStateConfiguration.cs`, `RecurringBillingPlanConfiguration.cs`, `AnnualFeePaymentConfiguration.cs` (nuevo), `Repository/Configurations/MemberProfileConfiguration.cs`, `Repository/Context/AppDbContext.cs`, migración `CuotaAnual` | Esquema |
| `Billing/Services/Recurring/HighVolume/RecurringChargeWorker.cs`, `RecurringBillingEnrollmentService.cs`, `Billing/Jobs/RecurringBillingSweepJob.cs`, `AdminAPI/Controllers/AdminRecurringBillingController.cs`, `AdminWeb/Components/Pages/AdminRecurringBillingStates.razor`, `Repository/Seeders/RecurringBillingSeeder.cs` | Suscripción anulable; el plan anual no crea estados por suscripción |
| `Repository/Services/AnnualFee/AnnualFeeSchedule.cs`, `AnnualFeeEligibility.cs`, `AnnualFeePlanQueries.cs`, `AnnualFeePaymentQueries.cs`, `AnnualFeeTokenQueries.cs` (nuevos) | Reglas y consultas compartidas |
| `Billing/Services/AnnualFee/AnnualFeeStateEnrollment.cs`, `Billing/Jobs/AnnualFeeStateEnrollmentJob.cs`, `Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs` (nuevos) | Alta de estados y registro |
| `SharedKernel/Billing/AnnualFeeContracts.cs` (nuevo), `SharedKernel/Security/StepUpOperations.cs`, `SharedComponents/Components/Account/StepUp/StepUpMessages.cs` | Contratos, operación de step-up y su texto |
| `Billing/Services/Recurring/ICommissionBalanceService.cs`, `CommissionBalanceService.cs` | `FundAnnualFeeAsync` |
| `Billing/Services/AnnualFee/AnnualFeeTokenConsumer.cs`, `IAnnualFeeChargeService.cs`, `AnnualFeeChargeService.cs`, `AnnualFeeChargeService.EWallet.cs`, `AnnualFeeChargeService.PayNow.cs`, `AnnualFeeChargeService.Reconcile.cs` (nuevos) | El cobro |
| `Billing/Jobs/AnnualFeeStalePendingJob.cs`, `Billing/Controllers/AnnualFeeController.cs` (nuevos) | Barrido y API de Billing |
| `Repository/Services/Teams/IAnnualFeeStatus.cs`, `Repository/Services/AnnualFee/BillingStateAnnualFeeStatus.cs` (nuevo), `BizCenter/Program.cs`, `AdminAPI/Program.cs` | «Calificado» con la anualidad real |
| `Repository/Services/AnnualFee/AnnualFeeOverviewService.cs` (nuevo) | Resumen y preferencia (BizCenter y Admin) |
| `BizCenter/Controllers/AnnualFeeController.cs` (nuevo), `BizCenter/Services/IBillingClient.cs`, `BillingClient.cs` | Endpoints del BizCenter |
| `AdminAPI/Controllers/AdminMemberAnnualFeeController.cs` (nuevo) | Lectura en el Admin |
| `SharedComponents/Components/Profile/AnnualFeeCard.razor`, `AnnualFeeMessages.cs` (nuevos), `UserProfilePage.razor`, `SharedComponents/Resources/SharedResources*.resx` (9) | Pantalla y textos |
| `Billing.Tests/Helpers/Repositorio.cs`, `BusyGate.cs` (nuevos), `Billing.Tests/Services/AnnualFee/*` (nuevos), `Billing.Tests/Services/Recurring/*` | Pruebas de Billing |
| `BizCenter.Tests/CuotaAnual/*` (nuevos), `BizCenter.Tests/Reportes/QualificationServiceTests.cs`, `CalificadoEnLosEquiposTests.cs`, `MiembroDelTokenTests.cs` | Pruebas del BizCenter y de la pantalla |
| `AdminAPI.Tests/AdminMemberAnnualFeeControllerTests.cs` (nuevo) | Prueba del Admin |
| `docs/deployment/pruebas-de-seguridad.md` | §13 |

**Comandos que se repiten.** Para no repetirlos en cada tarea:

- **Crear una migración** (el nombre lo da la tarea):

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

# Bloque 1 — Un solo motor de renovación

### Task 1: Apagar `MembershipAutoRenewalJob`

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Helpers/Repositorio.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/UnSoloMotorDeRenovacionTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Program.cs` (bloque `RecurringJob.AddOrUpdate<MembershipAutoRenewalJob>`)
- Modify: `MLMConquerorGlobalEdition.Billing/Jobs/MembershipAutoRenewalJob.cs` (comentario de la clase)

- [ ] **Step 1: Crear el ayudante que lee archivos del repositorio**

`MLMConquerorGlobalEdition.Billing.Tests/Helpers/Repositorio.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>
/// La raíz del repositorio (la carpeta del .slnx), para que las guardas lean archivos fuente como
/// Program.cs. Sube desde el directorio de salida de las pruebas.
/// </summary>
public static class Repositorio
{
    public static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx")))
            dir = dir.Parent;

        return dir?.FullName
               ?? throw new InvalidOperationException("No se encontró MLMConquerorGlobalEdition.slnx subiendo desde las pruebas.");
    }

    public static string Leer(params string[] partes) =>
        File.ReadAllText(Path.Combine(new[] { Raiz() }.Concat(partes).ToArray()));
}
```

- [ ] **Step 2: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/UnSoloMotorDeRenovacionTests.cs`:

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (decisión de Nick): un solo motor de renovación. MembershipAutoRenewalJob renovaba por su
/// cuenta lo mismo que cobra RecurringBillingProcessor, sin candado ni idempotencia: con los dos, una
/// suscripción podía cobrarse dos veces. Se queda el procesador (planificador + barrido).
/// </summary>
public class UnSoloMotorDeRenovacionTests
{
    private static readonly string Programa = Repositorio.Leer("MLMConquerorGlobalEdition.Billing", "Program.cs");

    [Fact]
    public void MembershipAutoRenewalJob_no_se_programa()
        => Programa.Should().NotContain("AddOrUpdate<MembershipAutoRenewalJob>");

    [Fact]
    public void Se_borra_de_Hangfire_si_un_despliegue_anterior_lo_dejo_programado()
        => Programa.Should().Contain("RecurringJob.RemoveIfExists(\"membership-auto-renewal\");");

    [Fact]
    public void El_motor_que_se_queda_sigue_programado()
    {
        Programa.Should().Contain("AddOrUpdate<RecurringBillingPlanningJob>");
        Programa.Should().Contain("AddOrUpdate<RecurringBillingSweepJob>");
    }
}
```

- [ ] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter UnSoloMotorDeRenovacionTests -nologo`
Expected: FAIL en `MembershipAutoRenewalJob_no_se_programa` y `Se_borra_de_Hangfire_si_un_despliegue_anterior_lo_dejo_programado`; PASS en la tercera.

- [ ] **Step 4: Quitar la programación en `Program.cs`**

En `MLMConquerorGlobalEdition.Billing/Program.cs`, sustituir:

```csharp
RecurringJob.AddOrUpdate<MembershipAutoRenewalJob>(
    "membership-auto-renewal",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 6 * * *",                    // Daily 6:00 AM UTC
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

por:

```csharp
// Plan K (decisión de Nick): un solo motor de renovación, RecurringBillingProcessor (planificador +
// trabajadores + barrido). MembershipAutoRenewalJob renovaba por su cuenta TODA suscripción activa con
// StartDate <= hoy - 30, sin candado ni idempotencia: con los dos, doble cobro. RemoveIfExists lo borra
// también de la base de Hangfire si un despliegue anterior lo dejó programado (quitar solo el
// AddOrUpdate lo dejaría vivo allí). La clase sigue registrada, como CommissionPayoutJob.
RecurringJob.RemoveIfExists("membership-auto-renewal");
```

- [ ] **Step 5: Dejarlo dicho en la clase**

En `MLMConquerorGlobalEdition.Billing/Jobs/MembershipAutoRenewalJob.cs`, sustituir el comentario de la clase:

```csharp
/// <summary>
/// HangFire recurring job — Daily 6:00 AM UTC.
/// Renews all active subscriptions where:
///   - IsAutoRenew = true
///   - CancellationDate is null
///   - IsFree = false
///   - (StartDate + 30 days) &lt;= today (monthly renewal window elapsed)
/// </summary>
```

por:

```csharp
/// <summary>
/// APAGADO (Plan K, decisión de Nick): ya no se programa. El único motor de renovación es
/// RecurringBillingProcessor; con los dos, una suscripción podía cobrarse dos veces. Program.cs lo
/// borra de Hangfire (RemoveIfExists("membership-auto-renewal")). Se conserva registrado por si hay que
/// lanzarlo a mano en una emergencia, sabiendo que NO toma el candado de fondos ni es idempotente.
/// Lo que hacía: renovar toda suscripción activa con IsAutoRenew, sin CancellationDate, no gratuita y
/// con StartDate + 30 días &lt;= hoy.
/// </summary>
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter UnSoloMotorDeRenovacionTests -nologo`
Expected: PASS (3 pruebas).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing.Tests/Helpers/Repositorio.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/UnSoloMotorDeRenovacionTests.cs \
        MLMConquerorGlobalEdition.Billing/Program.cs \
        MLMConquerorGlobalEdition.Billing/Jobs/MembershipAutoRenewalJob.cs
git commit -m "Un solo motor de renovacion: MembershipAutoRenewalJob deja de programarse y se borra de Hangfire, porque renovaba lo mismo que RecurringBillingProcessor sin candado ni idempotencia y con los dos se cobraba dos veces"
```

---

### Task 2: El débito de comisiones del motor va bajo el candado y con el saldo gastable

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Helpers/BusyGate.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorBajoCandadoTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs` (constructor, paso 6, helpers)
- Modify: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/CorteDeFacturacionTests.cs` (constructor del procesador)

- [ ] **Step 1: Crear la puerta ocupada para pruebas**

`MLMConquerorGlobalEdition.Billing.Tests/Helpers/BusyGate.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Services.Funds;

namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>El candado de fondos siempre ocupado: otra operación del mismo miembro está en curso.</summary>
public sealed class BusyGate : IMemberFundsGate
{
    public Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default) =>
        throw new MemberFundsBusyException(memberId);

    public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default) =>
        Task.FromResult(false);
}
```

- [ ] **Step 2: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorBajoCandadoTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (K18): la renovación de Travel Advantage pagada con comisiones toma el candado de fondos del
/// miembro y mira el saldo GASTABLE (sin lo reservado por un pago ni lo aún no pagable), igual que la
/// compra de tokens. Sin candado, un pago y una renovación podían gastar las mismas comisiones.
/// </summary>
public class MotorBajoCandadoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync(decimal comision, DateTime pagableDesde)
    {
        var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, StopAfterUnbilledDays = 90,
            PayFromCommissionBalanceFirst = true, TokenTypeId = 100, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipLevels.Add(new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.Add(new Product
        {
            Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3,
            MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-1", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-30), CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = "sub-1", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-30), NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Miembro, CommissionTypeId = 1, Amount = comision, Status = CommissionEarningStatus.Pending,
            EarnedDate = pagableDesde.AddDays(-7), PaymentDate = pagableDesde, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static RecurringBillingProcessor Procesador(AppDbContext db, IMemberFundsGate candado)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        var ruta = new Mock<IGatewayRouter>();
        ruta.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayRoutingPlan>.Failure("NO_ROUTE", "Sin ruta en la prueba."));

        return new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            ruta.Object, new Mock<IGatewayChargeOrchestrator>().Object, new Mock<ICardBrandDetector>().Object,
            candado, reloj.Object, NullLogger<RecurringBillingProcessor>.Instance);
    }

    [Fact]
    public async Task Con_saldo_gastable_paga_con_comisiones_dentro_del_candado()
    {
        await using var db = await SembrarAsync(200m, Ahora.AddDays(-1));
        var candado = new TransactionOnlyGate(db);

        var r = await Procesador(db, candado).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Success");
        r.Value.FundingSource.Should().Be("CommissionBalance");
        candado.Entered.Should().Contain(Miembro);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(1);
    }

    [Fact]
    public async Task Las_comisiones_que_aun_no_son_pagables_no_pagan_la_renovacion()
    {
        await using var db = await SembrarAsync(200m, Ahora.AddDays(5));
        var candado = new TransactionOnlyGate(db);

        var r = await Procesador(db, candado).ProcessAsync("estado-1");

        // Sin saldo gastable pasa a la tarjeta, que en la prueba no tiene ruta.
        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("NO_ROUTE");
        candado.Entered.Should().Contain(Miembro);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Con_el_candado_ocupado_no_debita_ni_avanza_y_cancela_el_pedido()
    {
        await using var db = await SembrarAsync(200m, Ahora.AddDays(-1));

        var r = await Procesador(db, new BusyGate()).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(RecurringBillingProcessor.MemberFundsBusyCode);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);

        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextAttemptDate.Should().Be(Ahora.Date);
        estado.CurrentAttemptIndex.Should().Be(0);
        estado.LastSuccessfulBillingDate.Should().BeNull();
    }
}
```

- [ ] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter MotorBajoCandadoTests -nologo`
Expected: FAIL de compilación (el constructor de `RecurringBillingProcessor` no recibe `IMemberFundsGate` y no existe `MemberFundsBusyCode`).

- [ ] **Step 4: Constructor y constante**

En `RecurringBillingProcessor.cs`, añadir los `using`:

```csharp
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
```

Sustituir los campos y el constructor:

```csharp
    private readonly AppDbContext _db;
    private readonly ICommissionBalanceService _commissionBalance;
    private readonly IRecurringBillingScheduler _scheduler;
    private readonly IGatewayRouter _router;
    private readonly IGatewayChargeOrchestrator _orchestrator;
    private readonly ICardBrandDetector _brandDetector;
    private readonly IMemberFundsGate _gate;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<RecurringBillingProcessor> _logger;

    private const string Actor = "recurring-billing-engine";

    /// <summary>
    /// El candado de fondos del miembro estaba tomado (un pago, una compra de tokens, otra renovación):
    /// no se movió nada, el pedido se canceló y el estado sigue igual para la siguiente pasada.
    /// </summary>
    public const string MemberFundsBusyCode = "MEMBER_FUNDS_BUSY";

    public RecurringBillingProcessor(
        AppDbContext db,
        ICommissionBalanceService commissionBalance,
        IRecurringBillingScheduler scheduler,
        IGatewayRouter router,
        IGatewayChargeOrchestrator orchestrator,
        ICardBrandDetector brandDetector,
        IMemberFundsGate gate,
        IDateTimeProvider dateTime,
        ILogger<RecurringBillingProcessor> logger)
    {
        _db               = db;
        _commissionBalance = commissionBalance;
        _scheduler        = scheduler;
        _router           = router;
        _orchestrator     = orchestrator;
        _brandDetector    = brandDetector;
        _gate             = gate;
        _dateTime         = dateTime;
        _logger           = logger;
    }
```

- [ ] **Step 5: El paso 6 bajo el candado**

Sustituir:

```csharp
        // 6. Commission-balance first?
        if (plan.PayFromCommissionBalanceFirst)
        {
            var balance = await _commissionBalance.GetAvailableAsync(state.MemberId, ct);
            if (balance.Available >= amountDue)
            {
                return await HandleCommissionFundedAsync(
                    state, plan, planProduct, subscription, order, productId, amountDue, now, ct);
            }
        }
```

por:

```csharp
        // 6. Commission-balance first? Bajo el candado de fondos del miembro y con el saldo GASTABLE
        //    (Plan K, K18): sin candado, un pago (PayoutOrchestrator) o una compra de tokens podía gastar
        //    las mismas comisiones a la vez, y el saldo "disponible" contaba comisiones ya reservadas.
        if (plan.PayFromCommissionBalanceFirst)
        {
            var funded = await TryCommissionFundedAsync(
                state, plan, planProduct, subscription, order, productId, amountDue, now, ct);
            if (funded is not null) return funded;
        }
```

- [ ] **Step 6: Los dos helpers nuevos**

Añadir, justo antes de `// ── Commission-balance path ───`:

```csharp
    /// <summary>
    /// Paga con comisiones bajo el candado del miembro. Null = el saldo gastable no alcanza y se pasa a
    /// la tarjeta. Ocupado = se cancela el pedido y no se toca el estado. Si algo falla dentro, se deshace
    /// entero (también el débito, que FundWithCommissionBalanceAsync escribe antes de comprobar el tipo
    /// de token) y el pedido se cancela con el contexto ya limpio.
    /// </summary>
    private async Task<Result<RecurringBillingProcessorResult>?> TryCommissionFundedAsync(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        RecurringBillingPlanProduct? planProduct,
        MembershipSubscription subscription,
        Orders order,
        string productId,
        decimal amountDue,
        DateTime now,
        CancellationToken ct)
    {
        IDbContextTransaction tx;
        try
        {
            tx = await _gate.EnterAsync(state.MemberId, ct);
        }
        catch (MemberFundsBusyException)
        {
            await CancelOrderAsync(order, now, ct);
            return Result<RecurringBillingProcessorResult>.Failure(MemberFundsBusyCode,
                $"Funds of member {state.MemberId} are busy; the renewal will be retried on the next pass.");
        }

        await using (tx)
        {
            var spendable = await _commissionBalance.GetSpendableAsync(state.MemberId, ct);
            if (spendable < amountDue)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return null;
            }

            try
            {
                var result = await HandleCommissionFundedAsync(
                    state, plan, planProduct, subscription, order, productId, amountDue, now, ct);

                if (result.IsSuccess)
                {
                    await tx.CommitAsync(ct);
                    return result;
                }

                await tx.RollbackAsync(CancellationToken.None);
                await CancelOrderAfterRollbackAsync(order.Id, now);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                await CancelOrderAfterRollbackAsync(order.Id, now);
                throw;
            }
        }
    }

    /// <summary>
    /// Tras deshacer, lo que el contexto creía guardado ya no lo está: se olvida y se relee el pedido
    /// (creado y guardado antes del candado) para cancelarlo de verdad.
    /// </summary>
    private async Task CancelOrderAfterRollbackAsync(string orderId, DateTime now)
    {
        _db.ChangeTracker.Clear();
        var fresh = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, CancellationToken.None);
        if (fresh is null || fresh.Status != OrderStatus.Processing) return;

        fresh.Status         = OrderStatus.Cancelled;
        fresh.LastUpdateDate = now;
        fresh.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(CancellationToken.None);
    }
```

- [ ] **Step 7: Actualizar el constructor en `CorteDeFacturacionTests`**

En `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/CorteDeFacturacionTests.cs`, sustituir:

```csharp
        return new RecurringBillingProcessor(
            db,
            new Mock<ICommissionBalanceService>().Object,
            new Mock<IRecurringBillingScheduler>().Object,
            new Mock<IGatewayRouter>().Object,
            new Mock<IGatewayChargeOrchestrator>().Object,
            new Mock<ICardBrandDetector>().Object,
            reloj.Object,
            NullLogger<RecurringBillingProcessor>.Instance);
```

por:

```csharp
        return new RecurringBillingProcessor(
            db,
            new Mock<ICommissionBalanceService>().Object,
            new Mock<IRecurringBillingScheduler>().Object,
            new Mock<IGatewayRouter>().Object,
            new Mock<IGatewayChargeOrchestrator>().Object,
            new Mock<ICardBrandDetector>().Object,
            new TransactionOnlyGate(db),
            reloj.Object,
            NullLogger<RecurringBillingProcessor>.Instance);
```

- [ ] **Step 8: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "MotorBajoCandadoTests|CorteDeFacturacionTests" -nologo`
Expected: PASS (5 pruebas). `IMemberFundsGate` ya está registrado en Billing por `AddPayoutServices` (`TryAddScoped`), así que el contenedor construye el procesador sin tocar `Program.cs`.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing.Tests/Helpers/BusyGate.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorBajoCandadoTests.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/CorteDeFacturacionTests.cs \
        MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs
git commit -m "La renovacion pagada con comisiones toma el candado de fondos del miembro y mira el saldo gastable, como la compra de tokens; con el candado ocupado cancela el pedido sin avanzar el estado y si algo falla dentro se deshace entero"
```

---

### Task 3: El motor no da por cobrada una renovación con tarjeta simulada

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/RenovacionSimuladaTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs` (`HandleCardChargeAsync`, antes de `// Immediate success`)

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/RenovacionSimuladaTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Regla de Nick: nunca se cobra con el stub. SpreedlyCardGatewayService devuelve un "éxito" con un id
/// "simulated-…". Siendo RecurringBillingProcessor el único motor (Plan K), daría la renovación por
/// pagada con dinero inventado. No se renueva, no avanza el estado y el pedido se cancela.
/// </summary>
public class RenovacionSimuladaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private static readonly DateTime InicioAnterior = Ahora.AddDays(-30);

    private static async Task<AppDbContext> SembrarAsync()
    {
        var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, StopAfterUnbilledDays = 90,
            PayFromCommissionBalanceFirst = false, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipLevels.Add(new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.Add(new Product
        {
            Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3,
            MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-1", MemberId = "AMB-000001", MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = InicioAnterior, CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = "sub-1", MemberId = "AMB-000001", RecurringBillingPlanId = 1,
            BillingAnchorDate = InicioAnterior, NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static RecurringBillingProcessor Procesador(AppDbContext db, string idDeTransaccion)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);

        var plan = new GatewayRoutingPlan { RouteBucketKey = "prueba", Steps = new List<GatewayAttemptPlan>() };
        var ruta = new Mock<IGatewayRouter>();
        ruta.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayRoutingPlan>.Success(plan));

        var orquestador = new Mock<IGatewayChargeOrchestrator>();
        orquestador.Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(),
                It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
            {
                Status = "Success", GatewayTransactionId = idDeTransaccion, PaymentHistoryId = "ph-1", AmountCharged = 119.97m
            }));

        return new RecurringBillingProcessor(
            db, new Mock<ICommissionBalanceService>().Object, new RecurringBillingScheduler(),
            ruta.Object, orquestador.Object, new Mock<ICardBrandDetector>().Object,
            new TransactionOnlyGate(db), reloj.Object, NullLogger<RecurringBillingProcessor>.Instance);
    }

    [Theory]
    [InlineData("simulated-7f3a")]
    [InlineData("STUB-NmiSpreedly-1")]
    public async Task Un_exito_simulado_no_renueva_ni_avanza_el_estado(string idSimulado)
    {
        await using var db = await SembrarAsync();

        var r = await Procesador(db, idSimulado).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(TokenPurchaseErrors.CardSimulatedCharge);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await db.RecurringBillingAttempts.CountAsync(a => a.Outcome == RecurringAttemptOutcome.Success)).Should().Be(0);

        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.LastSuccessfulBillingDate.Should().BeNull();
        estado.NextAttemptDate.Should().Be(Ahora.Date);
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).StartDate.Should().Be(InicioAnterior);
    }

    [Fact]
    public async Task Un_cobro_real_sigue_renovando()
    {
        await using var db = await SembrarAsync();

        var r = await Procesador(db, "ch_3Pq9real").ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Success");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter RenovacionSimuladaTests -nologo`
Expected: FAIL en los dos casos de `Un_exito_simulado_no_renueva_ni_avanza_el_estado` (hoy devuelve `Success`); PASS en `Un_cobro_real_sigue_renovando`. `CardChargeOutcomes.IsSimulated` reconoce los prefijos `simulated` (Spreedly) y `STUB-` (`StubGatewayBase`).

- [ ] **Step 3: Rechazar el éxito simulado**

En `RecurringBillingProcessor.cs`, añadir los `using`:

```csharp
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
```

En `HandleCardChargeAsync`, justo antes de la línea `// Immediate success`, añadir:

```csharp
        // Plan K (regla de Nick: nunca se cobra con el stub). El gateway de Spreedly todavía devuelve un
        // "éxito" simulado. Eso no es dinero: ni se renueva ni avanza el estado, para que el día que el
        // cobro sea real se intente de verdad. El pedido se cancela. El PaymentHistory que el orquestador
        // ya escribió queda (es suyo): §13 dice cómo encontrarlo.
        if (CardChargeOutcomes.IsSimulated(outcome.GatewayTransactionId))
        {
            _logger.LogCritical(
                "Renovación {StateId} de {MemberId}: la tarjeta devolvió un cobro simulado ({TxId}). No se renueva; " +
                "el cobro con tarjeta del motor no puede darse por bueno mientras el gateway sea un stub.",
                state.Id, state.MemberId, outcome.GatewayTransactionId);
            await CancelOrderAsync(order, now, ct);
            return Result<RecurringBillingProcessorResult>.Failure(TokenPurchaseErrors.CardSimulatedCharge,
                "The card gateway returned a simulated charge; the renewal was not applied.");
        }

```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "RenovacionSimuladaTests|MotorBajoCandadoTests|CorteDeFacturacionTests" -nologo`
Expected: PASS (8 pruebas).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/RenovacionSimuladaTests.cs \
        MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs
git commit -m "El motor de renovacion no da por cobrada una renovacion con un exito simulado del gateway de tarjeta: cancela el pedido, no avanza el estado ni la suscripcion y lo registra como critico"
```

---

# Bloque 2 — Modelo y alta de estados

### Task 4: Modelo: estado anual, plan marcado, pago de cuota y preferencia

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Enums/AnnualFeeEnums.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Billing/AnnualFeePayment.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/Billing/AnnualFeePaymentConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/ModeloDeCuotaAnualTests.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Enums/RecurringBillingEnums.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Billing/SubscriptionBillingState.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Billing/RecurringBillingPlan.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/Billing/SubscriptionBillingStateConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/Billing/RecurringBillingPlanConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/ModeloDeCuotaAnualTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// El esquema de la cuota anual (Plan K, K1, K2, K10, K23), mirado en el modelo de diseño de SQL Server
/// sin conectarse. Lo que de verdad hace cumplir estas reglas (índices filtrados y CHECK) solo lo aplica
/// SQL Server: SqlServerCuotaAnualTests lo comprueba contra la base.
/// </summary>
public class ModeloDeCuotaAnualTests
{
    private static readonly IModel Modelo = CrearModelo();

    private static IModel CrearModelo()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=modelo-sin-conexion;Integrated Security=true;TrustServerCertificate=True")
            .Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidad<T>() => Modelo.FindEntityType(typeof(T))!;

    private static IIndex Indice<T>(params string[] propiedades) =>
        Entidad<T>().GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(propiedades)
                                              && (i.GetDatabaseName() ?? "").StartsWith("IX_"));

    [Fact]
    public void El_estado_de_cobro_admite_estados_sin_suscripcion()
    {
        var p = Entidad<SubscriptionBillingState>().FindProperty(nameof(SubscriptionBillingState.MembershipSubscriptionId))!;
        p.IsNullable.Should().BeTrue();
        p.GetMaxLength().Should().Be(100);
    }

    [Fact]
    public void La_suscripcion_sigue_siendo_unica_entre_los_estados_que_la_tienen()
    {
        var i = Indice<SubscriptionBillingState>(nameof(SubscriptionBillingState.MembershipSubscriptionId));
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[MembershipSubscriptionId] IS NOT NULL");
    }

    [Fact]
    public void Un_miembro_tiene_como_mucho_un_estado_de_cuota_anual()
    {
        var i = Entidad<SubscriptionBillingState>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_SubscriptionBillingStates_AnnualFeeMember");
        i.IsUnique.Should().BeTrue();
        i.Properties.Select(p => p.Name).Should().Equal(nameof(SubscriptionBillingState.MemberId));
        i.GetFilter().Should().Be("[Purpose] = 2");
    }

    [Fact]
    public void El_CHECK_ata_el_proposito_a_tener_o_no_suscripcion()
    {
        var ck = Entidad<SubscriptionBillingState>().GetCheckConstraints()
            .Single(c => c.ModelName == "CK_SubscriptionBillingStates_Purpose");
        ck.Sql.Should().Contain("[Purpose] = 1 AND [MembershipSubscriptionId] IS NOT NULL")
              .And.Contain("[Purpose] = 2 AND [MembershipSubscriptionId] IS NULL");
    }

    [Fact]
    public void El_proposito_por_defecto_es_la_membresia()
        => new SubscriptionBillingState().Purpose.Should().Be(BillingStatePurpose.Membership);

    [Fact]
    public void Solo_un_plan_puede_ser_el_de_la_cuota_anual()
    {
        var i = Entidad<RecurringBillingPlan>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_RecurringBillingPlans_AnnualFee");
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[IsAnnualFeePlan] = 1");
    }

    [Fact]
    public void La_preferencia_de_pagar_con_saldo_empieza_encendida()
    {
        new MemberProfile().FeesFromBalance.Should().BeTrue();
        Entidad<MemberProfile>().FindProperty(nameof(MemberProfile.FeesFromBalance))!.GetDefaultValue().Should().Be(true);
    }

    [Fact]
    public void Un_ciclo_no_puede_tener_dos_pagos_vivos()
    {
        var i = Entidad<AnnualFeePayment>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_AnnualFeePayments_LivePerCycle");
        i.IsUnique.Should().BeTrue();
        i.Properties.Select(p => p.Name).Should().Equal(
            nameof(AnnualFeePayment.SubscriptionBillingStateId), nameof(AnnualFeePayment.CycleDueDate));
        i.GetFilter().Should().Be("[Status] IN (1, 2, 4)");
    }

    [Fact]
    public void La_clave_de_idempotencia_es_unica_por_miembro()
    {
        var i = Entidad<AnnualFeePayment>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_AnnualFeePayments_Member_IdempotencyKey");
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[IdempotencyKey] IS NOT NULL");
    }

    [Fact]
    public void Los_ids_de_miembro_miden_al_menos_100()
    {
        Entidad<AnnualFeePayment>().FindProperty(nameof(AnnualFeePayment.MemberId))!.GetMaxLength()
            .Should().BeGreaterThanOrEqualTo(100);
        Entidad<SubscriptionBillingState>().FindProperty(nameof(SubscriptionBillingState.MemberId))!.GetMaxLength()
            .Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void Los_estados_vivos_del_filtro_coinciden_con_el_enumerado()
    {
        ((int)AnnualFeePaymentStatus.Pending).Should().Be(1);
        ((int)AnnualFeePaymentStatus.Completed).Should().Be(2);
        ((int)AnnualFeePaymentStatus.Failed).Should().Be(3);
        ((int)AnnualFeePaymentStatus.NeedsReconciliation).Should().Be(4);
    }

    // ── Transiciones del pago ──────────────────────────────────────────────────

    private static readonly DateTime T = new(2027, 6, 10, 9, 0, 0);

    [Fact]
    public void Un_pago_pendiente_se_completa_y_queda_vivo()
    {
        var p = new AnnualFeePayment();
        p.Complete("ph-1", T, "prueba");
        p.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        p.PaymentHistoryId.Should().Be("ph-1");
        p.CompletedAt.Should().Be(T);
        p.IsLive.Should().BeTrue();
    }

    [Fact]
    public void Con_token_guarda_el_codigo_y_la_instancia()
    {
        var p = new AnnualFeePayment();
        p.CompleteWithToken(42, "ABCD-1234", T, "prueba");
        p.TokenTransactionId.Should().Be(42);
        p.TokenCode.Should().Be("ABCD-1234");
        p.Status.Should().Be(AnnualFeePaymentStatus.Completed);
    }

    [Fact]
    public void Un_pago_fallido_deja_de_estar_vivo_y_no_se_completa()
    {
        var p = new AnnualFeePayment();
        p.Fail("EWALLET_DEBIT_REJECTED", T, "prueba");
        p.IsLive.Should().BeFalse();
        var act = () => p.Complete("ph-1", T, "prueba");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Solo_un_pendiente_pasa_a_conciliar_y_un_conciliado_se_puede_cerrar()
    {
        var p = new AnnualFeePayment();
        p.MarkForReconciliation("EWALLET_OUTCOME_UNKNOWN", new string('x', 900), T, "prueba");
        p.Status.Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
        p.Notes!.Length.Should().Be(AnnualFeePayment.NotesMaxLength);
        var otraVez = () => p.MarkForReconciliation("X", null, T, "prueba");
        otraVez.Should().Throw<InvalidOperationException>();
        p.Complete("ph-2", T, "tesoreria");
        p.Status.Should().Be(AnnualFeePaymentStatus.Completed);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter ModeloDeCuotaAnualTests -nologo`
Expected: FAIL de compilación (`AnnualFeePayment`, `BillingStatePurpose`, `FeesFromBalance` no existen).

- [ ] **Step 3: Enumerados**

En `MLMConquerorGlobalEdition.Domain/Enums/RecurringBillingEnums.cs`, sustituir el enumerado `RecurringFundingSource`:

```csharp
/// <summary>Which funding source was used for a billing attempt.</summary>
public enum RecurringFundingSource
{
    CommissionBalance = 1,
    CreditCard = 2
}
```

por:

```csharp
/// <summary>Which funding source was used for a billing attempt.</summary>
public enum RecurringFundingSource
{
    CommissionBalance = 1,
    CreditCard = 2,

    /// <summary>Saldo real de i-Payout (cuota anual, Plan K).</summary>
    EWallet = 3,

    /// <summary>Un token de cuota que el miembro ya tenía (cuota anual, Plan K). No mueve dinero.</summary>
    Token = 4
}

/// <summary>
/// Para qué es un SubscriptionBillingState (Plan K, K1). Un estado de membresía cobra una suscripción y
/// la renueva; uno de cuota anual cobra la cuota del Biz Center del miembro y NO tiene suscripción
/// (CK_SubscriptionBillingStates_Purpose lo obliga en la base).
/// </summary>
public enum BillingStatePurpose
{
    Membership = 1,
    AnnualFee  = 2
}
```

`MLMConquerorGlobalEdition.Domain/Enums/AnnualFeeEnums.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// Estado de un pago de cuota anual. Los valores están en el filtro de UX_AnnualFeePayments_LivePerCycle
/// ("[Status] IN (1, 2, 4)"): no se renumeran.
/// </summary>
public enum AnnualFeePaymentStatus
{
    Pending             = 1,
    Completed           = 2,
    Failed              = 3,
    NeedsReconciliation = 4
}

/// <summary>Con qué se pagó la cuota. Sin tarjeta hasta el Plan I.</summary>
public enum AnnualFeePaymentMethod
{
    Token             = 1,
    CommissionBalance = 2,
    EWallet           = 3
}

/// <summary>Quién lo inició: el motor (aniversario) o el miembro ("Pagar ahora").</summary>
public enum AnnualFeePaymentSource
{
    Automatic = 1,
    PayNow    = 2
}
```

- [ ] **Step 4: Entidades**

En `MLMConquerorGlobalEdition.Domain/Entities/Billing/SubscriptionBillingState.cs`, sustituir:

```csharp
    public string MembershipSubscriptionId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
```

por:

```csharp
    /// <summary>
    /// La suscripción que cobra un estado de membresía. Null en un estado de cuota anual (Plan K): la
    /// cuota es del miembro, no de una suscripción. CK_SubscriptionBillingStates_Purpose lo exige.
    /// </summary>
    public string? MembershipSubscriptionId { get; set; }

    /// <summary>Membresía (por defecto) o cuota anual. Ver <see cref="BillingStatePurpose"/>.</summary>
    public BillingStatePurpose Purpose { get; set; } = BillingStatePurpose.Membership;

    public string MemberId { get; set; } = string.Empty;
```

En `MLMConquerorGlobalEdition.Domain/Entities/Billing/RecurringBillingPlan.cs`, sustituir:

```csharp
    public bool IsActive { get; set; } = true;
```

por:

```csharp
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// El plan de la cuota anual del Biz Center (Plan K, K2). Como mucho uno
    /// (UX_RecurringBillingPlans_AnnualFee). Sus estados son por miembro (Purpose = AnnualFee) y nunca
    /// se crean desde una suscripción: RecurringBillingEnrollmentService y el barrido lo saltan.
    /// </summary>
    public bool IsAnnualFeePlan { get; set; }
```

En `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs`, justo después de la propiedad `KeepQualified`, añadir:

```csharp

    /// <summary>
    /// Preferencia de pagar las cuotas con el saldo (Plan K, K7). Encendida: si no hay un token de cuota,
    /// el cobro automático usa las comisiones y después la eWallet. Apagada: solo tokens de cuota que el
    /// miembro ya tenga. Equivale a la DailyTokenDeduction del legado ("usar comisiones para pagar"), que
    /// en la cuota anual el legado ni consultaba (cobraba siempre del saldo); aquí sí se respeta.
    /// </summary>
    public bool FeesFromBalance { get; set; } = true;
```

`MLMConquerorGlobalEdition.Domain/Entities/Billing/AnnualFeePayment.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Un cobro de la cuota anual (Plan K, K10). Es a la vez la IDEMPOTENCIA de "Pagar ahora" ((MemberId,
/// IdempotencyKey) único) y la garantía contra el doble cobro: (SubscriptionBillingStateId,
/// CycleDueDate) es único entre los pagos vivos (Pending, Completed, NeedsReconciliation). Su Id es
/// también la referencia del débito en i-Payout.
/// </summary>
public class AnnualFeePayment : AuditChangesStringKey
{
    public const int FailureCodeMaxLength = 64;
    public const int NotesMaxLength       = 500;
    public const int TokenCodeMaxLength   = 64;

    private string? _failureCode;
    private string? _notes;

    public string MemberId { get; set; } = string.Empty;
    public string SubscriptionBillingStateId { get; set; } = string.Empty;

    /// <summary>El vencimiento del ciclo que paga (fecha, sin hora). Identifica el ciclo.</summary>
    public DateTime CycleDueDate { get; set; }

    /// <summary>Lo cobrado: el precio de la cuota con saldo; 0 con token (el dinero se movió al comprarlo).</summary>
    public decimal Amount { get; set; }

    public AnnualFeePaymentMethod Method { get; set; }
    public AnnualFeePaymentSource Source { get; set; }
    public AnnualFeePaymentStatus Status { get; set; } = AnnualFeePaymentStatus.Pending;
    public string OrderId { get; set; } = string.Empty;
    public string? PaymentHistoryId { get; set; }

    /// <summary>La instancia de token (con código) que se gastó, si se pagó con token.</summary>
    public long? TokenTransactionId { get; set; }
    public string? TokenCode { get; set; }

    /// <summary>La clave de la pantalla en "Pagar ahora". Null en el cobro automático.</summary>
    public string? IdempotencyKey { get; set; }

    public string? FailureCode
    {
        get => _failureCode;
        set => _failureCode = Clip(value, FailureCodeMaxLength);
    }

    public string? Notes
    {
        get => _notes;
        set => _notes = Clip(value, NotesMaxLength);
    }

    public DateTime? CompletedAt { get; set; }

    /// <summary>¿Ocupa su ciclo? No se mapea.</summary>
    public bool IsLive => Status is AnnualFeePaymentStatus.Pending
                                 or AnnualFeePaymentStatus.Completed
                                 or AnnualFeePaymentStatus.NeedsReconciliation;

    public void Complete(string? paymentHistoryId, DateTime now, string actor)
    {
        EnsureOpen("completar");
        Status           = AnnualFeePaymentStatus.Completed;
        PaymentHistoryId = paymentHistoryId;
        CompletedAt      = now;
        Touch(now, actor);
    }

    public void CompleteWithToken(long tokenTransactionId, string tokenCode, DateTime now, string actor)
    {
        EnsureOpen("completar con token");
        Status             = AnnualFeePaymentStatus.Completed;
        TokenTransactionId = tokenTransactionId;
        TokenCode          = Clip(tokenCode, TokenCodeMaxLength);
        CompletedAt        = now;
        Touch(now, actor);
    }

    /// <summary>No entró: un rechazo del eWallet, o tesorería lo da por no cobrado.</summary>
    public void Fail(string failureCode, DateTime now, string actor)
    {
        EnsureOpen("marcar fallido");
        Status      = AnnualFeePaymentStatus.Failed;
        FailureCode = failureCode;
        Touch(now, actor);
    }

    /// <summary>Resultado dudoso: ni avanza el ciclo ni se reintenta; lo cierra tesorería.</summary>
    public void MarkForReconciliation(string reasonCode, string? detail, DateTime now, string actor)
    {
        if (Status != AnnualFeePaymentStatus.Pending)
            throw new InvalidOperationException($"El pago {Id} está en {Status} y no puede pasar a conciliación.");

        Status      = AnnualFeePaymentStatus.NeedsReconciliation;
        FailureCode = reasonCode;
        Notes       = detail;
        Touch(now, actor);
    }

    private void EnsureOpen(string what)
    {
        if (Status is not (AnnualFeePaymentStatus.Pending or AnnualFeePaymentStatus.NeedsReconciliation))
            throw new InvalidOperationException($"El pago {Id} está en {Status} y no se puede {what}.");
    }

    private void Touch(DateTime now, string actor)
    {
        LastUpdateDate = now;
        LastUpdateBy   = actor;
    }

    private static string? Clip(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
```

- [ ] **Step 5: Configuración**

En `MLMConquerorGlobalEdition.Repository/Configurations/Billing/SubscriptionBillingStateConfiguration.cs`, añadir el `using MLMConquerorGlobalEdition.Domain.Enums;` y sustituir:

```csharp
        builder.Property(x => x.MembershipSubscriptionId).IsRequired().HasMaxLength(100);
```

por:

```csharp
        // Plan K (K1): anulable. Los estados de cuota anual no tienen suscripción.
        builder.Property(x => x.MembershipSubscriptionId).HasMaxLength(100);
        builder.Property(x => x.Purpose).HasConversion<int>().HasDefaultValue(BillingStatePurpose.Membership);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SubscriptionBillingStates_Purpose",
            "([Purpose] = 1 AND [MembershipSubscriptionId] IS NOT NULL) OR ([Purpose] = 2 AND [MembershipSubscriptionId] IS NULL)"));
```

y sustituir:

```csharp
        builder.HasIndex(x => x.MembershipSubscriptionId).IsUnique();
```

por:

```csharp
        builder.HasIndex(x => x.MembershipSubscriptionId).IsUnique().HasFilter("[MembershipSubscriptionId] IS NOT NULL");

        // Plan K (K1): un solo estado de cuota anual por miembro. Incluye los borrados lógicos: quien da
        // de alta estados los busca con IgnoreQueryFilters.
        builder.HasIndex(x => x.MemberId, "UX_SubscriptionBillingStates_AnnualFeeMember")
               .IsUnique()
               .HasFilter("[Purpose] = 2");
```

En `MLMConquerorGlobalEdition.Repository/Configurations/Billing/RecurringBillingPlanConfiguration.cs`, sustituir:

```csharp
        builder.HasIndex(x => x.IsActive);
```

por:

```csharp
        builder.HasIndex(x => x.IsActive);

        // Plan K (K2): como mucho un plan de cuota anual.
        builder.Property(x => x.IsAnnualFeePlan).IsRequired().HasDefaultValue(false);
        builder.HasIndex(x => x.IsAnnualFeePlan, "UX_RecurringBillingPlans_AnnualFee")
               .IsUnique()
               .HasFilter("[IsAnnualFeePlan] = 1");
```

En `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs`, sustituir:

```csharp
        builder.Property(x => x.KeepQualified).IsRequired().HasDefaultValue(false);
```

por:

```csharp
        builder.Property(x => x.KeepQualified).IsRequired().HasDefaultValue(false);

        // Plan K (K7): encendida por defecto, también para las filas que ya existen. HasSentinel(true):
        // EF solo omite la columna al insertar cuando vale true (y entonces la base pone su 1); un false
        // se manda siempre. Con el centinela por defecto (false) nunca se podría insertar un false.
        builder.Property(x => x.FeesFromBalance).IsRequired().HasDefaultValue(true).HasSentinel(true);
```

`MLMConquerorGlobalEdition.Repository/Configurations/Billing/AnnualFeePaymentConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class AnnualFeePaymentConfiguration : IEntityTypeConfiguration<AnnualFeePayment>
{
    public void Configure(EntityTypeBuilder<AnnualFeePayment> builder)
    {
        builder.ToTable("AnnualFeePayments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        // Id de miembro sin FK a MemberProfiles: 100 (como SubscriptionBillingStates). Ids de entidades: 450.
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SubscriptionBillingStateId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.PaymentHistoryId).HasMaxLength(450);

        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Method).HasConversion<int>();
        builder.Property(x => x.Source).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.TokenCode).HasMaxLength(AnnualFeePayment.TokenCodeMaxLength);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(TokenPurchaseRules.IdempotencyKeyMaxLength);
        builder.Property(x => x.FailureCode).HasMaxLength(AnnualFeePayment.FailureCodeMaxLength);
        builder.Property(x => x.Notes).HasMaxLength(AnnualFeePayment.NotesMaxLength);
        // 256, lo que mide un correo: el actor puede ser un email y no solo un id.
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(256);
        builder.Ignore(x => x.IsLive);

        // La idempotencia y el "un cobro vivo por ciclo" viven en la base, no solo en el código (K10).
        builder.HasIndex(x => new { x.MemberId, x.IdempotencyKey }, "UX_AnnualFeePayments_Member_IdempotencyKey")
               .IsUnique()
               .HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(x => new { x.SubscriptionBillingStateId, x.CycleDueDate }, "UX_AnnualFeePayments_LivePerCycle")
               .IsUnique()
               .HasFilter("[Status] IN (1, 2, 4)");
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreationDate });
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });

        builder.HasOne<SubscriptionBillingState>()
               .WithMany()
               .HasForeignKey(x => x.SubscriptionBillingStateId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

En `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs`, sustituir:

```csharp
    public DbSet<RecurringBillingAttempt>     RecurringBillingAttempts     => Set<RecurringBillingAttempt>();
```

por:

```csharp
    public DbSet<RecurringBillingAttempt>     RecurringBillingAttempts     => Set<RecurringBillingAttempt>();
    public DbSet<AnnualFeePayment>            AnnualFeePayments            => Set<AnnualFeePayment>();
```

(`MLMConquerorGlobalEdition.Domain.Entities.Billing` ya está entre los `using` de `AppDbContext.cs`, porque declara `RecurringBillingAttempt`.)

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter ModeloDeCuotaAnualTests -nologo`
Expected: PASS (16 pruebas).

Run: `dotnet build MLMConquerorGlobalEdition.slnx -nologo`
Expected: compila. Salen advertencias de nulabilidad (CS8604) en `RecurringChargeWorker.cs`, donde se pasa `MembershipSubscriptionId` (ahora `string?`) a `EmitActivatedEventsAsync` y `EmitDeactivatedEventIfNeededAsync`; ningún proyecto trata las advertencias como errores. La Tarea 6 las quita (y hace que esos métodos no emitan nada sin suscripción).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Enums/RecurringBillingEnums.cs \
        MLMConquerorGlobalEdition.Domain/Enums/AnnualFeeEnums.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Billing/SubscriptionBillingState.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Billing/RecurringBillingPlan.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Billing/AnnualFeePayment.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/Billing/SubscriptionBillingStateConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/Billing/RecurringBillingPlanConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/Billing/AnnualFeePaymentConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/ModeloDeCuotaAnualTests.cs
git commit -m "Modelo de la cuota anual: estado de cobro con proposito y sin suscripcion atado por CHECK, uno por miembro, plan marcado como el de la cuota, pago de cuota con indices unicos de idempotencia y de un cobro vivo por ciclo, y preferencia de pagar las cuotas con el saldo encendida por defecto"
```

---

### Task 5: Migración `CuotaAnual` con guardas

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/<marca>_CuotaAnual.cs` (y su `.Designer.cs`, generados)
- Modify: `MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs` (generado)

- [ ] **Step 1: Generar la migración**

Run (ver «Comandos que se repiten»): `dotnet ef migrations add CuotaAnual --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.SignupAPI`
Expected: «Done.» y tres archivos tocados.

- [ ] **Step 2: Revisar lo generado**

`Up` debe tener **solo** estas operaciones (en el orden que EF elija): `DropIndex IX_SubscriptionBillingStates_MembershipSubscriptionId`; `AlterColumn MembershipSubscriptionId` a `nullable: true`; `AddColumn Purpose` (`int`, `defaultValue: 1`); `AddColumn IsAnnualFeePlan` en `RecurringBillingPlans` (`bit`, `defaultValue: false`); `AddColumn FeesFromBalance` en `MemberProfiles` (`bit`, `defaultValue: true`); `CreateTable AnnualFeePayments` con su clave foránea a `SubscriptionBillingStates`; los cinco índices de `AnnualFeePayments`; `CreateIndex IX_SubscriptionBillingStates_MembershipSubscriptionId` con `filter: "[MembershipSubscriptionId] IS NOT NULL"`; `CreateIndex UX_SubscriptionBillingStates_AnnualFeeMember`; `CreateIndex UX_RecurringBillingPlans_AnnualFee`; `AddCheckConstraint CK_SubscriptionBillingStates_Purpose`. Cualquier otra operación sobre tablas ajenas: parar y avisar. Bloques `UpdateData` de semillas que no cambian nada: borrarlos de `Up` y de `Down`.

- [ ] **Step 3: Escribir `Up` y `Down` como deben quedar**

Sustituir el cuerpo de la clase por este (las columnas de auditoría heredadas, `CreationDate`…`RowVersion`, deben salir iguales que en la generada; si difieren en un tipo, vale el generado):

```csharp
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan K. Guardas antes de tocar nada: mejor fallar el despliegue que dejar datos que
            // contradicen el modelo. (1) El índice único filtrado de IsAnnualFeePlan fallaría con un
            // mensaje oscuro si hubiera dos planes con ese nombre. (2) Un estado por suscripción en el
            // plan anual quedaría como estado de membresía en el plan de la cuota: la cuota se cobraría
            // dos veces (K2). En local no hay ninguno de los dos casos.
            migrationBuilder.Sql(@"
IF (SELECT COUNT(*) FROM [RecurringBillingPlans] WHERE [Name] = N'Lifestyle Ambassador (Annual)') > 1
    THROW 51010, N'Hay mas de un plan Lifestyle Ambassador (Annual): dejar uno antes de migrar (Plan K, Tarea 5).', 1;
IF EXISTS (SELECT 1 FROM [SubscriptionBillingStates] s
           JOIN [RecurringBillingPlans] p ON p.[Id] = s.[RecurringBillingPlanId]
           WHERE p.[Name] = N'Lifestyle Ambassador (Annual)')
    THROW 51011, N'Hay estados de facturacion por suscripcion en el plan anual: revisarlos antes de migrar (Plan K, Tarea 5).', 1;");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionBillingStates_MembershipSubscriptionId",
                table: "SubscriptionBillingStates");

            // Se amplía la nulabilidad, no la longitud: nada se trunca.
            migrationBuilder.AlterColumn<string>(
                name: "MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "SubscriptionBillingStates",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnnualFeePlan",
                table: "RecurringBillingPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // El plan que ya existe es el de la cuota (K2). Es nuestro catálogo, no un dato del legado.
            migrationBuilder.Sql(
                "UPDATE [RecurringBillingPlans] SET [IsAnnualFeePlan] = 1 WHERE [Name] = N'Lifestyle Ambassador (Annual)';");

            migrationBuilder.AddColumn<bool>(
                name: "FeesFromBalance",
                table: "MemberProfiles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AnnualFeePayments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubscriptionBillingStateId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CycleDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PaymentHistoryId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TokenTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    TokenCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualFeePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualFeePayments_SubscriptionBillingStates_SubscriptionBillingStateId",
                        column: x => x.SubscriptionBillingStateId,
                        principalTable: "SubscriptionBillingStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualFeePayments_MemberId_CreationDate",
                table: "AnnualFeePayments",
                columns: new[] { "MemberId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualFeePayments_OrderId",
                table: "AnnualFeePayments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualFeePayments_Status_CreationDate",
                table: "AnnualFeePayments",
                columns: new[] { "Status", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "UX_AnnualFeePayments_LivePerCycle",
                table: "AnnualFeePayments",
                columns: new[] { "SubscriptionBillingStateId", "CycleDueDate" },
                unique: true,
                filter: "[Status] IN (1, 2, 4)");

            migrationBuilder.CreateIndex(
                name: "UX_AnnualFeePayments_Member_IdempotencyKey",
                table: "AnnualFeePayments",
                columns: new[] { "MemberId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                column: "MembershipSubscriptionId",
                unique: true,
                filter: "[MembershipSubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SubscriptionBillingStates_AnnualFeeMember",
                table: "SubscriptionBillingStates",
                column: "MemberId",
                unique: true,
                filter: "[Purpose] = 2");

            migrationBuilder.CreateIndex(
                name: "UX_RecurringBillingPlans_AnnualFee",
                table: "RecurringBillingPlans",
                column: "IsAnnualFeePlan",
                unique: true,
                filter: "[IsAnnualFeePlan] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionBillingStates_Purpose",
                table: "SubscriptionBillingStates",
                sql: "([Purpose] = 1 AND [MembershipSubscriptionId] IS NOT NULL) OR ([Purpose] = 2 AND [MembershipSubscriptionId] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionBillingStates_Purpose",
                table: "SubscriptionBillingStates");

            migrationBuilder.DropTable(name: "AnnualFeePayments");

            migrationBuilder.DropIndex(name: "UX_RecurringBillingPlans_AnnualFee", table: "RecurringBillingPlans");
            migrationBuilder.DropIndex(name: "UX_SubscriptionBillingStates_AnnualFeeMember", table: "SubscriptionBillingStates");
            migrationBuilder.DropIndex(name: "IX_SubscriptionBillingStates_MembershipSubscriptionId", table: "SubscriptionBillingStates");

            // Volver atrás borra los estados de cuota anual (no tienen suscripción y la columna vuelve a
            // ser obligatoria). Se recrean con AnnualFeeStateEnrollmentJob si se vuelve a migrar.
            migrationBuilder.Sql("DELETE FROM [SubscriptionBillingStates] WHERE [Purpose] = 2;");

            migrationBuilder.DropColumn(name: "Purpose", table: "SubscriptionBillingStates");
            migrationBuilder.DropColumn(name: "IsAnnualFeePlan", table: "RecurringBillingPlans");
            migrationBuilder.DropColumn(name: "FeesFromBalance", table: "MemberProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                column: "MembershipSubscriptionId",
                unique: true);
        }
```

- [ ] **Step 4: Aplicar a la base local y comprobar**

Run (ver «Comandos que se repiten»): `dotnet ef database update … --connection "Server=.;Database=MLMConquerorGlobalEditionDb;…"`
Expected: «Done.»

Run:

```bash
sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -W -s"|" -Q "SET NOCOUNT ON;
SELECT Id, Name, IsAnnualFeePlan FROM RecurringBillingPlans;
SELECT name, has_filter, filter_definition FROM sys.indexes WHERE name IN ('UX_SubscriptionBillingStates_AnnualFeeMember','IX_SubscriptionBillingStates_MembershipSubscriptionId','UX_AnnualFeePayments_LivePerCycle','UX_AnnualFeePayments_Member_IdempotencyKey','UX_RecurringBillingPlans_AnnualFee');
SELECT name FROM sys.check_constraints WHERE name = 'CK_SubscriptionBillingStates_Purpose';
SELECT COUNT(*) AS sinPreferencia FROM MemberProfiles WHERE FeesFromBalance = 0;
SELECT Purpose, COUNT(*) FROM SubscriptionBillingStates GROUP BY Purpose;"
```

Expected: el plan 2 con `IsAnnualFeePlan = 1` y el 1 con `0`; los cinco índices con `has_filter = 1` y su filtro; el `CHECK`; `sinPreferencia = 0`; todos los estados con `Purpose = 1` (118.263 en local).

- [ ] **Step 5: Ejecutar las pruebas de facturación**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS (las `[SqlServerFact]` existentes siguen pasando con la migración aplicada).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Migrations/*_CuotaAnual.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_CuotaAnual.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs
git commit -m "Migracion CuotaAnual: suscripcion anulable con proposito y CHECK, un estado de cuota por miembro, plan de la cuota marcado, tabla de pagos de cuota con indices unicos filtrados y preferencia encendida; se niega a migrar con dos planes anuales o con estados por suscripcion en el plan anual"
```

---

### Task 6: La suscripción puede faltar y el plan anual no crea estados por suscripción

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/PlanAnualSinEstadosPorSuscripcionTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/HighVolume/RecurringChargeWorker.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingEnrollmentService.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Jobs/RecurringBillingSweepJob.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Seeders/RecurringBillingSeeder.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminRecurringBillingController.cs` (`RecurringStateDto` y su proyección)
- Modify: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecurringBillingStates.razor` (`StateDto`)

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/PlanAnualSinEstadosPorSuscripcionTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (K1, K2). La cuota anual es por miembro: el plan anual nunca crea estados a partir de una
/// suscripción (si no, una suscripción de nivel 1 la cobraría como membresía y otra vez como cuota), y
/// un estado de cuota anual, que no tiene suscripción, no emite puntos ni disparadores de comisión.
/// </summary>
public class PlanAnualSinEstadosPorSuscripcionTests
{
    private static readonly DateTime Ahora = new(2027, 6, 10, 8, 0, 0);

    private static IDateTimeProvider Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Ahora);
        return m.Object;
    }

    private static async Task<AppDbContext> ConPlanesAsync()
    {
        var db = TestDbContextFactory.Create();
        db.MembershipLevels.AddRange(
            new MembershipLevel { Id = 1, Name = "Lifestyle Ambassador", SortOrder = 1, CreatedBy = "prueba" },
            new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.AddRange(
            new Product { Id = "prod-amb", Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1, MonthlyFee = 0m, SetupFee = 99m, AnnualPrice = 99m, IsActive = true, CreatedBy = "prueba" },
            new Product { Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3, MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba" });

        var anual = new RecurringBillingPlan
        {
            Id = 2, Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            PayFromCommissionBalanceFirst = true, IsActive = true, IsAnnualFeePlan = true, CreatedBy = "prueba"
        };
        anual.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = "prod-amb", CreatedBy = "prueba" });
        var viajes = new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, IsActive = true, CreatedBy = "prueba"
        };
        viajes.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = "prod-elite", CreatedBy = "prueba" });
        db.RecurringBillingPlans.AddRange(anual, viajes);
        await db.SaveChangesAsync();
        return db;
    }

    private static MembershipSubscription Suscripcion(string id, int nivel) => new()
    {
        Id = id, MemberId = "AMB-" + id, MembershipLevelId = nivel, SubscriptionStatus = MembershipStatus.Active,
        StartDate = Ahora.AddDays(-10), CreatedBy = "prueba"
    };

    [Fact]
    public async Task Una_suscripcion_de_nivel_1_no_crea_un_estado_en_el_plan_anual()
    {
        await using var db = await ConPlanesAsync();
        var sub = Suscripcion("s1", 1);
        db.MembershipSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        await new RecurringBillingEnrollmentService(db, Reloj()).EnsureStateForSubscriptionAsync(sub, "prueba");

        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_suscripcion_de_Travel_Advantage_si_crea_su_estado()
    {
        await using var db = await ConPlanesAsync();
        var sub = Suscripcion("s3", 3);
        db.MembershipSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        await new RecurringBillingEnrollmentService(db, Reloj()).EnsureStateForSubscriptionAsync(sub, "prueba");

        var estado = await db.SubscriptionBillingStates.SingleAsync();
        estado.RecurringBillingPlanId.Should().Be(1);
        estado.Purpose.Should().Be(BillingStatePurpose.Membership);
        estado.MembershipSubscriptionId.Should().Be("s3");
    }

    [Fact]
    public async Task El_barrido_tampoco_crea_estados_por_suscripcion_en_el_plan_anual()
    {
        await using var db = await ConPlanesAsync();
        db.MembershipSubscriptions.Add(Suscripcion("s1", 1));
        await db.SaveChangesAsync();

        var barrido = new RecurringBillingSweepJob(db, new Mock<IRecurringBillingProcessor>().Object,
            new RecurringBillingEnrollmentService(db, Reloj()), Reloj(), NullLogger<RecurringBillingSweepJob>.Instance);
        await barrido.ExecuteAsync();

        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("Success", RecurringBillingStatus.Active)]
    [InlineData("Failed", RecurringBillingStatus.Stopped)]
    public async Task Un_estado_de_cuota_anual_no_emite_puntos_ni_disparadores(string resultado, RecurringBillingStatus estadoFinal)
    {
        await using var db = TestDbContextFactory.Create();
        db.RecurringBillingBatches.Add(new RecurringBillingBatch
        {
            Id = "lote", RunDate = Ahora.Date, Gateway = CardProcessor.NmiSpreedly, WorkerCount = 1,
            ScheduledStartTime = Ahora, CaseCount = 1, Status = RecurringBillingBatchStatus.Planned,
            CreatedBy = "prueba", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.RecurringBillingBatchShards.Add(new RecurringBillingBatchShard
        {
            Id = "trozo", BatchId = "lote", ShardIndex = 0, IdRangeStart = 1, IdRangeEnd = 100,
            Status = RecurringBillingBatchStatus.Planned, CreatedBy = "prueba", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "anual-1", Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = null, MemberId = "AMB-1",
            RecurringBillingPlanId = 2, BillingAnchorDate = Ahora.AddYears(-1), NextBillingDate = Ahora.Date,
            NextAttemptDate = Ahora.Date, Status = estadoFinal, ShardKey = 5, CreatedBy = "prueba", CreationDate = Ahora
        });
        await db.SaveChangesAsync();

        var procesador = new Mock<IRecurringBillingProcessor>();
        procesador.Setup(p => p.ProcessAsync("anual-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                BillingStateId = "anual-1", Outcome = resultado, OrderId = resultado == "Success" ? "ord-1" : null
            }));

        // El estado Stopped no lo recogería el trabajador: se pone Active al sembrar y se para "durante" el proceso.
        if (estadoFinal == RecurringBillingStatus.Stopped)
        {
            var e = await db.SubscriptionBillingStates.SingleAsync();
            e.Status = RecurringBillingStatus.Active;
            await db.SaveChangesAsync();
            procesador.Setup(p => p.ProcessAsync("anual-1", false, It.IsAny<CancellationToken>()))
                .Callback(() => { e.Status = RecurringBillingStatus.Stopped; db.SaveChanges(); })
                .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
                {
                    BillingStateId = "anual-1", Outcome = resultado
                }));
        }

        var r = await new RecurringChargeWorker(db, procesador.Object, Reloj(), new Mock<ILogger<RecurringChargeWorker>>().Object)
            .ProcessShardAsync("trozo");

        r.IsSuccess.Should().BeTrue();
        (await db.PointDeltaEvents.CountAsync()).Should().Be(0);
        (await db.CommissionTriggerQueues.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_seeder_marca_el_plan_anual_y_no_el_de_viajes()
    {
        await using var db = TestDbContextFactory.Create();
        db.Products.Add(new Product { Id = "prod-amb", Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1, MonthlyFee = 0m, SetupFee = 99m, AnnualPrice = 99m, IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();

        await RecurringBillingSeeder.SeedAsync(db, NullLogger.Instance);

        (await db.RecurringBillingPlans.SingleAsync(p => p.Name == "Lifestyle Ambassador (Annual)")).IsAnnualFeePlan.Should().BeTrue();
        (await db.RecurringBillingPlans.SingleAsync(p => p.Name == "Travel Advantage")).IsAnnualFeePlan.Should().BeFalse();
    }

    [Fact]
    public async Task El_seeder_marca_el_plan_anual_que_ya_existia_sin_marca()
    {
        await using var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            IsActive = true, IsAnnualFeePlan = false, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        await RecurringBillingSeeder.SeedAsync(db, NullLogger.Instance);

        (await db.RecurringBillingPlans.SingleAsync(p => p.Name == "Lifestyle Ambassador (Annual)")).IsAnnualFeePlan.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter PlanAnualSinEstadosPorSuscripcionTests -nologo`
Expected: FAIL en `Una_suscripcion_de_nivel_1_no_crea_un_estado_en_el_plan_anual`, `El_barrido_tampoco_…` y los dos del seeder. Las dos del trabajador ya pasan hoy, por casualidad: buscan una suscripción con `Id == null`, no la encuentran y salen. El Step 4 lo hace explícito y quita las advertencias CS8604.

- [ ] **Step 3: El servicio de alta salta el plan anual**

En `RecurringBillingEnrollmentService.cs`, sustituir:

```csharp
        // Find the active plan that covers this product
        var planProduct = await _db.RecurringBillingPlanProducts
            .AsNoTracking()
            .Include(pp => pp.Plan)
            .FirstOrDefaultAsync(pp => pp.ProductId == productId && pp.Plan!.IsActive, ct);
```

por:

```csharp
        // Find the active plan that covers this product. El plan de la cuota anual nunca crea estados por
        // suscripción (Plan K, K2): sus estados son por miembro y los da de alta AnnualFeeStateEnrollment.
        var planProduct = await _db.RecurringBillingPlanProducts
            .AsNoTracking()
            .Include(pp => pp.Plan)
            .FirstOrDefaultAsync(pp => pp.ProductId == productId && pp.Plan!.IsActive && !pp.Plan.IsAnnualFeePlan, ct);
```

- [ ] **Step 4: El trabajador acepta un estado sin suscripción**

En `RecurringChargeWorker.cs`, sustituir la firma y el principio de `EmitActivatedEventsAsync`:

```csharp
    private async Task EmitActivatedEventsAsync(
        string batchId,
        string billingStateId,
        string memberId,
        string subscriptionId,
        string? orderId,
        DateTime now,
        CancellationToken ct)
    {
```

por:

```csharp
    private async Task EmitActivatedEventsAsync(
        string batchId,
        string billingStateId,
        string memberId,
        string? subscriptionId,
        string? orderId,
        DateTime now,
        CancellationToken ct)
    {
        // Plan K: un estado de cuota anual no tiene suscripción. La cuota no aporta puntos ni dispara
        // Fast Start ni Boost (su producto es CorporateFee).
        if (subscriptionId is null) return;

```

y la de `EmitDeactivatedEventIfNeededAsync`:

```csharp
    private async Task EmitDeactivatedEventIfNeededAsync(
        string batchId,
        string billingStateId,
        string memberId,
        string subscriptionId,
        DateTime now,
        CancellationToken ct)
    {
```

por:

```csharp
    private async Task EmitDeactivatedEventIfNeededAsync(
        string batchId,
        string billingStateId,
        string memberId,
        string? subscriptionId,
        DateTime now,
        CancellationToken ct)
    {
        // Plan K: sin suscripción no hay puntos que retirar (un fallo de la cuota no toca la membresía).
        if (subscriptionId is null) return;

```

- [ ] **Step 5: El barrido salta el plan anual**

En `RecurringBillingSweepJob.cs`, sustituir:

```csharp
            where sub.SubscriptionStatus == Domain.Entities.Membership.MembershipStatus.Active
               && !sub.IsDeleted
               && plan.IsActive
               && !_db.SubscriptionBillingStates.Any(s => s.MembershipSubscriptionId == sub.Id)
```

por:

```csharp
            where sub.SubscriptionStatus == Domain.Entities.Membership.MembershipStatus.Active
               && !sub.IsDeleted
               && plan.IsActive
               && !plan.IsAnnualFeePlan   // Plan K (K2): la cuota anual es por miembro, no por suscripción
               && !_db.SubscriptionBillingStates.Any(s => s.MembershipSubscriptionId == sub.Id)
```

- [ ] **Step 6: El seeder marca el plan**

En `RecurringBillingSeeder.cs`, sustituir el principio de `SeedLifestylePlanAsync`:

```csharp
        if (await db.RecurringBillingPlans.AnyAsync(p => p.Name == LifestylePlanName))
        {
            logger.LogInformation("RecurringBillingSeeder: Plan '{Name}' already exists — skipped.", LifestylePlanName);
            return;
        }
```

por:

```csharp
        var existing = await db.RecurringBillingPlans.FirstOrDefaultAsync(p => p.Name == LifestylePlanName);
        if (existing is not null)
        {
            // Plan K (K2): es el plan de la cuota anual. La migración CuotaAnual ya lo marca; esto cubre
            // una base que se siembre sin haber pasado por ella.
            if (!existing.IsAnnualFeePlan && !await db.RecurringBillingPlans.AnyAsync(p => p.IsAnnualFeePlan))
            {
                existing.IsAnnualFeePlan = true;
                await db.SaveChangesAsync();
                logger.LogInformation("RecurringBillingSeeder: Plan '{Name}' marcado como plan de la cuota anual.", LifestylePlanName);
                return;
            }

            logger.LogInformation("RecurringBillingSeeder: Plan '{Name}' already exists — skipped.", LifestylePlanName);
            return;
        }
```

y, en el `new RecurringBillingPlan` del mismo método, sustituir:

```csharp
            FixedAmountOverride       = null,  // use Product.AnnualPrice
            IsActive                  = true,
```

por:

```csharp
            FixedAmountOverride       = null,  // use Product.AnnualPrice
            IsActive                  = true,
            IsAnnualFeePlan           = true,  // Plan K: el plan de la cuota anual del Biz Center
```

- [ ] **Step 7: El Admin lee estados sin suscripción**

En `AdminRecurringBillingController.cs`, sustituir el registro:

```csharp
    public record RecurringStateDto(
        string Id, string MemberId, string MembershipSubscriptionId, int RecurringBillingPlanId,
        string PlanName, DateTime BillingAnchorDate, DateTime? LastSuccessfulBillingDate,
        DateTime NextBillingDate, int CurrentAttemptIndex, DateTime NextAttemptDate,
        RecurringBillingStatus Status, DateTime? LastAttemptAt, string? LastAttemptOutcome,
        string? LastFailureReason);
```

por:

```csharp
    public record RecurringStateDto(
        string Id, string MemberId, string? MembershipSubscriptionId, int RecurringBillingPlanId,
        string PlanName, DateTime BillingAnchorDate, DateTime? LastSuccessfulBillingDate,
        DateTime NextBillingDate, int CurrentAttemptIndex, DateTime NextAttemptDate,
        RecurringBillingStatus Status, DateTime? LastAttemptAt, string? LastAttemptOutcome,
        string? LastFailureReason, BillingStatePurpose Purpose);
```

y la proyección:

```csharp
            .Select(s => new RecurringStateDto(
                s.Id, s.MemberId, s.MembershipSubscriptionId, s.RecurringBillingPlanId,
                s.Plan!.Name, s.BillingAnchorDate, s.LastSuccessfulBillingDate,
                s.NextBillingDate, s.CurrentAttemptIndex, s.NextAttemptDate,
                s.Status, s.LastAttemptAt, s.LastAttemptOutcome, s.LastFailureReason))
```

por:

```csharp
            .Select(s => new RecurringStateDto(
                s.Id, s.MemberId, s.MembershipSubscriptionId, s.RecurringBillingPlanId,
                s.Plan!.Name, s.BillingAnchorDate, s.LastSuccessfulBillingDate,
                s.NextBillingDate, s.CurrentAttemptIndex, s.NextAttemptDate,
                s.Status, s.LastAttemptAt, s.LastAttemptOutcome, s.LastFailureReason, s.Purpose))
```

En `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecurringBillingStates.razor`, en el registro `StateDto` (línea ~232), cambiar `string MembershipSubscriptionId` por `string? MembershipSubscriptionId`. Donde la página pinte esa columna, mostrar `@(s.MembershipSubscriptionId ?? "—")`.

- [ ] **Step 8: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "PlanAnualSinEstadosPorSuscripcionTests|RecurringChargeWorkerTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.slnx -nologo`
Expected: compila sin errores.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/PlanAnualSinEstadosPorSuscripcionTests.cs \
        MLMConquerorGlobalEdition.Billing/Services/Recurring/HighVolume/RecurringChargeWorker.cs \
        MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingEnrollmentService.cs \
        MLMConquerorGlobalEdition.Billing/Jobs/RecurringBillingSweepJob.cs \
        MLMConquerorGlobalEdition.Repository/Seeders/RecurringBillingSeeder.cs \
        MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminRecurringBillingController.cs \
        MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecurringBillingStates.razor
git commit -m "El plan de la cuota anual nunca crea estados por suscripcion (alta y barrido lo saltan), el trabajador no emite puntos ni disparadores para un estado sin suscripcion, el seeder marca el plan anual y el Admin lista estados sin suscripcion con su proposito"
```

---

### Task 7: Contratos compartidos y reglas puras: calendario y exclusiones

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Billing/AnnualFeeContracts.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeSchedule.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeEligibility.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeScheduleTests.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeEligibilityTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeScheduleTests.cs`:

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>El calendario de la cuota (Plan K, K3, K4, K14): sin cobros hacia atrás y con el ancla en el aniversario.</summary>
public class AnnualFeeScheduleTests
{
    private static DateTime D(string s) => DateTime.Parse(s);

    [Theory]
    [InlineData("2026-06-05 14:30", "2026-10-17 09:00", "2027-06-05")] // menos de un año: el primer aniversario
    [InlineData("2025-10-16 23:34", "2026-10-17 09:00", "2027-10-16")] // el aniversario fue ayer: no se cobra hacia atrás
    [InlineData("2025-10-17 08:00", "2026-10-17 09:00", "2026-10-17")] // el aniversario es hoy: vence hoy
    [InlineData("2018-03-02 10:00", "2026-10-17 09:00", "2027-03-02")] // muy antiguo: el próximo aniversario
    [InlineData("2020-02-29 10:00", "2026-10-17 09:00", "2027-02-28")] // 29 de febrero
    public void El_primer_vencimiento_al_crear_el_estado_es_el_primer_aniversario_que_no_ha_pasado(
        string alta, string ahora, string esperado)
        => AnnualFeeSchedule.FirstDueOnCreation(D(alta), D(ahora)).Should().Be(D(esperado));

    [Theory]
    // K4 revisado por Nick (2026-10-17): puntual o adelantado suma un año al vencimiento; tarde, un año desde el pago.
    [InlineData("2027-06-05", "2027-05-06 10:00", "2028-06-05")] // 30 días antes: no pierde días
    [InlineData("2027-06-05", "2027-05-20 10:00", "2028-06-05")] // antes del vencimiento
    [InlineData("2027-06-05", "2027-06-05 23:59", "2028-06-05")] // el mismo día
    [InlineData("2027-06-05", "2027-06-06 05:00", "2028-06-06")] // un día tarde (reintento): se re-ancla al pago
    [InlineData("2027-06-05", "2027-07-05 23:00", "2028-07-05")] // 30 días tarde: sin gracia, se re-ancla
    [InlineData("2027-06-05", "2028-01-15 12:00", "2029-01-15")] // estado parado y pago meses después
    [InlineData("2027-02-28", "2028-02-29 12:00", "2029-02-28")] // tarde un 29 de febrero
    public void El_siguiente_vencimiento_tras_pagar(string ciclo, string pagado, string esperado)
        => AnnualFeeSchedule.NextDueAfterPayment(D(ciclo), D(pagado)).Should().Be(D(esperado));

    [Theory]
    [InlineData("2027-07-10", "2027-06-10 09:00", true)]  // vence en 30 días
    [InlineData("2027-07-11", "2027-06-10 09:00", false)] // vence en 31 días
    [InlineData("2027-06-10", "2027-06-10 09:00", true)]  // vence hoy
    [InlineData("2026-01-01", "2027-06-10 09:00", true)]  // vencida hace mucho
    public void Se_puede_pagar_desde_30_dias_antes(string vence, string ahora, bool esperado)
        => AnnualFeeSchedule.IsPayable(D(vence), D(ahora)).Should().Be(esperado);

    [Fact]
    public void El_primer_aniversario_es_la_fecha_de_alta_mas_un_anio_sin_hora()
        => AnnualFeeSchedule.FirstAnniversary(D("2026-06-05 14:30")).Should().Be(D("2027-06-05"));
}
```

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeEligibilityTests.cs`:

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Las exclusiones del legado (Plan K, K5) en el orden de BizCenterFeeMWRLife.BillMonthlyPayment:
/// cancelada, menos de un año, suspendida, cobrada hace poco y KeepQualified. Y lo que pide
/// "Pagar ahora" (K14).
/// </summary>
public class AnnualFeeEligibilityTests
{
    private static readonly DateTime Ahora = new(2027, 6, 10, 9, 0, 0);
    private static readonly DateTime Vence = new(2027, 6, 5);

    private static AnnualFeeMemberFacts Miembro(
        MemberAccountStatus estado = MemberAccountStatus.Active, DateTime? alta = null, bool mantener = false,
        MemberType tipo = MemberType.Ambassador)
        => new(true, tipo, estado, alta ?? new DateTime(2026, 6, 5), mantener);

    private static AnnualFeeDecision Automatico(AnnualFeeMemberFacts m, DateTime? vence = null, DateTime? ultimo = null, bool forzado = false)
        => AnnualFeeEligibility.ForAutomaticCharge(m, vence ?? Vence, ultimo, Ahora, forzado);

    [Fact]
    public void Vencida_y_activa_se_cobra()
        => Automatico(Miembro()).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void Inactiva_tambien_se_cobra()
        => Automatico(Miembro(MemberAccountStatus.Inactive)).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void Terminada_para_el_estado_para_siempre()
    {
        var d = Automatico(Miembro(MemberAccountStatus.Terminated));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Stop);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonAccountClosed);
    }

    [Fact]
    public void Sin_perfil_o_si_ya_no_es_Ambassador_tambien_se_para()
    {
        Automatico(AnnualFeeMemberFacts.Missing).Kind.Should().Be(AnnualFeeDecisionKind.Stop);
        Automatico(Miembro(tipo: MemberType.ExternalMember)).Kind.Should().Be(AnnualFeeDecisionKind.Stop);
    }

    [Fact]
    public void Con_menos_de_un_anio_se_reprograma_a_su_aniversario()
    {
        var d = Automatico(Miembro(alta: new DateTime(2026, 12, 1, 15, 0, 0)), vence: new DateTime(2027, 6, 1));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Reschedule);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonUnderOneYear);
        d.NextDue.Should().Be(new DateTime(2027, 12, 1));
    }

    [Theory]
    [InlineData(MemberAccountStatus.Suspended, AnnualFeeEligibility.ReasonSuspended)]
    [InlineData(MemberAccountStatus.Pending, AnnualFeeEligibility.ReasonAccountPending)]
    public void Suspendida_o_pendiente_se_mira_otra_vez_manana(MemberAccountStatus estado, string razon)
    {
        var d = Automatico(Miembro(estado));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Postpone);
        d.Reason.Should().Be(razon);
    }

    [Fact]
    public void Cobrada_hace_menos_de_11_meses_se_reprograma_a_un_anio_del_cobro()
    {
        var d = Automatico(Miembro(), ultimo: new DateTime(2027, 1, 10, 8, 0, 0));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Reschedule);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonRecentlyBilled);
        d.NextDue.Should().Be(new DateTime(2028, 1, 10));
    }

    [Fact]
    public void Cobrada_hace_mas_de_11_meses_se_cobra()
        => Automatico(Miembro(), ultimo: new DateTime(2026, 6, 5)).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void KeepQualified_no_se_cobra_ni_avanza_y_se_mira_manana()
    {
        var d = Automatico(Miembro(mantener: true));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Postpone);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonKeepQualified);
        d.NextDue.Should().BeNull();
    }

    [Fact]
    public void Si_aun_no_vence_se_reprograma_a_su_vencimiento()
    {
        var d = Automatico(Miembro(), vence: new DateTime(2027, 6, 20));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Reschedule);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonNotDue);
        d.NextDue.Should().Be(new DateTime(2027, 6, 20));
    }

    [Fact]
    public void Forzado_se_cobra_aunque_no_venza()
        => Automatico(Miembro(), vence: new DateTime(2027, 6, 20), forzado: true).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void Forzado_no_se_salta_las_exclusiones()
        => Automatico(Miembro(MemberAccountStatus.Suspended), forzado: true).Kind.Should().Be(AnnualFeeDecisionKind.Postpone);

    [Fact]
    public void El_orden_es_el_del_legado_cancelada_antes_que_KeepQualified()
        => Automatico(Miembro(MemberAccountStatus.Terminated, mantener: true)).Kind.Should().Be(AnnualFeeDecisionKind.Stop);

    [Fact]
    public void El_orden_es_el_del_legado_menos_de_un_anio_antes_que_suspendida()
        => Automatico(Miembro(MemberAccountStatus.Suspended, alta: new DateTime(2026, 12, 1)), vence: new DateTime(2027, 12, 1))
            .Reason.Should().Be(AnnualFeeEligibility.ReasonUnderOneYear);

    [Fact]
    public void Los_hechos_salen_del_perfil_y_un_perfil_borrado_es_como_no_tenerlo()
    {
        var perfil = new MemberProfile
        {
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
            EnrollDate = new DateTime(2026, 6, 5), KeepQualified = true
        };
        AnnualFeeMemberFacts.Of(perfil).Should().Be(new AnnualFeeMemberFacts(true, MemberType.Ambassador,
            MemberAccountStatus.Active, new DateTime(2026, 6, 5), true));

        perfil.IsDeleted = true;
        AnnualFeeMemberFacts.Of(perfil).Exists.Should().BeFalse();
        AnnualFeeMemberFacts.Of(null).Exists.Should().BeFalse();
    }

    // ── Pagar ahora ──────────────────────────────────────────────────────────

    [Fact]
    public void Pagar_ahora_vale_con_la_cuenta_activa_y_la_cuota_vencida()
        => AnnualFeeEligibility.ForPayNow(Miembro(), Vence, Ahora).Should().BeNull();

    [Fact]
    public void Pagar_ahora_vale_hasta_30_dias_antes()
    {
        AnnualFeeEligibility.ForPayNow(Miembro(), Ahora.Date.AddDays(30), Ahora).Should().BeNull();
        AnnualFeeEligibility.ForPayNow(Miembro(), Ahora.Date.AddDays(31), Ahora).Should().Be(AnnualFeeErrors.NotDue);
    }

    [Theory]
    [InlineData(MemberAccountStatus.Inactive)]
    [InlineData(MemberAccountStatus.Suspended)]
    [InlineData(MemberAccountStatus.Pending)]
    [InlineData(MemberAccountStatus.Terminated)]
    public void Pagar_ahora_exige_la_cuenta_activa(MemberAccountStatus estado)
        => AnnualFeeEligibility.ForPayNow(Miembro(estado), Vence, Ahora).Should().Be(AnnualFeeErrors.AccountNotActive);

    [Fact]
    public void Con_KeepQualified_no_hay_cuota_que_pagar()
        => AnnualFeeEligibility.ForPayNow(Miembro(mantener: true), Vence, Ahora).Should().Be(AnnualFeeErrors.Waived);

    [Fact]
    public void Un_cliente_no_tiene_cuota()
        => AnnualFeeEligibility.ForPayNow(Miembro(tipo: MemberType.ExternalMember), Vence, Ahora).Should().Be(AnnualFeeErrors.NotApplicable);
}
```

> **Revisado por las decisiones de Nick (2026-10-17).** Además de lo que sigue: `AnnualFeeMemberFacts` lleva un sexto campo, `MembershipOnHold` (la membresía vigente está en `HoldByBilling`); `ForAutomaticCharge` devuelve `Postpone` con `ReasonMembershipOnHold = "MEMBERSHIP_ON_HOLD"` justo después de suspendida/pendiente (paso 3b de K5) y `ForPayNow` devuelve `AnnualFeeErrors.MembershipOnHold = "ANNUAL_FEE_MEMBERSHIP_ON_HOLD"` después de `AccountNotActive`. Los hechos se leen con `AnnualFeeMemberQueries.FactsAsync(db, memberId)` (Repository): el perfil y la membresía vigente (suscripción no borrada más reciente por `StartDate` y `CreationDate`, como `QualificationService`). Las Tareas 12, 14 y 20 usan `FactsAsync` en lugar de `AnnualFeeMemberFacts.Of(member)`. Pruebas en `AnnualFeeEligibilityTests` y `AnnualFeeMemberQueriesTests`.

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "AnnualFeeScheduleTests|AnnualFeeEligibilityTests" -nologo`
Expected: FAIL de compilación (`AnnualFeeSchedule`, `AnnualFeeEligibility`, `AnnualFeeErrors` no existen).

- [ ] **Step 3: Contratos compartidos**

`MLMConquerorGlobalEdition.SharedKernel/Billing/AnnualFeeContracts.cs`:

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Reglas de la cuota anual del Biz Center (Plan K). Las aplican Billing (para cobrar) y el BizCenter
/// (para enseñar si se puede pagar); viven aquí para que no diverjan.
/// </summary>
public static class AnnualFeeRules
{
    /// <summary>"Annual: Biz Center" (HasData, mismo id en todos los entornos). Se gasta primero.</summary>
    public const int AnnualBizCenterTokenTypeId = 23;

    /// <summary>"Annual Fee" (HasData). Se gasta si no hay del 23.</summary>
    public const int AnnualFeeTokenTypeId = 10;

    /// <summary>
    /// Los tokens que pagan la cuota, en orden de preferencia (K8). No el 24 "Legacy Biz Center Fee"
    /// (no otorga producto) ni el 103 (token del plan que emite el camino de Travel Advantage).
    /// </summary>
    public static readonly IReadOnlyList<int> FeeTokenTypeIds = [AnnualBizCenterTokenTypeId, AnnualFeeTokenTypeId];

    /// <summary>"Pagar ahora" vale desde estos días antes del vencimiento (K14).</summary>
    public const int PayAheadDays = 30;

    /// <summary>"Cobrada hace poco": el último cobro fue hace menos de estos meses (K5).</summary>
    public const int RecentlyBilledMonths = 11;

    /// <summary>¿Cobra el motor la cuota en el aniversario? Apagado por defecto (K13). Se lee en cada llamada.</summary>
    public const string AutoChargeEnabledConfigKey = "Billing:AnnualFee:AutoChargeEnabled";

    /// <summary>Minutos que un pago de cuota puede seguir Pending antes de pasar a conciliar (K11).</summary>
    public const string StalePendingMinutesConfigKey = "Billing:AnnualFee:StalePendingMinutes";

    public const int DefaultStalePendingMinutes = 30;

    /// <summary>Estados de cuota que el trabajo de alta crea por guardado (K3).</summary>
    public const int EnrollmentBatchSize = 1000;

    /// <summary>Tope de lotes por ejecución del trabajo de alta: 500.000 estados.</summary>
    public const int EnrollmentMaxBatchesPerRun = 500;
}

/// <summary>Con qué se paga la cuota. Sin tarjeta hasta el Plan I. Coinciden con el enum de dominio.</summary>
public static class AnnualFeeMethods
{
    public const string Token             = "Token";
    public const string CommissionBalance = "CommissionBalance";
    public const string EWallet           = "EWallet";

    public static readonly IReadOnlyList<string> All = [Token, CommissionBalance, EWallet];

    /// <summary>
    /// El nombre canónico, o null si no es un método de la cuota (la tarjeta no lo es). Solo por nombre:
    /// "1" no vale, igual que en la compra de tokens.
    /// </summary>
    public static string? Normalize(string? value) =>
        All.FirstOrDefault(m => string.Equals(m, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Cómo está la cuota de un miembro, para la pantalla.</summary>
public static class AnnualFeeStatuses
{
    public const string NotApplicable = "NotApplicable";
    public const string UpToDate      = "UpToDate";
    public const string DueSoon       = "DueSoon";
    public const string Due           = "Due";
    public const string UnderReview   = "UnderReview";
    public const string Waived        = "Waived";
}

/// <summary>Estados de un pago de cuota tal y como viajan (nombres del enum de dominio).</summary>
public static class AnnualFeePaymentStatuses
{
    public const string Pending             = "Pending";
    public const string Completed           = "Completed";
    public const string Failed              = "Failed";
    public const string NeedsReconciliation = "NeedsReconciliation";
}

/// <summary>Códigos de error de la cuota anual. La pantalla los traduce con AnnualFeeMessages.</summary>
public static class AnnualFeeErrors
{
    public const string NotAMember                    = TokenPurchaseErrors.NotAMember;
    public const string Impersonation                 = "IMPERSONATION_CANNOT_PAY_ANNUAL_FEE";
    public const string MethodNotSupported            = "ANNUAL_FEE_METHOD_NOT_SUPPORTED";
    public const string IdempotencyKeyRequired        = TokenPurchaseErrors.IdempotencyKeyRequired;
    public const string IdempotencyKeyReused          = TokenPurchaseErrors.IdempotencyKeyReused;
    public const string NotApplicable                 = "ANNUAL_FEE_NOT_APPLICABLE";
    public const string PlanNotConfigured             = "ANNUAL_FEE_PLAN_NOT_CONFIGURED";
    public const string PriceNotSet                   = "ANNUAL_FEE_PRICE_NOT_SET";
    public const string StateNotFound                 = "ANNUAL_FEE_STATE_NOT_FOUND";
    public const string AccountNotActive              = "ANNUAL_FEE_ACCOUNT_NOT_ACTIVE";
    public const string Waived                        = "ANNUAL_FEE_WAIVED";
    public const string NotDue                        = "ANNUAL_FEE_NOT_DUE";
    public const string AlreadyPaid                   = "ANNUAL_FEE_ALREADY_PAID";
    public const string InProgress                    = "ANNUAL_FEE_IN_PROGRESS";
    public const string NeedsReview                   = "ANNUAL_FEE_NEEDS_REVIEW";
    public const string NoFeeToken                    = "ANNUAL_FEE_NO_TOKEN";
    public const string NoFunds                       = "ANNUAL_FEE_NO_FUNDS";
    public const string Busy                          = "ANNUAL_FEE_BUSY";
    public const string AutoChargeDisabled            = "ANNUAL_FEE_AUTO_CHARGE_DISABLED";
    public const string PaymentNotFound               = "ANNUAL_FEE_PAYMENT_NOT_FOUND";
    public const string NotUnderReview                = "ANNUAL_FEE_PAYMENT_NOT_UNDER_REVIEW";
    public const string NotCharged                    = "ANNUAL_FEE_NOT_CHARGED";
    public const string InsufficientCommissionBalance = TokenPurchaseErrors.InsufficientCommissionBalance;
    public const string InsufficientEWalletBalance    = TokenPurchaseErrors.InsufficientEWalletBalance;
    public const string EWalletNotFound               = TokenPurchaseErrors.EWalletNotFound;
    public const string EWalletUnavailable            = TokenPurchaseErrors.EWalletUnavailable;
    public const string EWalletRejected               = TokenPurchaseErrors.EWalletRejected;
}

/// <summary>
/// "Pagar ahora". NO lleva MemberId: el miembro sale del token en el BizCenter y otra vez en Billing.
/// </summary>
public sealed class PayAnnualFeeRequest
{
    public string Method         { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class AnnualFeePaymentResultDto
{
    public string   PaymentId    { get; set; } = string.Empty;
    public string   OrderId      { get; set; } = string.Empty;
    public string?  OrderNo      { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public string   Method       { get; set; } = string.Empty;
    public decimal  Amount       { get; set; }
    public DateTime CycleDueDate { get; set; }
    public DateTime NextDueDate  { get; set; }
    public string?  TokenCode    { get; set; }
}

/// <summary>Lo que enseña la tarjeta de la cuota (BizCenter y Admin).</summary>
public sealed class AnnualFeeOverviewDto
{
    public bool      Applies            { get; set; }
    public string    Status             { get; set; } = AnnualFeeStatuses.NotApplicable;
    public decimal?  Amount             { get; set; }
    public DateTime? NextDueDate        { get; set; }
    public DateTime? LastPaidDate       { get; set; }
    public bool      CanPayNow          { get; set; }

    /// <summary>Código de AnnualFeeErrors si no se puede pagar ahora.</summary>
    public string?   NotPayableReason   { get; set; }

    public int       AvailableFeeTokens { get; set; }
    public bool      FeesFromBalance    { get; set; }
    public List<AnnualFeePaymentRowDto> RecentPayments { get; set; } = new();
}

public sealed class AnnualFeePaymentRowDto
{
    public string   PaymentId    { get; set; } = string.Empty;
    public DateTime Date         { get; set; }
    public DateTime CycleDueDate { get; set; }
    public string   Method       { get; set; } = string.Empty;
    public string   Status       { get; set; } = string.Empty;
    public decimal  Amount       { get; set; }
}

public sealed class AnnualFeePreferenceRequest
{
    public bool FeesFromBalance { get; set; }
}

/// <summary>Tesorería cierra un pago de cuota a conciliar tras mirar i-Payout.</summary>
public sealed class ReconcileAnnualFeePaymentRequest
{
    /// <summary>True: el dinero salió; se completa y avanza el ciclo. False: no salió; se da por fallido.</summary>
    public bool    Charged          { get; set; }
    public string? GatewayReference { get; set; }
    public string? Notes            { get; set; }
}
```

- [ ] **Step 4: Calendario**

`MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeSchedule.cs`:

```csharp
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// El calendario de la cuota anual (Plan K). Puro: sin base ni reloj. Todas las fechas son de servidor
/// y se trabajan sin hora (.Date).
/// </summary>
public static class AnnualFeeSchedule
{
    /// <summary>Inscripción + 1 año: el ancla de Nick.</summary>
    public static DateTime FirstAnniversary(DateTime enrollDate) => enrollDate.Date.AddYears(1);

    /// <summary>
    /// Primer vencimiento al crear el estado (K3): el primer aniversario de la inscripción que no haya
    /// pasado ya (hoy incluido). No se cobra hacia atrás: el estado nace el día que se despliega, no el
    /// día que se inscribió el miembro. AddYears desde la inscripción cada vez, para que un 29 de febrero
    /// caiga el 28 en los años no bisiestos sin arrastrar el 28 a los bisiestos.
    /// </summary>
    public static DateTime FirstDueOnCreation(DateTime enrollDate, DateTime now)
    {
        var today = now.Date;
        for (var years = 1; ; years++)
        {
            var due = enrollDate.Date.AddYears(years);
            if (due >= today) return due;
        }
    }

    /// <summary>
    /// El siguiente vencimiento después de pagar el ciclo <paramref name="cycleDue"/> (K4, revisado por
    /// Nick el 2026-10-17): pagado el día del vencimiento o antes, un año después del vencimiento (pagar
    /// antes no hace perder días); pagado después, un año desde el pago (sin gracia que conserve el aniversario).
    /// </summary>
    public static DateTime NextDueAfterPayment(DateTime cycleDue, DateTime paidAt)
    {
        var due  = cycleDue.Date;
        var paid = paidAt.Date;
        return paid <= due ? due.AddYears(1) : paid.AddYears(1);
    }

    /// <summary>¿Se puede pagar ya? Desde <see cref="AnnualFeeRules.PayAheadDays"/> días antes del vencimiento.</summary>
    public static bool IsPayable(DateTime nextDue, DateTime now) =>
        nextDue.Date <= now.Date.AddDays(AnnualFeeRules.PayAheadDays);
}
```

- [ ] **Step 5: Exclusiones**

`MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeEligibility.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>Lo que importa del miembro para decidir. Un perfil borrado cuenta como inexistente.</summary>
public sealed record AnnualFeeMemberFacts(
    bool Exists, MemberType MemberType, MemberAccountStatus Status, DateTime EnrollDate, bool KeepQualified)
{
    public static readonly AnnualFeeMemberFacts Missing =
        new(false, MemberType.Ambassador, MemberAccountStatus.Terminated, DateTime.MinValue, false);

    public static AnnualFeeMemberFacts Of(MemberProfile? profile) =>
        profile is null || profile.IsDeleted
            ? Missing
            : new(true, profile.MemberType, profile.Status, profile.EnrollDate, profile.KeepQualified);
}

public enum AnnualFeeDecisionKind
{
    /// <summary>Cobrar ahora.</summary>
    Charge,

    /// <summary>No cobrar nunca más: el estado pasa a Stopped.</summary>
    Stop,

    /// <summary>No cobrar hoy; volver a mirar mañana sin mover el vencimiento.</summary>
    Postpone,

    /// <summary>No cobrar; el vencimiento pasa a <see cref="AnnualFeeDecision.NextDue"/>.</summary>
    Reschedule
}

public sealed record AnnualFeeDecision(AnnualFeeDecisionKind Kind, string? Reason = null, DateTime? NextDue = null)
{
    public static readonly AnnualFeeDecision Charge = new(AnnualFeeDecisionKind.Charge);
}

/// <summary>
/// Las exclusiones de la cuota anual (Plan K, K5, K14). Puro: sin base ni reloj; lo usan Billing (para
/// cobrar) y el BizCenter (para decir si se puede pagar y por qué no).
/// </summary>
public static class AnnualFeeEligibility
{
    public const string ReasonAccountClosed  = "ACCOUNT_CLOSED";
    public const string ReasonUnderOneYear   = "UNDER_ONE_YEAR";
    public const string ReasonSuspended      = "ACCOUNT_SUSPENDED";
    public const string ReasonAccountPending = "ACCOUNT_PENDING";
    public const string ReasonRecentlyBilled = "RECENTLY_BILLED";
    public const string ReasonKeepQualified  = "KEEP_QUALIFIED";
    public const string ReasonNotDue         = "NOT_DUE";

    /// <summary>
    /// ¿Se cobra hoy en automático? Orden del legado (BizCenterFeeMWRLife.BillMonthlyPayment):
    /// cancelada → menos de un año → suspendida → cobrada hace poco → KeepQualified. Después, si no
    /// vence todavía, se reprograma (salvo forzado, que solo se salta esa última comprobación).
    /// </summary>
    public static AnnualFeeDecision ForAutomaticCharge(
        AnnualFeeMemberFacts member, DateTime nextBillingDate, DateTime? lastSuccessfulBilling, DateTime now, bool force)
    {
        var today = now.Date;

        // 1. Cancelada, o ya no es Ambassador: no se cobra nunca más. Terminada no se reactiva.
        if (!member.Exists || member.MemberType != MemberType.Ambassador || member.Status == MemberAccountStatus.Terminated)
            return new(AnnualFeeDecisionKind.Stop, ReasonAccountClosed);

        // 2. Menos de un año: el primer cobro es el aniversario.
        var firstAnniversary = AnnualFeeSchedule.FirstAnniversary(member.EnrollDate);
        if (today < firstAnniversary)
            return new(AnnualFeeDecisionKind.Reschedule, ReasonUnderOneYear,
                       Max(firstAnniversary, nextBillingDate.Date));

        // 3. Suspendida o pendiente de activar: se vuelve a mirar mañana (aquí no hay SuspendUntil).
        if (member.Status == MemberAccountStatus.Suspended)
            return new(AnnualFeeDecisionKind.Postpone, ReasonSuspended);
        if (member.Status == MemberAccountStatus.Pending)
            return new(AnnualFeeDecisionKind.Postpone, ReasonAccountPending);

        // 4. Cobrada hace poco.
        if (lastSuccessfulBilling is { } last && last.Date > today.AddMonths(-AnnualFeeRules.RecentlyBilledMonths))
            return new(AnnualFeeDecisionKind.Reschedule, ReasonRecentlyBilled,
                       Max(nextBillingDate.Date, last.Date.AddYears(1)));

        // 5. KeepQualified: ni se cobra ni avanza (no cuenta como pagada; califica por la marca).
        if (member.KeepQualified)
            return new(AnnualFeeDecisionKind.Postpone, ReasonKeepQualified);

        // 6. Todavía no vence.
        if (!force && nextBillingDate.Date > today)
            return new(AnnualFeeDecisionKind.Reschedule, ReasonNotDue, nextBillingDate.Date);

        return AnnualFeeDecision.Charge;
    }

    /// <summary>
    /// ¿Puede el miembro pulsar "Pagar ahora"? Null si sí; si no, el código de AnnualFeeErrors. Lo que
    /// depende de pagos ya hechos (pagada, en curso, en revisión) lo mira quien tiene la base.
    /// </summary>
    public static string? ForPayNow(AnnualFeeMemberFacts member, DateTime nextBillingDate, DateTime now)
    {
        if (!member.Exists || member.MemberType != MemberType.Ambassador) return AnnualFeeErrors.NotApplicable;
        if (member.Status != MemberAccountStatus.Active)                  return AnnualFeeErrors.AccountNotActive;
        if (member.KeepQualified)                                         return AnnualFeeErrors.Waived;
        if (!AnnualFeeSchedule.IsPayable(nextBillingDate, now))           return AnnualFeeErrors.NotDue;
        return null;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
```

- [ ] **Step 6: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "AnnualFeeScheduleTests|AnnualFeeEligibilityTests" -nologo`
Expected: PASS (todas).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel/Billing/AnnualFeeContracts.cs \
        MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeSchedule.cs \
        MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeEligibility.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeScheduleTests.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeEligibilityTests.cs
git commit -m "Contratos de la cuota anual y reglas puras compartidas: primer vencimiento sin cobrar hacia atras, aniversario conservado si se paga a tiempo, ventana de pago de 30 dias y las cinco exclusiones del legado en su orden"
```

---

### Task 8: Alta de los estados de cuota anual

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeePlanQueries.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeStateEnrollment.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Jobs/AnnualFeeStateEnrollmentJob.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CuotaAnualDb.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeStateEnrollmentTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Program.cs`

- [ ] **Step 1: Siembra compartida de las pruebas de la cuota**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CuotaAnualDb.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Base y siembra de las pruebas de la cuota anual (Plan K). Los métodos de siembra añaden y guardan.
/// El plan anual es el 2 y su producto "prod-amb" cuesta 99 al año, como en la base local.
/// </summary>
public static class CuotaAnualDb
{
    public static readonly DateTime Ahora = new(2027, 6, 10, 9, 0, 0);
    public const int PlanAnual = 2;
    public const string Producto = "prod-amb";

    public static AppDbContext Nueva(string? nombre = null, IInterceptor? interceptor = null)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nombre ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (interceptor is not null) b.AddInterceptors(interceptor);
        return new AppDbContext(b.Options);
    }

    public static IDateTimeProvider Reloj(DateTime? ahora = null)
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(c => c.Now).Returns(ahora ?? Ahora);
        m.Setup(c => c.UtcNow).Returns(ahora ?? Ahora);
        return m.Object;
    }

    public static async Task PlanAsync(AppDbContext db, decimal precio = 99m, bool activo = true)
    {
        db.Products.Add(new Product
        {
            Id = Producto, Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1,
            AnnualPrice = precio, MonthlyFee = 0m, SetupFee = 99m, IsActive = true, CreatedBy = "prueba"
        });
        var plan = new RecurringBillingPlan
        {
            Id = PlanAnual, Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            PayFromCommissionBalanceFirst = true, TokenTypeId = 103, IsActive = activo, IsAnnualFeePlan = true,
            CreatedBy = "prueba"
        };
        plan.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = Producto, CreatedBy = "prueba" });
        db.RecurringBillingPlans.Add(plan);
        await db.SaveChangesAsync();
    }

    public static async Task<MemberProfile> MiembroAsync(
        AppDbContext db, string memberId, DateTime? alta = null,
        MemberAccountStatus estado = MemberAccountStatus.Active, MemberType tipo = MemberType.Ambassador,
        bool mantener = false, bool conSaldo = true, bool borrado = false)
    {
        var p = new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = memberId, Email = $"{memberId.ToLowerInvariant()}@prueba.test",
            FirstName = "Nombre", LastName = memberId, Country = "US", MemberType = tipo, Status = estado,
            EnrollDate = alta ?? new DateTime(2026, 6, 5, 14, 30, 0), KeepQualified = mantener,
            FeesFromBalance = conSaldo, IsDeleted = borrado, CreatedBy = "prueba"
        };
        db.MemberProfiles.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public static async Task<SubscriptionBillingState> EstadoAsync(
        AppDbContext db, string memberId, DateTime? vence = null,
        RecurringBillingStatus estado = RecurringBillingStatus.Active, DateTime? ultimoCobro = null)
    {
        var s = new SubscriptionBillingState
        {
            Id = "anual-" + memberId, Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = null,
            MemberId = memberId, RecurringBillingPlanId = PlanAnual, BillingAnchorDate = new DateTime(2026, 6, 5),
            NextBillingDate = vence ?? new DateTime(2027, 6, 5), NextAttemptDate = vence ?? new DateTime(2027, 6, 5),
            LastSuccessfulBillingDate = ultimoCobro, Status = estado, CreatedBy = "prueba", CreationDate = Ahora
        };
        db.SubscriptionBillingStates.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    /// <summary>Comisión pendiente y ya pagable (PaymentDate en el pasado): saldo gastable.</summary>
    public static async Task ComisionAsync(AppDbContext db, string memberId, decimal importe, DateTime? pagable = null)
    {
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = memberId, CommissionTypeId = 1, Amount = importe,
            Status = CommissionEarningStatus.Pending, EarnedDate = Ahora.AddDays(-10),
            PaymentDate = pagable ?? Ahora.AddDays(-1), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Una instancia de token canjeable (con código) y el saldo agregado de su tipo.</summary>
    public static async Task<TokenTransaction> TokenAsync(
        AppDbContext db, string memberId, int tipo = AnnualFeeRules.AnnualBizCenterTokenTypeId, string? codigo = null,
        DateTime? creado = null, TokenInstanceStatus estado = TokenInstanceStatus.Issued, DateTime? caduca = null)
    {
        var t = new TokenTransaction
        {
            MemberId = memberId, TokenTypeId = tipo, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = codigo ?? $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant(), OriginalOwnerMemberId = memberId,
            Status = estado, ExpiresAt = caduca, CreatedBy = "prueba", CreationDate = creado ?? Ahora.AddDays(-5)
        };
        db.TokenTransactions.Add(t);
        var saldo = await db.TokenBalances.FirstOrDefaultAsync(b => b.MemberId == memberId && b.TokenTypeId == tipo);
        if (saldo is null)
            db.TokenBalances.Add(new TokenBalance { MemberId = memberId, TokenTypeId = tipo, Balance = 1, CreatedBy = "prueba" });
        else
            saldo.Balance += 1;
        await db.SaveChangesAsync();
        return t;
    }
}
```

- [ ] **Step 2: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeStateEnrollmentTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Alta de los estados de cuota (Plan K, K3): uno por Ambassador no borrado y no Terminated, con el
/// primer aniversario que no haya pasado, por lotes e idempotente. Sin plan anual activo no crea nada.
/// </summary>
public class AnnualFeeStateEnrollmentTests
{
    private static AnnualFeeStateEnrollment Servicio(AppDbContext db) =>
        new(db, CuotaAnualDb.Reloj(), NullLogger<AnnualFeeStateEnrollment>.Instance);

    [Fact]
    public async Task Crea_un_estado_de_cuota_por_Ambassador_con_su_primer_vencimiento()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1", alta: new DateTime(2026, 6, 5, 14, 30, 0));
        await CuotaAnualDb.MiembroAsync(db, "AMB-2", alta: new DateTime(2025, 6, 1));

        var creados = await Servicio(db).EnsureMissingAsync(1000, 10);

        creados.Should().Be(2);
        var estados = await db.SubscriptionBillingStates.AsNoTracking().OrderBy(s => s.MemberId).ToListAsync();
        estados.Should().OnlyContain(s => s.Purpose == BillingStatePurpose.AnnualFee
                                          && s.MembershipSubscriptionId == null
                                          && s.RecurringBillingPlanId == CuotaAnualDb.PlanAnual
                                          && s.Status == RecurringBillingStatus.Active
                                          && s.CurrentAttemptIndex == 0
                                          && s.NextAttemptDate == s.NextBillingDate);
        estados[0].NextBillingDate.Should().Be(new DateTime(2027, 6, 5));   // aniversario de este año: vence
        estados[0].BillingAnchorDate.Should().Be(new DateTime(2026, 6, 5));
        estados[1].NextBillingDate.Should().Be(new DateTime(2028, 6, 1));   // 2027-06-01 ya pasó: no hacia atrás
    }

    [Fact]
    public async Task No_crea_estados_para_clientes_ni_terminados_ni_borrados()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "MBR-1", tipo: MemberType.ExternalMember);
        await CuotaAnualDb.MiembroAsync(db, "AMB-T", estado: MemberAccountStatus.Terminated);
        await CuotaAnualDb.MiembroAsync(db, "AMB-B", borrado: true);
        await CuotaAnualDb.MiembroAsync(db, "AMB-P", estado: MemberAccountStatus.Pending);
        await CuotaAnualDb.MiembroAsync(db, "AMB-S", estado: MemberAccountStatus.Suspended);

        await Servicio(db).EnsureMissingAsync(1000, 10);

        (await db.SubscriptionBillingStates.Select(s => s.MemberId).OrderBy(x => x).ToListAsync())
            .Should().Equal("AMB-P", "AMB-S");
    }

    [Fact]
    public async Task Es_idempotente()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");

        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(1);
        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(0);
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Recorre_todos_los_lotes_y_respeta_el_tope()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        for (var i = 1; i <= 5; i++) await CuotaAnualDb.MiembroAsync(db, $"AMB-{i}");

        (await Servicio(db).EnsureMissingAsync(batchSize: 2, maxBatches: 1)).Should().Be(2);
        (await Servicio(db).EnsureMissingAsync(batchSize: 2, maxBatches: 10)).Should().Be(3);
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Sin_plan_anual_activo_no_crea_nada()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db, activo: false);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");

        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(0);
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Para_un_miembro_lo_crea_si_falta_y_devuelve_el_que_hay_con_su_plan()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");

        var primero = await Servicio(db).EnsureForMemberAsync("AMB-1");
        var segundo = await Servicio(db).EnsureForMemberAsync("AMB-1");

        primero.Should().NotBeNull();
        segundo!.Id.Should().Be(primero!.Id);
        segundo.Plan!.IsAnnualFeePlan.Should().BeTrue();
        segundo.Plan.PlanProducts.Should().ContainSingle();
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Para_un_cliente_no_crea_nada()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "MBR-1", tipo: MemberType.ExternalMember);

        (await Servicio(db).EnsureForMemberAsync("MBR-1")).Should().BeNull();
    }
}
```

- [ ] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter AnnualFeeStateEnrollmentTests -nologo`
Expected: FAIL de compilación (`AnnualFeeStateEnrollment` no existe).

- [ ] **Step 4: Consultas del plan**

`MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeePlanQueries.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>Lo que cuesta la cuota y a qué producto se imputa.</summary>
public sealed record AnnualFeePrice(decimal Amount, string ProductId);

/// <summary>El plan de la cuota anual y su precio (Plan K, K2). Un solo sitio para Billing y el BizCenter.</summary>
public static class AnnualFeePlanQueries
{
    /// <summary>El plan marcado como de la cuota, activo, con sus productos. Null si no hay.</summary>
    public static Task<RecurringBillingPlan?> ActivePlanAsync(AppDbContext db, CancellationToken ct = default) =>
        db.RecurringBillingPlans
          .Include(p => p.PlanProducts)
          .FirstOrDefaultAsync(p => p.IsAnnualFeePlan && p.IsActive, ct);

    /// <summary>
    /// El importe: FixedAmountOverride del plan o, si no, el AnnualPrice de su producto (99 en local).
    /// Null si no hay producto o el importe no es positivo. Exige PlanProducts cargado.
    /// </summary>
    public static async Task<AnnualFeePrice?> PriceAsync(AppDbContext db, RecurringBillingPlan plan, CancellationToken ct = default)
    {
        var productId = plan.PlanProducts.OrderBy(pp => pp.Id).Select(pp => pp.ProductId).FirstOrDefault();
        if (productId is null) return null;

        var annualPrice = await db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => (decimal?)p.AnnualPrice)
            .FirstOrDefaultAsync(ct);

        var amount = plan.FixedAmountOverride ?? annualPrice ?? 0m;
        return amount > 0 ? new AnnualFeePrice(amount, productId) : null;
    }
}
```

- [ ] **Step 5: El servicio de alta**

`MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeStateEnrollment.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

public interface IAnnualFeeStateEnrollment
{
    /// <summary>
    /// Crea los estados de cuota que faltan, por lotes de <paramref name="batchSize"/> y como mucho
    /// <paramref name="maxBatches"/> lotes. Devuelve cuántos creó. Idempotente.
    /// </summary>
    Task<int> EnsureMissingAsync(int batchSize, int maxBatches, CancellationToken ct = default);

    /// <summary>El estado de cuota del miembro con su plan y productos; lo crea si falta. Null si no le toca.</summary>
    Task<SubscriptionBillingState?> EnsureForMemberAsync(string memberId, CancellationToken ct = default);
}

/// <summary>
/// Da de alta los estados de cuota anual (Plan K, K3): uno por Ambassador no borrado y no Terminated,
/// en el plan marcado IsAnnualFeePlan, con el primer aniversario de la inscripción que no haya pasado.
/// No migra nada del legado. UX_SubscriptionBillingStates_AnnualFeeMember impide duplicar aunque dos
/// instancias corran a la vez.
/// </summary>
public sealed class AnnualFeeStateEnrollment : IAnnualFeeStateEnrollment
{
    private const string Actor = "annual-fee-enrollment";

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AnnualFeeStateEnrollment> _logger;

    public AnnualFeeStateEnrollment(AppDbContext db, IDateTimeProvider clock, ILogger<AnnualFeeStateEnrollment> logger)
    {
        _db     = db;
        _clock  = clock;
        _logger = logger;
    }

    public async Task<int> EnsureMissingAsync(int batchSize, int maxBatches, CancellationToken ct = default)
    {
        var plan = await AnnualFeePlanQueries.ActivePlanAsync(_db, ct);
        if (plan is null)
        {
            _logger.LogWarning("Cuota anual: no hay plan activo marcado como IsAnnualFeePlan; no se crean estados.");
            return 0;
        }

        var planId  = plan.Id;
        var created = 0;

        for (var batch = 0; batch < maxBatches; batch++)
        {
            var candidates = await MissingAmbassadors()
                .OrderBy(m => m.MemberId)
                .Select(m => new { m.MemberId, m.EnrollDate })
                .Take(batchSize)
                .ToListAsync(ct);
            if (candidates.Count == 0) break;

            var now = _clock.Now;
            foreach (var c in candidates)
                _db.SubscriptionBillingStates.Add(NewState(c.MemberId, c.EnrollDate, planId, now));

            try
            {
                await _db.SaveChangesAsync(ct);
                created += candidates.Count;
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                // Otra instancia creó alguno a la vez (índice único). Se para aquí; la siguiente ejecución
                // sigue donde quede. No se reintenta en bucle: si el fallo fuera otro, se repetiría igual.
                _db.ChangeTracker.Clear();
                _logger.LogWarning(ex, "Cuota anual: un lote de alta chocó al guardar; se seguirá en la siguiente ejecución.");
                break;
            }

            _db.ChangeTracker.Clear();
            if (candidates.Count < batchSize) break;
        }

        return created;
    }

    public async Task<SubscriptionBillingState?> EnsureForMemberAsync(string memberId, CancellationToken ct = default)
    {
        var existing = await StateWithPlan().FirstOrDefaultAsync(s => s.MemberId == memberId, ct);
        if (existing is not null) return existing;

        var plan = await AnnualFeePlanQueries.ActivePlanAsync(_db, ct);
        if (plan is null) return null;

        var member = await Eligible()
            .Where(m => m.MemberId == memberId)
            .Select(m => new { m.MemberId, m.EnrollDate })
            .FirstOrDefaultAsync(ct);
        if (member is null) return null;

        var state = NewState(member.MemberId, member.EnrollDate, plan.Id, _clock.Now);
        _db.SubscriptionBillingStates.Add(state);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            // Lo creó otra petición a la vez: se usa el suyo.
            _db.ChangeTracker.Clear();
            _logger.LogInformation(ex, "Cuota anual: el estado de {MemberId} lo creó otra petición a la vez.", memberId);
        }

        return await StateWithPlan().FirstOrDefaultAsync(s => s.MemberId == memberId, ct);
    }

    private IQueryable<SubscriptionBillingState> StateWithPlan() =>
        _db.SubscriptionBillingStates
           .Include(s => s.Plan).ThenInclude(p => p!.PlanProducts)
           .Where(s => s.Purpose == BillingStatePurpose.AnnualFee);

    private IQueryable<MemberProfile> Eligible() =>
        _db.MemberProfiles.AsNoTracking()
           .Where(m => m.MemberType == MemberType.Ambassador
                    && !m.IsDeleted
                    && m.Status != MemberAccountStatus.Terminated);

    /// <summary>
    /// Ambassadors sin estado de cuota. IgnoreQueryFilters en los estados: el índice único incluye los
    /// borrados lógicos, así que uno borrado también cuenta como "ya tiene".
    /// </summary>
    private IQueryable<MemberProfile> MissingAmbassadors() =>
        Eligible().Where(m => !_db.SubscriptionBillingStates.IgnoreQueryFilters()
                                  .Any(s => s.Purpose == BillingStatePurpose.AnnualFee && s.MemberId == m.MemberId));

    private static SubscriptionBillingState NewState(string memberId, DateTime enrollDate, int planId, DateTime now)
    {
        var due = AnnualFeeSchedule.FirstDueOnCreation(enrollDate, now);
        return new SubscriptionBillingState
        {
            Purpose                   = BillingStatePurpose.AnnualFee,
            MembershipSubscriptionId  = null,
            MemberId                  = memberId,
            RecurringBillingPlanId    = planId,
            BillingAnchorDate         = enrollDate.Date,
            LastSuccessfulBillingDate = null,
            NextBillingDate           = due,
            NextAttemptDate           = due,
            CurrentAttemptIndex       = 0,
            Status                    = RecurringBillingStatus.Active,
            CreatedBy                 = Actor,
            CreationDate              = now,
            LastUpdateDate            = now
        };
    }
}
```

- [ ] **Step 6: El trabajo y el registro**

`MLMConquerorGlobalEdition.Billing/Jobs/AnnualFeeStateEnrollmentJob.cs`:

```csharp
using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Diario a las 4:30 UTC, antes del planificador (5:00): crea los estados de cuota anual que falten
/// (Plan K, K3). Tras el primer despliegue se lanza a mano desde el panel de Hangfire ("Trigger now").
/// </summary>
[Queue("billing")]
public class AnnualFeeStateEnrollmentJob
{
    private readonly IAnnualFeeStateEnrollment _enrollment;
    private readonly ILogger<AnnualFeeStateEnrollmentJob> _logger;

    public AnnualFeeStateEnrollmentJob(IAnnualFeeStateEnrollment enrollment, ILogger<AnnualFeeStateEnrollmentJob> logger)
    {
        _enrollment = enrollment;
        _logger     = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var created = await _enrollment.EnsureMissingAsync(
            AnnualFeeRules.EnrollmentBatchSize, AnnualFeeRules.EnrollmentMaxBatchesPerRun, ct);
        _logger.LogInformation("AnnualFeeStateEnrollmentJob: {Created} estados de cuota anual creados.", created);
    }
}
```

`MLMConquerorGlobalEdition.Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs`:

```csharp
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Extensions;

public static class AnnualFeeServiceCollectionExtensions
{
    /// <summary>
    /// Registra la cuota anual (Plan K). Va en un método para que la prueba de DI ejercite este mismo
    /// registro. Supone lo que Program.cs registra antes: el reloj de Billing, AddPayoutServices (el
    /// candado de fondos), AddTokenPurchaseServices (los fondos eWallet y la comprobación de step-up),
    /// ICommissionBalanceService e IRecurringBillingScheduler.
    /// </summary>
    public static IServiceCollection AddAnnualFeeServices(this IServiceCollection services)
    {
        services.AddScoped<IAnnualFeeStateEnrollment, AnnualFeeStateEnrollment>();
        services.AddScoped<AnnualFeeStateEnrollmentJob>();
        return services;
    }
}
```

En `MLMConquerorGlobalEdition.Billing/Program.cs`, justo después de `builder.Services.AddTokenPurchaseServices();`, añadir:

```csharp

// ── Cuota anual del Biz Center (Plan K) ──────────────────────────────────
// Después de AddTokenPurchaseServices: usa sus fondos eWallet y su comprobación de step-up, y el
// candado de fondos que trae AddPayoutServices.
builder.Services.AddAnnualFeeServices();
```

> **Revisado (2026-10-17).** El trabajo se registra **apagado**: con `Billing:AnnualFee:EnrollmentJobEnabled` en `false` (por defecto, sin clave en `appsettings`) se programa con `Cron.Never()`, así que no corre ni al arrancar ni de madrugada, pero sale en el panel de Hangfire y se puede lanzar a mano («Trigger now»). Con la clave en `true` se programa a las 4:30 UTC como dice abajo. Encenderlo es una decisión de despliegue, como `AutoChargeEnabled`. En esta ronda no se ejecuta contra la base local.

y, justo después del `RecurringJob.RemoveIfExists("membership-auto-renewal");` de la Tarea 1, añadir:

```csharp

// Alta de los estados de cuota anual que falten (Plan K, K3). Antes del planificador de las 5:00.
RecurringJob.AddOrUpdate<AnnualFeeStateEnrollmentJob>(
    "annual-fee-state-enrollment",
    job => job.ExecuteAsync(CancellationToken.None),
    "30 4 * * *",                   // Diario a las 4:30 UTC
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

- [ ] **Step 7: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "AnnualFeeStateEnrollmentTests|UnSoloMotorDeRenovacionTests" -nologo`
Expected: PASS.

- [ ] **Step 8: Probarlo contra la base local (sin arrancar Billing)**

El trabajo no se lanza solo hasta que Billing arranque. Para comprobar el volumen sin arrancar nada, contar lo que crearía:

```bash
sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -W -s"|" -Q "SET NOCOUNT ON;
SELECT COUNT(*) AS porCrear FROM MemberProfiles m
WHERE m.MemberType = 0 AND m.IsDeleted = 0 AND m.Status <> 4
  AND NOT EXISTS (SELECT 1 FROM SubscriptionBillingStates s WHERE s.Purpose = 2 AND s.MemberId = m.MemberId);"
```

Expected: ~119.445 (todos los Ambassadors locales). Anotar el número en la descripción del PR del bloque.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeePlanQueries.cs \
        MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeStateEnrollment.cs \
        MLMConquerorGlobalEdition.Billing/Jobs/AnnualFeeStateEnrollmentJob.cs \
        MLMConquerorGlobalEdition.Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs \
        MLMConquerorGlobalEdition.Billing/Program.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CuotaAnualDb.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeStateEnrollmentTests.cs
git commit -m "Alta de los estados de cuota anual: trabajo diario idempotente por lotes que crea un estado por Ambassador no terminado con el primer aniversario que no haya pasado, y creacion al vuelo para un miembro"
```

---

# Bloque 3 — El cobro

### Task 9: Operación de step-up de la cuota y su texto

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/StepUpDeLaCuotaTests.cs`
- Modify: `MLMConquerorGlobalEdition.SharedKernel/Security/StepUpOperations.cs`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Account/StepUp/StepUpMessages.cs`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources*.resx` (los 9)

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/StepUpDeLaCuotaTests.cs`:

```csharp
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Pagar la cuota con saldo o con un token exige step-up (Plan K, K14) con su propia operación: un
/// código confirmado para comprar tokens no vale para la cuota, y al revés.
/// </summary>
public class StepUpDeLaCuotaTests
{
    [Fact]
    public void La_operacion_de_la_cuota_esta_en_el_catalogo_cerrado()
    {
        StepUpOperations.AnnualFeeFromBalance.Should().Be("member.annual-fee.pay-from-balance");
        StepUpOperations.IsKnown(StepUpOperations.AnnualFeeFromBalance).Should().BeTrue();
    }

    [Fact]
    public void No_es_la_de_comprar_tokens()
        => StepUpOperations.AnnualFeeFromBalance.Should().NotBe(StepUpOperations.TokenPurchaseFromBalance);

    [Fact]
    public void El_dialogo_explica_por_que_pide_el_codigo()
        => StepUpMessages.WhyKeyFor(StepUpOperations.AnnualFeeFromBalance).Should().Be("StepUp.WhyAnnualFee");
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter StepUpDeLaCuotaTests -nologo`
Expected: FAIL de compilación (`AnnualFeeFromBalance` no existe).

- [ ] **Step 3: La operación**

En `MLMConquerorGlobalEdition.SharedKernel/Security/StepUpOperations.cs`, sustituir:

```csharp
    /// <summary>Pagar tokens con saldo propio: comisiones o eWallet. Tarjeta y cripto no lo piden.</summary>
    public const string TokenPurchaseFromBalance = "member.tokens.purchase-from-balance";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        PayoutMethodChange,
        TokenPurchaseFromBalance
    };
```

por:

```csharp
    /// <summary>Pagar tokens con saldo propio: comisiones o eWallet. Tarjeta y cripto no lo piden.</summary>
    public const string TokenPurchaseFromBalance = "member.tokens.purchase-from-balance";

    /// <summary>
    /// Pagar la cuota anual con lo que ya es del miembro: comisiones, eWallet o un token de cuota (Plan K).
    /// El token también: es dinero ya pagado y una sesión robada podría gastarlo.
    /// </summary>
    public const string AnnualFeeFromBalance = "member.annual-fee.pay-from-balance";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        PayoutMethodChange,
        TokenPurchaseFromBalance,
        AnnualFeeFromBalance
    };
```

- [ ] **Step 4: El texto del diálogo**

En `MLMConquerorGlobalEdition.SharedComponents/Components/Account/StepUp/StepUpMessages.cs`, sustituir:

```csharp
    public static string WhyKeyFor(string? operationKey) =>
        operationKey == StepUpOperations.TokenPurchaseFromBalance ? "StepUp.WhyTokenPurchase" : "StepUp.Why";
```

por:

```csharp
    public static string WhyKeyFor(string? operationKey) => operationKey switch
    {
        StepUpOperations.TokenPurchaseFromBalance => "StepUp.WhyTokenPurchase",
        StepUpOperations.AnnualFeeFromBalance     => "StepUp.WhyAnnualFee",
        _                                         => "StepUp.Why"
    };
```

En cada `.resx`, justo antes de `</root>`, añadir la línea de su idioma:

| Archivo | Línea |
|---|---|
| `SharedResources.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>For your security, enter a verification code before paying your annual fee.</value></data>` |
| `SharedResources.es.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>Por seguridad, ingresa un código de verificación antes de pagar tu cuota anual.</value></data>` |
| `SharedResources.pt.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>Por segurança, digite um código de verificação antes de pagar sua taxa anual.</value></data>` |
| `SharedResources.fr.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>Pour votre sécurité, saisissez un code de vérification avant de payer votre cotisation annuelle.</value></data>` |
| `SharedResources.it.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>Per sicurezza, inserisci un codice di verifica prima di pagare la quota annuale.</value></data>` |
| `SharedResources.de.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>Geben Sie zu Ihrer Sicherheit einen Bestätigungscode ein, bevor Sie Ihre Jahresgebühr bezahlen.</value></data>` |
| `SharedResources.ko.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>보안을 위해 연회비를 결제하기 전에 인증 코드를 입력하세요.</value></data>` |
| `SharedResources.zh.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>为了您的安全，支付年费前请输入验证码。</value></data>` |
| `SharedResources.ka.resx` | `<data name="StepUp.WhyAnnualFee" xml:space="preserve"><value>უსაფრთხოებისთვის, წლიური გადასახადის გადახდამდე შეიყვანეთ დამადასტურებელი კოდი.</value></data>` |

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "StepUpDeLaCuotaTests|StepUpDialogTests" -nologo`
Expected: PASS. SignupAPI emite retos solo para claves de `StepUpOperations`: la nueva queda admitida sin tocar SignupAPI.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel/Security/StepUpOperations.cs \
        MLMConquerorGlobalEdition.SharedComponents/Components/Account/StepUp/StepUpMessages.cs \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/StepUpDeLaCuotaTests.cs
git commit -m "Operacion de step-up propia para pagar la cuota anual con saldo o con un token, con su texto en el dialogo en los nueve idiomas"
```

---

### Task 10: El débito de comisiones de la cuota (`FundAnnualFeeAsync`)

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/DebitoDeComisionesDeLaCuotaTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/ICommissionBalanceService.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/CommissionBalanceService.cs` (región «Compra de tokens (Plan F)»)

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/DebitoDeComisionesDeLaCuotaTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// La cuota pagada con comisiones (Plan K, K9): un débito negativo ligado al pedido y su PaymentHistory,
/// SIN emitir tokens (FundWithCommissionBalanceAsync deja uno de regalo, R8). Comparte núcleo con la
/// compra de tokens, que sigue igual. Quien llama tiene el candado de fondos.
/// </summary>
public class DebitoDeComisionesDeLaCuotaTests
{
    private const string Miembro = "AMB-1";

    [Fact]
    public async Task Debita_la_cuota_y_deja_su_pago_sin_emitir_tokens()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await new CommissionBalanceService(db, CuotaAnualDb.Reloj()).FundAnnualFeeAsync(Miembro, 99m, "ord-1", "prueba");

        r.IsSuccess.Should().BeTrue();
        var debito = await db.CommissionEarnings.SingleAsync(e => e.Amount < 0);
        debito.Amount.Should().Be(-99m);
        debito.SourceOrderId.Should().Be("ord-1");
        debito.Notes.Should().StartWith("Annual fee debit");
        r.Value!.DebitEarningId.Should().Be(debito.Id);

        var pago = await db.PaymentHistories.SingleAsync();
        pago.Id.Should().Be(r.Value.PaymentHistoryId);
        pago.GatewayName.Should().Be("CommissionBalance");
        pago.GatewayTransactionId.Should().Be(debito.Id);
        pago.Amount.Should().Be(99m);

        (await db.TokenTransactions.CountAsync()).Should().Be(0);
        (await db.TokenBalances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_saldo_gastable_no_escribe_nada()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m, pagable: CuotaAnualDb.Ahora.AddDays(3));

        var r = await new CommissionBalanceService(db, CuotaAnualDb.Reloj()).FundAnnualFeeAsync(Miembro, 99m, "ord-1", "prueba");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(AnnualFeeErrors.InsufficientCommissionBalance);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_compra_de_tokens_sigue_debitando_con_su_nota()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await new CommissionBalanceService(db, CuotaAnualDb.Reloj()).FundTokenPurchaseAsync(Miembro, 50m, "ord-t", "prueba");

        r.IsSuccess.Should().BeTrue();
        (await db.CommissionEarnings.SingleAsync(e => e.Amount < 0)).Notes.Should().StartWith("Token purchase debit");
        (await db.PaymentHistories.SingleAsync()).Id.Should().Be(r.Value);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter DebitoDeComisionesDeLaCuotaTests -nologo`
Expected: FAIL de compilación (`FundAnnualFeeAsync` y `CommissionDebit` no existen).

- [ ] **Step 3: La interfaz**

En `ICommissionBalanceService.cs`, justo después de la declaración de `FundTokenPurchaseAsync`, añadir:

```csharp

    /// <summary>
    /// Paga la cuota anual con comisiones (Plan K, K9): consolida el residual si toca, escribe el débito
    /// negativo ligado al pedido y el PaymentHistory, en un solo guardado. NO emite tokens ni toca el
    /// pedido ni el pago de cuota: eso lo hace quien cobra, en la misma transacción y bajo el candado de
    /// fondos del miembro. Falla sin escribir nada si el saldo gastable no alcanza.
    /// </summary>
    Task<Result<CommissionDebit>> FundAnnualFeeAsync(
        string memberId, decimal amount, string orderId, string actor, CancellationToken ct = default);
```

y, al final del archivo, añadir:

```csharp

/// <summary>Lo que deja un débito de comisiones: su PaymentHistory y la fila negativa de CommissionEarnings.</summary>
public sealed record CommissionDebit(string PaymentHistoryId, string DebitEarningId);
```

- [ ] **Step 4: Un núcleo para los dos débitos**

En `CommissionBalanceService.cs`, sustituir el método `FundTokenPurchaseAsync` entero por:

```csharp
    public async Task<Result<string>> FundTokenPurchaseAsync(
        string memberId, decimal amount, string orderId, string actor, CancellationToken ct = default)
    {
        var debit = await FundOrderFromCommissionsAsync(memberId, amount, orderId, actor,
            consolidationComment: $"Consolidated to fund token purchase order #{orderId}",
            debitNote: $"Token purchase debit — {amount:F2} — Order {orderId}",
            ct);

        return debit.IsSuccess
            ? Result<string>.Success(debit.Value!.PaymentHistoryId)
            : Result<string>.Failure(debit.ErrorCode!, debit.Error!);
    }

    // ── Cuota anual (Plan K) ──────────────────────────────────────────────────

    public Task<Result<CommissionDebit>> FundAnnualFeeAsync(
        string memberId, decimal amount, string orderId, string actor, CancellationToken ct = default) =>
        FundOrderFromCommissionsAsync(memberId, amount, orderId, actor,
            consolidationComment: $"Consolidated to fund annual fee order #{orderId}",
            debitNote: $"Annual fee debit — {amount:F2} — Order {orderId}",
            ct);

    /// <summary>
    /// Débito de comisiones para un pedido: comprueba el saldo gastable, consolida el residual si toca y
    /// escribe el débito negativo y el PaymentHistory juntos. Lo comparten la compra de tokens y la cuota.
    /// </summary>
    private async Task<Result<CommissionDebit>> FundOrderFromCommissionsAsync(
        string memberId, decimal amount, string orderId, string actor,
        string consolidationComment, string debitNote, CancellationToken ct)
    {
        if (amount <= 0)
            return Result<CommissionDebit>.Failure("INVALID_AMOUNT", "Amount must be positive.");

        var spendable = await GetSpendableAsync(memberId, ct);
        if (spendable < amount)
            return Result<CommissionDebit>.Failure(TokenPurchaseErrors.InsufficientCommissionBalance,
                $"Commission balance {spendable:F2} is below {amount:F2}.");

        var now = _dateTime.Now;

        await ConsolidateCoreAsync(memberId, actor, paymentComment: consolidationComment, ct);

        var debit = new CommissionEarning
        {
            BeneficiaryMemberId = memberId,
            CommissionTypeId    = await GetDailyResidualCommissionTypeIdAsync(ct),
            Amount              = -amount,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = now,
            PaymentDate         = now,
            SourceOrderId       = orderId,
            Notes               = debitNote,
            IsManualEntry       = false,
            CreatedBy           = actor,
            CreationDate        = now,
            LastUpdateDate      = now
        };
        _db.CommissionEarnings.Add(debit);

        // El Id del débito ya existe (lo asigna la entidad al construirse), así que el pago puede
        // apuntarle sin un SaveChanges intermedio: débito y pago se escriben juntos.
        var payment = new PaymentHistory
        {
            OrderId              = orderId,
            MemberId             = memberId,
            Amount               = amount,
            GatewayName          = "CommissionBalance",
            GatewayTransactionId = debit.Id,
            TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt          = now,
            CreationDate         = now,
            CreatedBy            = actor,
            LastUpdateDate       = now,
            LastUpdateBy         = actor
        };
        _db.PaymentHistories.Add(payment);

        await _db.SaveChangesAsync(ct);
        return Result<CommissionDebit>.Success(new CommissionDebit(payment.Id, debit.Id));
    }
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "DebitoDeComisionesDeLaCuotaTests|SaldoParaComprarTokensTests|CommissionBalanceServiceTests|TokenPurchaseServiceTests" -nologo`
Expected: PASS (la compra de tokens no cambia).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/Recurring/ICommissionBalanceService.cs \
        MLMConquerorGlobalEdition.Billing/Services/Recurring/CommissionBalanceService.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/DebitoDeComisionesDeLaCuotaTests.cs
git commit -m "Debito de comisiones para la cuota anual: debito negativo y PaymentHistory en un solo guardado sin emitir tokens, con un nucleo compartido con la compra de tokens"
```

---

### Task 11: El token que paga la cuota

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeTokenQueries.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeTokenConsumer.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeTokenConsumerTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeTokenConsumerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Qué token paga la cuota (Plan K, K8): una instancia con código, del miembro, viva y sin caducar, de
/// los tipos 23 ("Annual: Biz Center") y 10 ("Annual Fee"), el 23 antes y el más antiguo primero.
/// Gastarla la marca Used ligada al pedido, deja el asiento del ledger y baja el saldo agregado.
/// </summary>
public class AnnualFeeTokenConsumerTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Ahora = CuotaAnualDb.Ahora;

    [Fact]
    public async Task Prefiere_el_tipo_23_aunque_el_10_sea_mas_antiguo()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, AnnualFeeRules.AnnualFeeTokenTypeId, "DIEZ", creado: Ahora.AddDays(-30));
        await CuotaAnualDb.TokenAsync(db, Miembro, AnnualFeeRules.AnnualBizCenterTokenTypeId, "VEINTITRES", creado: Ahora.AddDays(-1));

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora))!.ReferenceId.Should().Be("VEINTITRES");
    }

    [Fact]
    public async Task Dentro_del_mismo_tipo_el_mas_antiguo()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "NUEVO", creado: Ahora.AddDays(-1));
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "VIEJO", creado: Ahora.AddDays(-20));

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora))!.ReferenceId.Should().Be("VIEJO");
    }

    [Fact]
    public async Task No_vale_un_token_usado_anulado_caducado_de_otro_tipo_o_de_otro_miembro()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "USADO", estado: TokenInstanceStatus.Used);
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "ANULADO", estado: TokenInstanceStatus.Voided);
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "CADUCADO", caduca: Ahora.AddDays(-1));
        await CuotaAnualDb.TokenAsync(db, Miembro, tipo: 24, codigo: "LEGADO");
        await CuotaAnualDb.TokenAsync(db, Miembro, tipo: 103, codigo: "PLAN");
        await CuotaAnualDb.TokenAsync(db, "AMB-2", codigo: "AJENO");

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora)).Should().BeNull();
        (await AnnualFeeTokenQueries.Available(db, Miembro, Ahora).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_token_distribuido_al_miembro_tambien_paga_su_cuota()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "REGALADO", estado: TokenInstanceStatus.Distributed);

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora))!.ReferenceId.Should().Be("REGALADO");
    }

    [Fact]
    public async Task Gastarlo_lo_marca_usado_deja_el_ledger_y_baja_el_saldo()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "PAGA");
        var consumidor = new AnnualFeeTokenConsumer(db);

        var instancia = (await consumidor.FindAsync(Miembro, Ahora))!;
        var gastado = await consumidor.ConsumeAsync(instancia, "ord-1", "prueba", Ahora);
        await db.SaveChangesAsync();

        gastado.Code.Should().Be("PAGA");
        gastado.TokenTransactionId.Should().Be(instancia.Id);
        gastado.TokenTypeId.Should().Be(AnnualFeeRules.AnnualBizCenterTokenTypeId);

        var fila = await db.TokenTransactions.AsNoTracking().SingleAsync(t => t.ReferenceId == "PAGA");
        fila.Status.Should().Be(TokenInstanceStatus.Used);
        fila.UsedOnOrderId.Should().Be("ord-1");
        fila.UsedByMemberId.Should().Be(Miembro);
        fila.UsedAt.Should().Be(Ahora);

        (await db.TokenTransactions.CountAsync(t => t.ReferenceId == null && t.TransactionType == TokenTransactionType.Used))
            .Should().Be(1);
        (await db.TokenBalances.SingleAsync()).Balance.Should().Be(0);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter AnnualFeeTokenConsumerTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: La consulta compartida**

`MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeTokenQueries.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// Los tokens que pueden pagar la cuota de un miembro (Plan K, K8). La usan el BizCenter (para enseñar
/// cuántos tiene) y Billing (para gastar uno), así que los dos ven lo mismo.
/// </summary>
public static class AnnualFeeTokenQueries
{
    /// <summary>
    /// Instancias canjeables (con código) del miembro, de los tipos de cuota, vivas (Issued o Distributed:
    /// las mismas que MemberTokenQueries cuenta como disponibles) y sin caducar. Con seguimiento: quien
    /// gasta la modifica. Para contar, basta con CountAsync.
    /// </summary>
    public static IQueryable<TokenTransaction> Available(AppDbContext db, string memberId, DateTime now)
    {
        var types = AnnualFeeRules.FeeTokenTypeIds.ToArray();
        return db.TokenTransactions.Where(t =>
            t.MemberId == memberId
            && t.ReferenceId != null
            && types.Contains(t.TokenTypeId)
            && (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
            && (t.ExpiresAt == null || t.ExpiresAt > now));
    }

    /// <summary>El orden en que se gastan: "Annual: Biz Center" antes que "Annual Fee"; dentro, el más antiguo.</summary>
    public static IQueryable<TokenTransaction> InSpendingOrder(this IQueryable<TokenTransaction> query) =>
        query.OrderBy(t => t.TokenTypeId == AnnualFeeRules.AnnualBizCenterTokenTypeId ? 0 : 1)
             .ThenBy(t => t.CreationDate)
             .ThenBy(t => t.Id);
}
```

- [ ] **Step 4: El consumidor**

`MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeTokenConsumer.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>El token gastado: la instancia (con su id de siempre), su código y su tipo.</summary>
public sealed record ConsumedFeeToken(long TokenTransactionId, string Code, int TokenTypeId);

public interface IAnnualFeeTokenConsumer
{
    /// <summary>El token de cuota que se gastaría ahora (con seguimiento), o null.</summary>
    Task<TokenTransaction?> FindAsync(string memberId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Lo gasta para el pedido: Used, asiento del ledger y saldo agregado. NO guarda: quien cobra lo hace
    /// en la misma transacción que el pedido, el pago y el estado, bajo el candado del miembro.
    /// </summary>
    Task<ConsumedFeeToken> ConsumeAsync(
        TokenTransaction instance, string orderId, string actor, DateTime now, CancellationToken ct = default);
}

/// <summary>
/// Gasta un token de cuota (Plan K, K8). Igual que el canje en el alta (TokenRedemptionService): la
/// instancia queda Used con el pedido, un asiento sin código registra el uso y el saldo agregado baja.
/// Distribuir y transferir tokens toman el mismo candado del miembro, así que la instancia no cambia de
/// dueño entre leerla y gastarla.
/// </summary>
public sealed class AnnualFeeTokenConsumer : IAnnualFeeTokenConsumer
{
    private readonly AppDbContext _db;

    public AnnualFeeTokenConsumer(AppDbContext db) => _db = db;

    public Task<TokenTransaction?> FindAsync(string memberId, DateTime now, CancellationToken ct = default) =>
        AnnualFeeTokenQueries.Available(_db, memberId, now).InSpendingOrder().FirstOrDefaultAsync(ct);

    public async Task<ConsumedFeeToken> ConsumeAsync(
        TokenTransaction instance, string orderId, string actor, DateTime now, CancellationToken ct = default)
    {
        var owner = instance.MemberId;
        var code  = instance.ReferenceId!;

        instance.Status         = TokenInstanceStatus.Used;
        instance.UsedByMemberId = owner;
        instance.UsedAt         = now;
        instance.UsedOnOrderId  = orderId;

        // Asiento puro (sin código): no aparece como instancia canjeable.
        _db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId        = owner,
            TokenTypeId     = instance.TokenTypeId,
            TransactionType = TokenTransactionType.Used,
            Quantity        = 1,
            UsedByMemberId  = owner,
            UsedAt          = now,
            UsedOnOrderId   = orderId,
            Status          = TokenInstanceStatus.Used,
            ReferenceId     = null,
            CreatedBy       = actor,
            CreationDate    = now,
            Notes           = $"Token {code} used for the Biz Center annual fee (order {orderId})"
        });

        var balance = await _db.TokenBalances
            .FirstOrDefaultAsync(b => b.MemberId == owner && b.TokenTypeId == instance.TokenTypeId, ct);
        if (balance is not null && balance.Balance > 0)
        {
            balance.Balance       -= 1;
            balance.LastUpdateDate = now;
            balance.LastUpdateBy   = actor;
        }

        return new ConsumedFeeToken(instance.Id, code, instance.TokenTypeId);
    }
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter AnnualFeeTokenConsumerTests -nologo`
Expected: PASS (5 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeTokenQueries.cs \
        MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeTokenConsumer.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/AnnualFeeTokenConsumerTests.cs
git commit -m "Token que paga la cuota anual: instancias vivas de los tipos 23 y 10 del miembro, el 23 primero y el mas antiguo; gastarlo lo marca usado con el pedido, deja el asiento y baja el saldo"
```

---

### Task 12: Cobro automático: exclusiones, token, comisiones y fallo con cadencia

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeePaymentQueries.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/IAnnualFeeChargeService.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CobroAutomaticoDeCuotaAnualTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CobroAutomaticoDeCuotaAnualTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// El cobro automático de la cuota (Plan K): apagado por defecto (K13); exclusiones del legado (K5);
/// token siempre, comisiones solo con la preferencia (K6, K7); sin fondos, la cadencia del plan y al
/// final Stopped, sin tocar la membresía (K12); todo bajo el candado del miembro.
/// </summary>
public class CobroAutomaticoDeCuotaAnualTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Ahora = CuotaAnualDb.Ahora;
    private static readonly DateTime Vence = new(2027, 6, 5);

    internal static AnnualFeeChargeService Servicio(
        AppDbContext db, bool encendido = true, IMemberFundsGate? candado = null, Mock<IEWalletFunds>? ewallet = null)
    {
        var reloj  = CuotaAnualDb.Reloj();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AnnualFeeRules.AutoChargeEnabledConfigKey] = encendido ? "true" : "false"
        }).Build();

        return new AnnualFeeChargeService(
            db, reloj, candado ?? new TransactionOnlyGate(db), new CommissionBalanceService(db, reloj),
            (ewallet ?? new Mock<IEWalletFunds>()).Object, new AnnualFeeTokenConsumer(db),
            new AnnualFeeStateEnrollment(db, reloj, NullLogger<AnnualFeeStateEnrollment>.Instance),
            new RecurringBillingScheduler(), config, NullLogger<AnnualFeeChargeService>.Instance);
    }

    private static async Task<AppDbContext> ConMiembroAsync(
        bool conSaldo = true, MemberAccountStatus estado = MemberAccountStatus.Active, bool mantener = false,
        DateTime? vence = null, RecurringBillingStatus estadoCobro = RecurringBillingStatus.Active,
        DateTime? ultimoCobro = null, DateTime? alta = null)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, alta: alta, estado: estado, mantener: mantener, conSaldo: conSaldo);
        await CuotaAnualDb.EstadoAsync(db, Miembro, vence ?? Vence, estadoCobro, ultimoCobro);
        return db;
    }

    private static Task<SubscriptionBillingState> EstadoAsync(AppDbContext db) =>
        db.SubscriptionBillingStates.AsNoTracking().SingleAsync();

    [Fact]
    public async Task Con_el_interruptor_apagado_no_hace_nada()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db, encendido: false).ChargeDueAsync("anual-" + Miembro, force: false);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.AutoChargeDisabled);
        (await db.Orders.CountAsync()).Should().Be(0);
        (await EstadoAsync(db)).NextAttemptDate.Should().Be(Vence);
    }

    [Fact]
    public async Task Con_un_token_de_cuota_paga_aunque_la_preferencia_este_apagada()
    {
        await using var db = await ConMiembroAsync(conSaldo: false);
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "CUOTA-1");
        var candado = new TransactionOnlyGate(db);

        var r = await Servicio(db, candado: candado).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        r.Value.FundingSource.Should().Be(AnnualFeeMethods.Token);
        candado.Entered.Should().Contain(Miembro);

        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        pago.Method.Should().Be(AnnualFeePaymentMethod.Token);
        pago.Source.Should().Be(AnnualFeePaymentSource.Automatic);
        pago.Amount.Should().Be(0m);
        pago.TokenCode.Should().Be("CUOTA-1");
        pago.CycleDueDate.Should().Be(Vence);
        pago.IdempotencyKey.Should().BeNull();

        var pedido = await db.Orders.SingleAsync();
        pedido.Status.Should().Be(OrderStatus.Paid);
        pedido.TotalAmount.Should().Be(0m);
        pedido.MembershipSubscriptionId.Should().BeNull();
        (await db.OrderDetails.CountAsync()).Should().Be(0);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);

        (await db.TokenTransactions.SingleAsync(t => t.ReferenceId == "CUOTA-1")).UsedOnOrderId.Should().Be(pedido.Id);

        var estado = await EstadoAsync(db);
        // K4 revisado: el ciclo vencía el 2027-06-05 y se paga el 2027-06-10 (tarde): un año desde el pago.
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 10));
        estado.NextAttemptDate.Should().Be(new DateTime(2028, 6, 10));
        estado.LastSuccessfulBillingDate.Should().Be(Ahora);
        estado.Status.Should().Be(RecurringBillingStatus.Active);

        var intento = await db.RecurringBillingAttempts.SingleAsync();
        intento.FundingSource.Should().Be(RecurringFundingSource.Token);
        intento.Outcome.Should().Be(RecurringAttemptOutcome.Success);
        intento.ProductId.Should().Be(CuotaAnualDb.Producto);
    }

    [Fact]
    public async Task Sin_token_y_con_la_preferencia_paga_con_comisiones()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        r.Value.FundingSource.Should().Be(AnnualFeeMethods.CommissionBalance);

        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Method.Should().Be(AnnualFeePaymentMethod.CommissionBalance);
        pago.Amount.Should().Be(99m);
        pago.PaymentHistoryId.Should().Be(r.Value.PaymentHistoryId);

        (await db.Orders.SingleAsync()).TotalAmount.Should().Be(99m);
        (await db.CommissionEarnings.SingleAsync(e => e.Amount < 0)).Amount.Should().Be(-99m);
        (await db.RecurringBillingAttempts.SingleAsync()).CommissionDeductionEarningId.Should().NotBeNull();
        (await EstadoAsync(db)).NextBillingDate.Should().Be(new DateTime(2028, 6, 10)); // K4 revisado: tarde
    }

    [Fact]
    public async Task Con_la_preferencia_apagada_no_toca_las_comisiones_y_aplica_la_cadencia()
    {
        await using var db = await ConMiembroAsync(conSaldo: false);
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.NoFunds);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);

        var estado = await EstadoAsync(db);
        estado.Status.Should().Be(RecurringBillingStatus.Retrying);
        estado.CurrentAttemptIndex.Should().Be(1);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.NextBillingDate.Should().Be(Vence);

        var intento = await db.RecurringBillingAttempts.SingleAsync();
        intento.Outcome.Should().Be(RecurringAttemptOutcome.Failed);
        intento.FundingSource.Should().Be(RecurringFundingSource.Token);
    }

    [Fact]
    public async Task Agotada_la_cadencia_para_el_estado_sin_tocar_la_membresia_ni_la_cuenta()
    {
        await using var db = await ConMiembroAsync(estadoCobro: RecurringBillingStatus.Retrying);
        var estado = await db.SubscriptionBillingStates.SingleAsync();
        estado.CurrentAttemptIndex = 7; // la cadencia del plan anual tiene 7 reintentos
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-ta", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-10), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        (await EstadoAsync(db)).Status.Should().Be(RecurringBillingStatus.Stopped);
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Active);
        (await db.MemberProfiles.AsNoTracking().SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);
    }

    [Fact]
    public async Task Una_cuenta_terminada_para_el_estado()
    {
        await using var db = await ConMiembroAsync(estado: MemberAccountStatus.Terminated);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be("ACCOUNT_CLOSED");
        (await EstadoAsync(db)).Status.Should().Be(RecurringBillingStatus.Stopped);
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(MemberAccountStatus.Suspended, false, "ACCOUNT_SUSPENDED")]
    [InlineData(MemberAccountStatus.Pending, false, "ACCOUNT_PENDING")]
    [InlineData(MemberAccountStatus.Active, true, "KEEP_QUALIFIED")]
    public async Task Suspendida_pendiente_o_KeepQualified_se_mira_manana_sin_cobrar_ni_avanzar(
        MemberAccountStatus estadoCuenta, bool mantener, string razon)
    {
        await using var db = await ConMiembroAsync(estado: estadoCuenta, mantener: mantener);
        await CuotaAnualDb.TokenAsync(db, Miembro);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.FailureReason.Should().Be(razon);
        var estado = await EstadoAsync(db);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.NextBillingDate.Should().Be(Vence);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);
        (await AnnualFeeTokenQueriesCount(db)).Should().Be(1);
    }

    private static Task<int> AnnualFeeTokenQueriesCount(AppDbContext db) =>
        MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeeTokenQueries.Available(db, Miembro, Ahora).CountAsync();

    [Fact]
    public async Task Cobrada_hace_poco_se_reprograma_a_un_anio_del_cobro()
    {
        await using var db = await ConMiembroAsync(ultimoCobro: new DateTime(2027, 2, 1));
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.FailureReason.Should().Be("RECENTLY_BILLED");
        (await EstadoAsync(db)).NextBillingDate.Should().Be(new DateTime(2028, 2, 1));
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Si_aun_no_vence_se_reprograma_a_su_vencimiento()
    {
        await using var db = await ConMiembroAsync(vence: new DateTime(2027, 6, 20));
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.FailureReason.Should().Be("NOT_DUE");
        (await EstadoAsync(db)).NextAttemptDate.Should().Be(new DateTime(2027, 6, 20));
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_estado_parado_no_se_cobra_y_forzado_se_revive_y_se_cobra()
    {
        await using var db = await ConMiembroAsync(estadoCobro: RecurringBillingStatus.Stopped);
        await CuotaAnualDb.TokenAsync(db, Miembro);

        (await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false)).Value!.Outcome
            .Should().Be(AnnualFeeOutcomes.Skipped);

        var forzado = await Servicio(db, encendido: false).ChargeDueAsync("anual-" + Miembro, force: true);

        forzado.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        (await EstadoAsync(db)).Status.Should().Be(RecurringBillingStatus.Active);
    }

    [Fact]
    public async Task Un_ciclo_con_un_pago_vivo_no_se_cobra_otra_vez()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);
        db.AnnualFeePayments.Add(new AnnualFeePayment
        {
            MemberId = Miembro, SubscriptionBillingStateId = "anual-" + Miembro, CycleDueDate = Vence, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.Automatic,
            Status = AnnualFeePaymentStatus.NeedsReconciliation, OrderId = "ord-antes", CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.NeedsReview);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_el_candado_ocupado_no_mueve_nada()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db, candado: new BusyGate()).ChargeDueAsync("anual-" + Miembro, force: false);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(AnnualFeeErrors.Busy);
        (await db.Orders.CountAsync()).Should().Be(0);
        (await EstadoAsync(db)).NextAttemptDate.Should().Be(Vence);
    }

    [Fact]
    public async Task Un_estado_de_membresia_no_lo_cobra_este_servicio()
    {
        await using var db = await ConMiembroAsync();
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "membresia", Purpose = BillingStatePurpose.Membership, MembershipSubscriptionId = "sub-1",
            MemberId = Miembro, RecurringBillingPlanId = CuotaAnualDb.PlanAnual, NextBillingDate = Vence,
            NextAttemptDate = Vence, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("membresia", force: false);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(AnnualFeeErrors.StateNotFound);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter CobroAutomaticoDeCuotaAnualTests -nologo`
Expected: FAIL de compilación (`AnnualFeeChargeService`, `AnnualFeeOutcomes` no existen).

- [ ] **Step 3: Consulta de pagos vivos**

`MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeePaymentQueries.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>Los pagos de cuota que ocupan un ciclo (Plan K, K10). La misma regla que UX_AnnualFeePayments_LivePerCycle.</summary>
public static class AnnualFeePaymentQueries
{
    public static IQueryable<AnnualFeePayment> LiveForCycle(AppDbContext db, string billingStateId, DateTime cycleDue)
    {
        var cycle = cycleDue.Date;
        return db.AnnualFeePayments.Where(p =>
            p.SubscriptionBillingStateId == billingStateId
            && p.CycleDueDate == cycle
            && (p.Status == AnnualFeePaymentStatus.Pending
                || p.Status == AnnualFeePaymentStatus.Completed
                || p.Status == AnnualFeePaymentStatus.NeedsReconciliation));
    }
}
```

- [ ] **Step 4: La interfaz**

`MLMConquerorGlobalEdition.Billing/Services/AnnualFee/IAnnualFeeChargeService.cs`:

```csharp
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>Resultados del cobro automático, con los mismos nombres que RecurringBillingProcessorResult.Outcome.</summary>
public static class AnnualFeeOutcomes
{
    public const string Success = "Success";
    public const string Failed  = "Failed";
    public const string Skipped = "Skipped";
}

/// <summary>Lo que devuelve el cobro automático al motor.</summary>
public sealed record AnnualFeeChargeOutcome(
    string  Outcome,
    string? FundingSource    = null,
    string? OrderId          = null,
    string? PaymentHistoryId = null,
    string? FailureReason    = null);

/// <summary>El cobro de la cuota anual del Biz Center (Plan K).</summary>
public interface IAnnualFeeChargeService
{
    /// <summary>
    /// El cobro automático de un estado de cuota anual. Lo llama RecurringBillingProcessor cuando le
    /// llega un estado con Purpose = AnnualFee. <paramref name="force"/>: el bill-now de soporte.
    /// </summary>
    Task<Result<AnnualFeeChargeOutcome>> ChargeDueAsync(string billingStateId, bool force, CancellationToken ct = default);
}
```

- [ ] **Step 5: El servicio**

`MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>
/// El cobro de la cuota anual (Plan K). Tres formas de pagar, cada una bajo el candado de fondos del
/// miembro y con un AnnualFeePayment que ocupa el ciclo (UX_AnnualFeePayments_LivePerCycle):
///
///   • TOKEN: una transacción. Se gasta el token, se crea el pedido de 0, se completa el pago y avanza
///     el estado. Siempre que haya uno (K6).
///   • COMISIONES: una transacción. Débito + PaymentHistory (FundAnnualFeeAsync), pedido de 99 y estado.
///     En automático, solo con la preferencia FeesFromBalance (K7).
///   • EWALLET: dos fases, como la compra de tokens (archivo AnnualFeeChargeService.EWallet.cs).
///
/// Bajo el candado se relee el estado y se comprueba que el ciclo no esté ya pagado, en curso o en
/// revisión: un "Pagar ahora" y el motor no pueden cobrar el mismo ciclo. Un fallo sin fondos aplica la
/// cadencia del plan; nunca toca la membresía (K12).
/// </summary>
public sealed partial class AnnualFeeChargeService : IAnnualFeeChargeService
{
    private const string AutoActor = "annual-fee-engine";

    /// <summary>Interno: la clave de "Pagar ahora" ya tiene un pago; lo contesta la repetición.</summary>
    private const string ReplayCode = "ANNUAL_FEE_REPLAY";

    private readonly AppDbContext                    _db;
    private readonly IDateTimeProvider               _clock;
    private readonly IMemberFundsGate                _gate;
    private readonly ICommissionBalanceService       _commissions;
    private readonly IEWalletFunds                   _eWallet;
    private readonly IAnnualFeeTokenConsumer         _tokens;
    private readonly IAnnualFeeStateEnrollment       _enrollment;
    private readonly IRecurringBillingScheduler      _scheduler;
    private readonly IConfiguration                  _config;
    private readonly ILogger<AnnualFeeChargeService> _logger;

    public AnnualFeeChargeService(
        AppDbContext db, IDateTimeProvider clock, IMemberFundsGate gate, ICommissionBalanceService commissions,
        IEWalletFunds eWallet, IAnnualFeeTokenConsumer tokens, IAnnualFeeStateEnrollment enrollment,
        IRecurringBillingScheduler scheduler, IConfiguration config, ILogger<AnnualFeeChargeService> logger)
    {
        _db          = db;
        _clock       = clock;
        _gate        = gate;
        _commissions = commissions;
        _eWallet     = eWallet;
        _tokens      = tokens;
        _enrollment  = enrollment;
        _scheduler   = scheduler;
        _config      = config;
        _logger      = logger;
    }

    /// <summary>Se lee en cada llamada: apagarlo no exige reiniciar Billing (K13).</summary>
    private bool AutoChargeEnabled => _config.GetValue(AnnualFeeRules.AutoChargeEnabledConfigKey, false);

    // ── Cobro automático ──────────────────────────────────────────────────────

    public async Task<Result<AnnualFeeChargeOutcome>> ChargeDueAsync(string billingStateId, bool force, CancellationToken ct = default)
    {
        if (!force && !AutoChargeEnabled)
            return Skipped(AnnualFeeErrors.AutoChargeDisabled);

        var state = await _db.SubscriptionBillingStates
            .Include(s => s.Plan).ThenInclude(p => p!.PlanProducts)
            .FirstOrDefaultAsync(s => s.Id == billingStateId, ct);
        if (state is null || state.Purpose != BillingStatePurpose.AnnualFee || state.Plan is not { IsAnnualFeePlan: true })
            return Result<AnnualFeeChargeOutcome>.Failure(AnnualFeeErrors.StateNotFound,
                $"Annual-fee billing state '{billingStateId}' not found.");

        if (state.Status == RecurringBillingStatus.Stopped && !force)
            return Skipped("Billing state is Stopped.");

        var now    = _clock.Now;
        var member = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == state.MemberId, ct);

        var decision = AnnualFeeEligibility.ForAutomaticCharge(
            await AnnualFeeMemberQueries.FactsAsync(_db, state.MemberId, ct), state.NextBillingDate, state.LastSuccessfulBillingDate, now, force);
        if (decision.Kind != AnnualFeeDecisionKind.Charge)
        {
            ApplyDecision(state, decision, now);
            await _db.SaveChangesAsync(ct);
            return Skipped(decision.Reason);
        }

        var price = await AnnualFeePlanQueries.PriceAsync(_db, state.Plan, ct);
        if (price is null)
            return Result<AnnualFeeChargeOutcome>.Failure(AnnualFeeErrors.PriceNotSet, "The annual-fee plan has no price.");

        if (force && state.Status == RecurringBillingStatus.Stopped)
        {
            // Como el procesador: forzar revive un estado parado. Se guarda ya, porque bajo el candado el
            // estado se relee de la base y un cambio sin guardar se perdería.
            state.Status              = RecurringBillingStatus.Active;
            state.CurrentAttemptIndex = 0;
            state.LastUpdateDate      = now;
            state.LastUpdateBy        = AutoActor;
            await _db.SaveChangesAsync(ct);
        }

        var rule = force ? DueRule.Any : DueRule.Due;
        try
        {
            // 1. Un token de cuota, si lo hay: siempre (K6).
            var settled = await SettleWithTokenAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);

            // 2. Comisiones, solo con la preferencia (K7).
            if (settled.Kind == SettleKind.NotPossible && member!.FeesFromBalance)
                settled = await SettleWithCommissionsAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);

            if (settled.Kind != SettleKind.NotPossible)
                return Outcome(settled);
        }
        catch (MemberFundsBusyException)
        {
            return Result<AnnualFeeChargeOutcome>.Failure(AnnualFeeErrors.Busy,
                $"Funds of member {state.MemberId} are busy; the annual fee will be retried on the next pass.");
        }

        return await RecordNoFundsAsync(state, price, member!, ct);
    }

    // ── Formas de pago (bajo el candado) ─────────────────────────────────────

    private enum DueRule { Due, PayAhead, Any }

    private enum SettleKind { Paid, NotPossible, Blocked, Review }

    private sealed record Settled(
        SettleKind Kind, string? Code = null, AnnualFeePayment? Payment = null, Orders? Order = null, string? FundingSource = null)
    {
        public static Settled Paid(AnnualFeePayment p, Orders o, string source)   => new(SettleKind.Paid, null, p, o, source);
        public static Settled NotPossible(string code)                             => new(SettleKind.NotPossible, code);
        public static Settled Blocked(string code)                                 => new(SettleKind.Blocked, code);
        public static Settled Review(AnnualFeePayment p, Orders o, string source) => new(SettleKind.Review, AnnualFeeErrors.NeedsReview, p, o, source);
    }

    private async Task<Settled> SettleWithTokenAsync(
        SubscriptionBillingState state, AnnualFeePrice price, AnnualFeePaymentSource source, string? key,
        string actor, DueRule rule, CancellationToken ct)
    {
        var now = _clock.Now;
        await using var tx = await _gate.EnterAsync(state.MemberId, ct);

        var blocked = await BlockedUnderGateAsync(state, key, rule, now, ct);
        if (blocked is not null)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Settled.Blocked(blocked);
        }

        var instance = await _tokens.FindAsync(state.MemberId, now, ct);
        if (instance is null)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Settled.NotPossible(AnnualFeeErrors.NoFeeToken);
        }

        var cycle   = state.NextBillingDate.Date;
        var order   = NewOrder(state.MemberId, 0m, cycle, actor, now);
        var payment = NewPayment(state, cycle, 0m, AnnualFeePaymentMethod.Token, source, key, order, actor, now);
        var token   = await _tokens.ConsumeAsync(instance, order.Id, actor, now, ct);

        order.Notes = $"{order.Notes} — paid with token {token.Code}";
        payment.CompleteWithToken(token.TokenTransactionId, token.Code, now, actor);
        MarkPaid(order, actor, now);
        AdvanceOnSuccess(state, cycle, now, now, actor);
        AddAttempt(state, price.ProductId, RecurringFundingSource.Token, RecurringAttemptOutcome.Success, 0m,
            order.Id, paymentHistoryId: null, token.TokenTransactionId, commissionDebitId: null, failureReason: null, now, actor);

        return await CommitOrBlockAsync(tx, Settled.Paid(payment, order, AnnualFeeMethods.Token), key, ct);
    }

    private async Task<Settled> SettleWithCommissionsAsync(
        SubscriptionBillingState state, AnnualFeePrice price, AnnualFeePaymentSource source, string? key,
        string actor, DueRule rule, CancellationToken ct)
    {
        var now = _clock.Now;
        await using var tx = await _gate.EnterAsync(state.MemberId, ct);

        var blocked = await BlockedUnderGateAsync(state, key, rule, now, ct);
        if (blocked is not null)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Settled.Blocked(blocked);
        }

        var spendable = await _commissions.GetSpendableAsync(state.MemberId, ct);
        if (spendable < price.Amount)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Settled.NotPossible(AnnualFeeErrors.InsufficientCommissionBalance);
        }

        var cycle   = state.NextBillingDate.Date;
        var order   = NewOrder(state.MemberId, price.Amount, cycle, actor, now);
        var payment = NewPayment(state, cycle, price.Amount, AnnualFeePaymentMethod.CommissionBalance, source, key, order, actor, now);

        Result<CommissionDebit> fund;
        try
        {
            // Guarda a la vez el pedido, el pago (aún Pending), el débito y su PaymentHistory.
            fund = await _commissions.FundAnnualFeeAsync(state.MemberId, price.Amount, order.Id, actor, ct);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            return await BlockAfterCollisionAsync(tx, state.MemberId, key, ex);
        }

        if (!fund.IsSuccess)
        {
            // El saldo se miró bajo el candado: no debería pasar. El débito no escribió nada.
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return Settled.Blocked(fund.ErrorCode ?? AnnualFeeErrors.InsufficientCommissionBalance);
        }

        payment.Complete(fund.Value!.PaymentHistoryId, now, actor);
        MarkPaid(order, actor, now);
        AdvanceOnSuccess(state, cycle, now, now, actor);
        AddAttempt(state, price.ProductId, RecurringFundingSource.CommissionBalance, RecurringAttemptOutcome.Success,
            price.Amount, order.Id, fund.Value.PaymentHistoryId, tokenTransactionId: null, fund.Value.DebitEarningId,
            failureReason: null, now, actor);

        return await CommitOrBlockAsync(tx, Settled.Paid(payment, order, AnnualFeeMethods.CommissionBalance), key, ct);
    }

    /// <summary>
    /// Bajo el candado: relee el estado y dice si no se puede cobrar este ciclo. ReplayCode = la clave ya
    /// tiene pago. Pagado, en curso o en revisión = hay un pago vivo para el ciclo. En automático, si el
    /// vencimiento ya está en el futuro es que alguien lo pagó mientras tanto.
    /// </summary>
    private async Task<string?> BlockedUnderGateAsync(
        SubscriptionBillingState state, string? key, DueRule rule, DateTime now, CancellationToken ct)
    {
        await _db.Entry(state).ReloadAsync(ct);

        if (key is not null
            && await _db.AnnualFeePayments.AsNoTracking().AnyAsync(p => p.MemberId == state.MemberId && p.IdempotencyKey == key, ct))
            return ReplayCode;

        var cycle = state.NextBillingDate.Date;
        var live = await AnnualFeePaymentQueries.LiveForCycle(_db, state.Id, cycle).AsNoTracking()
            .Select(p => (AnnualFeePaymentStatus?)p.Status)
            .FirstOrDefaultAsync(ct);

        switch (live)
        {
            case AnnualFeePaymentStatus.Completed:           return AnnualFeeErrors.AlreadyPaid;
            case AnnualFeePaymentStatus.Pending:             return AnnualFeeErrors.InProgress;
            case AnnualFeePaymentStatus.NeedsReconciliation: return AnnualFeeErrors.NeedsReview;
        }

        return rule switch
        {
            DueRule.Due when cycle > now.Date                               => AnnualFeeErrors.AlreadyPaid,
            DueRule.PayAhead when !AnnualFeeSchedule.IsPayable(cycle, now)  => AnnualFeeErrors.NotDue,
            _                                                               => null
        };
    }

    /// <summary>
    /// Guarda y confirma. Si un índice único choca (otra instancia sin candado compartido cobró el mismo
    /// ciclo o usó la misma clave) no se guardó nada: se suelta y se dice por qué.
    /// </summary>
    private async Task<Settled> CommitOrBlockAsync(IDbContextTransaction tx, Settled paid, string? key, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return paid;
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            return await BlockAfterCollisionAsync(tx, paid.Payment!.MemberId, key, ex);
        }
    }

    private async Task<Settled> BlockAfterCollisionAsync(IDbContextTransaction tx, string memberId, string? key, Exception ex)
    {
        await tx.RollbackAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();
        _logger.LogWarning(ex, "Cuota anual de {MemberId}: el guardado chocó con otro cobro del mismo ciclo o la misma clave.", memberId);
        return Settled.Blocked(key is null ? AnnualFeeErrors.InProgress : ReplayCode);
    }

    // ── Sin fondos, decisiones y resultados ──────────────────────────────────

    /// <summary>Intento fallido: la cadencia del plan y, agotada, Stopped (MarkExpired). Ni pedido ni membresía.</summary>
    private async Task<Result<AnnualFeeChargeOutcome>> RecordNoFundsAsync(
        SubscriptionBillingState state, AnnualFeePrice price, MemberProfile member, CancellationToken ct)
    {
        var now = _clock.Now;
        _scheduler.Advance(state, state.Plan!, succeeded: false, attemptDate: now);
        state.LastFailureReason = AnnualFeeErrors.NoFunds;
        state.LastUpdateDate    = now;
        state.LastUpdateBy      = AutoActor;

        AddAttempt(state, price.ProductId,
            member.FeesFromBalance ? RecurringFundingSource.CommissionBalance : RecurringFundingSource.Token,
            RecurringAttemptOutcome.Failed, price.Amount, orderId: null, paymentHistoryId: null,
            tokenTransactionId: null, commissionDebitId: null, AnnualFeeErrors.NoFunds, now, AutoActor);

        await _db.SaveChangesAsync(ct);
        return Result<AnnualFeeChargeOutcome>.Success(
            new AnnualFeeChargeOutcome(AnnualFeeOutcomes.Failed, FailureReason: AnnualFeeErrors.NoFunds));
    }

    /// <summary>Lo que se hace con el estado cuando una exclusión impide cobrar hoy (K5). No es un intento.</summary>
    private static void ApplyDecision(SubscriptionBillingState state, AnnualFeeDecision decision, DateTime now)
    {
        switch (decision.Kind)
        {
            case AnnualFeeDecisionKind.Stop:
                state.Status            = RecurringBillingStatus.Stopped;
                state.LastFailureReason = decision.Reason;
                break;

            case AnnualFeeDecisionKind.Postpone:
                state.NextAttemptDate   = now.Date.AddDays(1);
                state.LastFailureReason = decision.Reason;
                break;

            case AnnualFeeDecisionKind.Reschedule:
                state.NextBillingDate     = decision.NextDue!.Value;
                state.NextAttemptDate     = decision.NextDue.Value;
                state.CurrentAttemptIndex = 0;
                state.Status              = RecurringBillingStatus.Active;
                state.LastFailureReason   = null;
                break;
        }

        state.LastAttemptOutcome = "Skipped";
        state.LastUpdateDate     = now;
        state.LastUpdateBy       = AutoActor;
    }

    private static Result<AnnualFeeChargeOutcome> Outcome(Settled s) => s.Kind switch
    {
        SettleKind.Paid => Result<AnnualFeeChargeOutcome>.Success(new AnnualFeeChargeOutcome(
            AnnualFeeOutcomes.Success, s.FundingSource, s.Order!.Id, s.Payment!.PaymentHistoryId)),
        SettleKind.Review => Result<AnnualFeeChargeOutcome>.Success(new AnnualFeeChargeOutcome(
            AnnualFeeOutcomes.Skipped, s.FundingSource, s.Order?.Id, FailureReason: AnnualFeeErrors.NeedsReview)),
        _ => Skipped(s.Code)
    };

    private static Result<AnnualFeeChargeOutcome> Skipped(string? reason) =>
        Result<AnnualFeeChargeOutcome>.Success(new AnnualFeeChargeOutcome(AnnualFeeOutcomes.Skipped, FailureReason: reason));

    // ── Piezas ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pedido de la cuota: SIN OrderDetails (K9: no suma puntos, no cuenta como pedido de membresía para
    /// el nivel por la orden, no dispara comisiones) y SIN el bucle de unicidad de OrderNo (K20).
    /// </summary>
    private Orders NewOrder(string memberId, decimal total, DateTime cycle, string actor, DateTime now)
    {
        var order = new Orders
        {
            MemberId       = memberId,
            OrderNo        = OrderNumberHelper.Generate("Annual Fee", now),
            TotalAmount    = total,
            Status         = OrderStatus.Processing,
            OrderDate      = now,
            Notes          = $"Biz Center annual fee — cycle {cycle:yyyy-MM-dd}",
            CreationDate   = now,
            CreatedBy      = actor,
            LastUpdateDate = now,
            LastUpdateBy   = actor
        };
        _db.Orders.Add(order);
        return order;
    }

    private AnnualFeePayment NewPayment(
        SubscriptionBillingState state, DateTime cycle, decimal amount, AnnualFeePaymentMethod method,
        AnnualFeePaymentSource source, string? key, Orders order, string actor, DateTime now)
    {
        var payment = new AnnualFeePayment
        {
            MemberId                   = state.MemberId,
            SubscriptionBillingStateId = state.Id,
            CycleDueDate               = cycle,
            Amount                     = amount,
            Method                     = method,
            Source                     = source,
            Status                     = AnnualFeePaymentStatus.Pending,
            OrderId                    = order.Id,
            IdempotencyKey             = key,
            CreatedBy                  = actor,
            CreationDate               = now,
            LastUpdateDate             = now,
            LastUpdateBy               = actor
        };
        _db.AnnualFeePayments.Add(payment);
        return payment;
    }

    private static void MarkPaid(Orders order, string actor, DateTime now)
    {
        order.Status         = OrderStatus.Paid;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = actor;
    }

    private static void Cancel(Orders order, string actor, DateTime now)
    {
        order.Status         = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = actor;
    }

    /// <summary>Cobrado el ciclo <paramref name="cycle"/> en <paramref name="paidAt"/>: el siguiente según K4.</summary>
    private static void AdvanceOnSuccess(SubscriptionBillingState state, DateTime cycle, DateTime paidAt, DateTime now, string actor)
    {
        var next = AnnualFeeSchedule.NextDueAfterPayment(cycle, paidAt);
        state.LastSuccessfulBillingDate = paidAt;
        state.NextBillingDate           = next;
        state.NextAttemptDate           = next;
        state.CurrentAttemptIndex       = 0;
        state.Status                    = RecurringBillingStatus.Active;
        state.LastAttemptAt             = paidAt;
        state.LastAttemptOutcome        = "Success";
        state.LastFailureReason         = null;
        state.LastUpdateDate            = now;
        state.LastUpdateBy              = actor;
    }

    private void AddAttempt(
        SubscriptionBillingState state, string productId, RecurringFundingSource source, RecurringAttemptOutcome outcome,
        decimal amount, string? orderId, string? paymentHistoryId, long? tokenTransactionId, string? commissionDebitId,
        string? failureReason, DateTime now, string actor)
    {
        _db.RecurringBillingAttempts.Add(new RecurringBillingAttempt
        {
            SubscriptionBillingStateId   = state.Id,
            MemberId                     = state.MemberId,
            ProductId                    = productId,
            AttemptIndex                 = state.CurrentAttemptIndex,
            AttemptedAt                  = now,
            Amount                       = amount,
            FundingSource                = source,
            Outcome                      = outcome,
            PaymentHistoryId             = paymentHistoryId,
            OrderId                      = orderId,
            TokenTransactionId           = tokenTransactionId,
            CommissionDeductionEarningId = commissionDebitId,
            FailureReason                = failureReason,
            CreatedBy                    = actor,
            CreationDate                 = now
        });
    }
}
```

- [ ] **Step 6: Registrar**

En `AnnualFeeServiceCollectionExtensions.cs`, sustituir el cuerpo de `AddAnnualFeeServices` por:

```csharp
        services.AddScoped<IAnnualFeeStateEnrollment, AnnualFeeStateEnrollment>();
        services.AddScoped<IAnnualFeeTokenConsumer, AnnualFeeTokenConsumer>();
        services.AddScoped<IAnnualFeeChargeService, AnnualFeeChargeService>();
        services.AddScoped<AnnualFeeStateEnrollmentJob>();
        return services;
```

- [ ] **Step 7: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter CobroAutomaticoDeCuotaAnualTests -nologo`
Expected: PASS (todas).

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeePaymentQueries.cs \
        MLMConquerorGlobalEdition.Billing/Services/AnnualFee/IAnnualFeeChargeService.cs \
        MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.cs \
        MLMConquerorGlobalEdition.Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CobroAutomaticoDeCuotaAnualTests.cs
git commit -m "Cobro automatico de la cuota anual bajo el candado: apagado por defecto, exclusiones del legado, token siempre y comisiones con la preferencia, un pago vivo por ciclo y, sin fondos, la cadencia del plan sin tocar la membresia"
```

---

### Task 13: Cobro con eWallet en dos fases

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.EWallet.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CobroDeCuotaConEWalletTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.cs` (`ChargeDueAsync`, paso 3)

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CobroDeCuotaConEWalletTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// La cuota con eWallet (Plan K, K11), en dos fases como la compra de tokens: se reserva el ciclo, se
/// relee el saldo real con el candado, se debita y se verifica. Lo dudoso queda a conciliar y no se
/// reintenta; un rechazo es un intento fallido.
/// </summary>
public class CobroDeCuotaConEWalletTests
{
    private const string Miembro = "AMB-1";
    private const string Usuario = "amb-1";

    private static async Task<AppDbContext> ConMiembroAsync(bool conSaldo = true)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, conSaldo: conSaldo);
        await CuotaAnualDb.EstadoAsync(db, Miembro);
        return db;
    }

    private static Mock<IEWalletFunds> EWallet(decimal saldo, EWalletDebitOutcome? resultado = null, decimal? saldoAlDebitar = null)
    {
        var m = new Mock<IEWalletFunds>();
        m.Setup(e => e.UserNameOfAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync(Usuario);
        m.SetupSequence(e => e.BalanceAsync(Usuario, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<decimal>.Success(saldo))
         .ReturnsAsync(Result<decimal>.Success(saldoAlDebitar ?? saldo));
        m.Setup(e => e.DebitAsync(Usuario, 99m, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(resultado ?? new EWalletDebitOutcome(EWalletDebitStatus.Completed, null, "ok"));
        return m;
    }

    private static Task<Result<AnnualFeeChargeOutcome>> Cobrar(AppDbContext db, Mock<IEWalletFunds> ew, TransactionOnlyGate? candado = null) =>
        CobroAutomaticoDeCuotaAnualTests.Servicio(db, candado: candado ?? new TransactionOnlyGate(db), ewallet: ew)
            .ChargeDueAsync("anual-" + Miembro, force: false);

    [Fact]
    public async Task Sin_token_ni_comisiones_paga_con_la_eWallet_en_dos_fases()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m);
        var candado = new TransactionOnlyGate(db);

        var r = await Cobrar(db, ew, candado);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        r.Value.FundingSource.Should().Be(AnnualFeeMethods.EWallet);
        candado.Entered.Count(m => m == Miembro).Should().BeGreaterThanOrEqualTo(2);

        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        pago.Method.Should().Be(AnnualFeePaymentMethod.EWallet);
        ew.Verify(e => e.DebitAsync(Usuario, 99m, 150m, pago.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var ph = await db.PaymentHistories.SingleAsync();
        ph.GatewayName.Should().Be("eWallet");
        ph.Amount.Should().Be(99m);
        pago.PaymentHistoryId.Should().Be(ph.Id);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).NextBillingDate.Should().Be(new DateTime(2028, 6, 10)); // K4 revisado
        (await db.RecurringBillingAttempts.SingleAsync()).FundingSource.Should().Be(RecurringFundingSource.EWallet);
    }

    [Fact]
    public async Task Con_la_preferencia_apagada_no_se_llama_a_iPayout()
    {
        await using var db = await ConMiembroAsync(conSaldo: false);
        var ew = EWallet(150m);

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        ew.Verify(e => e.UserNameOfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_rechazo_es_un_intento_fallido_y_libera_el_ciclo()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m, new EWalletDebitOutcome(EWalletDebitStatus.Rejected, "E12", "rechazado"));

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Failed);
        pago.FailureCode.Should().Be(AnnualFeeErrors.EWalletRejected);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).CurrentAttemptIndex.Should().Be(1);
    }

    [Fact]
    public async Task Un_resultado_dudoso_queda_a_conciliar_y_no_se_reintenta()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m, new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_DEBIT_PENDING", "?"));

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.NeedsReview);
        (await db.AnnualFeePayments.SingleAsync()).Status.Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextBillingDate.Should().Be(new DateTime(2027, 6, 5));
        estado.NextAttemptDate.Should().Be(CuotaAnualDb.Ahora.Date.AddDays(1));
        estado.LastAttemptOutcome.Should().Be("NeedsReconciliation");

        // La pasada siguiente ve el pago a conciliar y no vuelve a debitar.
        var otra = await Cobrar(db, EWallet(150m));
        otra.Value!.FailureReason.Should().Be(AnnualFeeErrors.NeedsReview);
        ew.Verify(e => e.DebitAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_el_debito_lanza_queda_a_conciliar()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m);
        ew.Setup(e => e.DebitAsync(Usuario, 99m, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new TimeoutException("i-Payout no contestó"));

        await Cobrar(db, ew);

        (await db.AnnualFeePayments.SingleAsync()).Status.Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
    }

    [Fact]
    public async Task Si_el_saldo_ya_no_alcanza_en_la_segunda_lectura_no_se_debita()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m, saldoAlDebitar: 50m);

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Failed);
        pago.FailureCode.Should().Be(AnnualFeeErrors.InsufficientEWalletBalance);
        ew.Verify(e => e.DebitAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter CobroDeCuotaConEWalletTests -nologo`
Expected: FAIL en las que esperan el cobro con eWallet (hoy el servicio no lo intenta y devuelve `Failed`); PASS en `Con_la_preferencia_apagada_no_se_llama_a_iPayout`.

- [ ] **Step 3: La segunda parte del servicio**

`MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.EWallet.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>
/// La cuota con eWallet (Plan K, K11), en dos fases como TokenPurchaseService.PayWithEWalletAsync:
/// (1) bajo el candado se reserva el ciclo (pago Pending, pedido Processing) y se confirma antes de tocar
/// i-Payout; (2) con el candado otra vez y sin cancelación se relee el saldo real, se debita, se
/// verifica por saldo y se cierra. Lo dudoso nunca se reintenta: queda NeedsReconciliation.
/// </summary>
public sealed partial class AnnualFeeChargeService
{
    private const string EWalletGatewayName = "eWallet";

    private async Task<Settled> SettleWithEWalletAsync(
        SubscriptionBillingState state, AnnualFeePrice price, AnnualFeePaymentSource source, string? key,
        string actor, DueRule rule, CancellationToken ct)
    {
        var userName = await _eWallet.UserNameOfAsync(state.MemberId, ct);
        if (userName is null) return Settled.NotPossible(AnnualFeeErrors.EWalletNotFound);

        var charge = EWalletFunds.ChargeAmount(price.Amount);

        // Comprobación temprana para no reservar en vano; la que cuenta se repite con el candado.
        var early = await SafeBalanceAsync(state.MemberId, userName, ct);
        if (!early.IsSuccess)     return Settled.NotPossible(AnnualFeeErrors.EWalletUnavailable);
        if (early.Value < charge) return Settled.NotPossible(AnnualFeeErrors.InsufficientEWalletBalance);

        // Fase 1 — reservar el ciclo.
        var now = _clock.Now;
        Orders order;
        AnnualFeePayment payment;
        DateTime cycle;
        await using (var tx = await _gate.EnterAsync(state.MemberId, ct))
        {
            var blocked = await BlockedUnderGateAsync(state, key, rule, now, ct);
            if (blocked is not null)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return Settled.Blocked(blocked);
            }

            cycle   = state.NextBillingDate.Date;
            order   = NewOrder(state.MemberId, charge, cycle, actor, now);
            payment = NewPayment(state, cycle, charge, AnnualFeePaymentMethod.EWallet, source, key, order, actor, now);
            try
            {
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                return await BlockAfterCollisionAsync(tx, state.MemberId, key, ex);
            }
        }

        // Fase 2 — con el candado: ningún otro débito nuestro del mismo miembro se cruza con la
        // verificación por saldo. Sin cancelación: desde aquí puede moverse dinero.
        IDbContextTransaction tx2;
        try
        {
            tx2 = await _gate.EnterAsync(state.MemberId, CancellationToken.None);
        }
        catch (MemberFundsBusyException)
        {
            return await AbandonAsync(order, payment, AnnualFeeErrors.Busy, actor, notPossible: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cuota anual {PaymentId}: no se pudo tomar el candado antes del débito eWallet.", payment.Id);
            return await AbandonAsync(order, payment, AnnualFeeErrors.Busy, actor, notPossible: false);
        }

        await using (tx2)
        {
            var before = await SafeBalanceAsync(state.MemberId, userName, CancellationToken.None);
            if (!before.IsSuccess || before.Value < charge)
            {
                await ReleaseOpenTransactionAsync();
                return await AbandonAsync(order, payment,
                    before.IsSuccess ? AnnualFeeErrors.InsufficientEWalletBalance : AnnualFeeErrors.EWalletUnavailable,
                    actor, notPossible: true);
            }

            EWalletDebitOutcome outcome;
            try
            {
                outcome = await _eWallet.DebitAsync(userName, charge, before.Value, payment.Id,
                    $"Biz Center annual fee {order.OrderNo}", CancellationToken.None);
            }
            catch (Exception ex)
            {
                // El débito pudo salir antes de la excepción: a conciliar, nunca liberar ni reintentar.
                _logger.LogError(ex, "Cuota anual {PaymentId}: el débito eWallet lanzó una excepción.", payment.Id);
                outcome = new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_OUTCOME_UNKNOWN", ex.GetType().Name);
            }

            if (outcome.Status != EWalletDebitStatus.Completed)
                _logger.LogWarning("Cuota anual {PaymentId}: débito eWallet de {User} {Status} {Code} {Detail}",
                    payment.Id, EWalletClient.MaskUserName(userName), outcome.Status, outcome.Code, outcome.Detail);

            var at = _clock.Now;
            try
            {
                switch (outcome.Status)
                {
                    case EWalletDebitStatus.Rejected:
                        payment.Fail(AnnualFeeErrors.EWalletRejected, at, actor);
                        payment.Notes = $"{outcome.Code}: {outcome.Detail}";
                        Cancel(order, actor, at);
                        await _db.SaveChangesAsync(CancellationToken.None);
                        await tx2.CommitAsync(CancellationToken.None);
                        return Settled.NotPossible(AnnualFeeErrors.EWalletRejected);

                    case EWalletDebitStatus.NeedsReconciliation:
                        payment.MarkForReconciliation(outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN", outcome.Detail, at, actor);
                        MarkStateUnderReview(state, at, actor);
                        await _db.SaveChangesAsync(CancellationToken.None);
                        await tx2.CommitAsync(CancellationToken.None);
                        return Settled.Review(payment, order, AnnualFeeMethods.EWallet);
                }

                // Lo cobrado es el importe a centavos que mandó EWalletFunds.
                var history = AddEWalletPaymentHistory(order, charge, payment.Id, actor, at);
                payment.Complete(history.Id, at, actor);
                MarkPaid(order, actor, at);
                AdvanceOnSuccess(state, cycle, at, at, actor);
                AddAttempt(state, price.ProductId, RecurringFundingSource.EWallet, RecurringAttemptOutcome.Success, charge,
                    order.Id, history.Id, tokenTransactionId: null, commissionDebitId: null, failureReason: null, at, actor);
                await _db.SaveChangesAsync(CancellationToken.None);
                await tx2.CommitAsync(CancellationToken.None);
                return Settled.Paid(payment, order, AnnualFeeMethods.EWallet);
            }
            catch (Exception ex)
            {
                // No se pudo dejar constancia del resultado del débito: el dinero pudo salir.
                var code = outcome.Status switch
                {
                    EWalletDebitStatus.Completed => "EWALLET_CHARGED_NOT_RECORDED",
                    EWalletDebitStatus.Rejected  => "EWALLET_REJECTED_NOT_SAVED",
                    _                            => outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN"
                };
                return await RescueToReconciliationAsync(payment.Id, order, code, ex.GetType().Name, actor, ex);
            }
        }
    }

    /// <summary>El estado no avanza; la pasada de mañana verá el pago a conciliar y no hará nada.</summary>
    private static void MarkStateUnderReview(SubscriptionBillingState state, DateTime at, string actor)
    {
        state.LastAttemptAt      = at;
        state.LastAttemptOutcome = "NeedsReconciliation";
        state.LastFailureReason  = AnnualFeeErrors.NeedsReview;
        state.NextAttemptDate    = at.Date.AddDays(1);
        state.LastUpdateDate     = at;
        state.LastUpdateBy       = actor;
    }

    private PaymentHistory AddEWalletPaymentHistory(Orders order, decimal amount, string? gatewayTxId, string actor, DateTime now)
    {
        var history = new PaymentHistory
        {
            OrderId = order.Id, MemberId = order.MemberId, Amount = amount, GatewayName = EWalletGatewayName,
            GatewayTransactionId = gatewayTxId, TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt = now, CreationDate = now, CreatedBy = actor, LastUpdateDate = now, LastUpdateBy = actor
        };
        _db.PaymentHistories.Add(history);
        return history;
    }

    /// <summary>
    /// Suelta la reserva cuando consta que NO se movió dinero. Si el guardado falla, el pago se queda
    /// Pending y el barrido lo pasa a conciliar (lado seguro: tesorería lo cierra como no cobrado).
    /// </summary>
    private async Task<Settled> AbandonAsync(Orders order, AnnualFeePayment payment, string code, string actor, bool notPossible)
    {
        try
        {
            var now = _clock.Now;
            payment.Fail(code, now, actor);
            Cancel(order, actor, now);
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cuota anual {PaymentId}: no se pudo soltar la reserva ({Code}); queda para el barrido de Pending.",
                payment.Id, code);
        }
        return notPossible ? Settled.NotPossible(code) : Settled.Blocked(code);
    }

    /// <summary>
    /// Último recurso cuando no se pudo guardar el resultado de un débito: se suelta la transacción, se
    /// olvida el contexto, se relee el pago y se deja a conciliar. Si tampoco se puede, Critical: lo
    /// recoge el barrido de Pending (AnnualFeeStalePendingJob). Nunca libera.
    /// </summary>
    private async Task<Settled> RescueToReconciliationAsync(
        string paymentId, Orders order, string code, string? detail, string actor, Exception cause)
    {
        _logger.LogError(cause, "Cuota anual {PaymentId}: no se pudo guardar el resultado del débito ({Code}); se deja a conciliar.",
            paymentId, code);
        try
        {
            await ReleaseOpenTransactionAsync();
            _db.ChangeTracker.Clear();

            var payment = await _db.AnnualFeePayments.FirstOrDefaultAsync(p => p.Id == paymentId, CancellationToken.None);
            if (payment is null)
            {
                _logger.LogCritical("Cuota anual {PaymentId}: no aparece al releerla tras un débito ({Code}). Revisar a mano.", paymentId, code);
                return Settled.Blocked(AnnualFeeErrors.NeedsReview);
            }

            if (payment.Status == AnnualFeePaymentStatus.Pending)
            {
                payment.MarkForReconciliation(code, detail, _clock.Now, actor);
                await _db.SaveChangesAsync(CancellationToken.None);
            }

            return Settled.Review(payment, order, AnnualFeeMethods.EWallet);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "Cuota anual {PaymentId} quedó Pending con un débito posiblemente hecho ({Code}) y no se pudo pasar a conciliar. " +
                "La recogerá el barrido de Pending; no liberar sin mirar i-Payout.", paymentId, code);
            return Settled.Blocked(AnnualFeeErrors.NeedsReview);
        }
    }

    /// <summary>El saldo de i-Payout sin que su texto llegue a nadie: un fallo es EWALLET_UNAVAILABLE.</summary>
    private async Task<Result<decimal>> SafeBalanceAsync(string memberId, string userName, CancellationToken ct)
    {
        try
        {
            var balance = await _eWallet.BalanceAsync(userName, ct);
            if (balance.IsSuccess) return balance;

            _logger.LogWarning("Saldo eWallet de {MemberId} ({User}) no disponible: {Code} {Error}",
                memberId, EWalletClient.MaskUserName(userName), balance.ErrorCode, balance.Error);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Saldo eWallet de {MemberId} ({User}): i-Payout no respondió.",
                memberId, EWalletClient.MaskUserName(userName));
        }

        return Result<decimal>.Failure(AnnualFeeErrors.EWalletUnavailable, "The eWallet balance is not available.");
    }

    /// <summary>Deshace y suelta la transacción abierta en el contexto, si la hay, sin lanzar.</summary>
    private async Task ReleaseOpenTransactionAsync()
    {
        if (_db.Database.CurrentTransaction is not { } open) return;
        try
        {
            await open.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo deshacer la transacción abierta; se descarta.");
        }
        finally
        {
            await open.DisposeAsync();
        }
    }
}
```

- [ ] **Step 4: El paso 3 del cobro automático**

En `AnnualFeeChargeService.cs` (`ChargeDueAsync`), sustituir:

```csharp
            // 2. Comisiones, solo con la preferencia (K7).
            if (settled.Kind == SettleKind.NotPossible && member!.FeesFromBalance)
                settled = await SettleWithCommissionsAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);
```

por:

```csharp
            // 2. Comisiones y 3. eWallet, solo con la preferencia (K7).
            if (settled.Kind == SettleKind.NotPossible && member!.FeesFromBalance)
                settled = await SettleWithCommissionsAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);
            if (settled.Kind == SettleKind.NotPossible && member!.FeesFromBalance)
                settled = await SettleWithEWalletAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "CobroDeCuotaConEWalletTests|CobroAutomaticoDeCuotaAnualTests" -nologo`
Expected: PASS (todas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.cs \
        MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.EWallet.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/CobroDeCuotaConEWalletTests.cs
git commit -m "Cuota anual con eWallet en dos fases como la compra de tokens: reserva del ciclo, relectura del saldo real con el candado, debito verificado por saldo, rechazo como intento fallido y lo dudoso a conciliar sin reintento"
```

---

### Task 14: «Pagar ahora» con idempotencia

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.PayNow.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/PagarAhoraTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/IAnnualFeeChargeService.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/PagarAhoraTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// "Pagar ahora" (Plan K, K14): el miembro elige token, comisiones o eWallet; nunca tarjeta. Misma clave
/// = mismo resultado (K10). Solo cuentas activas, sin KeepQualified y dentro de la ventana de 30 días. El
/// interruptor del cobro automático no le afecta. Revive un estado parado.
/// </summary>
public class PagarAhoraTests
{
    private const string Miembro = "AMB-1";

    private static async Task<AppDbContext> ConMiembroAsync(
        MemberAccountStatus estado = MemberAccountStatus.Active, bool mantener = false, DateTime? vence = null,
        RecurringBillingStatus estadoCobro = RecurringBillingStatus.Active, bool conEstado = true,
        MemberType tipo = MemberType.Ambassador, DateTime? alta = null)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, alta: alta, estado: estado, mantener: mantener, tipo: tipo);
        if (conEstado) await CuotaAnualDb.EstadoAsync(db, Miembro, vence ?? new DateTime(2027, 6, 5), estadoCobro);
        return db;
    }

    private static Task<Result<AnnualFeePaymentResultDto>> Pagar(
        AppDbContext db, string metodo, string? clave = null, Mock<IEWalletFunds>? ewallet = null) =>
        CobroAutomaticoDeCuotaAnualTests.Servicio(db, encendido: false, ewallet: ewallet)
            .PayNowAsync(new AnnualFeePayNowInput(Miembro, "user-001",
                new PayAnnualFeeRequest { Method = metodo, IdempotencyKey = clave ?? Guid.NewGuid().ToString("N") }));

    [Fact]
    public async Task Con_un_token_paga_y_devuelve_el_codigo_y_el_siguiente_vencimiento()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "CUOTA-9");

        var r = await Pagar(db, AnnualFeeMethods.Token);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Method.Should().Be(AnnualFeeMethods.Token);
        r.Value.Status.Should().Be(AnnualFeePaymentStatuses.Completed);
        r.Value.Amount.Should().Be(0m);
        r.Value.TokenCode.Should().Be("CUOTA-9");
        r.Value.CycleDueDate.Should().Be(new DateTime(2027, 6, 5));
        r.Value.NextDueDate.Should().Be(new DateTime(2028, 6, 10)); // K4 revisado: pagado tarde, un año desde el pago
        (await db.AnnualFeePayments.SingleAsync()).Source.Should().Be(AnnualFeePaymentSource.PayNow);
    }

    [Fact]
    public async Task Con_comisiones_cobra_el_precio_de_la_cuota()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Pagar(db, "commissionbalance");

        r.Value!.Method.Should().Be(AnnualFeeMethods.CommissionBalance);
        r.Value.Amount.Should().Be(99m);
        (await db.AnnualFeePayments.SingleAsync()).CreatedBy.Should().Be("user-001");
    }

    [Fact]
    public async Task Con_eWallet_cobra_en_dos_fases()
    {
        await using var db = await ConMiembroAsync();
        var ew = new Mock<IEWalletFunds>();
        ew.Setup(e => e.UserNameOfAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync("amb-1");
        ew.Setup(e => e.BalanceAsync("amb-1", It.IsAny<CancellationToken>())).ReturnsAsync(Result<decimal>.Success(500m));
        ew.Setup(e => e.DebitAsync("amb-1", 99m, 500m, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new EWalletDebitOutcome(EWalletDebitStatus.Completed, null, "ok"));

        var r = await Pagar(db, AnnualFeeMethods.EWallet, ewallet: ew);

        r.Value!.Method.Should().Be(AnnualFeeMethods.EWallet);
        r.Value.Status.Should().Be(AnnualFeePaymentStatuses.Completed);
    }

    [Fact]
    public async Task La_misma_clave_devuelve_el_mismo_pago_sin_cobrar_otra_vez()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);

        var primero = await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");
        var segundo = await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");

        segundo.IsSuccess.Should().BeTrue();
        segundo.Value!.PaymentId.Should().Be(primero.Value!.PaymentId);
        (await db.Orders.CountAsync()).Should().Be(1);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(1);
    }

    [Fact]
    public async Task La_misma_clave_con_otro_metodo_no_es_un_reintento()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);
        await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");

        var r = await Pagar(db, AnnualFeeMethods.Token, "clave-1");

        r.ErrorCode.Should().Be(AnnualFeeErrors.IdempotencyKeyReused);
    }

    [Theory]
    [InlineData("CreditCard")]
    [InlineData("Crypto")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Tarjeta_cripto_o_un_numero_no_son_metodos_de_la_cuota(string metodo)
    {
        await using var db = await ConMiembroAsync();

        (await Pagar(db, metodo)).ErrorCode.Should().Be(AnnualFeeErrors.MethodNotSupported);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_clave_valida_no_hace_nada()
    {
        await using var db = await ConMiembroAsync();

        (await Pagar(db, AnnualFeeMethods.Token, "clave con espacios")).ErrorCode.Should().Be(AnnualFeeErrors.IdempotencyKeyRequired);
    }

    [Fact]
    public async Task Fuera_de_la_ventana_de_30_dias_no_vence_todavia()
    {
        await using var db = await ConMiembroAsync(vence: CuotaAnualDb.Ahora.Date.AddDays(31));
        await CuotaAnualDb.TokenAsync(db, Miembro);

        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.NotDue);
    }

    [Fact]
    public async Task Pagada_la_cuota_un_segundo_pago_con_otra_clave_no_vence()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);
        await Pagar(db, AnnualFeeMethods.CommissionBalance);

        (await Pagar(db, AnnualFeeMethods.CommissionBalance)).ErrorCode.Should().Be(AnnualFeeErrors.NotDue);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(1);
    }

    [Fact]
    public async Task Con_KeepQualified_no_hay_cuota()
    {
        await using var db = await ConMiembroAsync(mantener: true);
        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.Waived);
    }

    [Fact]
    public async Task Con_la_cuenta_suspendida_no_se_paga()
    {
        await using var db = await ConMiembroAsync(estado: MemberAccountStatus.Suspended);
        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.AccountNotActive);
    }

    [Fact]
    public async Task Un_cliente_no_tiene_cuota()
    {
        await using var db = await ConMiembroAsync(tipo: MemberType.ExternalMember, conEstado: false);
        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.NotApplicable);
    }

    [Fact]
    public async Task Sin_token_dice_que_no_hay_y_la_misma_clave_vale_despues_de_comprarlo()
    {
        await using var db = await ConMiembroAsync();

        (await Pagar(db, AnnualFeeMethods.Token, "clave-t")).ErrorCode.Should().Be(AnnualFeeErrors.NoFeeToken);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);

        await CuotaAnualDb.TokenAsync(db, Miembro);
        (await Pagar(db, AnnualFeeMethods.Token, "clave-t")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_estado_lo_crea_y_cobra_si_el_aniversario_cae_dentro_de_la_ventana()
    {
        await using var db = await ConMiembroAsync(conEstado: false, alta: new DateTime(2026, 6, 20));
        await CuotaAnualDb.TokenAsync(db, Miembro);

        var r = await Pagar(db, AnnualFeeMethods.Token);

        r.IsSuccess.Should().BeTrue();
        r.Value!.CycleDueDate.Should().Be(new DateTime(2027, 6, 20));
        r.Value.NextDueDate.Should().Be(new DateTime(2028, 6, 20));
    }

    [Fact]
    public async Task Revive_un_estado_parado_y_re_ancla_si_paga_muy_tarde()
    {
        await using var db = await ConMiembroAsync(vence: new DateTime(2027, 1, 5), estadoCobro: RecurringBillingStatus.Stopped);
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);

        var r = await Pagar(db, AnnualFeeMethods.CommissionBalance);

        r.IsSuccess.Should().BeTrue();
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.Status.Should().Be(RecurringBillingStatus.Active);
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 10));
    }

    [Fact]
    public async Task Con_el_candado_ocupado_no_cobra()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db, candado: new BusyGate())
            .PayNowAsync(new AnnualFeePayNowInput(Miembro, "user-001",
                new PayAnnualFeeRequest { Method = AnnualFeeMethods.CommissionBalance, IdempotencyKey = "k1" }));

        r.ErrorCode.Should().Be(AnnualFeeErrors.Busy);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HasPayment_dice_si_la_clave_ya_tiene_pago_de_este_miembro()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);
        await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");
        var s = CobroAutomaticoDeCuotaAnualTests.Servicio(db);

        (await s.HasPaymentAsync(Miembro, "clave-1")).Should().BeTrue();
        (await s.HasPaymentAsync("AMB-2", "clave-1")).Should().BeFalse();
        (await s.HasPaymentAsync(Miembro, "otra")).Should().BeFalse();
        (await s.HasPaymentAsync(Miembro, null)).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter PagarAhoraTests -nologo`
Expected: FAIL de compilación (`PayNowAsync`, `AnnualFeePayNowInput`, `HasPaymentAsync` no existen).

- [ ] **Step 3: La interfaz**

En `IAnnualFeeChargeService.cs`, añadir antes de la interfaz:

```csharp
/// <summary>Quién paga (siempre del token), con qué identidad queda el rastro y qué pide.</summary>
public sealed record AnnualFeePayNowInput(string MemberId, string Actor, MLMConquerorGlobalEdition.SharedKernel.Billing.PayAnnualFeeRequest Request);
```

y, dentro de la interfaz, después de `ChargeDueAsync`:

```csharp

    /// <summary>"Pagar ahora" del miembro del token (K14). Misma clave = mismo resultado (K10).</summary>
    Task<Result<MLMConquerorGlobalEdition.SharedKernel.Billing.AnnualFeePaymentResultDto>> PayNowAsync(
        AnnualFeePayNowInput input, CancellationToken ct = default);

    /// <summary>
    /// ¿Esta clave ya tiene un pago de este miembro, en el estado que sea? Con ella, PayNowAsync solo
    /// devuelve lo que ya pasó: el controlador no quema un permiso de step-up en una repetición.
    /// </summary>
    Task<bool> HasPaymentAsync(string memberId, string? idempotencyKey, CancellationToken ct = default);
```

- [ ] **Step 4: La tercera parte del servicio**

`MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.PayNow.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>
/// "Pagar ahora" (Plan K, K14). El miembro sale del token (lo pone el controlador). La pantalla manda una
/// clave por intento: la misma clave devuelve el mismo pago (o el mismo fallo) sin mover dinero otra vez.
/// Un intento que no llegó a crear el pago (sin token, sin saldo) no ocupa la clave: se puede repetir.
/// </summary>
public sealed partial class AnnualFeeChargeService
{
    public Task<bool> HasPaymentAsync(string memberId, string? idempotencyKey, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(idempotencyKey)
            ? Task.FromResult(false)
            : _db.AnnualFeePayments.AsNoTracking()
                 .AnyAsync(p => p.MemberId == memberId && p.IdempotencyKey == idempotencyKey, ct);

    public async Task<Result<AnnualFeePaymentResultDto>> PayNowAsync(AnnualFeePayNowInput input, CancellationToken ct = default)
    {
        var req = input.Request;

        // Solo por nombre y sin tarjeta: la tarjeta queda para el Plan I y nunca se cobra con el stub.
        var methodName = AnnualFeeMethods.Normalize(req.Method);
        if (methodName is null)
            return PayFail(AnnualFeeErrors.MethodNotSupported, "This payment method is not available for the annual fee.");
        if (!TokenPurchaseRules.IsValidIdempotencyKey(req.IdempotencyKey))
            return PayFail(AnnualFeeErrors.IdempotencyKeyRequired, "A valid idempotency key is required.");

        var method = Enum.Parse<AnnualFeePaymentMethod>(methodName);
        var replay = await ReplayAsync(input.MemberId, req.IdempotencyKey, method, ct);
        if (replay is not null) return replay;

        var member = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == input.MemberId, ct);
        if (member is null || member.MemberType != MemberType.Ambassador)
            return PayFail(AnnualFeeErrors.NotApplicable, "Only Ambassadors have a Biz Center annual fee.");

        var state = await _enrollment.EnsureForMemberAsync(input.MemberId, ct);
        if (state is null)
            return PayFail(AnnualFeeErrors.PlanNotConfigured, "The annual fee is not configured.");

        var refusal = AnnualFeeEligibility.ForPayNow(
            await AnnualFeeMemberQueries.FactsAsync(_db, input.MemberId, ct), state.NextBillingDate, _clock.Now);
        if (refusal is not null)
            return PayFail(refusal, "The annual fee cannot be paid right now.");

        var price = await AnnualFeePlanQueries.PriceAsync(_db, state.Plan!, ct);
        if (price is null)
            return PayFail(AnnualFeeErrors.PriceNotSet, "The annual fee has no price.");

        Settled settled;
        try
        {
            settled = method switch
            {
                AnnualFeePaymentMethod.Token =>
                    await SettleWithTokenAsync(state, price, AnnualFeePaymentSource.PayNow, req.IdempotencyKey, input.Actor, DueRule.PayAhead, ct),
                AnnualFeePaymentMethod.CommissionBalance =>
                    await SettleWithCommissionsAsync(state, price, AnnualFeePaymentSource.PayNow, req.IdempotencyKey, input.Actor, DueRule.PayAhead, ct),
                _ =>
                    await SettleWithEWalletAsync(state, price, AnnualFeePaymentSource.PayNow, req.IdempotencyKey, input.Actor, DueRule.PayAhead, ct)
            };
        }
        catch (MemberFundsBusyException)
        {
            return PayFail(AnnualFeeErrors.Busy, "Another payment for this account is in progress. Try again in a moment.");
        }

        switch (settled.Kind)
        {
            case SettleKind.Paid:
                return Result<AnnualFeePaymentResultDto>.Success(ToResult(settled.Payment!, settled.Order!, state.NextBillingDate));
            case SettleKind.Review:
                return PayFail(AnnualFeeErrors.NeedsReview, "We couldn't confirm the eWallet charge. It will be reviewed; do not retry.");
        }

        if (settled.Code == ReplayCode)
            return await ReplayAsync(input.MemberId, req.IdempotencyKey, method, ct)
                   ?? PayFail(AnnualFeeErrors.InProgress, "This payment is still being processed.");

        return PayFail(settled.Code ?? AnnualFeeErrors.NoFunds, MessageFor(settled.Code));
    }

    /// <summary>
    /// La misma clave del mismo miembro: Completed → el pago; Pending → en curso; NeedsReconciliation →
    /// en revisión; Failed → el mismo fallo (la pantalla genera otra clave). Con otro método no es un
    /// reintento: IDEMPOTENCY_KEY_REUSED.
    /// </summary>
    private async Task<Result<AnnualFeePaymentResultDto>?> ReplayAsync(
        string memberId, string key, AnnualFeePaymentMethod method, CancellationToken ct)
    {
        var existing = await _db.AnnualFeePayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.IdempotencyKey == key, ct);
        if (existing is null) return null;

        if (existing.Method != method)
            return PayFail(AnnualFeeErrors.IdempotencyKeyReused,
                "This idempotency key was already used for a different payment. Start a new one.");

        switch (existing.Status)
        {
            case AnnualFeePaymentStatus.Pending:
                return PayFail(AnnualFeeErrors.InProgress, "This payment is still being processed.");
            case AnnualFeePaymentStatus.NeedsReconciliation:
                return PayFail(AnnualFeeErrors.NeedsReview, "This payment is being reviewed; do not retry.");
            case AnnualFeePaymentStatus.Failed:
                return PayFail(existing.FailureCode ?? AnnualFeeErrors.NoFunds, "This payment already failed. Start a new one.");
        }

        var order   = await _db.Orders.AsNoTracking().FirstAsync(o => o.Id == existing.OrderId, ct);
        var nextDue = await _db.SubscriptionBillingStates.AsNoTracking()
            .Where(s => s.Id == existing.SubscriptionBillingStateId)
            .Select(s => s.NextBillingDate)
            .FirstAsync(ct);
        return Result<AnnualFeePaymentResultDto>.Success(ToResult(existing, order, nextDue));
    }

    private static AnnualFeePaymentResultDto ToResult(AnnualFeePayment payment, Orders order, DateTime nextDue) => new()
    {
        PaymentId    = payment.Id,
        OrderId      = order.Id,
        OrderNo      = order.OrderNo,
        Status       = payment.Status.ToString(),
        Method       = payment.Method.ToString(),
        Amount       = payment.Amount,
        CycleDueDate = payment.CycleDueDate,
        NextDueDate  = nextDue.Date,
        TokenCode    = payment.TokenCode
    };

    /// <summary>Mensajes fijos: el texto de i-Payout se queda en el log y en las notas, nunca en la respuesta.</summary>
    private static string MessageFor(string? code) => code switch
    {
        AnnualFeeErrors.NoFeeToken                    => "You have no annual-fee token available.",
        AnnualFeeErrors.InsufficientCommissionBalance => "Your commission balance is not enough for the annual fee.",
        AnnualFeeErrors.InsufficientEWalletBalance    => "Your eWallet balance is not enough for the annual fee.",
        AnnualFeeErrors.EWalletNotFound               => "You have no approved eWallet account.",
        AnnualFeeErrors.EWalletUnavailable            => "We couldn't reach your eWallet right now. Try again later.",
        AnnualFeeErrors.EWalletRejected               => "Your eWallet declined the charge.",
        AnnualFeeErrors.AlreadyPaid                   => "The annual fee for this cycle is already paid.",
        AnnualFeeErrors.InProgress                    => "This payment is still being processed.",
        AnnualFeeErrors.NeedsReview                   => "A payment for this cycle is being reviewed; do not retry.",
        AnnualFeeErrors.NotDue                        => "The annual fee is not due yet.",
        AnnualFeeErrors.Busy                          => "Another payment for this account is in progress. Try again in a moment.",
        _                                             => "The annual fee could not be paid."
    };

    private static Result<AnnualFeePaymentResultDto> PayFail(string code, string message) =>
        Result<AnnualFeePaymentResultDto>.Failure(code, message);
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "PagarAhoraTests|CobroAutomaticoDeCuotaAnualTests|CobroDeCuotaConEWalletTests" -nologo`
Expected: PASS (todas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/AnnualFee/IAnnualFeeChargeService.cs \
        MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.PayNow.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/PagarAhoraTests.cs
git commit -m "Pagar ahora la cuota anual con token, comisiones o eWallet y nunca tarjeta: misma clave mismo resultado, solo cuentas activas sin KeepQualified dentro de la ventana de 30 dias, crea el estado si falta y revive uno parado"
```

---

### Task 15: Conciliación y barrido de pagos pendientes

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.Reconcile.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Jobs/AnnualFeeStalePendingJob.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/ConciliacionDeCuotaAnualTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/AnnualFee/IAnnualFeeChargeService.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Program.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/ConciliacionDeCuotaAnualTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Lo dudoso lo cierra tesorería (Plan K, K11): cobrado → se completa y avanza el ciclo desde el día del
/// débito; no cobrado → fallido y el ciclo queda libre. Un pago que se quedó Pending pasa a conciliar.
/// </summary>
public class ConciliacionDeCuotaAnualTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Vence = new(2027, 6, 5);

    private static async Task<(AppDbContext Db, AnnualFeePayment Pago)> ConPagoAsync(
        AnnualFeePaymentStatus estado = AnnualFeePaymentStatus.NeedsReconciliation, DateTime? creado = null)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro);
        await CuotaAnualDb.EstadoAsync(db, Miembro, Vence);
        var pedido = new Orders
        {
            MemberId = Miembro, OrderNo = "AF0606-1", TotalAmount = 99m, Status = OrderStatus.Processing,
            OrderDate = CuotaAnualDb.Ahora.AddDays(-4), CreatedBy = "prueba"
        };
        db.Orders.Add(pedido);
        var pago = new AnnualFeePayment
        {
            MemberId = Miembro, SubscriptionBillingStateId = "anual-" + Miembro, CycleDueDate = Vence, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.Automatic, Status = estado,
            OrderId = pedido.Id, CreatedBy = "annual-fee-engine", CreationDate = creado ?? new DateTime(2027, 6, 6, 5, 0, 0)
        };
        db.AnnualFeePayments.Add(pago);
        await db.SaveChangesAsync();
        return (db, pago);
    }

    [Fact]
    public async Task Cobrado_se_completa_y_avanza_el_ciclo_desde_el_dia_del_debito()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true, GatewayReference = "IPY-123", Notes = "visto en i-Payout" },
            "tesoreria@empresa.com");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Status.Should().Be(AnnualFeePaymentStatuses.Completed);
        var guardado = await db.AnnualFeePayments.AsNoTracking().SingleAsync();
        guardado.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        guardado.Notes.Should().Contain("visto en i-Payout");
        var ph = await db.PaymentHistories.SingleAsync();
        ph.GatewayName.Should().Be("eWallet");
        ph.GatewayTransactionId.Should().Be("IPY-123");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);

        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 6)); // K4 revisado: debitado un día tarde
        estado.LastSuccessfulBillingDate.Should().Be(new DateTime(2027, 6, 6, 5, 0, 0));
    }

    [Fact]
    public async Task No_cobrado_queda_fallido_y_libera_el_ciclo()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = false }, "tesoreria@empresa.com");

        r.IsSuccess.Should().BeTrue();
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync()).Status.Should().Be(AnnualFeePaymentStatus.Failed);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).NextBillingDate.Should().Be(Vence);
        (await MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeePaymentQueries
            .LiveForCycle(db, "anual-" + Miembro, Vence).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Solo_se_concilia_lo_que_esta_a_conciliar()
    {
        var (db, pago) = await ConPagoAsync(AnnualFeePaymentStatus.Completed);
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true }, "tesoreria");

        r.ErrorCode.Should().Be(AnnualFeeErrors.NotUnderReview);
    }

    [Fact]
    public async Task Un_pago_que_no_existe_es_404()
    {
        await using var db = CuotaAnualDb.Nueva();

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync("no-existe",
            new ReconcileAnnualFeePaymentRequest { Charged = true }, "tesoreria");

        r.ErrorCode.Should().Be(AnnualFeeErrors.PaymentNotFound);
    }

    [Fact]
    public async Task Un_Pending_de_mas_de_30_minutos_pasa_a_conciliar_y_uno_reciente_no()
    {
        var (db, viejo) = await ConPagoAsync(AnnualFeePaymentStatus.Pending, CuotaAnualDb.Ahora.AddMinutes(-40));
        await using var _ = db;
        db.AnnualFeePayments.Add(new AnnualFeePayment
        {
            MemberId = "AMB-2", SubscriptionBillingStateId = "anual-AMB-2", CycleDueDate = Vence, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.PayNow,
            Status = AnnualFeePaymentStatus.Pending, OrderId = "ord-2", CreatedBy = "user-2",
            CreationDate = CuotaAnualDb.Ahora.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var movidos = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).MoveStalePendingToReconciliationAsync(30, "barrido");

        movidos.Should().Be(1);
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync(p => p.Id == viejo.Id)).Status
            .Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync(p => p.MemberId == "AMB-2")).Status
            .Should().Be(AnnualFeePaymentStatus.Pending);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter ConciliacionDeCuotaAnualTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: La interfaz**

En `IAnnualFeeChargeService.cs`, dentro de la interfaz, después de `HasPaymentAsync`:

```csharp

    /// <summary>Tesorería cierra un pago a conciliar tras mirar i-Payout (K11).</summary>
    Task<Result<MLMConquerorGlobalEdition.SharedKernel.Billing.AnnualFeePaymentResultDto>> ReconcileAsync(
        string paymentId, MLMConquerorGlobalEdition.SharedKernel.Billing.ReconcileAnnualFeePaymentRequest request,
        string actor, CancellationToken ct = default);

    /// <summary>Pasa a conciliar los pagos que llevan <paramref name="minutes"/> minutos o más en Pending. Nunca libera.</summary>
    Task<int> MoveStalePendingToReconciliationAsync(int minutes, string actor, CancellationToken ct = default);
```

- [ ] **Step 4: La cuarta parte del servicio**

`MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.Reconcile.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>Conciliación y barrido de los pagos de cuota dudosos (Plan K, K11).</summary>
public sealed partial class AnnualFeeChargeService
{
    public async Task<Result<AnnualFeePaymentResultDto>> ReconcileAsync(
        string paymentId, ReconcileAnnualFeePaymentRequest request, string actor, CancellationToken ct = default)
    {
        var memberId = await _db.AnnualFeePayments.AsNoTracking()
            .Where(p => p.Id == paymentId)
            .Select(p => p.MemberId)
            .FirstOrDefaultAsync(ct);
        if (memberId is null)
            return PayFail(AnnualFeeErrors.PaymentNotFound, "Annual-fee payment not found.");

        try
        {
            await using var tx = await _gate.EnterAsync(memberId, ct);

            var payment = await _db.AnnualFeePayments.FirstAsync(p => p.Id == paymentId, ct);
            if (payment.Status != AnnualFeePaymentStatus.NeedsReconciliation)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return PayFail(AnnualFeeErrors.NotUnderReview, "This payment is not under review.");
            }

            var order = await _db.Orders.FirstAsync(o => o.Id == payment.OrderId, ct);
            var state = await _db.SubscriptionBillingStates
                .Include(s => s.Plan).ThenInclude(p => p!.PlanProducts)
                .FirstAsync(s => s.Id == payment.SubscriptionBillingStateId, ct);
            var now = _clock.Now;

            if (request.Charged)
            {
                var history = AddEWalletPaymentHistory(order, payment.Amount,
                    string.IsNullOrWhiteSpace(request.GatewayReference) ? payment.Id : request.GatewayReference.Trim(), actor, now);
                payment.Complete(history.Id, now, actor);
                MarkPaid(order, actor, now);

                if (state.NextBillingDate.Date == payment.CycleDueDate.Date)
                {
                    // El débito se hizo el día del pago, no hoy: el siguiente vencimiento se cuenta desde entonces.
                    AdvanceOnSuccess(state, payment.CycleDueDate, payment.CreationDate, now, actor);
                    var productId = state.Plan!.PlanProducts.OrderBy(pp => pp.Id).Select(pp => pp.ProductId).FirstOrDefault() ?? string.Empty;
                    AddAttempt(state, productId, RecurringFundingSource.EWallet, RecurringAttemptOutcome.Success, payment.Amount,
                        order.Id, history.Id, tokenTransactionId: null, commissionDebitId: null, failureReason: null, now, actor);
                }
                else
                {
                    _logger.LogWarning("Cuota anual {PaymentId}: conciliada como cobrada, pero el estado ya vence el {Due} y no el {Cycle}; no se mueve.",
                        payment.Id, state.NextBillingDate, payment.CycleDueDate);
                }
            }
            else
            {
                payment.Fail(AnnualFeeErrors.NotCharged, now, actor);
                Cancel(order, actor, now);
            }

            payment.Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? payment.Notes
                : string.IsNullOrWhiteSpace(payment.Notes) ? request.Notes.Trim() : $"{payment.Notes} | {request.Notes.Trim()}";

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<AnnualFeePaymentResultDto>.Success(ToResult(payment, order, state.NextBillingDate));
        }
        catch (MemberFundsBusyException)
        {
            return PayFail(AnnualFeeErrors.Busy, "Another payment for this account is in progress. Try again in a moment.");
        }
    }

    public async Task<int> MoveStalePendingToReconciliationAsync(int minutes, string actor, CancellationToken ct = default)
    {
        var now    = _clock.Now;
        var cutoff = now.AddMinutes(-minutes);
        var stale  = await _db.AnnualFeePayments
            .Where(p => p.Status == AnnualFeePaymentStatus.Pending && p.CreationDate <= cutoff)
            .ToListAsync(ct);

        foreach (var payment in stale)
            payment.MarkForReconciliation("STALE_PENDING",
                $"Pending for {minutes}+ minutes: the process stopped between the charge and its record.", now, actor);

        try
        {
            await _db.SaveChangesAsync(ct);
            return stale.Count;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Otro proceso lo cerró a la vez (la fase 2 terminó justo ahora): se deja para la próxima pasada.
            _logger.LogWarning(ex, "Barrido de pagos de cuota: alguno cambió mientras tanto; se mira en la próxima pasada.");
            return 0;
        }
    }
}
```

- [ ] **Step 5: El trabajo de barrido**

`MLMConquerorGlobalEdition.Billing/Jobs/AnnualFeeStalePendingJob.cs`:

```csharp
using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Cada hora: pasa a conciliar los pagos de cuota que se quedaron Pending (el proceso murió entre el
/// débito de i-Payout y su registro). Nunca los libera: el dinero pudo moverse (Plan K, K11).
/// </summary>
[Queue("billing")]
public class AnnualFeeStalePendingJob
{
    private const string Actor = "annual-fee-stale-sweep";

    private readonly IAnnualFeeChargeService _service;
    private readonly IConfiguration _config;
    private readonly ILogger<AnnualFeeStalePendingJob> _logger;

    public AnnualFeeStalePendingJob(IAnnualFeeChargeService service, IConfiguration config, ILogger<AnnualFeeStalePendingJob> logger)
    {
        _service = service;
        _config  = config;
        _logger  = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var minutes = _config.GetValue(AnnualFeeRules.StalePendingMinutesConfigKey, AnnualFeeRules.DefaultStalePendingMinutes);
        var moved   = await _service.MoveStalePendingToReconciliationAsync(minutes, Actor, ct);
        if (moved > 0)
            _logger.LogWarning("AnnualFeeStalePendingJob: {Moved} pagos de cuota pasaron a conciliar.", moved);
    }
}
```

En `AnnualFeeServiceCollectionExtensions.cs`, añadir antes del `return services;`:

```csharp
        services.AddScoped<AnnualFeeStalePendingJob>();
```

En `Billing/Program.cs`, justo después del `AddOrUpdate<AnnualFeeStateEnrollmentJob>` de la Tarea 8, añadir:

```csharp

// Pagos de cuota anual que se quedaron Pending: a conciliar, nunca liberados (Plan K, K11).
RecurringJob.AddOrUpdate<AnnualFeeStalePendingJob>(
    "annual-fee-stale-pending",
    job => job.ExecuteAsync(CancellationToken.None),
    "45 * * * *",                   // Cada hora, a los 45 (no coincide con los de tokens)
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "ConciliacionDeCuotaAnualTests|PagarAhoraTests" -nologo`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/AnnualFee/IAnnualFeeChargeService.cs \
        MLMConquerorGlobalEdition.Billing/Services/AnnualFee/AnnualFeeChargeService.Reconcile.cs \
        MLMConquerorGlobalEdition.Billing/Jobs/AnnualFeeStalePendingJob.cs \
        MLMConquerorGlobalEdition.Billing/Extensions/AnnualFeeServiceCollectionExtensions.cs \
        MLMConquerorGlobalEdition.Billing/Program.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/ConciliacionDeCuotaAnualTests.cs
git commit -m "Conciliacion de los pagos de cuota anual dudosos por tesoreria, que completa y avanza el ciclo desde el dia del debito o lo da por no cobrado, y barrido horario que pasa a conciliar los Pending olvidados"
```

---

### Task 16: El motor delega los estados de cuota anual

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorDelegaLaCuotaAnualTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs`
- Modify: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/CorteDeFacturacionTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorBajoCandadoTests.cs`
- Modify: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/RenovacionSimuladaTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorDelegaLaCuotaAnualTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Un solo motor (Plan K): RecurringBillingProcessor recibe los estados de cuota anual del planificador y
/// del barrido y los pasa a AnnualFeeChargeService. No crea pedidos de renovación ni toca suscripciones.
/// Un estado que mezcla propósito y plan no se cobra.
/// </summary>
public class MotorDelegaLaCuotaAnualTests
{
    private readonly Mock<IAnnualFeeChargeService> _cuota = new();

    private RecurringBillingProcessor Procesador(AppDbContext db) => new(
        db, new Mock<ICommissionBalanceService>().Object, new RecurringBillingScheduler(),
        new Mock<IGatewayRouter>().Object, new Mock<IGatewayChargeOrchestrator>().Object,
        new Mock<ICardBrandDetector>().Object, new TransactionOnlyGate(db), _cuota.Object,
        CuotaAnualDb.Reloj(), NullLogger<RecurringBillingProcessor>.Instance);

    private static async Task<AppDbContext> ConEstadoAsync(BillingStatePurpose proposito, bool planAnual)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        if (!planAnual)
        {
            var plan = await db.RecurringBillingPlans.SingleAsync();
            plan.IsAnnualFeePlan = false;
        }
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado", Purpose = proposito,
            MembershipSubscriptionId = proposito == BillingStatePurpose.Membership ? "sub-1" : null,
            MemberId = "AMB-1", RecurringBillingPlanId = CuotaAnualDb.PlanAnual, NextBillingDate = new DateTime(2027, 6, 5),
            NextAttemptDate = new DateTime(2027, 6, 5), Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Un_estado_de_cuota_anual_lo_cobra_el_servicio_de_la_cuota(bool forzado)
    {
        await using var db = await ConEstadoAsync(BillingStatePurpose.AnnualFee, planAnual: true);
        _cuota.Setup(c => c.ChargeDueAsync("estado", forzado, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result<AnnualFeeChargeOutcome>.Success(
                  new AnnualFeeChargeOutcome(AnnualFeeOutcomes.Success, "Token", "ord-1", null)));

        var r = await Procesador(db).ProcessAsync("estado", forzado);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Success");
        r.Value.FundingSource.Should().Be("Token");
        r.Value.OrderId.Should().Be("ord-1");
        _cuota.Verify(c => c.ChargeDueAsync("estado", forzado, It.IsAny<CancellationToken>()), Times.Once);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_fallo_del_servicio_de_la_cuota_llega_tal_cual()
    {
        await using var db = await ConEstadoAsync(BillingStatePurpose.AnnualFee, planAnual: true);
        _cuota.Setup(c => c.ChargeDueAsync("estado", false, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result<AnnualFeeChargeOutcome>.Failure("ANNUAL_FEE_BUSY", "ocupado"));

        var r = await Procesador(db).ProcessAsync("estado");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("ANNUAL_FEE_BUSY");
    }

    [Theory]
    [InlineData(BillingStatePurpose.Membership, true)]
    [InlineData(BillingStatePurpose.AnnualFee, false)]
    public async Task Un_estado_que_mezcla_proposito_y_plan_no_se_cobra(BillingStatePurpose proposito, bool planAnual)
    {
        await using var db = await ConEstadoAsync(proposito, planAnual);

        var r = await Procesador(db).ProcessAsync("estado");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(RecurringBillingProcessor.AnnualFeeStateMismatchCode);
        _cuota.VerifyNoOtherCalls();
        (await db.Orders.CountAsync()).Should().Be(0);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter MotorDelegaLaCuotaAnualTests -nologo`
Expected: FAIL de compilación (el constructor no recibe `IAnnualFeeChargeService`).

- [ ] **Step 3: Constructor**

En `RecurringBillingProcessor.cs`, añadir `using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;` y, en los campos, después de `private readonly IMemberFundsGate _gate;`:

```csharp
    private readonly IAnnualFeeChargeService _annualFee;
```

Debajo de `MemberFundsBusyCode`:

```csharp

    /// <summary>
    /// El estado y su plan no coinciden: un estado de membresía en el plan de la cuota, o uno de cuota en
    /// otro plan. No se cobra nada; hay que mirarlo a mano (Plan K, K2).
    /// </summary>
    public const string AnnualFeeStateMismatchCode = "ANNUAL_FEE_STATE_MISMATCH";
```

En la firma del constructor, después de `IMemberFundsGate gate,` añadir `IAnnualFeeChargeService annualFee,`, y en el cuerpo, después de `_gate = gate;`, añadir `_annualFee = annualFee;`.

- [ ] **Step 4: Delegar**

En `ProcessAsync`, justo después de `var plan = state.Plan!;`, añadir:

```csharp

        // Plan K (K1, K2): un estado de cuota anual lo cobra AnnualFeeChargeService. Mismo motor
        // (planificador, trabajadores, barrido) y otro cobro: no hay suscripción que renovar. Va antes de
        // la comprobación de Stopped porque el servicio de la cuota la hace a su manera (y revive si se fuerza).
        if (state.Purpose == BillingStatePurpose.AnnualFee || plan.IsAnnualFeePlan)
            return await DelegateAnnualFeeAsync(state, plan, forceBillNow, ct);
```

Añadir el método, justo antes de `// ── Commission-balance path ───`:

```csharp
    private async Task<Result<RecurringBillingProcessorResult>> DelegateAnnualFeeAsync(
        SubscriptionBillingState state, RecurringBillingPlan plan, bool forceBillNow, CancellationToken ct)
    {
        if (state.Purpose != BillingStatePurpose.AnnualFee || !plan.IsAnnualFeePlan)
        {
            _logger.LogError(
                "Estado {StateId} de {MemberId}: propósito {Purpose} en el plan {PlanId} (IsAnnualFeePlan={IsAnnual}). No se cobra.",
                state.Id, state.MemberId, state.Purpose, plan.Id, plan.IsAnnualFeePlan);
            return Result<RecurringBillingProcessorResult>.Failure(AnnualFeeStateMismatchCode,
                "The billing state purpose does not match its plan.");
        }

        var charged = await _annualFee.ChargeDueAsync(state.Id, forceBillNow, ct);
        if (!charged.IsSuccess)
            return Result<RecurringBillingProcessorResult>.Failure(charged.ErrorCode!, charged.Error!);

        var o = charged.Value!;
        return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
        {
            BillingStateId   = state.Id,
            Outcome          = o.Outcome,
            FundingSource    = o.FundingSource,
            PaymentHistoryId = o.PaymentHistoryId,
            OrderId          = o.OrderId,
            FailureReason    = o.FailureReason
        });
    }
```

- [ ] **Step 5: Las otras pruebas que construyen el procesador**

En `CorteDeFacturacionTests.cs`, `MotorBajoCandadoTests.cs` y `RenovacionSimuladaTests.cs`, en la llamada a `new RecurringBillingProcessor(…)`, añadir justo después del argumento del candado (`new TransactionOnlyGate(db)` o `candado`):

```csharp
            new Mock<MLMConquerorGlobalEdition.Billing.Services.AnnualFee.IAnnualFeeChargeService>().Object,
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "MotorDelegaLaCuotaAnualTests|CorteDeFacturacionTests|MotorBajoCandadoTests|RenovacionSimuladaTests|PlanAnualSinEstadosPorSuscripcionTests" -nologo`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorDelegaLaCuotaAnualTests.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/CorteDeFacturacionTests.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/MotorBajoCandadoTests.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/RenovacionSimuladaTests.cs
git commit -m "Un solo motor: RecurringBillingProcessor pasa los estados de cuota anual al servicio de la cuota sin crear pedidos de renovacion ni tocar suscripciones, y no cobra un estado cuyo proposito no coincide con su plan"
```

---

### Task 17: API de Billing para pagar y conciliar, y registro completo

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Controllers/AnnualFeeController.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/AnnualFeeControllerTests.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/DI/AnnualFeeDiTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

`MLMConquerorGlobalEdition.Billing.Tests/AnnualFeeControllerTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests;

/// <summary>
/// Pagar la cuota en Billing (Plan K): el miembro sale del token; el personal no paga por nadie y una
/// suplantación tampoco; el permiso de step-up se valida aquí también y se quema con la marca de Billing
/// salvo en una repetición de una clave que ya tiene pago. Los 403 van con "data":null.
/// </summary>
public class AnnualFeeControllerTests
{
    private readonly Mock<IAnnualFeeChargeService> _svc = new();
    private readonly Mock<IStepUpGrantCheck> _stepUp = new();

    private static ClaimsPrincipal Usuario(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static ClaimsPrincipal Miembro(params Claim[] extra) =>
        Usuario(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-001"),
            new Claim(CallerIdentity.MemberIdClaim, "AMB-100001"),
            new Claim(ClaimTypes.Role, AppRoles.Ambassador)
        }.Concat(extra).ToArray());

    private AnnualFeeController Controlador(ClaimsPrincipal user, params string[] grants)
    {
        _svc.Setup(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnnualFeePaymentResultDto>.Success(new AnnualFeePaymentResultDto { PaymentId = "p1" }));
        var ctx = new DefaultHttpContext { User = user };
        if (grants.Length > 0) ctx.Request.Headers[StepUpGrant.HeaderName] = grants;
        return new AnnualFeeController(_svc.Object, _stepUp.Object, NullLogger<AnnualFeeController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    private static PayAnnualFeeRequest Pedido(string clave = "clave-1") =>
        new() { Method = AnnualFeeMethods.CommissionBalance, IdempotencyKey = clave };

    private void PermisoBueno(bool sinQuemar = true)
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.AnnualFeeFromBalance)).Returns("jti-1");
        _stepUp.Setup(s => s.TryConsumeAsync("jti-1", It.IsAny<CancellationToken>())).ReturnsAsync(sinQuemar);
    }

    private static void Es403(IActionResult r, string codigo)
    {
        var obj = r.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        obj.Value.Should().BeOfType<ApiResponse<object>>().Which.ErrorCode.Should().Be(codigo);
    }

    [Fact]
    public async Task Con_permiso_valido_paga_el_miembro_del_token_y_quema_el_permiso()
    {
        PermisoBueno();

        var r = await Controlador(Miembro(), "permiso").Pay(Pedido(), default);

        r.Should().BeOfType<OkObjectResult>();
        _svc.Verify(s => s.PayNowAsync(It.Is<AnnualFeePayNowInput>(i => i.MemberId == "AMB-100001" && i.Actor == "user-001"),
            It.IsAny<CancellationToken>()), Times.Once);
        _stepUp.Verify(s => s.TryConsumeAsync("jti-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_permiso_es_403_STEP_UP_REQUIRED()
    {
        var r = await Controlador(Miembro()).Pay(Pedido(), default);

        Es403(r, StepUpGrant.RequiredErrorCode);
        _svc.Verify(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_permiso_de_comprar_tokens_no_vale_para_la_cuota()
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.TokenPurchaseFromBalance)).Returns("jti-t");

        Es403(await Controlador(Miembro(), "permiso").Pay(Pedido(), default), StepUpGrant.RequiredErrorCode);
    }

    [Fact]
    public async Task Con_el_permiso_ya_quemado_en_Billing_es_403_STEP_UP_INVALID()
    {
        PermisoBueno(sinQuemar: false);

        Es403(await Controlador(Miembro(), "permiso").Pay(Pedido(), default), StepUpGrant.InvalidErrorCode);
        _svc.Verify(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_la_cache_no_responde_falla_cerrado()
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.AnnualFeeFromBalance)).Returns("jti-1");
        _stepUp.Setup(s => s.TryConsumeAsync("jti-1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("redis"));

        Es403(await Controlador(Miembro(), "permiso").Pay(Pedido(), default), StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Repetir_una_clave_con_pago_no_quema_el_permiso()
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.AnnualFeeFromBalance)).Returns("jti-1");
        _svc.Setup(s => s.HasPaymentAsync("AMB-100001", "clave-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var r = await Controlador(Miembro(), "permiso").Pay(Pedido(), default);

        r.Should().BeOfType<OkObjectResult>();
        _stepUp.Verify(s => s.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suplantando_no_se_paga_ni_se_mira_el_permiso()
    {
        var r = await Controlador(Miembro(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9")), "permiso")
            .Pay(Pedido(), default);

        Es403(r, AnnualFeeErrors.Impersonation);
        _stepUp.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task El_personal_sin_miembro_no_paga_por_nadie()
    {
        var r = await Controlador(Usuario(new Claim(ClaimTypes.NameIdentifier, "staff-1"), new Claim(ClaimTypes.Role, AppRoles.Admin)))
            .Pay(Pedido(), default);

        Es403(r, AnnualFeeErrors.NotAMember);
    }

    [Theory]
    [InlineData(AnnualFeeErrors.IdempotencyKeyReused, StatusCodes.Status409Conflict)]
    [InlineData(AnnualFeeErrors.Busy, StatusCodes.Status409Conflict)]
    [InlineData(AnnualFeeErrors.InProgress, StatusCodes.Status409Conflict)]
    [InlineData(AnnualFeeErrors.NotDue, StatusCodes.Status400BadRequest)]
    [InlineData(AnnualFeeErrors.MethodNotSupported, StatusCodes.Status400BadRequest)]
    public async Task Los_fallos_salen_con_su_estado(string codigo, int estado)
    {
        PermisoBueno();
        var c = Controlador(Miembro(), "permiso");
        _svc.Setup(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnnualFeePaymentResultDto>.Failure(codigo, "x"));

        var r = await c.Pay(Pedido(), default);

        r.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(estado);
    }

    [Fact]
    public void Conciliar_es_solo_para_tesoreria()
    {
        var roles = typeof(AnnualFeeController).GetMethod(nameof(AnnualFeeController.Reconcile))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
        roles.Should().BeEquivalentTo(AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.BillingManager);
    }

    [Fact]
    public void Pagar_no_recibe_un_memberId()
    {
        foreach (var p in typeof(AnnualFeeController).GetMethod(nameof(AnnualFeeController.Pay))!.GetParameters())
        {
            p.Name!.Should().NotContainEquivalentOf("memberId");
            p.ParameterType.GetProperty("MemberId").Should().BeNull();
        }
    }
}
```

`MLMConquerorGlobalEdition.Billing.Tests/DI/AnnualFeeDiTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Extensions;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.DI;

/// <summary>
/// La cuota anual y el motor se resuelven con el registro real (AddAnnualFeeServices, el mismo que llama
/// Program.cs) sobre lo que Program.cs registra antes. Si falta una pieza, falla aquí y no de madrugada.
/// </summary>
public class AnnualFeeDiTests
{
    private static ServiceProvider Proveedor(bool conCuota = true)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReceiptStorage:LocalPath"]        = Path.Combine(Path.GetTempPath(), "di-test-receipts-cuota"),
            ["ReceiptStorage:PublicBaseUrl"]    = "https://localhost:7001",
            ["DataProtection:Certificate:Path"] = Path.Combine(Path.GetTempPath(), "di-test-keyring", "cert.pfx"),
            ["Jwt:Issuer"]          = "MLMConquerorGlobalEdition",
            ["Jwt:Audience"]        = "MLMConquerorGlobalEdition.Clients",
            ["Jwt:PublicKeyBase64"] = Convert.ToBase64String(
                System.Security.Cryptography.RSA.Create(2048).ExportSubjectPublicKeyInfo())
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
                ApplicationName = "DiTests",
                ContentRootPath = Path.GetTempPath()
            });

        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("di-cuota-" + Guid.NewGuid().ToString("N")));

        // ── Lo que Program.cs registra antes de la cuota anual ───────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddDistributedMemoryCache();
        services.AddSingleton(Mock.Of<MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService>());
        services.AddSingleton(Mock.Of<Hangfire.IBackgroundJobClient>());
        services.AddSingleton(Mock.Of<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService>());
        services.AddTransient<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService,
                              MLMConquerorGlobalEdition.SharedKernel.Services.NullEmailService>();

        services.AddScoped<SpreedlyCardGatewayService>();
        services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<SpreedlyCardGatewayService>());
        services.AddScoped<ICardGatewayResolver, CardGatewayResolver>();
        services.AddScoped<IGatewaySplitSelector, GatewaySplitSelector>();
        services.AddScoped<IGatewayRouter, GatewayRouter>();
        services.AddScoped<IGatewayChargeOrchestrator, GatewayChargeOrchestrator>();
        services.AddSingleton<ICardBrandDetector, CardBrandDetector>();
        services.AddHttpClient("CurrencyConverter");
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddScoped<IRecurringBillingScheduler, RecurringBillingScheduler>();
        services.AddScoped<ICommissionBalanceService, CommissionBalanceService>();
        services.AddScoped<IRecurringBillingProcessor, RecurringBillingProcessor>();

        services.AddPayoutServices();
        services.AddTokenPurchaseServices();

        // ── Lo que se prueba ──────────────────────────────────────────────────
        if (conCuota) services.AddAnnualFeeServices();

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(IAnnualFeeChargeService))]
    [InlineData(typeof(IAnnualFeeStateEnrollment))]
    [InlineData(typeof(IAnnualFeeTokenConsumer))]
    [InlineData(typeof(AnnualFeeStateEnrollmentJob))]
    [InlineData(typeof(AnnualFeeStalePendingJob))]
    [InlineData(typeof(IRecurringBillingProcessor))]
    public void Cada_pieza_se_resuelve(Type servicio)
    {
        using var provider = Proveedor();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService(servicio).Should().NotBeNull();
    }

    [Fact]
    public void El_controlador_se_construye_con_lo_registrado()
    {
        using var provider = Proveedor();
        using var scope = provider.CreateScope();

        ActivatorUtilities.CreateInstance<AnnualFeeController>(scope.ServiceProvider).Should().NotBeNull();
    }

    [Fact]
    public void Sin_el_registro_el_motor_no_se_resuelve()
    {
        using var provider = Proveedor(conCuota: false);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IRecurringBillingProcessor>();
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "AnnualFeeControllerTests|AnnualFeeDiTests" -nologo`
Expected: FAIL de compilación (`AnnualFeeController` no existe).

- [ ] **Step 3: El controlador**

`MLMConquerorGlobalEdition.Billing/Controllers/AnnualFeeController.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Controllers;

/// <summary>Cuota anual del Biz Center (Plan K). Lo llama el BizCenter reenviando el JWT del miembro.</summary>
/// <remarks>
/// QUIEN PAGA ES EL DEL TOKEN: el contrato no lleva MemberId. El personal (sin memberId) no paga por nadie;
/// una suplantación tampoco, sea o no de solo lectura. Todo pago de la cuota es con lo que ya es del
/// miembro (comisiones, eWallet o un token), así que exige siempre el permiso de step-up, validado aquí
/// también para que llamar directamente a Billing no se salte el código.
/// </remarks>
[ApiController]
[Route("api/v1/billing/annual-fee")]
[Authorize]
public class AnnualFeeController : ControllerBase
{
    private const string Treasury = AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.BillingManager;

    private readonly IAnnualFeeChargeService _service;
    private readonly IStepUpGrantCheck _stepUp;
    private readonly ILogger<AnnualFeeController> _logger;

    public AnnualFeeController(IAnnualFeeChargeService service, IStepUpGrantCheck stepUp, ILogger<AnnualFeeController> logger)
    {
        _service = service;
        _stepUp  = stepUp;
        _logger  = logger;
    }

    /// <summary>POST api/v1/billing/annual-fee/pay — "Pagar ahora" con token, comisiones o eWallet.</summary>
    [HttpPost("pay")]
    public async Task<IActionResult> Pay([FromBody] PayAnnualFeeRequest request, CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null)
            return Forbidden(AnnualFeeErrors.NotAMember, "Only members can pay their annual fee.");
        if (ImpersonationScope.IsImpersonating(User))
            return Forbidden(AnnualFeeErrors.Impersonation, "The annual fee cannot be paid during impersonation.");

        var userId  = CallerIdentity.UserIdOf(User) ?? string.Empty;
        var refusal = await AuthorizeAsync(memberId, userId, request.IdempotencyKey, ct);
        if (refusal is not null) return refusal;

        var actor  = string.IsNullOrEmpty(userId) ? memberId : userId;
        var result = await _service.PayNowAsync(new AnnualFeePayNowInput(memberId, actor, request), ct);
        if (result.IsSuccess) return Ok(ApiResponse<AnnualFeePaymentResultDto>.Ok(result.Value!));

        // Conflicto con lo guardado o con otra operación en curso: 409. Lo demás: la petición no vale (400).
        var body = ApiResponse<AnnualFeePaymentResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier);
        return result.ErrorCode is AnnualFeeErrors.IdempotencyKeyReused or AnnualFeeErrors.Busy
                                 or AnnualFeeErrors.InProgress or AnnualFeeErrors.AlreadyPaid
            ? StatusCode(StatusCodes.Status409Conflict, body)
            : BadRequest(body);
    }

    /// <summary>
    /// POST api/v1/billing/annual-fee/payments/{paymentId}/reconcile — tesorería cierra un pago a conciliar
    /// tras mirar i-Payout: cobrado (completa y avanza el ciclo) o no cobrado (fallido, el ciclo queda libre).
    /// </summary>
    [HttpPost("payments/{paymentId}/reconcile")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> Reconcile(string paymentId, [FromBody] ReconcileAnnualFeePaymentRequest request, CancellationToken ct)
    {
        var email  = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var actor  = string.IsNullOrEmpty(email) ? CallerIdentity.UserIdOf(User) ?? "admin" : email;
        var result = await _service.ReconcileAsync(paymentId, request, actor, ct);
        if (result.IsSuccess) return Ok(ApiResponse<AnnualFeePaymentResultDto>.Ok(result.Value!));

        var status = result.ErrorCode switch
        {
            AnnualFeeErrors.PaymentNotFound                         => StatusCodes.Status404NotFound,
            AnnualFeeErrors.NotUnderReview or AnnualFeeErrors.Busy => StatusCodes.Status409Conflict,
            _                                                       => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, ApiResponse<AnnualFeePaymentResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Mismo orden que la compra de tokens: (1) validar el permiso; sin él, 403 STEP_UP_REQUIRED. (2) Si la
    /// clave ya tiene pago, NO se quema: el servicio solo devolverá lo que ya pasó. (3) Si no, se quema
    /// con la marca de Billing antes de cobrar; ya usado, 403 STEP_UP_INVALID. Caché caída: falla cerrado.
    /// </summary>
    private async Task<IActionResult?> AuthorizeAsync(string memberId, string userId, string? idempotencyKey, CancellationToken ct)
    {
        var jti = _stepUp.ValidJti(SingleGrantHeader(), userId, StepUpOperations.AnnualFeeFromBalance);
        if (jti is null)
            return Forbidden(StepUpGrant.RequiredErrorCode, "Paying the annual fee requires a verification code.");

        if (await _service.HasPaymentAsync(memberId, idempotencyKey, ct))
            return null;

        bool claimed;
        try
        {
            claimed = await _stepUp.TryConsumeAsync(jti, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pago de cuota rechazado: no se pudo marcar el permiso de step-up como usado. Usuario {UserId}, TraceId {TraceId}.",
                userId, HttpContext.TraceIdentifier);
            return Forbidden(StepUpGrant.InvalidErrorCode, "The verification could not be checked. Try again.");
        }

        if (!claimed)
        {
            _logger.LogWarning("Pago de cuota rechazado: permiso de step-up repetido. Usuario {UserId}, jti {Jti}, TraceId {TraceId}.",
                userId, jti, HttpContext.TraceIdentifier);
            return Forbidden(StepUpGrant.InvalidErrorCode, "This verification was already used. Request a new code.");
        }

        return null;
    }

    private string? SingleGrantHeader()
    {
        var values = Request.Headers[StepUpGrant.HeaderName];
        return values.Count == 1 ? values[0] : null;
    }

    private IActionResult Forbidden(string code, string message) =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(code, message, HttpContext.TraceIdentifier));
}
```

- [ ] **Step 4: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "AnnualFeeControllerTests|AnnualFeeDiTests|TokenPurchaseDiTests" -nologo`
Expected: PASS. Si `AnnualFeeDiTests` falla porque falta un servicio que `Program.cs` sí registra, añadirlo en el bloque «Lo que Program.cs registra antes» de la prueba (no en `AddAnnualFeeServices`) y anotarlo en el comentario.

- [ ] **Step 5: Ejecutar Billing entero**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS (las `[SqlServerFact]` necesitan la migración de la Tarea 5 aplicada en local).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Controllers/AnnualFeeController.cs \
        MLMConquerorGlobalEdition.Billing.Tests/AnnualFeeControllerTests.cs \
        MLMConquerorGlobalEdition.Billing.Tests/DI/AnnualFeeDiTests.cs
git commit -m "API de Billing para la cuota anual: pagar con el miembro del token, step-up validado y quemado con la marca de Billing salvo en una repeticion, nunca suplantando, y conciliar solo para tesoreria; prueba de que el motor y la cuota se resuelven con el registro real"
```

---

### Task 18: Pruebas contra SQL Server

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/SqlServerCuotaAnualTests.cs`

- [ ] **Step 1: Escribir las pruebas**

`MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/SqlServerCuotaAnualTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Lo que InMemory no puede probar (Plan K, R14): los dos índices únicos filtrados, el CHECK del propósito
/// y dos "Pagar ahora" simultáneos del mismo miembro con el candado real. Cada prueba usa un miembro
/// sintético propio y borra lo que escribió. Se saltan sin SQL Server; exigen la migración CuotaAnual.
/// </summary>
public class SqlServerCuotaAnualTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TSK-{Guid.NewGuid():N}"[..20];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!SqlServerTestDb.Reachable) return;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = SqlServerTestDb.Db();
                var m = _member;
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET DEADLOCK_PRIORITY HIGH;
                    DELETE FROM AnnualFeePayments WHERE MemberId = {m};
                    DELETE FROM RecurringBillingAttempts WHERE MemberId = {m};
                    DELETE FROM TokenTransactions WHERE MemberId = {m} OR OriginalOwnerMemberId = {m};
                    DELETE FROM TokenBalances WHERE MemberId = {m};
                    DELETE FROM PaymentHistories WHERE MemberId = {m};
                    DELETE FROM CommissionEarnings WHERE BeneficiaryMemberId = {m};
                    DELETE FROM Orders WHERE MemberId = {m};
                    DELETE FROM SubscriptionBillingStates WHERE MemberId = {m};
                    DELETE FROM MemberProfiles WHERE MemberId = {m};");
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1205 && attempt < 8)
            {
                await Task.Delay(500 * attempt);
            }
        }
    }

    private async Task<int> PlanAnualAsync(AppDbContext db) =>
        (await db.RecurringBillingPlans.AsNoTracking().SingleAsync(p => p.IsAnnualFeePlan)).Id;

    private SubscriptionBillingState EstadoAnual(int plan, string? suscripcion = null) => new()
    {
        Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = suscripcion, MemberId = _member,
        RecurringBillingPlanId = plan, BillingAnchorDate = DateTime.Today.AddYears(-1), NextBillingDate = DateTime.Today,
        NextAttemptDate = DateTime.Today, Status = RecurringBillingStatus.Active, CreatedBy = "prueba-sql",
        CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now
    };

    [SqlServerFact]
    public async Task Un_miembro_no_puede_tener_dos_estados_de_cuota()
    {
        await using var db = SqlServerTestDb.Db();
        var plan = await PlanAnualAsync(db);
        db.SubscriptionBillingStates.Add(EstadoAnual(plan));
        await db.SaveChangesAsync();

        await using var otra = SqlServerTestDb.Db();
        otra.SubscriptionBillingStates.Add(EstadoAnual(plan));
        var act = () => otra.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.InnerException!.Message
            .Should().Contain("UX_SubscriptionBillingStates_AnnualFeeMember");
    }

    [SqlServerFact]
    public async Task El_CHECK_no_deja_un_estado_de_cuota_con_suscripcion_ni_uno_de_membresia_sin_ella()
    {
        await using var db = SqlServerTestDb.Db();
        var plan = await PlanAnualAsync(db);

        db.SubscriptionBillingStates.Add(EstadoAnual(plan, suscripcion: "sub-x"));
        (await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("CK_SubscriptionBillingStates_Purpose");

        await using var otra = SqlServerTestDb.Db();
        var membresia = EstadoAnual(plan);
        membresia.Purpose = BillingStatePurpose.Membership;
        otra.SubscriptionBillingStates.Add(membresia);
        (await FluentActions.Awaiting(() => otra.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("CK_SubscriptionBillingStates_Purpose");
    }

    [SqlServerFact]
    public async Task Un_ciclo_no_admite_dos_pagos_vivos_pero_si_uno_vivo_y_otro_fallido()
    {
        await using var db = SqlServerTestDb.Db();
        var estado = EstadoAnual(await PlanAnualAsync(db));
        db.SubscriptionBillingStates.Add(estado);
        await db.SaveChangesAsync();

        AnnualFeePayment Pago(AnnualFeePaymentStatus s) => new()
        {
            MemberId = _member, SubscriptionBillingStateId = estado.Id, CycleDueDate = DateTime.Today, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.Automatic, Status = s,
            OrderId = Guid.NewGuid().ToString(), CreatedBy = "prueba-sql", CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now
        };

        db.AnnualFeePayments.Add(Pago(AnnualFeePaymentStatus.Failed));
        db.AnnualFeePayments.Add(Pago(AnnualFeePaymentStatus.NeedsReconciliation));
        await db.SaveChangesAsync();

        await using var otra = SqlServerTestDb.Db();
        otra.AnnualFeePayments.Add(Pago(AnnualFeePaymentStatus.Pending));
        (await FluentActions.Awaiting(() => otra.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("UX_AnnualFeePayments_LivePerCycle");
    }

    [SqlServerFact]
    public async Task Dos_Pagar_ahora_a_la_vez_con_claves_distintas_cobran_una_sola_vez()
    {
        await using (var semilla = SqlServerTestDb.Db())
        {
            semilla.MemberProfiles.Add(new MemberProfile
            {
                UserId = Guid.NewGuid(), MemberId = _member, Email = $"{_member.ToLowerInvariant()}@prueba.test",
                FirstName = "Prueba", LastName = "Cuota", Country = "US", MemberType = MemberType.Ambassador,
                Status = MemberAccountStatus.Active, EnrollDate = DateTime.Today.AddYears(-1), FeesFromBalance = true,
                CreatedBy = "prueba-sql", CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now
            });
            semilla.SubscriptionBillingStates.Add(EstadoAnual(await PlanAnualAsync(semilla)));
            for (var i = 0; i < 2; i++)
                semilla.TokenTransactions.Add(new TokenTransaction
                {
                    MemberId = _member, TokenTypeId = AnnualFeeRules.AnnualBizCenterTokenTypeId,
                    TransactionType = TokenTransactionType.Purchased, Quantity = 1,
                    ReferenceId = $"K{Guid.NewGuid():N}"[..16].ToUpperInvariant(), OriginalOwnerMemberId = _member,
                    Status = TokenInstanceStatus.Issued, CreatedBy = "prueba-sql", CreationDate = DateTime.Now.AddDays(-i - 1)
                });
            await semilla.SaveChangesAsync();
        }

        async Task<string?> PagarAsync(string clave)
        {
            await using var db = SqlServerTestDb.Db();
            var reloj = CuotaAnualDb.Reloj(DateTime.Now);
            var servicio = new AnnualFeeChargeService(
                db, reloj, new SqlServerMemberFundsGate(db), new CommissionBalanceService(db, reloj),
                new Mock<IEWalletFunds>().Object, new AnnualFeeTokenConsumer(db),
                new AnnualFeeStateEnrollment(db, reloj, NullLogger<AnnualFeeStateEnrollment>.Instance),
                new RecurringBillingScheduler(), new ConfigurationBuilder().Build(), NullLogger<AnnualFeeChargeService>.Instance);
            var r = await servicio.PayNowAsync(new AnnualFeePayNowInput(_member, "prueba-sql",
                new PayAnnualFeeRequest { Method = AnnualFeeMethods.Token, IdempotencyKey = clave }));
            return r.IsSuccess ? null : r.ErrorCode;
        }

        var resultados = await Task.WhenAll(PagarAsync("clave-a"), PagarAsync("clave-b"));

        resultados.Count(e => e is null).Should().Be(1);
        resultados.Single(e => e is not null).Should().BeOneOf(AnnualFeeErrors.NotDue, AnnualFeeErrors.AlreadyPaid);

        await using var lectura = SqlServerTestDb.Db();
        (await lectura.AnnualFeePayments.CountAsync(p => p.MemberId == _member && p.Status == AnnualFeePaymentStatus.Completed))
            .Should().Be(1);
        (await AnnualFeeTokenQueries.Available(lectura, _member, DateTime.Now).CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter SqlServerCuotaAnualTests -nologo`
Expected: PASS (4 pruebas) con la migración `CuotaAnual` aplicada en local; **saltadas** (no verdes) si no hay SQL Server. Si una sale «saltada» con SQL Server levantado, revisar `MLM_TEST_SQLSERVER` antes de seguir.

- [ ] **Step 3: Comprobar que no queda nada**

```bash
sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SubscriptionBillingStates WHERE MemberId LIKE 'TSK-%'; SELECT COUNT(*) FROM AnnualFeePayments WHERE MemberId LIKE 'TSK-%'; SELECT COUNT(*) FROM MemberProfiles WHERE MemberId LIKE 'TSK-%';"
```

Expected: `0`, `0`, `0`.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing.Tests/Services/AnnualFee/SqlServerCuotaAnualTests.cs
git commit -m "Pruebas contra SQL Server de la cuota anual: un estado de cuota por miembro, CHECK del proposito, un pago vivo por ciclo y dos Pagar ahora simultaneos que cobran una sola vez con el candado real"
```

---

# Bloque 4 — Calificado

### Task 19: La anualidad pagada cuenta en «Calificado»

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/AnnualFee/BillingStateAnnualFeeStatus.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/CuotaAnualSiembra.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/BillingStateAnnualFeeStatusTests.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/RegistroDeLaAnualidadTests.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/IAnnualFeeStatus.cs` (se borra `AnnualFeeNotModeled`)
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs:98-99`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs:85-86`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/QualificationServiceTests.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CalificadoEnLosEquiposTests.cs`

- [ ] **Step 1: Siembra de las pruebas de la cuota en el BizCenter**

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/CuotaAnualSiembra.cs`:

```csharp
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Siembra de las pruebas de la cuota anual del lado del BizCenter (Plan K). Solo añade al contexto: quien
/// la usa guarda. El reloj es el de SharedKernel (RelojFijo), el que reciben los servicios de Repository.
/// </summary>
public static class CuotaAnualSiembra
{
    public static readonly DateTime Ahora = new(2027, 6, 10, 9, 0, 0);
    public static RelojFijo Reloj() => new(Ahora);

    public static void Plan(AppDbContext db, decimal precio = 99m)
    {
        db.Products.Add(new Product
        {
            Id = "prod-amb", Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1,
            MonthlyFee = 0m, SetupFee = 99m, AnnualPrice = precio, IsActive = true, CreatedBy = "prueba"
        });
        var plan = new RecurringBillingPlan
        {
            Id = 2, Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            IsActive = true, IsAnnualFeePlan = true, CreatedBy = "prueba"
        };
        plan.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = "prod-amb", CreatedBy = "prueba" });
        db.RecurringBillingPlans.Add(plan);
    }

    public static SubscriptionBillingState Estado(
        AppDbContext db, string memberId, DateTime vence, RecurringBillingStatus estado = RecurringBillingStatus.Active,
        BillingStatePurpose proposito = BillingStatePurpose.AnnualFee, DateTime? ultimoCobro = null)
    {
        var s = new SubscriptionBillingState
        {
            Id = $"estado-{memberId}-{proposito}", Purpose = proposito,
            MembershipSubscriptionId = proposito == BillingStatePurpose.Membership ? "sub-" + memberId : null,
            MemberId = memberId, RecurringBillingPlanId = 2, BillingAnchorDate = vence.AddYears(-1),
            NextBillingDate = vence, NextAttemptDate = vence, LastSuccessfulBillingDate = ultimoCobro,
            Status = estado, CreatedBy = "prueba"
        };
        db.SubscriptionBillingStates.Add(s);
        return s;
    }

    public static AnnualFeePayment Pago(
        AppDbContext db, string memberId, string estadoId, DateTime ciclo, AnnualFeePaymentStatus estado, DateTime creado,
        AnnualFeePaymentMethod metodo = AnnualFeePaymentMethod.CommissionBalance, decimal importe = 99m)
    {
        var p = new AnnualFeePayment
        {
            MemberId = memberId, SubscriptionBillingStateId = estadoId, CycleDueDate = ciclo, Amount = importe,
            Method = metodo, Source = AnnualFeePaymentSource.PayNow, Status = estado, OrderId = Guid.NewGuid().ToString(),
            CreatedBy = "prueba", CreationDate = creado,
            CompletedAt = estado == AnnualFeePaymentStatus.Completed ? creado : null
        };
        db.AnnualFeePayments.Add(p);
        return p;
    }

    public static void Token(AppDbContext db, string memberId, int tipo = AnnualFeeRules.AnnualBizCenterTokenTypeId,
        TokenInstanceStatus estado = TokenInstanceStatus.Issued) =>
        db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId = memberId, TokenTypeId = tipo, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant(), OriginalOwnerMemberId = memberId,
            Status = estado, CreatedBy = "prueba", CreationDate = Ahora.AddDays(-3)
        });
}
```

- [ ] **Step 2: Escribir las pruebas que fallan**

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/BillingStateAnnualFeeStatusTests.cs`:

```csharp
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Plan K (K16): «anualidad pagada» = el estado de cuota anual está al día (vence después de hoy). Es la
/// rama (b) de «Calificado» del Plan J, que hasta ahora nunca se cumplía (AnnualFeeNotModeled).
/// </summary>
public class BillingStateAnnualFeeStatusTests
{
    private static readonly DateTime Hoy = CuotaAnualSiembra.Ahora.Date;

    private static async Task<IReadOnlySet<string>> PagadosAsync(Action<AppDbContext> siembra, params string[] ids)
    {
        await using var db = ReportesDb.Nueva();
        siembra(db);
        await db.SaveChangesAsync();
        return await new BillingStateAnnualFeeStatus(db, CuotaAnualSiembra.Reloj()).PaidCurrentAnnualFeeAsync(ids);
    }

    [Fact]
    public async Task Un_estado_que_vence_despues_de_hoy_esta_pagado()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(1)), "AMB-1"))
            .Should().BeEquivalentTo(new[] { "AMB-1" });

    [Fact]
    public async Task Si_vence_hoy_ya_no_esta_pagado()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy), "AMB-1")).Should().BeEmpty();

    [Fact]
    public async Task Un_estado_vencido_no_esta_pagado()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(-20)), "AMB-1")).Should().BeEmpty();

    [Fact]
    public async Task Un_estado_parado_no_cuenta()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(100), RecurringBillingStatus.Stopped), "AMB-1"))
            .Should().BeEmpty();

    [Fact]
    public async Task Un_estado_de_membresia_no_es_la_cuota()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(30), proposito: BillingStatePurpose.Membership), "AMB-1"))
            .Should().BeEmpty();

    [Fact]
    public async Task Sin_estado_no_esta_pagado_y_por_lote_solo_salen_los_pagados()
        => (await PagadosAsync(db =>
            {
                CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(200));
                CuotaAnualSiembra.Estado(db, "AMB-2", Hoy.AddDays(-1));
            }, "AMB-1", "AMB-2", "AMB-3", "AMB-1"))
            .Should().BeEquivalentTo(new[] { "AMB-1" });

    [Fact]
    public async Task Calificado_con_la_anualidad_real_inscrito_hace_dos_anios()
    {
        await using var db = ReportesDb.Nueva();
        var alta = CuotaAnualSiembra.Ahora.AddYears(-2);
        ReportesDb.Miembro(db, "AMB-1", alta: alta);
        ReportesDb.Miembro(db, "AMB-2", alta: alta);
        ReportesDb.Suscripcion(db, "AMB-1", 3, MembershipStatus.Active, alta);
        ReportesDb.Suscripcion(db, "AMB-2", 3, MembershipStatus.Active, alta);
        CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(120));
        CuotaAnualSiembra.Estado(db, "AMB-2", Hoy.AddDays(-3));
        await db.SaveChangesAsync();

        var reloj = CuotaAnualSiembra.Reloj();
        var calificado = await new QualificationService(db, reloj, new BillingStateAnnualFeeStatus(db, reloj))
            .AreQualifiedAsync(new[] { "AMB-1", "AMB-2" });

        calificado["AMB-1"].Should().BeTrue();
        calificado["AMB-2"].Should().BeFalse();
    }
}
```

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/RegistroDeLaAnualidadTests.cs`:

```csharp
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Plan K (K16): los dos anfitriones que calculan «Calificado» (BizCenter y AdminAPI) registran la
/// anualidad real, y la implementación que siempre decía «nadie pagó» ya no existe para que nadie la
/// vuelva a registrar por error.
/// </summary>
public class RegistroDeLaAnualidadTests
{
    [Theory]
    [InlineData("MLMConquerorGlobalEdition.BizCenter")]
    [InlineData("MLMConquerorGlobalEdition.AdminAPI")]
    public void El_anfitrion_registra_la_anualidad_real(string proyecto)
    {
        var programa = File.ReadAllText(Path.Combine(ReportesDb.RaizDelRepositorio(), proyecto, "Program.cs"));
        programa.Should().Contain("Repository.Services.AnnualFee.BillingStateAnnualFeeStatus>")
                .And.NotContain("AnnualFeeNotModeled");
    }

    [Fact]
    public void AnnualFeeNotModeled_ya_no_existe()
        => typeof(MLMConquerorGlobalEdition.Repository.Services.Teams.IAnnualFeeStatus).Assembly
            .GetTypes().Should().NotContain(t => t.Name == "AnnualFeeNotModeled");
}
```

- [ ] **Step 3: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "BillingStateAnnualFeeStatusTests|RegistroDeLaAnualidadTests" -nologo`
Expected: FAIL de compilación (`BillingStateAnnualFeeStatus` no existe).

- [ ] **Step 4: La implementación real**

`MLMConquerorGlobalEdition.Repository/Services/AnnualFee/BillingStateAnnualFeeStatus.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// «¿Pagó su anualidad?» de verdad (Plan K, K16): el estado de cuota anual del miembro está al día, es
/// decir, su siguiente vencimiento es posterior a hoy (hora de servidor) y no está parado. Un estado
/// vencido sin pagar, parado o con el pago a conciliar (el estado no avanzó) no está al día. Sustituye a
/// AnnualFeeNotModeled, que siempre decía que no.
/// </summary>
/// <remarks>
/// Un estado recién creado para un Ambassador antiguo vence en su próximo aniversario y cuenta como al
/// día hasta entonces, aunque no haya pagado en este sistema (no se migran datos del legado). Pregunta
/// P1 del Plan K: si Nick prefiere exigir un cobro registrado, basta con añadir
/// <c>s.LastSuccessfulBillingDate != null</c> a la consulta.
/// </remarks>
public sealed class BillingStateAnnualFeeStatus : IAnnualFeeStatus
{
    private static readonly IReadOnlySet<string> Nobody = new HashSet<string>();

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public BillingStateAnnualFeeStatus(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<IReadOnlySet<string>> PaidCurrentAnnualFeeAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (ids.Count == 0) return Nobody;

        var tomorrow = _clock.Now.Date.AddDays(1);
        var paid = await _db.SubscriptionBillingStates.AsNoTracking()
            .Where(s => s.Purpose == BillingStatePurpose.AnnualFee
                     && ids.Contains(s.MemberId)
                     && s.Status != RecurringBillingStatus.Stopped
                     && s.NextBillingDate >= tomorrow)
            .Select(s => s.MemberId)
            .ToListAsync(ct);

        return paid.ToHashSet(StringComparer.Ordinal);
    }
}
```

- [ ] **Step 5: Borrar `AnnualFeeNotModeled` y actualizar el comentario de la interfaz**

En `MLMConquerorGlobalEdition.Repository/Services/Teams/IAnnualFeeStatus.cs`, sustituir el archivo entero por:

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// ¿Pagó el miembro su anualidad en curso? Rama (b) de «Calificado» (Plan J, corrección de Nick N3).
/// La implementa <c>Repository.Services.AnnualFee.BillingStateAnnualFeeStatus</c> (Plan K): la anualidad
/// cuenta como pagada si el estado de cuota anual del miembro está al día.
/// </summary>
public interface IAnnualFeeStatus
{
    /// <summary>Los miembros de <paramref name="memberIds"/> con la anualidad en curso pagada.</summary>
    Task<IReadOnlySet<string>> PaidCurrentAnnualFeeAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default);
}
```

- [ ] **Step 6: Registrar la implementación real en los dos anfitriones**

En `MLMConquerorGlobalEdition.BizCenter/Program.cs`, sustituir:

```csharp
// «Calificado» (Plan J, N3): un solo servicio. La anualidad no existe todavía (Plan K).
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IAnnualFeeStatus,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.AnnualFeeNotModeled>();
```

por:

```csharp
// «Calificado» (Plan J, N3): un solo servicio. La anualidad pagada es el estado de cuota anual al día (Plan K).
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IAnnualFeeStatus,
                            MLMConquerorGlobalEdition.Repository.Services.AnnualFee.BillingStateAnnualFeeStatus>();
```

En `MLMConquerorGlobalEdition.AdminAPI/Program.cs`, sustituir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IAnnualFeeStatus,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.AnnualFeeNotModeled>();
```

por:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IAnnualFeeStatus,
                            MLMConquerorGlobalEdition.Repository.Services.AnnualFee.BillingStateAnnualFeeStatus>();
```

Los dos anfitriones ya registran el `IDateTimeProvider` de SharedKernel (`BizCenter/Program.cs:138`, `AdminAPI/Program.cs:88`).

- [ ] **Step 7: Las pruebas del Plan J que usaban la implementación borrada**

En `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/QualificationServiceTests.cs`:

- en el comentario de la clase, sustituir «La anualidad todavía no existe (Plan K): hoy la da AnnualFeeNotModeled, que siempre dice que no.» por «La anualidad la da IAnnualFeeStatus; aquí se simula (la real, BillingStateAnnualFeeStatus, tiene sus pruebas en CuotaAnual).»;
- sustituir `new(db, Reloj, anualidad ?? new AnnualFeeNotModeled());` por `new(db, Reloj, anualidad ?? new AnualidadPagadaPor());`;
- borrar la prueba `La_anualidad_no_modelada_nunca_esta_pagada` (las tres líneas desde `[Fact]`).

En `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CalificadoEnLosEquiposTests.cs`, añadir `using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;` y sustituir:

```csharp
    internal static QualificationService Calificacion(AppDbContext db) =>
        new(db, new RelojFijo(new DateTime(2026, 10, 16, 12, 0, 0)), new AnnualFeeNotModeled());
```

por:

```csharp
    // Sin estados de cuota en la siembra: nadie tiene la anualidad pagada, como antes del Plan K.
    internal static QualificationService Calificacion(AppDbContext db)
    {
        var reloj = new RelojFijo(new DateTime(2026, 10, 16, 12, 0, 0));
        return new(db, reloj, new BillingStateAnnualFeeStatus(db, reloj));
    }
```

- [ ] **Step 8: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "BillingStateAnnualFeeStatusTests|RegistroDeLaAnualidadTests|QualificationServiceTests|CalificadoEnLosEquiposTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.slnx -nologo`
Expected: compila (nadie más usa `AnnualFeeNotModeled`; comprobarlo con `grep -rn AnnualFeeNotModeled --include=*.cs .`, que solo debe devolver la línea de `RegistroDeLaAnualidadTests`).

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/AnnualFee/BillingStateAnnualFeeStatus.cs \
        MLMConquerorGlobalEdition.Repository/Services/Teams/IAnnualFeeStatus.cs \
        MLMConquerorGlobalEdition.BizCenter/Program.cs \
        MLMConquerorGlobalEdition.AdminAPI/Program.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/CuotaAnualSiembra.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/BillingStateAnnualFeeStatusTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/RegistroDeLaAnualidadTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/QualificationServiceTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CalificadoEnLosEquiposTests.cs
git commit -m "Calificado con la anualidad real: cuenta como pagada si el estado de cuota anual vence despues de hoy y no esta parado; BizCenter y AdminAPI la registran y desaparece la implementacion que siempre decia que nadie habia pagado"
```

---

# Bloque 5 — BizCenter y pantalla

### Task 20: Resumen de la cuota y preferencia (Repository)

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeOverviewService.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/AnnualFeeOverviewServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/AnnualFeeOverviewServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Lo que enseña la tarjeta de la cuota (Plan K, K21, K22): estado, importe, vencimiento, último pago,
/// tokens de cuota, preferencia, últimos pagos y si se puede pagar ahora y por qué no, con las MISMAS
/// reglas que Billing (AnnualFeeEligibility). La preferencia solo la cambia un Ambassador.
/// </summary>
public class AnnualFeeOverviewServiceTests
{
    private const string Yo = "AMB-1";
    private static readonly DateTime Hoy = CuotaAnualSiembra.Ahora.Date;

    private static async Task<AppDbContext> BaseAsync(
        DateTime? vence = null, bool mantener = false, MemberType tipo = MemberType.Ambassador, bool conPlan = true,
        bool conEstado = true, DateTime? alta = null)
    {
        var db = ReportesDb.Nueva();
        if (conPlan) CuotaAnualSiembra.Plan(db);
        ReportesDb.Miembro(db, Yo, tipo: tipo, alta: alta ?? new DateTime(2026, 6, 5)).KeepQualified = mantener;
        if (conEstado) CuotaAnualSiembra.Estado(db, Yo, vence ?? new DateTime(2027, 6, 5), ultimoCobro: null);
        await db.SaveChangesAsync();
        return db;
    }

    private static AnnualFeeOverviewService Servicio(AppDbContext db) => new(db, CuotaAnualSiembra.Reloj());

    [Fact]
    public async Task A_un_cliente_no_le_aplica()
    {
        await using var db = await BaseAsync(tipo: MemberType.ExternalMember, conEstado: false);
        var r = await Servicio(db).GetAsync(Yo);
        r.Applies.Should().BeFalse();
        r.Status.Should().Be(AnnualFeeStatuses.NotApplicable);
        r.CanPayNow.Should().BeFalse();
    }

    [Fact]
    public async Task Al_dia_y_lejos_del_vencimiento_no_se_puede_pagar_todavia()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(200));
        var r = await Servicio(db).GetAsync(Yo);
        r.Applies.Should().BeTrue();
        r.Status.Should().Be(AnnualFeeStatuses.UpToDate);
        r.Amount.Should().Be(99m);
        r.NextDueDate.Should().Be(Hoy.AddDays(200));
        r.CanPayNow.Should().BeFalse();
        r.NotPayableReason.Should().Be(AnnualFeeErrors.NotDue);
    }

    [Fact]
    public async Task Dentro_de_los_30_dias_vence_pronto_y_se_puede_pagar()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(10));
        var r = await Servicio(db).GetAsync(Yo);
        r.Status.Should().Be(AnnualFeeStatuses.DueSoon);
        r.CanPayNow.Should().BeTrue();
        r.NotPayableReason.Should().BeNull();
    }

    [Fact]
    public async Task Vencida_se_puede_pagar()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5));
        var r = await Servicio(db).GetAsync(Yo);
        r.Status.Should().Be(AnnualFeeStatuses.Due);
        r.CanPayNow.Should().BeTrue();
    }

    [Fact]
    public async Task Con_KeepQualified_esta_exenta()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5), mantener: true);
        var r = await Servicio(db).GetAsync(Yo);
        r.Status.Should().Be(AnnualFeeStatuses.Waived);
        r.NotPayableReason.Should().Be(AnnualFeeErrors.Waived);
    }

    [Fact]
    public async Task Con_un_pago_a_conciliar_esta_en_revision_y_no_se_puede_pagar()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5));
        CuotaAnualSiembra.Pago(db, Yo, $"estado-{Yo}-AnnualFee", Hoy.AddDays(-5), AnnualFeePaymentStatus.NeedsReconciliation, Hoy.AddDays(-4));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetAsync(Yo);

        r.Status.Should().Be(AnnualFeeStatuses.UnderReview);
        r.CanPayNow.Should().BeFalse();
        r.NotPayableReason.Should().Be(AnnualFeeErrors.NeedsReview);
    }

    [Fact]
    public async Task Sin_estado_todavia_enseña_el_vencimiento_que_tendra()
    {
        await using var db = await BaseAsync(conEstado: false, alta: new DateTime(2025, 6, 1));
        var r = await Servicio(db).GetAsync(Yo);
        r.NextDueDate.Should().Be(new DateTime(2028, 6, 1)); // el aniversario de 2027 ya pasó: no se cobra hacia atrás
        r.Status.Should().Be(AnnualFeeStatuses.UpToDate);
    }

    [Fact]
    public async Task Sin_plan_configurado_no_se_puede_pagar()
    {
        await using var db = await BaseAsync(conPlan: false, vence: Hoy.AddDays(-5));
        var r = await Servicio(db).GetAsync(Yo);
        r.CanPayNow.Should().BeFalse();
        r.NotPayableReason.Should().Be(AnnualFeeErrors.PlanNotConfigured);
        r.Amount.Should().BeNull();
    }

    [Fact]
    public async Task Cuenta_los_tokens_de_cuota_vivos_y_solo_esos()
    {
        await using var db = await BaseAsync();
        CuotaAnualSiembra.Token(db, Yo, AnnualFeeRules.AnnualBizCenterTokenTypeId);
        CuotaAnualSiembra.Token(db, Yo, AnnualFeeRules.AnnualFeeTokenTypeId);
        CuotaAnualSiembra.Token(db, Yo, 24);
        CuotaAnualSiembra.Token(db, Yo, estado: TokenInstanceStatus.Used);
        CuotaAnualSiembra.Token(db, "AMB-2");
        await db.SaveChangesAsync();

        (await Servicio(db).GetAsync(Yo)).AvailableFeeTokens.Should().Be(2);
    }

    [Fact]
    public async Task Enseña_los_ultimos_cinco_pagos_del_mas_reciente_al_mas_antiguo()
    {
        await using var db = await BaseAsync();
        for (var i = 0; i < 7; i++)
            CuotaAnualSiembra.Pago(db, Yo, $"estado-{Yo}-AnnualFee", Hoy.AddYears(-i), AnnualFeePaymentStatus.Failed, Hoy.AddDays(-i));
        await db.SaveChangesAsync();

        var pagos = (await Servicio(db).GetAsync(Yo)).RecentPayments;

        pagos.Should().HaveCount(5);
        pagos.Select(p => p.Date).Should().BeInDescendingOrder();
        pagos[0].Method.Should().Be(AnnualFeeMethods.CommissionBalance);
        pagos[0].Status.Should().Be(AnnualFeePaymentStatuses.Failed);
    }

    [Fact]
    public async Task La_preferencia_se_guarda_y_se_lee()
    {
        await using var db = await BaseAsync();
        var s = Servicio(db);

        (await s.SetFeesFromBalanceAsync(Yo, false, "user-1")).Should().BeTrue();
        (await s.GetAsync(Yo)).FeesFromBalance.Should().BeFalse();
        (await db.MemberProfiles.AsNoTracking().SingleAsync()).FeesFromBalance.Should().BeFalse();

        (await s.SetFeesFromBalanceAsync(Yo, true, "user-1")).Should().BeTrue();
        (await s.GetAsync(Yo)).FeesFromBalance.Should().BeTrue();
    }

    [Fact]
    public async Task Un_cliente_no_tiene_preferencia_de_cuotas()
    {
        await using var db = await BaseAsync(tipo: MemberType.ExternalMember, conEstado: false);
        (await Servicio(db).SetFeesFromBalanceAsync(Yo, false, "user-1")).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter AnnualFeeOverviewServiceTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: El servicio**

`MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeOverviewService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>La tarjeta de la cuota anual: lo que se enseña y la preferencia (Plan K, K21).</summary>
public interface IAnnualFeeOverviewService
{
    Task<AnnualFeeOverviewDto> GetAsync(string memberId, CancellationToken ct = default);

    /// <summary>Guarda la preferencia de pagar las cuotas con el saldo. False si el miembro no es Ambassador.</summary>
    Task<bool> SetFeesFromBalanceAsync(string memberId, bool value, string actor, CancellationToken ct = default);
}

/// <summary>
/// Solo lee (salvo la preferencia). Lo usan el BizCenter (el miembro del token) y el Admin (el de la ruta,
/// solo lectura). Decide "se puede pagar" con las mismas reglas que Billing (AnnualFeeEligibility), para
/// que la pantalla no ofrezca lo que Billing va a rechazar.
/// </summary>
public sealed class AnnualFeeOverviewService : IAnnualFeeOverviewService
{
    public const int RecentPaymentsShown = 5;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public AnnualFeeOverviewService(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<AnnualFeeOverviewDto> GetAsync(string memberId, CancellationToken ct = default)
    {
        var member = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        var facts  = await AnnualFeeMemberQueries.FactsAsync(_db, memberId, ct); // con la membresía en HoldByBilling (R5)
        if (!facts.Exists || facts.MemberType != MemberType.Ambassador)
            return new AnnualFeeOverviewDto();

        var now   = _clock.Now;
        var plan  = await AnnualFeePlanQueries.ActivePlanAsync(_db, ct);
        var price = plan is null ? null : await AnnualFeePlanQueries.PriceAsync(_db, plan, ct);
        var state = await _db.SubscriptionBillingStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Purpose == BillingStatePurpose.AnnualFee && s.MemberId == memberId, ct);

        // Sin estado todavía (el trabajo de alta aún no ha pasado): el vencimiento que tendrá al crearse.
        var nextDue = state?.NextBillingDate.Date ?? AnnualFeeSchedule.FirstDueOnCreation(facts.EnrollDate, now);

        var underReview = state is not null
            && await AnnualFeePaymentQueries.LiveForCycle(_db, state.Id, nextDue).AsNoTracking()
                   .AnyAsync(p => p.Status == AnnualFeePaymentStatus.Pending
                               || p.Status == AnnualFeePaymentStatus.NeedsReconciliation, ct);

        var reason = plan is null || price is null ? AnnualFeeErrors.PlanNotConfigured
                   : underReview                   ? AnnualFeeErrors.NeedsReview
                   : AnnualFeeEligibility.ForPayNow(facts, nextDue, now);

        // Los enumerados se proyectan tal cual y se pasan a texto en memoria (no depende de que EF traduzca ToString).
        var payments = (await _db.AnnualFeePayments.AsNoTracking()
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.CreationDate)
                .Take(RecentPaymentsShown)
                .Select(p => new { p.Id, Date = p.CompletedAt ?? p.CreationDate, p.CycleDueDate, p.Method, p.Status, p.Amount })
                .ToListAsync(ct))
            .Select(p => new AnnualFeePaymentRowDto
            {
                PaymentId = p.Id, Date = p.Date, CycleDueDate = p.CycleDueDate,
                Method = p.Method.ToString(), Status = p.Status.ToString(), Amount = p.Amount
            })
            .ToList();

        return new AnnualFeeOverviewDto
        {
            Applies            = true,
            Status             = StatusOf(facts.KeepQualified, underReview, nextDue, now),
            Amount             = price?.Amount,
            NextDueDate        = nextDue,
            LastPaidDate       = state?.LastSuccessfulBillingDate,
            CanPayNow          = reason is null,
            NotPayableReason   = reason,
            AvailableFeeTokens = await AnnualFeeTokenQueries.Available(_db, memberId, now).CountAsync(ct),
            FeesFromBalance    = member!.FeesFromBalance,
            RecentPayments     = payments
        };
    }

    public async Task<bool> SetFeesFromBalanceAsync(string memberId, bool value, string actor, CancellationToken ct = default)
    {
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.MemberType == MemberType.Ambassador, ct);
        if (member is null) return false;

        member.FeesFromBalance = value;
        member.LastUpdateDate  = _clock.Now;
        member.LastUpdateBy    = actor;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string StatusOf(bool keepQualified, bool underReview, DateTime nextDue, DateTime now)
    {
        if (keepQualified)                                          return AnnualFeeStatuses.Waived;
        if (underReview)                                            return AnnualFeeStatuses.UnderReview;
        if (nextDue > now.Date.AddDays(AnnualFeeRules.PayAheadDays)) return AnnualFeeStatuses.UpToDate;
        if (nextDue > now.Date)                                     return AnnualFeeStatuses.DueSoon;
        return AnnualFeeStatuses.Due;
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter AnnualFeeOverviewServiceTests -nologo`
Expected: PASS (todas).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/AnnualFee/AnnualFeeOverviewService.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/AnnualFeeOverviewServiceTests.cs
git commit -m "Resumen de la cuota anual para la pantalla con las mismas reglas que Billing: estado, importe, vencimiento, ultimo pago, tokens de cuota, ultimos pagos, si se puede pagar y por que no, y la preferencia de pagar las cuotas con el saldo"
```

---

### Task 21: Endpoints del BizCenter

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter/Controllers/AnnualFeeController.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/CuotaAnualEnElBizCenterTests.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Services/IBillingClient.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Services/BillingClient.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/CuotaAnualEnElBizCenterTests.cs`:

```csharp
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// La cuota en el BizCenter (Plan K): el resumen y la preferencia se resuelven aquí con el miembro del
/// token; pagar exige step-up y se reenvía a Billing con el Bearer y el permiso, sin MemberId, y lo que
/// conteste Billing llega tal cual. Nada que escriba se hace suplantando. La tarjeta no es un método.
/// </summary>
public class CuotaAnualEnElBizCenterTests
{
    private readonly Mock<IAnnualFeeOverviewService> _overview = new();
    private readonly Mock<IBillingClient> _billing = new(MockBehavior.Strict);

    private AnnualFeeController Controlador(bool suplantando = false, string? grant = null)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-1");
        usuario.Setup(u => u.UserId).Returns("user-001");

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer jwt.miembro";
        if (grant is not null) ctx.Request.Headers[StepUpGrant.HeaderName] = grant;
        var claims = new List<Claim> { new("memberId", "AMB-1"), new(ClaimTypes.NameIdentifier, "user-001") };
        if (suplantando) claims.Add(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        return new AnnualFeeController(_overview.Object, _billing.Object, usuario.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    [Fact]
    public async Task El_resumen_es_del_miembro_del_token()
    {
        _overview.Setup(o => o.GetAsync("AMB-1", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new AnnualFeeOverviewDto { Applies = true, Status = AnnualFeeStatuses.Due });

        var r = await Controlador().Get(default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<AnnualFeeOverviewDto>>()
         .Which.Data!.Status.Should().Be(AnnualFeeStatuses.Due);
    }

    [Fact]
    public async Task La_preferencia_se_guarda_para_el_miembro_del_token_con_el_usuario_como_actor()
    {
        _overview.Setup(o => o.SetFeesFromBalanceAsync("AMB-1", false, "user-001", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var r = await Controlador().SetPreference(new AnnualFeePreferenceRequest { FeesFromBalance = false }, default);

        r.Should().BeOfType<OkObjectResult>();
        _overview.VerifyAll();
    }

    [Fact]
    public async Task Suplantando_no_se_cambia_la_preferencia()
    {
        var r = await Controlador(suplantando: true).SetPreference(new AnnualFeePreferenceRequest { FeesFromBalance = false }, default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _overview.Verify(o => o.SetFeesFromBalanceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pagar_reenvia_el_Bearer_y_el_permiso_a_Billing_y_devuelve_su_estado()
    {
        var pedido = new PayAnnualFeeRequest { Method = AnnualFeeMethods.Token, IdempotencyKey = "clave01" };
        _billing.Setup(b => b.PayAnnualFeeAsync(pedido, "jwt.miembro", "permiso", It.IsAny<CancellationToken>()))
                .ReturnsAsync(BillingResult<AnnualFeePaymentResultDto>.Fail(StatusCodes.Status409Conflict, AnnualFeeErrors.IdempotencyKeyReused, "x"));

        var r = await Controlador(grant: "permiso").Pay(pedido, default);

        var obj = r.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        obj.Value.Should().BeOfType<ApiResponse<AnnualFeePaymentResultDto>>().Which.ErrorCode.Should().Be(AnnualFeeErrors.IdempotencyKeyReused);
    }

    [Fact]
    public async Task Suplantando_no_se_paga_ni_se_llama_a_Billing()
    {
        var r = await Controlador(suplantando: true, grant: "permiso")
            .Pay(new PayAnnualFeeRequest { Method = AnnualFeeMethods.Token, IdempotencyKey = "clave01" }, default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _billing.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("CreditCard")]
    [InlineData("Crypto")]
    public async Task La_tarjeta_y_la_cripto_no_pagan_la_cuota(string metodo)
    {
        var r = await Controlador(grant: "permiso").Pay(new PayAnnualFeeRequest { Method = metodo, IdempotencyKey = "clave01" }, default);

        r.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeOfType<ApiResponse<AnnualFeePaymentResultDto>>()
         .Which.ErrorCode.Should().Be(AnnualFeeErrors.MethodNotSupported);
        _billing.VerifyNoOtherCalls();
    }

    [Fact]
    public void Pagar_exige_step_up_de_la_cuota()
        => typeof(AnnualFeeController).GetMethod(nameof(AnnualFeeController.Pay))!
            .GetCustomAttribute<RequiresStepUpAttribute>()!.OperationKey.Should().Be(StepUpOperations.AnnualFeeFromBalance);

    // ── El cliente de Billing ────────────────────────────────────────────────

    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Ultima = r;
            return Task.FromResult(f(r));
        }
    }

    [Fact]
    public async Task El_cliente_manda_el_pago_a_Billing_con_el_permiso_y_lee_la_respuesta()
    {
        var h = new Manejador(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true,"data":{"paymentId":"p1","status":"Completed","method":"Token"}}""", Encoding.UTF8, "application/json")
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(BillingClient.HttpClientName))
               .Returns(() => new HttpClient(h, disposeHandler: false) { BaseAddress = new Uri("https://billing.pruebas/") });

        var r = await new BillingClient(factory.Object, NullLogger<BillingClient>.Instance)
            .PayAnnualFeeAsync(new PayAnnualFeeRequest { Method = "Token", IdempotencyKey = "k" }, "jwt.miembro", "permiso");

        r.IsSuccess.Should().BeTrue();
        r.Value!.PaymentId.Should().Be("p1");
        h.Ultima!.Method.Should().Be(HttpMethod.Post);
        h.Ultima.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/annual-fee/pay");
        h.Ultima.Headers.Authorization!.Parameter.Should().Be("jwt.miembro");
        h.Ultima.Headers.GetValues(StepUpGrant.HeaderName).Should().Equal("permiso");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter CuotaAnualEnElBizCenterTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: El cliente de Billing**

En `IBillingClient.cs`, dentro de la interfaz, después de `CancelTokenPurchaseAsync`:

```csharp

    /// <summary>"Pagar ahora" la cuota anual (Plan K). El permiso de step-up ya lo validó y quemó el filtro.</summary>
    Task<BillingResult<AnnualFeePaymentResultDto>> PayAnnualFeeAsync(
        PayAnnualFeeRequest request, string bearerToken, string? stepUpGrant, CancellationToken ct = default);
```

En `BillingClient.cs`, después de `CancelTokenPurchaseAsync`:

```csharp

    public Task<BillingResult<AnnualFeePaymentResultDto>> PayAnnualFeeAsync(
        PayAnnualFeeRequest request, string bearerToken, string? stepUpGrant, CancellationToken ct = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/billing/annual-fee/pay") { Content = JsonContent.Create(request) };
        if (!string.IsNullOrWhiteSpace(stepUpGrant))
            message.Headers.TryAddWithoutValidation(StepUpGrant.HeaderName, stepUpGrant);
        return SendAsync<AnnualFeePaymentResultDto>(message, bearerToken, ct);
    }
```

- [ ] **Step 4: El controlador**

`MLMConquerorGlobalEdition.BizCenter/Controllers/AnnualFeeController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// La cuota anual del miembro que llama (Plan K). El miembro sale del token: no hay sujeto en la URL ni
/// en el cuerpo. El resumen y la preferencia se resuelven aquí; el pago lo hace Billing, al que se
/// reenvía el JWT y el permiso de step-up.
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/billing/annual-fee")]
[Authorize]
public class AnnualFeeController : ControllerBase
{
    private readonly IAnnualFeeOverviewService _overview;
    private readonly IBillingClient _billing;
    private readonly ICurrentUserService _currentUser;

    public AnnualFeeController(IAnnualFeeOverviewService overview, IBillingClient billing, ICurrentUserService currentUser)
    {
        _overview    = overview;
        _billing     = billing;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/v1/bizcenter/billing/annual-fee</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse<AnnualFeeOverviewDto>.Ok(await _overview.GetAsync(_currentUser.MemberId, ct)));

    /// <summary>
    /// PUT /api/v1/bizcenter/billing/annual-fee/preference — pagar las cuotas con el saldo (K7). Sin
    /// step-up (K15); nunca suplantando (la suplantación de solo lectura ya la corta el middleware).
    /// </summary>
    [HttpPut("preference")]
    public async Task<IActionResult> SetPreference([FromBody] AnnualFeePreferenceRequest request, CancellationToken ct)
    {
        if (ImpersonationScope.IsImpersonating(User))
            return ImpersonationForbidden();

        var actor = string.IsNullOrEmpty(_currentUser.UserId) ? _currentUser.MemberId : _currentUser.UserId;
        var saved = await _overview.SetFeesFromBalanceAsync(_currentUser.MemberId, request.FeesFromBalance, actor, ct);
        return saved
            ? Ok(ApiResponse<AnnualFeePreferenceRequest>.Ok(request))
            : BadRequest(ApiResponse<AnnualFeePreferenceRequest>.Fail(AnnualFeeErrors.NotApplicable,
                "Only Ambassadors have a Biz Center annual fee.", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// POST /api/v1/bizcenter/billing/annual-fee/pay — "Pagar ahora" con token, comisiones o eWallet.
    /// El filtro de step-up valida y quema el permiso antes de llegar aquí; se reenvía a Billing en
    /// X-Step-Up-Token, que lo valida y quema con su propia marca. Tarjeta: 400, hasta el Plan I.
    /// </summary>
    [HttpPost("pay")]
    [RequiresStepUp(StepUpOperations.AnnualFeeFromBalance)]
    public async Task<IActionResult> Pay([FromBody] PayAnnualFeeRequest request, CancellationToken ct)
    {
        if (ImpersonationScope.IsImpersonating(User))
            return ImpersonationForbidden();
        if (AnnualFeeMethods.Normalize(request.Method) is null)
            return BadRequest(ApiResponse<AnnualFeePaymentResultDto>.Fail(AnnualFeeErrors.MethodNotSupported,
                "This payment method is not available for the annual fee.", HttpContext.TraceIdentifier));

        var grant = Request.Headers[StepUpGrant.HeaderName].ToString();
        return this.ToActionResult(await _billing.PayAnnualFeeAsync(request, BearerToken.From(Request), grant, ct));
    }

    private IActionResult ImpersonationForbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
            AnnualFeeErrors.Impersonation, "The annual fee cannot be changed or paid during impersonation.", HttpContext.TraceIdentifier));
}
```

- [ ] **Step 5: Registrar el resumen**

En `MLMConquerorGlobalEdition.BizCenter/Program.cs`, justo después del registro de `IAnnualFeeStatus` (Tarea 19), añadir:

```csharp
// Tarjeta de la cuota anual (Plan K): resumen y preferencia. El pago lo hace Billing.
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.AnnualFee.IAnnualFeeOverviewService,
                            MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeeOverviewService>();
```

- [ ] **Step 6: Miembro del token**

En `MiembroDelTokenTests.cs`, añadir al final de la lista de `Acciones()`:

```csharp
        (typeof(AnnualFeeController), nameof(AnnualFeeController.Get)),
        (typeof(AnnualFeeController), nameof(AnnualFeeController.SetPreference)),
        (typeof(AnnualFeeController), nameof(AnnualFeeController.Pay)),
```

- [ ] **Step 7: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "CuotaAnualEnElBizCenterTests|MiembroDelTokenTests|CompraDeTokensEnElBizCenterTests|PropiedadEnElCentroDeNegociosTests" -nologo`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Controllers/AnnualFeeController.cs \
        MLMConquerorGlobalEdition.BizCenter/Services/IBillingClient.cs \
        MLMConquerorGlobalEdition.BizCenter/Services/BillingClient.cs \
        MLMConquerorGlobalEdition.BizCenter/Program.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/CuotaAnualEnElBizCenterTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MiembroDelTokenTests.cs
git commit -m "Cuota anual en el BizCenter: resumen y preferencia del miembro del token, y Pagar ahora con step-up propio reenviado a Billing con el Bearer y el permiso, sin tarjeta ni cripto y nunca suplantando"
```

---

### Task 22: La cuota en el perfil de miembro del Admin (solo lectura)

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberAnnualFeeController.cs`
- Create: `MLMConquerorGlobalEdition.AdminAPI.Tests/AdminMemberAnnualFeeControllerTests.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs`

- [ ] **Step 1: Escribir la prueba que falla**

`MLMConquerorGlobalEdition.AdminAPI.Tests/AdminMemberAnnualFeeControllerTests.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests;

/// <summary>
/// En el perfil de miembro del Admin la cuota se ve pero no se toca (Plan K, K21): un solo GET con el
/// miembro de la ruta y los mismos roles que el resto del perfil. Pagar y la preferencia son del miembro.
/// </summary>
public class AdminMemberAnnualFeeControllerTests
{
    [Fact]
    public async Task Devuelve_el_resumen_del_miembro_de_la_ruta()
    {
        var overview = new Mock<IAnnualFeeOverviewService>();
        overview.Setup(o => o.GetAsync("AMB-7", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnnualFeeOverviewDto { Applies = true, Status = AnnualFeeStatuses.UpToDate });

        var r = await new AdminMemberAnnualFeeController(overview.Object).Get("AMB-7", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<AnnualFeeOverviewDto>>()
         .Which.Data!.Status.Should().Be(AnnualFeeStatuses.UpToDate);
    }

    [Fact]
    public void Solo_lee()
        => typeof(AdminMemberAnnualFeeController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Should().OnlyContain(m => m == "GET");

    [Fact]
    public void Tiene_los_roles_del_perfil_de_miembro()
    {
        var roles = typeof(AdminMemberAnnualFeeController).GetCustomAttribute<AuthorizeAttribute>()!.Roles;
        roles.Should().Be(typeof(AdminMemberProfileController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminMemberAnnualFeeControllerTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: El controlador**

`MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberAnnualFeeController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// La cuota anual de un miembro en su perfil del Admin (Plan K, K21). Solo lectura: pagar y cambiar la
/// preferencia son cosas del miembro. Mismos roles que AdminMemberProfileController.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/billing/annual-fee")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager,SupportLevel1,SupportLevel2,SupportLevel3")]
public class AdminMemberAnnualFeeController : ControllerBase
{
    private readonly IAnnualFeeOverviewService _overview;

    public AdminMemberAnnualFeeController(IAnnualFeeOverviewService overview) => _overview = overview;

    /// <summary>GET /api/v1/admin/members/{memberId}/billing/annual-fee</summary>
    [HttpGet]
    public async Task<IActionResult> Get(string memberId, CancellationToken ct) =>
        Ok(ApiResponse<AnnualFeeOverviewDto>.Ok(await _overview.GetAsync(memberId, ct)));
}
```

- [ ] **Step 4: Registrar**

En `MLMConquerorGlobalEdition.AdminAPI/Program.cs`, justo después del registro de `IAnnualFeeStatus` (Tarea 19), añadir:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.AnnualFee.IAnnualFeeOverviewService,
                            MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeeOverviewService>();
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminMemberAnnualFeeControllerTests -nologo`
Expected: PASS (3 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberAnnualFeeController.cs \
        MLMConquerorGlobalEdition.AdminAPI/Program.cs \
        MLMConquerorGlobalEdition.AdminAPI.Tests/AdminMemberAnnualFeeControllerTests.cs
git commit -m "La cuota anual se ve en el perfil de miembro del Admin con los roles del perfil y sin poder pagarla ni cambiar la preferencia"
```

---

### Task 23: Tarjeta `AnnualFeeCard` en el perfil y textos

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Profile/AnnualFeeMessages.cs`
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Profile/AnnualFeeCard.razor`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/AnnualFeeCardTests.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/TextosDeCuotaAnualTests.cs`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Profile/UserProfilePage.razor` (columna derecha de la pestaña Perfil)
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources*.resx` (los 9)

- [ ] **Step 1: Escribir las pruebas que fallan**

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/TextosDeCuotaAnualTests.cs`:

```csharp
using System.Xml.Linq;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>Las claves del Plan K existen y no están vacías en los nueve idiomas.</summary>
public class TextosDeCuotaAnualTests
{
    private static readonly string[] Culturas = { "", ".es", ".pt", ".fr", ".it", ".de", ".ko", ".zh", ".ka" };

    public static readonly string[] Claves =
    {
        "StepUp.WhyAnnualFee",
        "AnnualFee.Title", "AnnualFee.LoadFailed", "AnnualFee.NotApplicable",
        "AnnualFee.Status.UpToDate", "AnnualFee.Status.DueSoon", "AnnualFee.Status.Due",
        "AnnualFee.Status.UnderReview", "AnnualFee.Status.Waived", "AnnualFee.Status.NotApplicable",
        "AnnualFee.NextDue", "AnnualFee.LastPaid", "AnnualFee.TokensAvailable",
        "AnnualFee.Preference", "AnnualFee.PreferenceHelp", "AnnualFee.PreferenceSaved",
        "AnnualFee.PayWith", "AnnualFee.Method.Token", "AnnualFee.Method.CommissionBalance", "AnnualFee.Method.EWallet",
        "AnnualFee.CardSoon", "AnnualFee.PayNow", "AnnualFee.Retry", "AnnualFee.Paid", "AnnualFee.ReadOnly",
        "AnnualFee.Error.NotDue", "AnnualFee.Error.AlreadyPaid", "AnnualFee.Error.InProgress",
        "AnnualFee.Error.NeedsReview", "AnnualFee.Error.NoToken", "AnnualFee.Error.InsufficientCommissions",
        "AnnualFee.Error.EWallet", "AnnualFee.Error.Waived", "AnnualFee.Error.AccountNotActive",
        "AnnualFee.Error.Busy", "AnnualFee.Error.Impersonation", "AnnualFee.Error.StepUp",
        "AnnualFee.Error.Unavailable", "AnnualFee.Error.Generic",
    };

    public static IEnumerable<object[]> Archivos() => Culturas.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Cada_idioma_tiene_las_claves_del_plan_K_y_no_estan_vacias(string cultura)
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(), "MLMConquerorGlobalEdition.SharedComponents",
            "Resources", $"SharedResources{cultura}.resx");
        using var archivo = File.OpenRead(ruta);
        var datos = XDocument.Load(archivo).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

        Claves.Where(k => !datos.ContainsKey(k)).Should().BeEmpty($"SharedResources{cultura}.resx tiene que traerlas");
        Claves.Where(k => datos.TryGetValue(k, out var v) && string.IsNullOrWhiteSpace(v)).Should().BeEmpty();
        datos["AnnualFee.Paid"].Should().Contain("{0}");
    }

    [Fact]
    public void Cada_codigo_de_error_de_la_cuota_tiene_un_texto_con_clave()
    {
        foreach (var codigo in typeof(MLMConquerorGlobalEdition.SharedKernel.Billing.AnnualFeeErrors).GetFields()
                     .Select(f => (string)f.GetValue(null)!))
            Claves.Should().Contain(MLMConquerorGlobalEdition.SharedComponents.Components.Profile.AnnualFeeMessages.ErrorKeyFor(codigo));
    }
}
```

`MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/AnnualFeeCardTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Components.Profile;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedComponents.Services;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// La tarjeta de la cuota (Plan K, K21): enseña el estado; en el BizCenter deja cambiar la preferencia y
/// "Pagar ahora" con step-up, repitiendo EXACTAMENTE el intento (misma clave) cuando no se sabe si
/// entró; en el Admin y suplantando es de solo lectura y no pide saldos.
/// </summary>
public class AnnualFeeCardTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, string?, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var cuerpo = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            return f(r, cuerpo);
        }
    }

    private sealed class ClienteQueConcede : IStepUpClient
    {
        public Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string o, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<StepUpChallenge>(true, null,
                new StepUpChallenge("reto", "Email", "n***@x.com", DateTime.Now.AddMinutes(5))));

        public Task<StepUpOutcome<string>> VerifyAsync(string o, string c, string code, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<string>(true, null, "permiso"));
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private const string Resumen = """
        {"success":true,"data":{"applies":true,"status":"DueSoon","amount":99,"nextDueDate":"2027-06-20T00:00:00",
         "lastPaidDate":null,"canPayNow":true,"notPayableReason":null,"availableFeeTokens":1,"feesFromBalance":true,"recentPayments":[]}}
        """;
    private const string Fondos = """{"success":true,"data":{"commissionBalance":150,"eWalletBalance":null,"hasEWallet":false,"cardEnabled":false}}""";
    private const string Pagado = """{"success":true,"data":{"paymentId":"p1","status":"Completed","method":"Token","amount":0,"nextDueDate":"2028-06-20T00:00:00"}}""";

    private readonly List<(string Metodo, string Ruta, string? Cuerpo, string? Permiso)> _peticiones = new();

    private IRenderedComponent<AnnualFeeCard> Montar(
        Func<HttpRequestMessage, string?, HttpResponseMessage?>? pago = null, bool soloLectura = false,
        bool suplantando = false, string resumen = Resumen)
    {
        var http = new HttpClient(new Manejador((r, cuerpo) =>
        {
            var permiso = r.Headers.TryGetValues(StepUpGrant.HeaderName, out var v) ? v.Single() : null;
            _peticiones.Add((r.Method.Method, r.RequestUri!.AbsolutePath, cuerpo, permiso));
            if (r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/billing/annual-fee")) return Json(HttpStatusCode.OK, resumen);
            if (r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/wallet/balances")) return Json(HttpStatusCode.OK, Fondos);
            if (r.Method == HttpMethod.Put) return Json(HttpStatusCode.OK, """{"success":true,"data":{"feesFromBalance":false}}""");
            return pago?.Invoke(r, cuerpo) ?? Json(HttpStatusCode.OK, Pagado);
        })) { BaseAddress = new Uri("https://api.pruebas/") };

        Services.AddSharedComponents();
        Services.AddSingleton(http);
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        Services.AddSingleton<IStepUpClient>(new ClienteQueConcede());
        var vista = new Mock<IViewContextService>();
        vista.Setup(v => v.IsImpersonating).Returns(suplantando);
        Services.AddSingleton(vista.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;

        return Render<AnnualFeeCard>(p => p.Add(c => c.ApiBaseUrl, "api/v1/bizcenter").Add(c => c.ReadOnly, soloLectura));
    }

    [Fact]
    public void Enseña_estado_importe_vencimiento_y_tokens()
    {
        var t = Montar();

        t.WaitForAssertion(() => t.Find(".annual-fee-status").TextContent.Should().Contain("AnnualFee.Status.DueSoon"));
        t.Find(".annual-fee-amount").TextContent.Should().Be("$99.00");
        t.Find(".annual-fee-next").TextContent.Should().Be("Jun 20, 2027");
        t.Find(".annual-fee-tokens").TextContent.Should().Be("1");
        t.FindAll(".annual-fee-pay-button").Should().ContainSingle();
    }

    [Fact]
    public void A_un_cliente_le_dice_que_no_aplica()
    {
        var t = Montar(resumen: """{"success":true,"data":{"applies":false,"status":"NotApplicable"}}""");

        t.WaitForAssertion(() => t.Find(".annual-fee-na"));
        t.FindAll(".annual-fee-pay-button").Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void En_solo_lectura_o_suplantando_no_hay_botones_ni_se_piden_saldos(bool soloLectura, bool suplantando)
    {
        var t = Montar(soloLectura: soloLectura, suplantando: suplantando);

        t.WaitForAssertion(() => t.Find(".annual-fee-readonly"));
        t.FindAll(".annual-fee-pay-button").Should().BeEmpty();
        t.FindAll(".annual-fee-preference").Should().BeEmpty();
        _peticiones.Should().NotContain(p => p.Ruta.EndsWith("/wallet/balances"));
    }

    [Fact]
    public async Task Pagar_pide_el_codigo_y_repite_una_vez_con_el_permiso_y_la_misma_clave()
    {
        var intentos = 0;
        var t = Montar(pago: (_, _) => ++intentos == 1
            ? Json(HttpStatusCode.Forbidden, """{"errorCode":"STEP_UP_REQUIRED"}""")
            : Json(HttpStatusCode.OK, Pagado));
        t.WaitForAssertion(() => t.Find(".annual-fee-pay-button"));

        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());
        t.WaitForAssertion(() => t.Find(".stepup-target"));
        t.Find("#stepup-code").Input("123456");
        _ = t.Find(".stepup-confirm").ClickAsync(new());

        t.WaitForAssertion(() => t.Find(".annual-fee-paid"));
        var pagos = _peticiones.Where(p => p.Metodo == "POST").ToList();
        pagos.Should().HaveCount(2);
        pagos[0].Permiso.Should().BeNull();
        pagos[1].Permiso.Should().Be("permiso");
        Clave(pagos[0].Cuerpo).Should().Be(Clave(pagos[1].Cuerpo));
        Metodo(pagos[1].Cuerpo).Should().Be("Token");
        await Task.CompletedTask;
    }

    [Fact]
    public void Si_Billing_no_contesta_el_reintento_repite_la_misma_clave()
    {
        var t = Montar(pago: (_, _) => Json(HttpStatusCode.BadGateway, """{"success":false,"errorCode":"BILLING_UNAVAILABLE"}"""));
        t.WaitForAssertion(() => t.Find(".annual-fee-pay-button"));

        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());
        t.WaitForAssertion(() => t.Find(".annual-fee-error").TextContent.Should().Contain("AnnualFee.Error.Unavailable"));
        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());

        t.WaitForAssertion(() => _peticiones.Count(p => p.Metodo == "POST").Should().Be(2));
        var pagos = _peticiones.Where(p => p.Metodo == "POST").ToList();
        Clave(pagos[0].Cuerpo).Should().Be(Clave(pagos[1].Cuerpo));
    }

    [Fact]
    public void Un_fallo_definitivo_se_traduce_y_el_siguiente_intento_lleva_otra_clave()
    {
        var t = Montar(pago: (_, _) => Json(HttpStatusCode.BadRequest, """{"success":false,"errorCode":"ANNUAL_FEE_NOT_DUE"}"""));
        t.WaitForAssertion(() => t.Find(".annual-fee-pay-button"));

        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());
        t.WaitForAssertion(() => t.Find(".annual-fee-error").TextContent.Should().Contain("AnnualFee.Error.NotDue"));
        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());

        t.WaitForAssertion(() => _peticiones.Count(p => p.Metodo == "POST").Should().Be(2));
        var pagos = _peticiones.Where(p => p.Metodo == "POST").ToList();
        Clave(pagos[0].Cuerpo).Should().NotBe(Clave(pagos[1].Cuerpo));
    }

    [Fact]
    public void Sin_tokens_el_metodo_token_no_se_puede_elegir_y_la_tarjeta_no_aparece()
    {
        var t = Montar(resumen: Resumen.Replace("\"availableFeeTokens\":1", "\"availableFeeTokens\":0"));

        t.WaitForAssertion(() => t.Find("#af-Token").HasAttribute("disabled").Should().BeTrue());
        t.Find("#af-CommissionBalance").HasAttribute("disabled").Should().BeFalse();
        t.Find("#af-EWallet").HasAttribute("disabled").Should().BeTrue();
        t.FindAll("#af-CreditCard").Should().BeEmpty();
        t.Find(".annual-fee-card-soon");
    }

    [Fact]
    public void Cambiar_la_preferencia_la_manda_al_servidor()
    {
        var t = Montar();
        t.WaitForAssertion(() => t.Find(".annual-fee-preference"));

        t.Find(".annual-fee-preference").Change(false);

        t.WaitForAssertion(() => t.Find(".annual-fee-preference-saved"));
        var put = _peticiones.Single(p => p.Metodo == "PUT");
        put.Ruta.Should().Be("/api/v1/bizcenter/billing/annual-fee/preference");
        put.Cuerpo.Should().Contain("\"feesFromBalance\":false");
    }

    private static string Clave(string? cuerpo) =>
        JsonDocument.Parse(cuerpo!).RootElement.GetProperty("idempotencyKey").GetString()!;

    private static string Metodo(string? cuerpo) =>
        JsonDocument.Parse(cuerpo!).RootElement.GetProperty("method").GetString()!;
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "AnnualFeeCardTests|TextosDeCuotaAnualTests" -nologo`
Expected: FAIL de compilación (`AnnualFeeCard`, `AnnualFeeMessages` no existen).

- [ ] **Step 3: Los mensajes**

`MLMConquerorGlobalEdition.SharedComponents/Components/Profile/AnnualFeeMessages.cs`:

```csharp
using System.Net;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Profile;

/// <summary>
/// Qué texto enseña la tarjeta de la cuota para cada código de error (Plan K) y si un fallo deja el
/// intento pendiente (misma clave al repetir). Nunca se enseña el texto crudo del servidor.
/// </summary>
public static class AnnualFeeMessages
{
    public static string ErrorKeyFor(string? errorCode) => errorCode?.Trim().ToUpperInvariant() switch
    {
        AnnualFeeErrors.NotDue                        => "AnnualFee.Error.NotDue",
        AnnualFeeErrors.AlreadyPaid                   => "AnnualFee.Error.AlreadyPaid",
        AnnualFeeErrors.InProgress                    => "AnnualFee.Error.InProgress",
        AnnualFeeErrors.NeedsReview                   => "AnnualFee.Error.NeedsReview",
        AnnualFeeErrors.NoFeeToken                    => "AnnualFee.Error.NoToken",
        AnnualFeeErrors.InsufficientCommissionBalance => "AnnualFee.Error.InsufficientCommissions",
        AnnualFeeErrors.InsufficientEWalletBalance
            or AnnualFeeErrors.EWalletNotFound
            or AnnualFeeErrors.EWalletUnavailable
            or AnnualFeeErrors.EWalletRejected        => "AnnualFee.Error.EWallet",
        AnnualFeeErrors.Waived                        => "AnnualFee.Error.Waived",
        AnnualFeeErrors.AccountNotActive              => "AnnualFee.Error.AccountNotActive",
        AnnualFeeErrors.Busy                          => "AnnualFee.Error.Busy",
        AnnualFeeErrors.Impersonation
            or ImpersonationScope.ReadOnlyErrorCode   => "AnnualFee.Error.Impersonation",
        StepUpGrant.RequiredErrorCode
            or StepUpGrant.InvalidErrorCode
            or StepUpGrant.CooldownErrorCode          => "AnnualFee.Error.StepUp",
        TokenPurchaseErrors.BillingUnavailable        => "AnnualFee.Error.Unavailable",
        _                                             => "AnnualFee.Error.Generic"
    };

    /// <summary>
    /// ¿El intento quedó sin resolver? En curso, en revisión o Billing sin contestar: se repite con la
    /// MISMA clave (Billing devuelve lo que ya hizo). Lo demás es definitivo y el siguiente lleva otra.
    /// </summary>
    public static bool KeepsKey(string? errorCode, HttpStatusCode status) =>
        errorCode is AnnualFeeErrors.InProgress or AnnualFeeErrors.NeedsReview or TokenPurchaseErrors.BillingUnavailable
        || (errorCode is null && (int)status >= 500);
}
```

- [ ] **Step 4: La tarjeta**

`MLMConquerorGlobalEdition.SharedComponents/Components/Profile/AnnualFeeCard.razor`:

```razor
@using System.Net
@using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp
@using MLMConquerorGlobalEdition.SharedKernel.Billing
@using MLMConquerorGlobalEdition.SharedKernel.Security
@inject HttpClient Http
@inject IStringLocalizer<SharedResources> L
@inject IViewContextService ViewContext

@*
    Cuota anual del Biz Center (Plan K, K21). Componente unificado: lo monta el perfil en el BizCenter y
    en el Admin; la ruta base la pone quien lo monta (ProfileApiPaths.ProfileBase) y en el Admin va en
    solo lectura. Suplantando también es de solo lectura (el servidor lo rechaza igualmente).

    PAGAR. Siempre con step-up (StepUpDialog + StepUpHttp): token, comisiones o eWallet. Sin tarjeta
    hasta el Plan I. Un intento que acaba sin saber si entró (Billing no contestó, en curso, en revisión)
    se guarda tal cual y el botón lo repite EXACTAMENTE, con la misma clave: Billing devuelve lo que ya
    hizo y no cobra dos veces. Un fallo definitivo libera el intento y el siguiente lleva otra clave.
*@

@if (CanAct)
{
    <StepUpDialog @ref="_stepUp" />
}

<div class="card border-0 shadow-sm rounded-3 mb-4 annual-fee-card">
    <div class="card-body p-4">
        <h6 class="fs-18 mb-3 fw-semibold">
            <i class="ri-calendar-check-line me-2 text-primary"></i>@L["AnnualFee.Title"]
        </h6>

        @if (_loadFailed)
        {
            <div class="alert alert-warning small mb-0 annual-fee-load-error" role="alert">@L["AnnualFee.LoadFailed"]</div>
        }
        else if (_overview is null)
        {
            <div class="text-muted small">…</div>
        }
        else if (!_overview.Applies)
        {
            <p class="text-muted small mb-0 annual-fee-na">@L["AnnualFee.NotApplicable"]</p>
        }
        else
        {
            <div class="d-flex justify-content-between align-items-center mb-3">
                <span class="badge fs-13 px-3 py-2 annual-fee-status @StatusClass">@L[$"AnnualFee.Status.{_overview.Status}"]</span>
                <span class="fw-semibold annual-fee-amount">@Money.Usd(_overview.Amount)</span>
            </div>

            <div class="row g-2 small mb-3">
                <div class="col-6">
                    <div class="text-muted">@L["AnnualFee.NextDue"]</div>
                    <div class="fw-medium annual-fee-next">@(_overview.NextDueDate?.ToString("MMM dd, yyyy", Us) ?? "—")</div>
                </div>
                <div class="col-6">
                    <div class="text-muted">@L["AnnualFee.LastPaid"]</div>
                    <div class="fw-medium annual-fee-last">@(_overview.LastPaidDate?.ToString("MMM dd, yyyy", Us) ?? "—")</div>
                </div>
                <div class="col-12">
                    <div class="text-muted">@L["AnnualFee.TokensAvailable"]</div>
                    <div class="fw-medium annual-fee-tokens">@_overview.AvailableFeeTokens</div>
                </div>
            </div>

            @if (CanAct)
            {
                <div class="form-check form-switch mb-1">
                    <input class="form-check-input annual-fee-preference" type="checkbox" role="switch" id="annualFeePreference"
                           checked="@_overview.FeesFromBalance" disabled="@_savingPreference" @onchange="OnPreferenceChangedAsync" />
                    <label class="form-check-label small" for="annualFeePreference">@L["AnnualFee.Preference"]</label>
                </div>
                <p class="text-muted small mb-3">@L["AnnualFee.PreferenceHelp"]</p>

                @if (_overview.CanPayNow || _pending is not null)
                {
                    <div class="annual-fee-pay">
                        <div class="small text-muted mb-1">@L["AnnualFee.PayWith"]</div>
                        @foreach (var (method, key) in MethodList)
                        {
                            var id = $"af-{method}";
                            <div class="form-check">
                                <input class="form-check-input annual-fee-method" type="radio" name="annualFeeMethod" id="@id"
                                       checked="@(_method == method)" disabled="@(!IsAvailable(method) || _pending is not null || _busy)"
                                       @onchange="() => _method = method" />
                                <label class="form-check-label small" for="@id">@L[key] @Detail(method)</label>
                            </div>
                        }
                        <p class="text-muted small mt-1 mb-2 annual-fee-card-soon">@L["AnnualFee.CardSoon"]</p>
                        <button type="button" class="btn btn-primary btn-sm annual-fee-pay-button" disabled="@(!CanPay)" @onclick="PayAsync">
                            @(_pending is null ? L["AnnualFee.PayNow"] : L["AnnualFee.Retry"])
                        </button>
                    </div>
                }
                else if (_overview.NotPayableReason is { } reason)
                {
                    <p class="text-muted small mb-0 annual-fee-not-payable">@L[AnnualFeeMessages.ErrorKeyFor(reason)]</p>
                }
            }
            else
            {
                <p class="text-muted small mb-0 annual-fee-readonly">@L["AnnualFee.ReadOnly"]</p>
            }

            @if (_errorKey is not null)
            {
                <div class="alert alert-danger small mt-3 mb-0 annual-fee-error" role="alert">@L[_errorKey]</div>
            }
            @if (_result is not null)
            {
                <div class="alert alert-success small mt-3 mb-0 annual-fee-paid" role="status">@L["AnnualFee.Paid", _result.NextDueDate.ToString("MMM dd, yyyy", Us)]</div>
            }
            @if (_preferenceSaved)
            {
                <div class="small text-success mt-2 annual-fee-preference-saved">@L["AnnualFee.PreferenceSaved"]</div>
            }
        }
    </div>
</div>

@code {
    /// <summary>"api/v1/bizcenter" para el miembro; "api/v1/admin/members/{id}" en el Admin.</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = "api/v1/bizcenter";

    /// <summary>Solo enseñar (Admin). Suplantando se fuerza aunque no lo pidan.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    private static readonly System.Globalization.CultureInfo Us = System.Globalization.CultureInfo.GetCultureInfo("en-US");

    private static readonly (string Method, string Key)[] MethodList =
    [
        (AnnualFeeMethods.Token,             "AnnualFee.Method.Token"),
        (AnnualFeeMethods.CommissionBalance, "AnnualFee.Method.CommissionBalance"),
        (AnnualFeeMethods.EWallet,           "AnnualFee.Method.EWallet")
    ];

    private StepUpDialog _stepUp = default!;
    private AnnualFeeOverviewDto? _overview;
    private TokenPurchaseFundsDto? _funds;
    private bool _loadFailed;
    private string? _method;
    private string _idempotencyKey = NewKey();
    private PayAnnualFeeRequest? _pending;
    private bool _busy;
    private bool _savingPreference;
    private bool _preferenceSaved;
    private string? _errorKey;
    private AnnualFeePaymentResultDto? _result;

    private bool CanAct => !ReadOnly && !ViewContext.IsImpersonating;
    private bool CanPay => !_busy && (_pending is not null || (_method is not null && IsAvailable(_method)));

    private string StatusClass => _overview?.Status switch
    {
        AnnualFeeStatuses.UpToDate    => "bg-success-subtle text-success",
        AnnualFeeStatuses.DueSoon     => "bg-warning text-dark",
        AnnualFeeStatuses.Due         => "bg-danger-subtle text-danger",
        AnnualFeeStatuses.UnderReview => "bg-info-subtle text-info",
        _                             => "bg-secondary-subtle text-secondary"
    };

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loadFailed = false;
        try
        {
            var resp = await Http.GetFromJsonAsync<Wrapper<AnnualFeeOverviewDto>>($"{ApiBaseUrl}/billing/annual-fee");
            _overview = resp?.Success == true ? resp.Data : null;
            _loadFailed = _overview is null;
        }
        catch
        {
            _loadFailed = true;
            return;
        }

        if (!CanAct || _overview is not { Applies: true }) return;

        // Los saldos solo hacen falta para elegir método; si no llegan, comisiones y eWallet quedan sin
        // ofrecer (Billing los comprobaría igual) y el token sigue disponible.
        try
        {
            var funds = await Http.GetFromJsonAsync<Wrapper<TokenPurchaseFundsDto>>($"{ApiBaseUrl}/wallet/balances");
            _funds = funds?.Success == true ? funds.Data : null;
        }
        catch
        {
            _funds = null;
        }

        if (_method is null || !IsAvailable(_method))
            _method = MethodList.Select(m => m.Method).FirstOrDefault(IsAvailable);
    }

    private bool IsAvailable(string method) => method switch
    {
        AnnualFeeMethods.Token             => (_overview?.AvailableFeeTokens ?? 0) > 0,
        AnnualFeeMethods.CommissionBalance => _funds is not null && _overview?.Amount is decimal a && _funds.CommissionBalance >= a,
        AnnualFeeMethods.EWallet           => _funds is { HasEWallet: true }
                                              && (_funds.EWalletBalance is null || _overview?.Amount is not decimal b || _funds.EWalletBalance >= b),
        _                                  => false
    };

    private string Detail(string method) => method switch
    {
        AnnualFeeMethods.Token             => $"({_overview?.AvailableFeeTokens ?? 0})",
        AnnualFeeMethods.CommissionBalance => _funds is null ? string.Empty : $"({Money.Usd(_funds.CommissionBalance)})",
        AnnualFeeMethods.EWallet           => _funds is { HasEWallet: true } ? $"({Money.Usd(_funds.EWalletBalance)})" : string.Empty,
        _                                  => string.Empty
    };

    private async Task PayAsync()
    {
        // La guarda va antes del primer await: un segundo clic llega con _busy ya encendido.
        if (!CanPay) return;

        _busy = true;
        _errorKey = null;
        _result = null;
        var request = _pending ?? new PayAnnualFeeRequest { Method = _method!, IdempotencyKey = _idempotencyKey };
        try
        {
            HttpRequestMessage Build() =>
                new(HttpMethod.Post, $"{ApiBaseUrl}/billing/annual-fee/pay") { Content = JsonContent.Create(request) };

            var resp = await StepUpHttp.SendAsync(Http, StepUpOperations.AnnualFeeFromBalance, Build, _stepUp.RequireAsync);

            // Canceló el código: no se ejecutó nada y la clave sigue valiendo.
            if (resp is null) return;

            using (resp)
            {
                var body = await ReadAsync<AnnualFeePaymentResultDto>(resp);
                if (resp.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
                {
                    _result = body.Data;
                    _pending = null;
                    _idempotencyKey = NewKey();
                    await LoadAsync();
                }
                else
                {
                    var code = body?.ErrorCode;
                    _errorKey = AnnualFeeMessages.ErrorKeyFor(code);
                    if (AnnualFeeMessages.KeepsKey(code, resp.StatusCode))
                    {
                        _pending = request;
                    }
                    else
                    {
                        _pending = null;
                        _idempotencyKey = NewKey();
                    }
                }
            }
        }
        catch (Exception)
        {
            // No se sabe si entró (red, tiempo agotado): se guarda el intento para repetirlo tal cual.
            _errorKey = "AnnualFee.Error.Unavailable";
            _pending = request;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task OnPreferenceChangedAsync(ChangeEventArgs e)
    {
        var value = e.Value is bool b && b;
        _savingPreference = true;
        _preferenceSaved = false;
        _errorKey = null;
        try
        {
            using var resp = await Http.PutAsJsonAsync($"{ApiBaseUrl}/billing/annual-fee/preference",
                new AnnualFeePreferenceRequest { FeesFromBalance = value });
            if (resp.IsSuccessStatusCode)
            {
                _overview!.FeesFromBalance = value;
                _preferenceSaved = true;
            }
            else
            {
                _errorKey = AnnualFeeMessages.ErrorKeyFor((await ReadAsync<object>(resp))?.ErrorCode);
            }
        }
        catch
        {
            _errorKey = "AnnualFee.Error.Unavailable";
        }
        finally
        {
            _savingPreference = false;
        }
    }

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static async Task<Wrapper<T>?> ReadAsync<T>(HttpResponseMessage resp)
    {
        try { return await resp.Content.ReadFromJsonAsync<Wrapper<T>>(); }
        catch { return null; }
    }

    private sealed class Wrapper<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorCode { get; set; }
    }
}
```

- [ ] **Step 5: Montarla en el perfil**

En `MLMConquerorGlobalEdition.SharedComponents/Components/Profile/UserProfilePage.razor`, sustituir:

```razor
                <MembershipCard Snapshot="@_membershipSnapshot" />
```

por:

```razor
                <MembershipCard Snapshot="@_membershipSnapshot" />

                @* Plan K: la cuota anual va aparte de la membresía. En el Admin, solo lectura. *@
                <AnnualFeeCard ApiBaseUrl="@ProfileBase" ReadOnly="@IsAdminContext" />
```

- [ ] **Step 6: Los textos en los nueve idiomas**

En cada `.resx` de `MLMConquerorGlobalEdition.SharedComponents/Resources`, justo antes de `</root>`, añadir su bloque (cada línea es una clave; `StepUp.WhyAnnualFee` ya se añadió en la Tarea 9).

`SharedResources.resx` (inglés):

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Biz Center annual fee</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>We couldn't load your annual fee. Try again later.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Only Ambassadors pay the Biz Center annual fee.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>Up to date</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>Due soon</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>Due</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>Under review</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>Waived</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>Not applicable</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>Next due date</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>Last paid</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>Annual-fee tokens available</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>Pay my fees with my commissions and eWallet</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>If you don't have an annual-fee token, on your due date we'll use your commissions first and then your eWallet.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>Preference saved.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>Pay with</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>Annual-fee token</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>Commissions</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>Card payments will be available soon.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>Pay now</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>Retry payment</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>Annual fee paid. Your next due date is {0}.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>Read only.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>Your annual fee isn't due yet. You can pay it starting 30 days before the due date.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>Your annual fee for this cycle is already paid.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>A payment for this cycle is still being processed. Wait a moment and try again.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>A payment for this cycle is being reviewed. Don't pay again; we'll contact you if needed.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>You don't have an annual-fee token available.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>Your commission balance isn't enough to pay the annual fee.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>We couldn't charge your eWallet. Check your balance and account, or choose another method.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>You don't need to pay the annual fee.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>Only active accounts can pay the annual fee.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>Another payment on your account is in progress. Try again in a moment.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>The annual fee can't be paid or changed while impersonating a member.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>The verification code couldn't be used. Request a new code and try again.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>We couldn't confirm the payment. Try again: we'll recognize it and won't charge you twice.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>The annual fee couldn't be paid. Try again later.</value></data>
```

`SharedResources.es.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Cuota anual del Biz Center</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>No pudimos cargar tu cuota anual. Inténtalo más tarde.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Solo los Ambassadors pagan la cuota anual del Biz Center.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>Al día</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>Vence pronto</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>Vencida</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>En revisión</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>Exenta</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>No aplica</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>Próximo vencimiento</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>Último pago</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>Tokens de cuota disponibles</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>Pagar mis cuotas con mis comisiones y mi eWallet</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>Si no tienes un token de cuota, el día del vencimiento usaremos primero tus comisiones y después tu eWallet.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>Preferencia guardada.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>Pagar con</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>Token de cuota</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>Comisiones</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>El pago con tarjeta estará disponible pronto.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>Pagar ahora</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>Reintentar el pago</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>Cuota anual pagada. Tu próximo vencimiento es el {0}.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>Solo lectura.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>Tu cuota anual todavía no vence. Podrás pagarla desde 30 días antes del vencimiento.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>La cuota anual de este ciclo ya está pagada.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>Un pago de este ciclo todavía se está procesando. Espera un momento y vuelve a intentarlo.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>Un pago de este ciclo está en revisión. No vuelvas a pagar; te contactaremos si hace falta.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>No tienes ningún token de cuota disponible.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>Tu saldo de comisiones no alcanza para pagar la cuota anual.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>No pudimos cobrar en tu eWallet. Revisa tu saldo y tu cuenta, o elige otro método.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>No necesitas pagar la cuota anual.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>Solo las cuentas activas pueden pagar la cuota anual.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>Hay otro pago en curso en tu cuenta. Inténtalo de nuevo en un momento.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>La cuota anual no se puede pagar ni cambiar mientras se suplanta a un miembro.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>No se pudo usar el código de verificación. Pide uno nuevo y vuelve a intentarlo.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>No pudimos confirmar el pago. Vuelve a intentarlo: lo reconoceremos y no te cobraremos dos veces.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>No se pudo pagar la cuota anual. Inténtalo más tarde.</value></data>
```

`SharedResources.pt.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Taxa anual do Biz Center</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>Não conseguimos carregar sua taxa anual. Tente novamente mais tarde.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Somente Ambassadors pagam a taxa anual do Biz Center.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>Em dia</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>Vence em breve</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>Vencida</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>Em análise</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>Isenta</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>Não se aplica</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>Próximo vencimento</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>Último pagamento</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>Tokens de taxa disponíveis</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>Pagar minhas taxas com minhas comissões e minha eWallet</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>Se você não tiver um token de taxa, no dia do vencimento usaremos primeiro suas comissões e depois sua eWallet.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>Preferência salva.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>Pagar com</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>Token de taxa</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>Comissões</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>O pagamento com cartão estará disponível em breve.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>Pagar agora</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>Tentar o pagamento novamente</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>Taxa anual paga. Seu próximo vencimento é {0}.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>Somente leitura.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>Sua taxa anual ainda não venceu. Você poderá pagá-la a partir de 30 dias antes do vencimento.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>A taxa anual deste ciclo já está paga.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>Um pagamento deste ciclo ainda está sendo processado. Aguarde um momento e tente novamente.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>Um pagamento deste ciclo está em análise. Não pague novamente; entraremos em contato se necessário.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>Você não tem nenhum token de taxa disponível.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>Seu saldo de comissões não é suficiente para pagar a taxa anual.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>Não conseguimos cobrar na sua eWallet. Verifique seu saldo e sua conta, ou escolha outro método.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>Você não precisa pagar a taxa anual.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>Somente contas ativas podem pagar a taxa anual.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>Há outro pagamento em andamento na sua conta. Tente novamente em instantes.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>A taxa anual não pode ser paga nem alterada enquanto se representa um membro.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>Não foi possível usar o código de verificação. Solicite um novo e tente novamente.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>Não conseguimos confirmar o pagamento. Tente novamente: vamos reconhecê-lo e não cobraremos duas vezes.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>Não foi possível pagar a taxa anual. Tente novamente mais tarde.</value></data>
```

`SharedResources.fr.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Cotisation annuelle du Biz Center</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>Nous n'avons pas pu charger votre cotisation annuelle. Réessayez plus tard.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Seuls les Ambassadors paient la cotisation annuelle du Biz Center.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>À jour</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>Échéance proche</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>Échue</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>En cours de vérification</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>Exonérée</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>Non applicable</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>Prochaine échéance</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>Dernier paiement</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>Jetons de cotisation disponibles</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>Payer mes cotisations avec mes commissions et mon eWallet</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>Si vous n'avez pas de jeton de cotisation, le jour de l'échéance nous utiliserons d'abord vos commissions, puis votre eWallet.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>Préférence enregistrée.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>Payer avec</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>Jeton de cotisation</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>Commissions</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>Le paiement par carte sera bientôt disponible.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>Payer maintenant</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>Réessayer le paiement</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>Cotisation annuelle payée. Votre prochaine échéance est le {0}.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>Lecture seule.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>Votre cotisation annuelle n'est pas encore due. Vous pourrez la payer à partir de 30 jours avant l'échéance.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>La cotisation annuelle de ce cycle est déjà payée.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>Un paiement de ce cycle est encore en cours de traitement. Patientez un instant et réessayez.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>Un paiement de ce cycle est en cours de vérification. Ne payez pas à nouveau ; nous vous contacterons si nécessaire.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>Vous n'avez aucun jeton de cotisation disponible.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>Votre solde de commissions ne suffit pas pour payer la cotisation annuelle.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>Nous n'avons pas pu débiter votre eWallet. Vérifiez votre solde et votre compte, ou choisissez un autre moyen.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>Vous n'avez pas besoin de payer la cotisation annuelle.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>Seuls les comptes actifs peuvent payer la cotisation annuelle.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>Un autre paiement est en cours sur votre compte. Réessayez dans un instant.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>La cotisation annuelle ne peut être ni payée ni modifiée en mode d'emprunt d'identité.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>Le code de vérification n'a pas pu être utilisé. Demandez-en un nouveau et réessayez.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>Nous n'avons pas pu confirmer le paiement. Réessayez : nous le reconnaîtrons et ne vous débiterons pas deux fois.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>La cotisation annuelle n'a pas pu être payée. Réessayez plus tard.</value></data>
```

`SharedResources.it.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Quota annuale del Biz Center</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>Non siamo riusciti a caricare la tua quota annuale. Riprova più tardi.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Solo gli Ambassador pagano la quota annuale del Biz Center.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>In regola</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>In scadenza</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>Scaduta</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>In verifica</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>Esente</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>Non applicabile</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>Prossima scadenza</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>Ultimo pagamento</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>Token quota disponibili</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>Paga le mie quote con le mie commissioni e il mio eWallet</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>Se non hai un token quota, alla scadenza useremo prima le tue commissioni e poi il tuo eWallet.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>Preferenza salvata.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>Paga con</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>Token quota</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>Commissioni</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>Il pagamento con carta sarà presto disponibile.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>Paga ora</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>Riprova il pagamento</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>Quota annuale pagata. La tua prossima scadenza è il {0}.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>Sola lettura.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>La tua quota annuale non è ancora scaduta. Potrai pagarla a partire da 30 giorni prima della scadenza.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>La quota annuale di questo ciclo è già pagata.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>Un pagamento di questo ciclo è ancora in elaborazione. Attendi un momento e riprova.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>Un pagamento di questo ciclo è in verifica. Non pagare di nuovo; ti contatteremo se necessario.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>Non hai nessun token quota disponibile.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>Il tuo saldo commissioni non basta per pagare la quota annuale.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>Non siamo riusciti ad addebitare il tuo eWallet. Controlla saldo e conto, oppure scegli un altro metodo.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>Non devi pagare la quota annuale.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>Solo gli account attivi possono pagare la quota annuale.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>È in corso un altro pagamento sul tuo account. Riprova tra un momento.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>La quota annuale non può essere pagata né modificata durante l'impersonificazione di un membro.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>Non è stato possibile usare il codice di verifica. Richiedine uno nuovo e riprova.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>Non siamo riusciti a confermare il pagamento. Riprova: lo riconosceremo e non ti addebiteremo due volte.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>Non è stato possibile pagare la quota annuale. Riprova più tardi.</value></data>
```

`SharedResources.de.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Jahresgebühr des Biz Center</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>Ihre Jahresgebühr konnte nicht geladen werden. Versuchen Sie es später erneut.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Nur Ambassadors zahlen die Jahresgebühr des Biz Center.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>Aktuell</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>Bald fällig</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>Fällig</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>In Prüfung</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>Befreit</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>Nicht zutreffend</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>Nächste Fälligkeit</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>Letzte Zahlung</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>Verfügbare Gebühren-Token</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>Meine Gebühren mit meinen Provisionen und meiner eWallet bezahlen</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>Wenn Sie kein Gebühren-Token haben, verwenden wir am Fälligkeitstag zuerst Ihre Provisionen und dann Ihre eWallet.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>Einstellung gespeichert.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>Bezahlen mit</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>Gebühren-Token</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>Provisionen</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>Kartenzahlung ist bald verfügbar.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>Jetzt bezahlen</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>Zahlung erneut versuchen</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>Jahresgebühr bezahlt. Ihre nächste Fälligkeit ist am {0}.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>Nur Lesezugriff.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>Ihre Jahresgebühr ist noch nicht fällig. Sie können sie ab 30 Tage vor der Fälligkeit bezahlen.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>Die Jahresgebühr für diesen Zeitraum ist bereits bezahlt.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>Eine Zahlung für diesen Zeitraum wird noch verarbeitet. Warten Sie einen Moment und versuchen Sie es erneut.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>Eine Zahlung für diesen Zeitraum wird geprüft. Zahlen Sie nicht erneut; wir melden uns bei Bedarf.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>Sie haben kein Gebühren-Token verfügbar.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>Ihr Provisionsguthaben reicht nicht für die Jahresgebühr.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>Ihre eWallet konnte nicht belastet werden. Prüfen Sie Guthaben und Konto oder wählen Sie eine andere Zahlungsart.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>Sie müssen die Jahresgebühr nicht bezahlen.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>Nur aktive Konten können die Jahresgebühr bezahlen.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>Auf Ihrem Konto läuft gerade eine andere Zahlung. Versuchen Sie es gleich noch einmal.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>Die Jahresgebühr kann nicht bezahlt oder geändert werden, während ein Mitglied vertreten wird.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>Der Bestätigungscode konnte nicht verwendet werden. Fordern Sie einen neuen an und versuchen Sie es erneut.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>Die Zahlung konnte nicht bestätigt werden. Versuchen Sie es erneut: Wir erkennen sie und belasten Sie nicht doppelt.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>Die Jahresgebühr konnte nicht bezahlt werden. Versuchen Sie es später erneut.</value></data>
```

`SharedResources.ko.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Biz Center 연회비</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>연회비 정보를 불러오지 못했습니다. 나중에 다시 시도하세요.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Biz Center 연회비는 Ambassador만 납부합니다.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>납부 완료</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>곧 만기</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>만기</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>검토 중</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>면제</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>해당 없음</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>다음 만기일</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>마지막 결제</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>사용 가능한 연회비 토큰</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>수수료와 eWallet로 회비 결제</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>연회비 토큰이 없으면 만기일에 수수료를 먼저 사용하고 그다음 eWallet을 사용합니다.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>설정이 저장되었습니다.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>결제 수단</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>연회비 토큰</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>수수료</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>카드 결제는 곧 제공됩니다.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>지금 결제</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>결제 다시 시도</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>연회비가 결제되었습니다. 다음 만기일은 {0}입니다.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>읽기 전용.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>연회비 만기가 아직 아닙니다. 만기 30일 전부터 결제할 수 있습니다.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>이번 주기의 연회비는 이미 결제되었습니다.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>이번 주기의 결제가 아직 처리 중입니다. 잠시 후 다시 시도하세요.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>이번 주기의 결제가 검토 중입니다. 다시 결제하지 마세요. 필요하면 연락드리겠습니다.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>사용 가능한 연회비 토큰이 없습니다.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>수수료 잔액이 연회비를 결제하기에 부족합니다.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>eWallet에서 결제하지 못했습니다. 잔액과 계정을 확인하거나 다른 방법을 선택하세요.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>연회비를 결제할 필요가 없습니다.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>활성 계정만 연회비를 결제할 수 있습니다.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>계정에서 다른 결제가 진행 중입니다. 잠시 후 다시 시도하세요.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>회원을 대리하는 동안에는 연회비를 결제하거나 변경할 수 없습니다.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>인증 코드를 사용할 수 없습니다. 새 코드를 요청한 후 다시 시도하세요.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>결제를 확인하지 못했습니다. 다시 시도하세요. 결제를 인식하므로 중복 청구되지 않습니다.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>연회비를 결제하지 못했습니다. 나중에 다시 시도하세요.</value></data>
```

`SharedResources.zh.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Biz Center 年费</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>无法加载您的年费信息。请稍后重试。</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>只有 Ambassador 需要缴纳 Biz Center 年费。</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>已缴清</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>即将到期</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>已到期</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>审核中</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>已豁免</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>不适用</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>下次到期日</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>上次付款</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>可用年费代币</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>用我的佣金和 eWallet 支付费用</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>如果您没有年费代币，到期日我们会先使用您的佣金，然后使用您的 eWallet。</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>偏好已保存。</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>支付方式</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>年费代币</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>佣金</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>银行卡支付即将上线。</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>立即支付</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>重试支付</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>年费已支付。您的下次到期日为 {0}。</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>只读。</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>您的年费尚未到期。到期前 30 天起即可支付。</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>本周期的年费已支付。</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>本周期的一笔付款仍在处理中。请稍候再试。</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>本周期的一笔付款正在审核。请勿重复支付；如有需要我们会联系您。</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>您没有可用的年费代币。</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>您的佣金余额不足以支付年费。</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>无法从您的 eWallet 扣款。请检查余额和账户，或选择其他方式。</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>您无需支付年费。</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>只有活跃账户才能支付年费。</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>您的账户有另一笔付款正在进行。请稍后再试。</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>代理会员期间无法支付或更改年费。</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>无法使用该验证码。请重新获取验证码后再试。</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>无法确认付款。请重试：我们会识别这笔付款，不会重复扣款。</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>无法支付年费。请稍后重试。</value></data>
```

`SharedResources.ka.resx`:

```xml
  <data name="AnnualFee.Title" xml:space="preserve"><value>Biz Center-ის წლიური გადასახადი</value></data>
  <data name="AnnualFee.LoadFailed" xml:space="preserve"><value>თქვენი წლიური გადასახადის ჩატვირთვა ვერ მოხერხდა. სცადეთ მოგვიანებით.</value></data>
  <data name="AnnualFee.NotApplicable" xml:space="preserve"><value>Biz Center-ის წლიურ გადასახადს მხოლოდ Ambassador-ები იხდიან.</value></data>
  <data name="AnnualFee.Status.UpToDate" xml:space="preserve"><value>გადახდილია</value></data>
  <data name="AnnualFee.Status.DueSoon" xml:space="preserve"><value>ვადა მალე გადის</value></data>
  <data name="AnnualFee.Status.Due" xml:space="preserve"><value>ვადა გასულია</value></data>
  <data name="AnnualFee.Status.UnderReview" xml:space="preserve"><value>განხილვაშია</value></data>
  <data name="AnnualFee.Status.Waived" xml:space="preserve"><value>გათავისუფლებულია</value></data>
  <data name="AnnualFee.Status.NotApplicable" xml:space="preserve"><value>არ ეხება</value></data>
  <data name="AnnualFee.NextDue" xml:space="preserve"><value>შემდეგი გადახდის ვადა</value></data>
  <data name="AnnualFee.LastPaid" xml:space="preserve"><value>ბოლო გადახდა</value></data>
  <data name="AnnualFee.TokensAvailable" xml:space="preserve"><value>ხელმისაწვდომი გადასახადის ტოკენები</value></data>
  <data name="AnnualFee.Preference" xml:space="preserve"><value>ჩემი გადასახადების გადახდა საკომისიოებით და eWallet-ით</value></data>
  <data name="AnnualFee.PreferenceHelp" xml:space="preserve"><value>თუ გადასახადის ტოკენი არ გაქვთ, ვადის დღეს ჯერ თქვენს საკომისიოებს გამოვიყენებთ, შემდეგ კი eWallet-ს.</value></data>
  <data name="AnnualFee.PreferenceSaved" xml:space="preserve"><value>პარამეტრი შენახულია.</value></data>
  <data name="AnnualFee.PayWith" xml:space="preserve"><value>გადახდის საშუალება</value></data>
  <data name="AnnualFee.Method.Token" xml:space="preserve"><value>გადასახადის ტოკენი</value></data>
  <data name="AnnualFee.Method.CommissionBalance" xml:space="preserve"><value>საკომისიოები</value></data>
  <data name="AnnualFee.Method.EWallet" xml:space="preserve"><value>eWallet</value></data>
  <data name="AnnualFee.CardSoon" xml:space="preserve"><value>ბარათით გადახდა მალე იქნება ხელმისაწვდომი.</value></data>
  <data name="AnnualFee.PayNow" xml:space="preserve"><value>გადახდა ახლავე</value></data>
  <data name="AnnualFee.Retry" xml:space="preserve"><value>გადახდის ხელახლა ცდა</value></data>
  <data name="AnnualFee.Paid" xml:space="preserve"><value>წლიური გადასახადი გადახდილია. შემდეგი ვადაა {0}.</value></data>
  <data name="AnnualFee.ReadOnly" xml:space="preserve"><value>მხოლოდ წაკითხვა.</value></data>
  <data name="AnnualFee.Error.NotDue" xml:space="preserve"><value>თქვენი წლიური გადასახადის ვადა ჯერ არ დამდგარა. მისი გადახდა ვადამდე 30 დღით ადრე შეგეძლებათ.</value></data>
  <data name="AnnualFee.Error.AlreadyPaid" xml:space="preserve"><value>ამ ციკლის წლიური გადასახადი უკვე გადახდილია.</value></data>
  <data name="AnnualFee.Error.InProgress" xml:space="preserve"><value>ამ ციკლის გადახდა ჯერ კიდევ მუშავდება. მოიცადეთ და სცადეთ ხელახლა.</value></data>
  <data name="AnnualFee.Error.NeedsReview" xml:space="preserve"><value>ამ ციკლის გადახდა განხილვაშია. ხელახლა ნუ გადაიხდით; საჭიროების შემთხვევაში დაგიკავშირდებით.</value></data>
  <data name="AnnualFee.Error.NoToken" xml:space="preserve"><value>ხელმისაწვდომი გადასახადის ტოკენი არ გაქვთ.</value></data>
  <data name="AnnualFee.Error.InsufficientCommissions" xml:space="preserve"><value>საკომისიოების ნაშთი არ კმარა წლიური გადასახადის გადასახდელად.</value></data>
  <data name="AnnualFee.Error.EWallet" xml:space="preserve"><value>eWallet-დან ჩამოჭრა ვერ მოხერხდა. შეამოწმეთ ნაშთი და ანგარიში, ან აირჩიეთ სხვა საშუალება.</value></data>
  <data name="AnnualFee.Error.Waived" xml:space="preserve"><value>წლიური გადასახადის გადახდა არ გჭირდებათ.</value></data>
  <data name="AnnualFee.Error.AccountNotActive" xml:space="preserve"><value>წლიური გადასახადის გადახდა მხოლოდ აქტიურ ანგარიშებს შეუძლიათ.</value></data>
  <data name="AnnualFee.Error.Busy" xml:space="preserve"><value>თქვენს ანგარიშზე სხვა გადახდა მიმდინარეობს. სცადეთ ცოტა ხანში.</value></data>
  <data name="AnnualFee.Error.Impersonation" xml:space="preserve"><value>წევრის სახელით მოქმედებისას წლიური გადასახადის გადახდა ან შეცვლა შეუძლებელია.</value></data>
  <data name="AnnualFee.Error.StepUp" xml:space="preserve"><value>დამადასტურებელი კოდის გამოყენება ვერ მოხერხდა. მოითხოვეთ ახალი კოდი და სცადეთ ხელახლა.</value></data>
  <data name="AnnualFee.Error.Unavailable" xml:space="preserve"><value>გადახდის დადასტურება ვერ მოხერხდა. სცადეთ ხელახლა: ვიცნობთ მას და ორჯერ არ ჩამოგეჭრებათ.</value></data>
  <data name="AnnualFee.Error.Generic" xml:space="preserve"><value>წლიური გადასახადის გადახდა ვერ მოხერხდა. სცადეთ მოგვიანებით.</value></data>
```

- [ ] **Step 7: Ejecutar y ver que pasan**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "AnnualFeeCardTests|TextosDeCuotaAnualTests|TextosDeReportesTests|StepUpEnComponentesTests|SharedComponentsNoServerHostingTests" -nologo`
Expected: PASS. `SharedComponentsNoServerHostingTests` confirma que la tarjeta no arrastra nada de servidor a SharedComponents.

- [ ] **Step 8: Verlo en pantalla**

Con la migración aplicada en local, arrancar SignupAPI (7005), Billing, BizCenter (7003) y BizCenterWeb (7004) con el perfil `https`. Lanzar a mano `annual-fee-state-enrollment` desde el panel de Hangfire de Billing (`/hangfire`, «Recurring jobs», «Trigger now») y esperar a que acabe. Entrar con un Ambassador y abrir `/profile`:

1. Debajo de *Membresía* sale *Cuota anual del Biz Center* con «Al día» y el vencimiento del aniversario de 2027.
2. Con `UPDATE SubscriptionBillingStates SET NextBillingDate = CAST(GETDATE() AS date), NextAttemptDate = CAST(GETDATE() AS date) WHERE Purpose = 2 AND MemberId = '<id>'` y recargar: «Vencida», los tres métodos (el token, apagado si no tiene) y «El pago con tarjeta estará disponible pronto».
3. «Pagar ahora» con comisiones (si el miembro tiene saldo gastable): pide el código, paga y enseña el siguiente vencimiento.
4. Apagar la preferencia: «Preferencia guardada.»; recargar y sigue apagada.
5. En AdminWeb (7001), el perfil del mismo miembro enseña la tarjeta sin botones ni interruptor.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Profile/AnnualFeeMessages.cs \
        MLMConquerorGlobalEdition.SharedComponents/Components/Profile/AnnualFeeCard.razor \
        MLMConquerorGlobalEdition.SharedComponents/Components/Profile/UserProfilePage.razor \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.es.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.pt.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.fr.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.it.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.de.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ko.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.zh.resx \
        MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources.ka.resx \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/AnnualFeeCardTests.cs \
        MLMConquerorGlobalEdition.BizCenter.Tests/CuotaAnual/TextosDeCuotaAnualTests.cs
git commit -m "Tarjeta unificada de la cuota anual en el perfil: estado, vencimiento, tokens, preferencia y Pagar ahora con step-up que repite el mismo intento cuando no se sabe si entro; de solo lectura en el Admin y suplantando, y textos en los nueve idiomas"
```

---

# Cierre

### Task 24: Pruebas de seguridad, §13

**Files:**
- Modify: `docs/deployment/pruebas-de-seguridad.md` (sección nueva al final, después de §12)

- [ ] **Step 1: Añadir la sección al final del archivo**

````markdown

## 13. Cuota anual del Biz Center (Plan K)

Qué protege: la cuota anual de $99 de cada Ambassador, cobrada **aparte** de la membresía con un
estado de cobro anual por miembro (`SubscriptionBillingStates` con `Purpose = 2` y sin suscripción),
por el único motor de renovación que queda (`RecurringBillingProcessor`; `MembershipAutoRenewalJob`
apagado), y el botón «Pagar ahora» del perfil. Cuatro reglas que estas pruebas comprueban: **quien paga
sale del token** (ningún endpoint acepta `memberId`), **pagar con lo que ya es del miembro exige step-up
y nunca se hace suplantando**, **un ciclo no se cobra dos veces** (candado de fondos + índice único de
pagos vivos por ciclo) y **nunca se cobra con tarjeta** (la tarjeta queda para el Plan I y su gateway es
un stub). Plan: `docs/superpowers/plans/2026-10-17-plan-k-cuota-anual.md`.

Endpoints:

| Endpoint | Qué hace |
|---|---|
| `GET api/v1/bizcenter/billing/annual-fee` (BizCenter, 7003) | resumen de la cuota del miembro del token |
| `PUT api/v1/bizcenter/billing/annual-fee/preference` | pagar las cuotas con comisiones y eWallet (sí/no) |
| `POST api/v1/bizcenter/billing/annual-fee/pay` | «Pagar ahora» (`Token`, `CommissionBalance`, `EWallet`); exige `X-Step-Up-Token` de `member.annual-fee.pay-from-balance` |
| `POST api/v1/billing/annual-fee/pay` (Billing) | lo mismo, llamado por el BizCenter con el JWT del miembro; vuelve a validar y quema el permiso |
| `POST api/v1/billing/annual-fee/payments/{id}/reconcile` (Billing) | tesorería (`SuperAdmin`, `Admin`, `BillingManager`) cierra un pago a conciliar |
| `GET api/v1/admin/members/{memberId}/billing/annual-fee` (AdminAPI, 7002) | resumen en el perfil de miembro, solo lectura |

Trabajos de Hangfire (Billing): `annual-fee-state-enrollment` (4:30 UTC, alta de estados),
`annual-fee-stale-pending` (cada hora, a los 45). `membership-auto-renewal` ya no existe.

**Condiciones de despliegue:**

- **Migración `CuotaAnual`** (la aplica `MigrateAsync` al arrancar cualquier servicio): altera
  `SubscriptionBillingStates` (columna `MembershipSubscriptionId` a anulable, `Purpose`, índices y
  `CHECK`), añade `FeesFromBalance` a `MemberProfiles` y `IsAnnualFeePlan` a `RecurringBillingPlans`, y
  crea `AnnualFeePayments`. Se niega a migrar con `THROW 51010` (más de un plan «Lifestyle Ambassador
  (Annual)») o `THROW 51011` (estados por suscripción en ese plan). Antes, en producción:

  ```sql
  SELECT COUNT(*) AS planesAnuales FROM RecurringBillingPlans WHERE Name = N'Lifestyle Ambassador (Annual)';
  SELECT COUNT(*) AS estadosEnPlanAnual FROM SubscriptionBillingStates s
  JOIN RecurringBillingPlans p ON p.Id = s.RecurringBillingPlanId WHERE p.Name = N'Lifestyle Ambassador (Annual)';
  ```

  Esperado: `1` y `0`. Resultado en producción: ________. Desplegar fuera de horas de tráfico.
- **Conciliación de §12 antes del Bloque 1.** Apagar `MembershipAutoRenewalJob` deja de renovar por
  esa vía los estados `Stopped` con la membresía `Active` (33 en local). Ejecutar antes la consulta de
  §12, con `AND s.Purpose = 1` para dejar fuera los estados de cuota, y que Nick decida. Resultado: ____.
- **`membership-auto-renewal` borrado de Hangfire**: tras el primer arranque de Billing con el Bloque 1,
  en `/hangfire` → *Recurring jobs* no aparece. Comprobado: ________.
- **Alta de estados**: tras desplegar el Bloque 2, lanzar a mano `annual-fee-state-enrollment` desde
  `/hangfire` y comprobar:

  ```sql
  SELECT COUNT(*) AS ambassadors FROM MemberProfiles WHERE MemberType = 0 AND IsDeleted = 0 AND Status <> 4;
  SELECT COUNT(*) AS estadosDeCuota FROM SubscriptionBillingStates WHERE Purpose = 2;
  SELECT MIN(NextBillingDate) AS primerVencimiento FROM SubscriptionBillingStates WHERE Purpose = 2;
  ```

  Esperado: los dos recuentos iguales y el primer vencimiento **hoy o después** (no se cobra hacia
  atrás). En local: ~119.445. Resultado en producción: ________.
- **Cobro automático apagado** (`Billing:AnnualFee:AutoChargeEnabled`, por defecto `false`). Se enciende
  solo cuando Nick lo diga (P10). Medir el primer ciclo con él encendido en un entorno de prueba (R2).
  Fecha de encendido en producción: ________.
- **La tarjeta sigue apagada** (`Billing:TokenPurchase:CardEnabled = false`, `Signup:CardEnabled = false`)
  y la cuota no la acepta en ningún caso.
- **Rastro del stub (R10)**: los `PaymentHistory` simulados que el orquestador escribió antes de que el
  motor rechazara la renovación:

  ```sql
  SELECT Id, OrderId, MemberId, Amount, GatewayName, GatewayTransactionId, CreationDate
  FROM PaymentHistories WHERE GatewayTransactionId LIKE 'simulated-%' OR GatewayTransactionId LIKE 'STUB-%'
  ORDER BY CreationDate DESC;
  ```

  Pasar el resultado a tesorería: no son dinero. Resultado en producción: ________.
- **«Calificado» con la anualidad real**: los Ambassadors con más de 12 meses cuyo estado de cuota vence
  en el futuro pasan a «Calificado» (P1); los que tienen el ciclo vencido sin pagar, no. Avisar a
  soporte. En local solo hay 2 Ambassadors con más de un año.
- **Pendiente fuera de este plan**: índice de `Orders.OrderNo` (R11), `bill-now` del Admin sin registrar
  (R4), `POST billing/memberships/renew` (P4), token sin consumir de `FundWithCommissionBalanceAsync` (R8).

### Pruebas

Preparación: SignupAPI (7005), Billing, BizCenter (7003), BizCenterWeb (7004), AdminAPI (7002) y
AdminWeb (7001) arrancados con el perfil `https` contra la base local, con `CuotaAnual` aplicada y
`annual-fee-state-enrollment` lanzado. Dos Ambassadors A y B; A con 150 de comisiones pagables y un token
«Annual: Biz Center» (comprado en `/tokens` o sembrado en SQL); su estado de cuota puesto a vencer hoy
(`UPDATE SubscriptionBillingStates SET NextBillingDate = CAST(GETDATE() AS date), NextAttemptDate = CAST(GETDATE() AS date) WHERE Purpose = 2 AND MemberId = @A`).
`TOKEN_A` es el Bearer de A y `PERMISO_A` un permiso de step-up de `member.annual-fee.pay-from-balance`
emitido para A.

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 13.1 | IDOR en el resumen | `GET /api/v1/bizcenter/billing/annual-fee?memberId={B}` con `TOKEN_A` | el resumen de A; el parámetro se ignora | ☐ |
| 13.2 | IDOR al pagar | `POST …/annual-fee/pay` con `TOKEN_A`, `PERMISO_A` y `"memberId":"{B}"` en el cuerpo | paga (o rechaza) la cuota de **A**; la de B no cambia | ☐ |
| 13.3 | Sin step-up | `POST …/annual-fee/pay` con `TOKEN_A` y sin cabecera | `403 STEP_UP_REQUIRED`; nada en `AnnualFeePayments` | ☐ |
| 13.4 | Permiso de otra operación | el mismo pago con un permiso de `member.tokens.purchase-from-balance` | `403 STEP_UP_INVALID` (BizCenter) y ningún pago | ☐ |
| 13.5 | Permiso repetido | dos pagos con claves distintas y el mismo `PERMISO_A` | el segundo, `403 STEP_UP_INVALID` | ☐ |
| 13.6 | Billing directo | `POST https://<billing>/api/v1/billing/annual-fee/pay` con `TOKEN_A`: sin permiso; con un permiso ya usado en el BizCenter | `403 STEP_UP_REQUIRED`; el segundo pasa una vez y la repetición con otra clave da `403 STEP_UP_INVALID` | ☐ |
| 13.7 | Suplantación completa | un administrador suplanta a A (token completo) y llama a `pay` y a `preference` | `403 IMPERSONATION_CANNOT_PAY_ANNUAL_FEE` en BizCenter y en Billing; la tarjeta del perfil sale de solo lectura | ☐ |
| 13.8 | Suplantación de solo lectura | lo mismo con token de solo lectura | `403 IMPERSONATION_READ_ONLY` (middleware); el `GET` funciona | ☐ |
| 13.9 | Tarjeta | `pay` con `"method":"CreditCard"` y con `"Crypto"`, y con `"1"` | `400 ANNUAL_FEE_METHOD_NOT_SUPPORTED`; Billing no recibe nada | ☐ |
| 13.10 | Idempotencia | dos `pay` con la misma clave y el mismo método (dos permisos) | el mismo `paymentId`; un solo pedido y un solo débito | ☐ |
| 13.11 | Clave reutilizada | la misma clave con otro método | `409 IDEMPOTENCY_KEY_REUSED` | ☐ |
| 13.12 | Doble pago del ciclo | dos `pay` a la vez con claves distintas (dos pestañas) | uno `200`; el otro `400 ANNUAL_FEE_NOT_DUE` o `409 ANNUAL_FEE_ALREADY_PAID`; un solo pago `Completed` | ☐ |
| 13.13 | Ventana de pago | `pay` con el estado venciendo dentro de 31 días | `400 ANNUAL_FEE_NOT_DUE` | ☐ |
| 13.14 | `KeepQualified` | `UPDATE MemberProfiles SET KeepQualified = 1 WHERE MemberId = @A` y `pay` | `400 ANNUAL_FEE_WAIVED`; la tarjeta dice «Exenta» | ☐ |
| 13.15 | Conciliar sin rol | `POST …/annual-fee/payments/{id}/reconcile` con un token de soporte (`SupportLevel1`) y con el de A | `403` en los dos | ☐ |
| 13.16 | Billing a través de AdminAPI | `POST https://localhost:7002/api/v1/billing/annual-fee/pay` con `TOKEN_A` | `404`, `401` o `500` sin efectos: nada en `AnnualFeePayments` (R4) | ☐ |
| 13.17 | Cobro automático apagado | con el interruptor en `false`, lanzar `recurring-billing-sweep` desde Hangfire | los estados de cuota que vencen hoy no se cobran; ni pedidos ni pagos nuevos | ☐ |
| 13.18 | Exclusiones | con el interruptor en `true` y un estado vencido por cada caso (cuenta `Terminated`, `Suspended`, membresía en `HoldByBilling`, inscrita hace menos de un año, `KeepQualified`, cobro hace 2 meses), lanzar el barrido | `Stopped` / mañana / aniversario / mañana / a un año del cobro; ningún cobro | ☐ |
| 13.19 | Orden de cobro | A con token y comisiones, el interruptor encendido, barrido | se gasta el token (`TokenTransactions` `Used` con el pedido); las comisiones no se tocan | ☐ |
| 13.20 | Preferencia apagada | A sin token, con comisiones y `FeesFromBalance = 0`, barrido | intento `Failed` (`ANNUAL_FEE_NO_FUNDS`), sin débito; la membresía y `MemberProfiles.Status` no cambian | ☐ |
| 13.21 | Un solo motor | `/hangfire` → *Recurring jobs* | no está `membership-auto-renewal`; sí `recurring-billing-planning`, `recurring-billing-sweep`, `annual-fee-state-enrollment` y `annual-fee-stale-pending` | ☐ |
| 13.22 | Stub de tarjeta | una renovación de Travel Advantage sin comisiones con el gateway de prueba | la renovación **no** se aplica (`CARD_SIMULATED_CHARGE` en el log, pedido `Cancelled`, estado sin avanzar) | ☐ |
| 13.23 | Candado en Travel Advantage | una compra de tokens con comisiones de A mientras el barrido renueva su Travel Advantage | nunca se gasta más de lo gastable; una de las dos espera o devuelve ocupado | ☐ |
| 13.24 | Admin solo lectura | perfil de A en AdminWeb | la tarjeta sin botones ni interruptor; `GET api/v1/admin/members/{A}/billing/annual-fee` responde; no hay `POST`/`PUT` en esa ruta | ☐ |
| 13.25 | Hora de servidor | cambiar la zona horaria del navegador a UTC+14 y abrir el perfil | el estado y el vencimiento no cambian | ☐ |
````

- [ ] **Step 2: Commit**

```bash
git add docs/deployment/pruebas-de-seguridad.md
git commit -m "Pruebas de seguridad, seccion 13: cuota anual del Biz Center con sus condiciones de despliegue y pruebas de pago, step-up, suplantacion, idempotencia, exclusiones y un solo motor"
```

---

### Task 25: Verificación completa

**Files:** ninguno (verificación).

- [ ] **Step 1: Compilar la solución**

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo`
Expected: `0 Error(s)`.

- [ ] **Step 2: Todas las pruebas de los proyectos tocados**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`, `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`, `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests -nologo` y `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo`
Expected: PASS en los cuatro, también lo anterior al plan. Las `[SqlServerFact]` pasan con SQL Server local y `CuotaAnual` aplicada (salir `Skipped` no vale como verde para cerrar el plan).

- [ ] **Step 3: Nada se quedó con la implementación vieja**

Run:

```bash
grep -rn "AnnualFeeNotModeled\|AddOrUpdate<MembershipAutoRenewalJob>" --include=*.cs .
```

Expected: solo las líneas de las guardas (`RegistroDeLaAnualidadTests`, `UnSoloMotorDeRenovacionTests`).

- [ ] **Step 4: Arrancar en local**

Con `CuotaAnual` ya aplicada a mano (Tarea 5), seis consolas con `--launch-profile https`: SignupAPI, Billing, BizCenter, BizCenterWeb, AdminAPI y AdminWeb. En `/hangfire` de Billing:

1. *Recurring jobs*: no está `membership-auto-renewal`; están `annual-fee-state-enrollment` y `annual-fee-stale-pending`.
2. «Trigger now» en `annual-fee-state-enrollment`; al acabar, la consulta de §13 («Alta de estados») da los dos recuentos iguales y ningún vencimiento anterior a hoy.

- [ ] **Step 5: Bloque 3 a mano** — Con un Ambassador de prueba, su estado de cuota a vencer hoy y 150 de comisiones pagables, en `https://localhost:7004/profile`: «Pagar ahora» con comisiones → código de verificación → «Cuota anual pagada. Tu próximo vencimiento es…» con el aniversario del año siguiente. En SQL: un `AnnualFeePayments` `Completed`, un débito de −99 en `CommissionEarnings`, el pedido `AF…` `Paid` sin `OrderDetails` y el estado con `NextBillingDate` un año después.

- [ ] **Step 6: Cobro automático** — Volver a poner el estado a vencer hoy (y borrar o fallar el pago anterior en SQL), encender `Billing__AnnualFee__AutoChargeEnabled=true` en la consola de Billing y relanzarlo; «Trigger now» en `recurring-billing-sweep`: el estado se cobra con el token si lo tiene, si no con comisiones. Apagar el interruptor otra vez.

- [ ] **Step 7: Calificado** — En *Mi equipo* del patrocinador de un Ambassador inscrito hace más de un año (en local, uno de los dos de 2025-10-16): con su estado de cuota al día sale «Calificado»; con el estado vencido, «No calificado».

- [ ] **Step 8: Pruebas de seguridad** — Ejecutar §13 de `docs/deployment/pruebas-de-seguridad.md` y marcar las que pasen con fecha y cómo se probó, como en §10.

- [ ] **Step 9: Cerrar** — Parar los procesos. Las marcas de §13 van en su propio commit:

```bash
git add docs/deployment/pruebas-de-seguridad.md
git commit -m "Pruebas de seguridad, seccion 13: resultado de la verificacion local del plan K"
```

---

## Autorrevisión

- **Cobertura del encargo.**
  - *Cobro aparte con segundo estado y ancla en la inscripción + 1 año*: K1, K3, K4; Tareas 4–8 (modelo, migración, alta) y 12 (cobro).
  - *Exclusiones del legado* (cancelada, menos de un año, suspendida, cobrada hace poco, `KeepQualified`): K5; `AnnualFeeEligibility` (Tarea 7) y su aplicación en el cobro automático (Tarea 12).
  - *Un solo motor*: `MembershipAutoRenewalJob` apagado (Tarea 1); los estados anuales los recoge el mismo planificador/barrido y `RecurringBillingProcessor` los delega (Tarea 16); el plan anual no crea estados por suscripción (Tarea 6).
  - *«Pagar ahora»* con comisiones, eWallet o token y nunca tarjeta: Tareas 14 (servicio), 17 (Billing), 21 (BizCenter), 23 (pantalla); la tarjeta se rechaza en las tres capas (Tareas 14, 21 y la pantalla no la ofrece).
  - *Preferencia por miembro* con la semántica del legado documentada (`DailyTokenDeduction`, `AjaxController.TokenDeductionPreference`, `BizCenterFeeMWRLife.cs`) y el orden token → comisiones → eWallet: K6, K7; Tareas 4 (columna), 12–13 (cobro), 20–21 (lectura y cambio), 23 (interruptor).
  - *Anualidad pagada en «Calificado»*: K16; Tarea 19.
  - *Reglas generales*: miembro del token (Tareas 17, 21 y `MiembroDelTokenTests`); step-up y bloqueo bajo suplantación (Tareas 9, 17, 21, 23); candado, idempotencia y conciliación (Tareas 12–15, 18); hora de servidor (todas las piezas reciben `IDateTimeProvider`; cron en UTC como el resto); componente unificado (Tarea 23, con `ProfileBase` y solo lectura en el Admin); ids `nvarchar(100)`/`(450)` (K23, guarda en `ModeloDeCuotaAnualTests`); migración pequeña con guardas (Tarea 5); sin migrar datos del legado (K3).
  - *Cuestiones a resolver*: alta de los ~119.000 estados (K3: trabajo idempotente, con el porqué frente a migración de datos y creación perezosa; las suscripciones en `HoldByBilling` en R5 y P2); `FundWithCommissionBalanceAsync` sin candado (Tarea 2, K18; la cuota no lo usa: Tarea 10); `Orders.OrderNo` (R11, K20: la cuota no usa el bucle); conciliación de §12 (R13 y condición de §13); tipos de token y compra en `/tokens` (K8); §13 (Tarea 24).
  - *Preguntas para Nick*: P1–P10, ninguna bloquea.
- **Marcadores:** ningún «TBD» ni «implementar después». Los cambios en archivos existentes van como «sustituir X por Y» con el texto literal o con un punto de inserción nombrado; la migración trae su `Up` y su `Down` completos.
- **Nombres usados en varias tareas:** `BillingStatePurpose`, `RecurringFundingSource.EWallet/Token`, `AnnualFeePayment` (`Complete`, `CompleteWithToken`, `Fail`, `MarkForReconciliation`, `IsLive`), `AnnualFeePaymentStatus/Method/Source`, `RecurringBillingPlan.IsAnnualFeePlan`, `MemberProfile.FeesFromBalance`; `AnnualFeeRules`, `AnnualFeeMethods.Normalize`, `AnnualFeeStatuses`, `AnnualFeePaymentStatuses`, `AnnualFeeErrors`, `PayAnnualFeeRequest`, `AnnualFeePaymentResultDto`, `AnnualFeeOverviewDto`, `AnnualFeePaymentRowDto`, `AnnualFeePreferenceRequest`, `ReconcileAnnualFeePaymentRequest`; `StepUpOperations.AnnualFeeFromBalance`; `AnnualFeeSchedule` (`FirstAnniversary`, `FirstDueOnCreation`, `NextDueAfterPayment`, `IsPayable`), `AnnualFeeEligibility` (`ForAutomaticCharge`, `ForPayNow`, `Reason*`), `AnnualFeeMemberFacts.Of/Missing`, `AnnualFeeDecision(Kind)`, `AnnualFeePlanQueries` (`ActivePlanAsync`, `PriceAsync`, `AnnualFeePrice`), `AnnualFeePaymentQueries.LiveForCycle`, `AnnualFeeTokenQueries` (`Available`, `InSpendingOrder`), `BillingStateAnnualFeeStatus`, `IAnnualFeeOverviewService` (`GetAsync`, `SetFeesFromBalanceAsync`); `IAnnualFeeStateEnrollment` (`EnsureMissingAsync`, `EnsureForMemberAsync`), `IAnnualFeeTokenConsumer` (`FindAsync`, `ConsumeAsync`, `ConsumedFeeToken`), `IAnnualFeeChargeService` (`ChargeDueAsync`, `PayNowAsync`, `HasPaymentAsync`, `ReconcileAsync`, `MoveStalePendingToReconciliationAsync`), `AnnualFeeOutcomes`, `AnnualFeeChargeOutcome`, `AnnualFeePayNowInput`; `ICommissionBalanceService.FundAnnualFeeAsync`, `CommissionDebit`; `RecurringBillingProcessor.MemberFundsBusyCode/AnnualFeeStateMismatchCode`; `AddAnnualFeeServices`; jobs `AnnualFeeStateEnrollmentJob`, `AnnualFeeStalePendingJob`; controladores `Billing.Controllers.AnnualFeeController` (`Pay`, `Reconcile`), `BizCenter.Controllers.AnnualFeeController` (`Get`, `SetPreference`, `Pay`), `AdminMemberAnnualFeeController.Get`; `IBillingClient.PayAnnualFeeAsync`; `AnnualFeeCard`, `AnnualFeeMessages` (`ErrorKeyFor`, `KeepsKey`). Comprobado que cada uno se define en la tarea que lo usa por primera vez o antes.
- **Orden de constructores:** `RecurringBillingProcessor(db, commissionBalance, scheduler, router, orchestrator, brandDetector, gate, annualFee, dateTime, logger)` desde la Tarea 16 (el `gate` entra en la Tarea 2); las tres pruebas que lo construyen se actualizan en las dos tareas. `AnnualFeeChargeService(db, clock, gate, commissions, eWallet, tokens, enrollment, scheduler, config, logger)` desde la Tarea 12 (el `eWallet` ya entra entonces para no cambiar el constructor en la 13).
- **Siembras compartidas:** `CuotaAnualDb` (Billing.Tests, Tarea 8) y `CuotaAnualSiembra` (BizCenter.Tests, Tarea 19); `CobroAutomaticoDeCuotaAnualTests.Servicio` la reutilizan las pruebas de eWallet, «Pagar ahora» y conciliación.
- **Lo que InMemory no prueba** (transacciones reales, índices filtrados, `CHECK`, `sp_getapplock`) lo cubre la Tarea 18; en InMemory el candado es `TransactionOnlyGate`, que no deshace nada, por eso los caminos «no se puede» del servicio no escriben antes de comprobar.
