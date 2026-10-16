# Plan H — Entrax (SBP, rublos) en el alta

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que un aspirante de un país configurado (por defecto Rusia) pueda pagar el alta en rublos por SBP con Entrax (PaymentQrService, API Cash Register v1): la pantalla muestra un QR y un enlace, el servidor cobra el importe del pedido en USD (Entrax convierte), y la activación —miembro, suscripción, comisiones y pago registrado— la hace SOLO el servidor, una vez por pago, por un único camino idempotente al que llegan el webhook, la consulta de la pantalla, un barrido de Hangfire y el botón *Re-check* del admin.

**Architecture:** SignupAPI es dueño del cobro del alta. `CompleteSignup` con `PaymentMethodType.Entrax` delega en `EntraxSignupCheckout`, que reserva el pedido (`Processing`), guarda un `EntraxPayment` y pide el QR. La liquidación vive en `EntraxSignupSettlement`: siempre re-consulta `GET /payment/{paymentId}`, exige `paid`, USD e importe suficiente, y activa reutilizando `ISignupActivationService` (el mismo de la confirmación cripto, ahora con `ActivatePendingSignupAsync`). El cliente HTTP (`IEntraxClient`, firma HMAC, opciones, guarda de arranque y un `FakeEntraxClient` para Development) vive en `Repository/Services/Entrax` para que la compra de tokens pueda reutilizarlo después. La pantalla usa un componente único, `EntraxQrPayment.razor`, en `SharedComponents.Lite` (sin Syncfusion), con los contratos en `SharedKernel/Billing/EntraxContracts.cs`. AdminAPI lista los pagos y reenvía *Re-check* a SignupAPI con el Bearer del administrador.

**Tech Stack:** .NET 10, ASP.NET Core MVC, EF Core 10 (SQL Server), MediatR, Hangfire, Blazor WebAssembly (Signups.Client), Blazor Server (AdminWeb, Syncfusion), xUnit + FluentAssertions + Moq + bUnit, EF InMemory. Rama: `plan-h-entrax-en-el-alta` (ya creada desde `master`, que incluye el Plan F).

**Reglas del repo que aplican en todo el plan:**

- Hora de servidor (`IDateTimeProvider.Now`) para negocio y persistencia. `UtcNow` solo en el `Timestamp` unix de la firma HMAC y en la comprobación del `timestamp` del callback.
- Componentes unificados: el componente del QR se escribe una vez y lo usan `Signup.razor` y `MemberJoin.razor`; lo específico (cómo consultar el estado, qué hacer al pagar) entra por parámetro.
- Comentarios y nombres de pruebas en español.
- Commits: mensaje en español, solo la descripción de lo que se hizo; **sin** líneas `Co-Authored-By` ni menciones de herramientas. `git add` con rutas explícitas.
- No tocar la matriz de paridad (`MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md`).
- La migración se crea, no se aplica a mano: la aplica SignupAPI al arrancar (`MigrateAsync`).

---

## Riesgos

| # | Riesgo | Mitigación en este plan |
|---|---|---|
| R1 | **El comercio de Entrax puede no aceptar USD** ("USD, EUR depending on merchant configuration"). Si no, `POST /qr` falla con `INVALID_CURRENCY` o `RATE_NOT_AVAILABLE`. | El alta devuelve `502 ENTRAX_UNAVAILABLE`, el pedido vuelve a `Pending` y el aspirante puede elegir otro método. **Nunca** convertimos nosotros (fallo del legado). Condición de despliegue: compra real con Entrax en USD antes de anunciar, con un importe mínimo y con la aprobación de Nick, porque PaymentQr no tiene sandbox documentado (`pruebas-de-seguridad.md` §10; la Tarea 22 solo prueba con el fake). |
| R2 | **La firma del callback está mal documentada.** El texto dice Base64 sobre el cuerpo crudo; el ejemplo de cabecera parece hexadecimal y el ejemplo JS firma `JSON.stringify(req.body)` (re-serializado). | Se acepta Base64 **o** hexadecimal de 64 caracteres, siempre sobre el cuerpo crudo y en tiempo constante. Si Entrax firmara otra cosa, los callbacks se rechazan con 401 pero **ningún alta se pierde**: la pantalla y el barrido liquidan por `GET /payment/{id}`, que es lo único en lo que confiamos. Verificar en la prueba real (§10). |
| R3 | Si Entrax no tiene clave activa manda el callback **sin firma**. | Se rechaza con 401 (fallo cerrado). Sin `Entrax:Secret` configurado también se rechaza todo. La liquidación sigue por consulta y barrido. |
| R4 | **Límite de Entrax: 100 peticiones/min por IP.** Una pantalla que consulta cada 5 s por aspirante lo agotaría. | La pantalla pregunta a SignupAPI, no a Entrax. SignupAPI solo re-consulta a Entrax si la última consulta de ese pago tiene ≥ `Entrax:PollThrottleSeconds` (15 s); el barrido toma como mucho `Entrax:SweepBatchSize` (50) pagos cada 2 min, los menos consultados primero. Un 429 se trata como `ProviderUnavailable` y se reintenta más tarde. |
| R5 | **Doble cobro**: dos QR del mismo pedido pagados, o un QR pagado cuando el pedido ya se activó por otro medio (cripto, token). | El segundo pago queda `ToReview` con `ORDER_ALREADY_SETTLED`, sin activar nada, con `LogCritical` y visible en *Admin → Billing → Entrax Signups → To review*. La devolución es manual (la API v1 no documenta reembolsos). |
| R6 | La activación no es una sola transacción: `RecurringBillingEnrollmentService.EnsureStateForSubscriptionAsync` guarda por su cuenta (igual que en la confirmación cripto), y el usuario de Identity se activa después de guardar. | El `EntraxPayment` se marca `Paid` **antes** de activar, así viaja en el primer guardado con su `RowVersion`. Si el proceso cae tras guardar y antes de activar el usuario, un *Re-check* (o cualquier liquidación posterior) de un pago `Paid` vuelve a activar el usuario (idempotente). Comisiones: la red de `SourceOrderId` de los servicios de bonos sigue debajo. |
| R7 | Ventana de expiración desconocida (el ejemplo da ~15 min) y pagos tardíos. | El callback reintenta 35 min y el barrido re-consulta los `Expired` durante `Entrax:LatePaymentWindowMinutes` (120). Un pago tardío activa si el pedido sigue sin activar (aunque haya vuelto a `Pending` o haya otro intento en curso); si ya se activó, `ToReview`. |
| R8 | EF InMemory no aplica índices únicos ni `RowVersion`: las pruebas no demuestran la unicidad de `ExternalOrderId`/`ExternalPaymentId` ni la carrera real. | Las pruebas fuerzan `DbUpdateConcurrencyException` con dobles y comprueban la configuración del modelo. La carrera se verifica contra SQL Server en la Tarea 22 (dos `POST /dev/entrax/{id}/paid` + `entrax-status` simultáneos). |
| R9 | El alta pública rehacía la fase 1 en cada clic: tras un QR caducado, "elegir otro método" crearía el alta otra vez y chocaría con el correo. | `HandleJoinNow` se salta la fase 1 si ya hay `_signupId`. Consecuencia aceptada: los datos personales que se editen después del primer intento no se reenvían (el alta ya existe). |
| R10 | El `timestamp` del callback puede ser el del evento (no el del reintento) y quizá en milisegundos. | Tolerancia `Entrax:CallbackToleranceMinutes` = 60 (mínimo forzado 40, porque Entrax reintenta 35 min); valores > 10¹¹ se leen como milisegundos. Un callback viejo se rechaza con 400; la liquidación no depende de él. |
| R11 | Endpoints anónimos (estado y callback). | El de estado exige el id del alta **y** el id de nuestro pago (GUID aleatorio, 122 bits), devuelve solo estado y segundos restantes, tiene regla de rate limit propia (30/min por IP) y nunca devuelve `ex.ToString()`. El callback solo actúa tras firma válida y siempre re-consulta. |
| R12 | `appsettings.Development.json` está en `.gitignore`. | El fake se enciende con variables de entorno (`Entrax__Enabled`, `Entrax__UseFake`, `Entrax__Secret`) en la prueba manual; nada de secretos en el `appsettings.json` base (lo vigila una prueba). |
| R13 | Rate limit general de SignupAPI (60/min por IP, regla `*`) y Entrax llamando desde pocas IP para todas las altas. | Condición de despliegue: añadir las IP de salida de Entrax a `IpRateLimiting:IpWhitelist`. Mientras no estén, un 429 hace que Entrax reintente y la consulta/barrido liquidan igual. |
| R14 | `CompleteSignup` crea `MemberStatistics` en cada llamada: un segundo intento (QR nuevo u otro método tras volver a `Pending`) duplicaría la fila. | Se crea solo si no existe (Tarea 10). |
| R15 | `AdminSignupsController.RetryComplete` (altas zombis) actúa sobre pedidos `Pending`; un alta Entrax fallida vuelve a `Pending`. | Ese endpoint manda `paymentMethod = 1` sin tarjeta y el validador lo rechaza; no activa nada. No se toca aquí (queda anotado). |

## Decisiones

**Del encargo (vinculantes):** las 11 del encargo del Plan H: `PaymentMethodType.Entrax = 5` solo si `Entrax:Enabled` y país en `Entrax:Countries` (`400 ENTRAX_NOT_AVAILABLE`); precio = `order.TotalAmount` del servidor, QR en USD; `orderId` propio estable; pedido `Processing` con miembro/suscripción `Pending` y usuario inactivo; entidad `EntraxPayment`; log de llamadas sin datos personales; activación solo en el servidor por un único camino idempotente (webhook, estado, barrido); `failed`/`expired` devuelven el pedido a `Pending`; webhook con firma en tiempo constante, 401 sin firma, tolerancia ≥ 40 min y 2xx solo si se procesó; estado anónimo sin filtrar datos y **sin** devolver tokens; cliente tipado con HMAC, guarda de arranque y fake de Development; UI con componente único; admin con *Re-check*; documentación de seguridad.

**Tomadas aquí (con su porqué):**

| # | Decisión | Por qué |
|---|---|---|
| H1 | El cliente de Entrax (`IEntraxClient`, `EntraxClient`, `FakeEntraxClient`, `EntraxOptions`, `EntraxSignature`, `AddEntrax`) va en `Repository/Services/Entrax`, no en SignupAPI. La liquidación del **alta** sí va en SignupAPI. | Mismo criterio que los clientes de payout (`PayoutGatewayServiceCollectionExtensions`): la compra de tokens (fuera de alcance) podrá reutilizarlo desde Billing sin referenciar SignupAPI. |
| H2 | `ExternalOrderId = "SIGNUP-{order.Id}-{n}"`, con `n` = número de intentos previos del pedido + 1. Índice único. | Estable y trazable; dos peticiones simultáneas chocan en el índice y la segunda recibe `409 ENTRAX_IN_PROGRESS`. |
| H3 | `CompleteSignup` guarda el `EntraxPayment` (y el pedido en `Processing`) **antes** de llamar a Entrax; si el guardado posterior falla, el callback encuentra la fila por `orderId` y rellena el `paymentId`. Filas sin `paymentId` con más de 30 min → `Failed` (`NO_PAYMENT_ID`) y el pedido vuelve a `Pending`. | El legado perdía la relación QR↔alta (orderId generado ≠ devuelto). Así ningún QR queda huérfano sin rastro. |
| H4 | Columna `LastCheckedAt` en `EntraxPayment`. | Necesaria para el estrangulamiento de R4. |
| H5 | Columnas `PaidAmountUsd`, `FailureCode` y `Notes` en `EntraxPayment`. | El admin necesita ver cuánto se pagó y por qué un pago quedó a revisar. |
| H6 | Log de llamadas = tabla nueva `EntraxApiCalls` con campos **estructurados** (operación, ids externos, HTTP, código de error, estado del proveedor, duración). **Sin cuerpos.** | Los cuerpos de Entrax pueden traer `transactionDetails.payerPhone`; con campos estructurados no hay nada personal que filtrar y no hace falta redactar. |
| H7 | La activación se reutiliza añadiendo `ISignupActivationService.ActivatePendingSignupAsync(order, now, ct)` (carga miembro, suscripción `Pending` y puntos, y llama a `ActivateAsync`) y `SignupUserActivation.ActivateAsync` (usuario de Identity). `ConfirmCryptoPaymentHandler` pasa a usar las dos. | Un solo sitio para "el dinero del alta está cobrado"; la cripto y Entrax no pueden divergir. |
| H8 | Pago registrado: `PaymentHistory` `Captured`, `GatewayName = "Entrax"`, `GatewayTransactionId = paymentId`, `Amount = amount.original` (USD). | Igual que la compra de tokens (`TokenPurchaseService`); el importe en la moneda del pedido. |
| H9 | Si al pagar hay una `CryptoPaymentConfirmation` `AwaitingPayment` del mismo pedido (el aspirante eligió cripto tras caducar el QR), se cancela con nota "Paid via Entrax (…)". | Si no, un administrador podría confirmar después una transferencia que no existe. |
| H10 | `CompleteSignupHandler` recibe `IEntraxSignupCheckout? entraxCheckout = null` como **último parámetro opcional**. Sin él, Entrax no está disponible. | Las 17 construcciones del handler en las pruebas existentes no cambian; el contenedor de DI lo inyecta porque está registrado. Falla cerrado. |
| H11 | Estado para la pantalla: `GET api/v1/signups/{signupId}/entrax-status?ref={paymentRef}`, donde `paymentRef` es `EntraxPayment.Id`. Devuelve `Status`, `ExpiresAt` y `SecondsRemaining`. | `SecondsRemaining` evita que el navegador (en otro huso) interprete una hora de servidor. |
| H12 | Métodos disponibles: `GET api/v1/signups/payment-options` → `{ entraxCountries: ["RU"] }` (vacío si está apagado). La pantalla compara con el país elegido. | El alta no tenía catálogo de métodos; una llamada al cargar basta y el servidor vuelve a comprobarlo. |
| H13 | **Textos de la UI por parámetro, en inglés por defecto**, como `CryptoDepositPicker`. **No** van a `.resx`. | Ni `Signups.Client` ni `SharedComponents.Lite` tienen recursos: el alta entera está en inglés escrito en la página. Los `.resx` existentes (`SharedComponents/Resources`, 9 culturas) no son alcanzables desde el alta, que tiene prohibido referenciar `SharedComponents` (lo vigila `SignupsClientLigeroTests`). Cuando el alta se localice, la página pasará sus textos al componente sin tocarlo. |
| H14 | `MemberJoin.razor` vive en `Signups.Client` (no en el BizCenter) y es el alta de miembro desde la web del patrocinador. Recibe la misma opción. El BizCenter no tiene página de alta. | Es la única otra pantalla que completa altas. |
| H15 | Al pagar no se devuelven tokens: la pantalla dice "pago recibido, inicia sesión" y enlaza a `/login`. | La activación ocurre fuera de la petición del navegador (webhook/barrido); emitir tokens a quien consulta un endpoint anónimo sería entregar una sesión a quien tenga dos ids. |
| H16 | El barrido (`entrax-signup-sweep`, cada 2 min, cola `signups`) corre siempre, aunque `Entrax:Enabled` sea `false`. | Si se apaga con pagos en vuelo, esos pagos tienen que seguir liquidándose. |
| H17 | En Development, `Entrax:UseFake = true` registra `FakeEntraxClient` (singleton en memoria) y mapea `POST /api/v1/dev/entrax/{paymentId}/{paid|underpaid|failed|expired}` y `POST /api/v1/dev/entrax/sweep`. Fuera de Development `UseFake` hace fallar el arranque. | Probar de punta a punta sin credenciales, sin que el fake pueda llegar a producción. |
| H18 | Fuera de Development, `Entrax:Enabled = true` sin `KeyId`, `Secret`, `CallbackUrl` https absoluta o `BaseUrl` https → `InvalidOperationException` al arrancar. En Development solo se escribe un aviso por consola. | Lo pide el encargo; en Development se puede trabajar con el fake o sin túnel. |
| H19 | Fuera de Development, con `Enabled = false` pero alguna credencial puesta, tienen que estar `KeyId` y `Secret` y `BaseUrl` https. El cliente nunca lanza: sin credenciales o sin https devuelve `NOT_CONFIGURED` sin llamar. | El barrido corre con Entrax apagado (H16): apagar es dejar de ofrecer, no quitar credenciales. |
| H20 | El barrido cierra en local como `Expired` (`LOCAL_EXPIRY`) un QR que caducó hace más de `Entrax:LocalExpiryGraceMinutes` (10) si Entrax no contesta (caído, `NOT_CONFIGURED`) o sigue en `pending`; el pedido vuelve a `Pending` de forma condicional y el pago sigue en la ventana de pagos tardíos. Con `Enabled = false` y pagos esperando, el barrido escribe un `Critical`. | Un pedido no puede quedarse en `Processing` para siempre. Apagar Entrax no es quitar credenciales: sin ellas, esos pagos solo se resuelven por caducidad local o a mano. |
| H21 | Estado nuevo `Superseded` (dominio y `EntraxSignupStatuses`). Al activar con un QR, los demás `AwaitingPayment` del pedido pasan a `Superseded`; el endpoint de estado devuelve `Superseded` con `SecondsRemaining = 0` si el pedido ya está `Completed` (por cualquier medio). La pantalla (Tarea 17) lo trata como "alta completada, inicia sesión" y oculta el QR. El barrido sigue mirando los `Superseded` mientras puedan pagarse (`ExpiresAt` dentro de la ventana de pagos tardíos); si alguien los paga, `ToReview` con `ORDER_ALREADY_SETTLED`. | Un QR vivo de un alta ya activada no puede seguir pareciendo cobrable. |

## Fuera de alcance

- **Compra de tokens con Entrax** (Billing). El cliente queda en `Repository` para reutilizarlo.
- **Novix** (pago de comisiones a wallet USDT).
- **Upgrade de membresía** con Entrax.
- **Devoluciones** de pagos `ToReview`: se hacen a mano con Entrax; aquí solo se señalan.
- **Localización del alta** (H13).
- **`AdminSignupsController.RetryComplete`** (R15).
- **Endpoints `/rates`, `/deposits`, `/thb-rate`** de Entrax: no se usan.

## Mapa de archivos

| Archivo | Responsabilidad |
|---|---|
| `SharedKernel/Billing/EntraxContracts.cs` (nuevo) | DTOs del alta con Entrax para API y pantalla; nombres de estado. |
| `Domain/Entities/Orders/EntraxPaymentStatus.cs`, `EntraxPayment.cs`, `EntraxApiCall.cs` (nuevos) | Dominio. |
| `Repository/Configurations/EntraxPaymentConfiguration.cs`, `EntraxApiCallConfiguration.cs` (nuevos), `Repository/Context/AppDbContext.cs`, `Repository/Migrations/*_EntraxEnElAlta.cs` | Esquema. |
| `Repository/Services/Entrax/EntraxOptions.cs`, `EntraxOptionsGuard.cs`, `EntraxSignature.cs`, `EntraxProviderContracts.cs`, `EntraxClient.cs`, `FakeEntraxClient.cs`, `EntraxServiceCollectionExtensions.cs` (nuevos) | Cliente del proveedor. |
| `SignupAPI/Services/ISignupActivationService.cs`, `SignupActivationService.cs`, `SignupUserActivation.cs` (nuevo) | Activación reutilizable. |
| `SignupAPI/Features/Signups/Commands/ConfirmCryptoPayment/ConfirmCryptoPaymentHandler.cs` | Usa la activación reutilizable. |
| `SignupAPI/Services/Entrax/EntraxSignupErrors.cs`, `EntraxCallLog.cs`, `EntraxQrSanitizer.cs`, `EntraxSignupCheckout.cs`, `EntraxSignupSettlement.cs`, `EntraxCallbackProcessor.cs`, `EntraxSignupStatusService.cs` (nuevos) | Lógica del alta con Entrax. |
| `SignupAPI/DTOs/PaymentMethodType.cs`, `SignupAPI/DTOs/SignupResponse.cs`, `SignupAPI/Features/Signups/Commands/CompleteSignup/CompleteSignupHandler.cs`, `SignupAPI/Controllers/SignupsController.cs` | `Entrax = 5`, respuesta con QR, rama Entrax, códigos HTTP. |
| `SignupAPI/Controllers/EntraxSignupsController.cs` (nuevo) | Callback, estado, métodos disponibles, *Re-check*. |
| `SignupAPI/Jobs/EntraxSignupSweepJob.cs` (nuevo), `SignupAPI/Program.cs`, `SignupAPI/appsettings.json` | Barrido, DI, endpoints de Development, configuración sin secretos, rate limit. |
| `SharedComponents.Lite/Payments/EntraxQrPayment.razor` (nuevo) | Componente único del QR. |
| `Signups.Client/Payments/EntraxSignupApi.cs` (nuevo), `Signups.Client/_Imports.razor` | Las tres llamadas del alta con Entrax, una vez para las dos páginas. |
| `Signups.Client/Pages/Signup.razor`, `Signups.Client/Pages/MemberJoin.razor` | Pestaña "Rubles (SBP)"; la fase 1 no se repite si el alta ya existe. |
| `AdminAPI/Controllers/AdminEntraxSignupsController.cs` (nuevo) | Lista y *Re-check*. |
| `AdminWeb/Components/Pages/AdminEntraxSignups.razor` (nuevo), `SharedComponents/Components/Layout/AdminSidebarMenu.razor` | Pantalla admin y menú. |
| `docs/deployment/pruebas-de-seguridad.md` | §10 Entrax en el alta. |
| `Signups.Tests/Entrax/*` (nuevos), `BizCenter.Tests/EntraxQrPaymentTests.cs` (nuevo), `AdminAPI.Tests/Features/CryptoSignups/AdminEntraxSignupsControllerTests.cs` (nuevo) | Pruebas. |

Rutas abreviadas: todos los proyectos llevan el prefijo `MLMConquerorGlobalEdition.` (p. ej. `SignupAPI/…` = `MLMConquerorGlobalEdition.SignupAPI/…`). En los bloques de archivos de cada tarea van completas.

---

### Task 1: Contratos compartidos (SharedKernel)

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Billing/EntraxContracts.cs`

- [ ] **Step 1: Crear `EntraxContracts.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Estados del pago del alta con Entrax tal como los ve la pantalla. Son los nombres de
/// <c>Domain.Entities.Orders.EntraxPaymentStatus</c>; una prueba vigila que no se separen.
/// </summary>
public static class EntraxSignupStatuses
{
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Paid            = "Paid";
    public const string Failed          = "Failed";
    public const string Expired         = "Expired";
    public const string ToReview        = "ToReview";

    public static readonly IReadOnlyList<string> All = [AwaitingPayment, Paid, Failed, Expired, ToReview];
}

/// <summary>
/// Lo que devuelve completar un alta con Entrax: el QR que hay que pagar. Lo produce SignupAPI y lo
/// pinta <c>SharedComponents.Lite/Payments/EntraxQrPayment.razor</c>.
/// </summary>
public sealed class EntraxSignupPaymentDto
{
    /// <summary>
    /// Id de nuestro registro del pago (GUID aleatorio). Junto con el id del alta es lo que pide el
    /// endpoint de estado: sin los dos no se consulta nada.
    /// </summary>
    public string PaymentRef { get; set; } = string.Empty;

    /// <summary><c>data:image/png;base64,…</c> validado en el servidor, o null si Entrax mandó otra cosa.</summary>
    public string? QrImage { get; set; }

    /// <summary>Enlace https para abrir el pago en el móvil, o null si no era https.</summary>
    public string? QrLink { get; set; }

    public decimal AmountUsd    { get; set; }
    public decimal AmountRub    { get; set; }
    public decimal ExchangeRate { get; set; }

    /// <summary>Hora de servidor. Informativa: la cuenta atrás usa <see cref="SecondsRemaining"/>.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Segundos hasta que caduca el QR, calculados en el servidor.</summary>
    public int SecondsRemaining { get; set; }
}

/// <summary>Estado del pago para la pantalla del alta. No lleva nada más a propósito: el endpoint es anónimo.</summary>
public sealed class EntraxSignupStatusDto
{
    public string    Status           { get; set; } = EntraxSignupStatuses.AwaitingPayment;
    public DateTime? ExpiresAt        { get; set; }
    public int       SecondsRemaining { get; set; }
}

/// <summary>Métodos de pago del alta que dependen de la configuración del servidor.</summary>
public sealed class SignupPaymentOptionsDto
{
    /// <summary>Países (ISO2) en los que se ofrece pagar en rublos por SBP. Vacío = apagado.</summary>
    public IReadOnlyList<string> EntraxCountries { get; set; } = [];
}
```

- [ ] **Step 2: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.SharedKernel -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel/Billing/EntraxContracts.cs
git commit -m "Contratos del alta con Entrax: QR, estado y metodos disponibles"
```

---

### Task 2: Dominio, configuración y migración

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Orders/EntraxPaymentStatus.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Orders/EntraxPayment.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Orders/EntraxApiCall.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/EntraxPaymentConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/EntraxApiCallConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:97`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_EntraxEnElAlta.cs` (vía `dotnet ef`)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxPaymentModelTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Lo que el modelo tiene que garantizar por sí mismo: un pedido externo y un pago externo no se
/// repiten, y la fila del pago lleva token de concurrencia (es lo que decide la carrera al activar).
/// EF InMemory no aplica los índices; aquí se comprueba que están declarados.
/// </summary>
public class EntraxPaymentModelTests
{
    [Fact]
    public void Los_estados_del_dominio_y_los_de_la_pantalla_se_llaman_igual()
        => Enum.GetNames<EntraxPaymentStatus>().Should().BeEquivalentTo(EntraxSignupStatuses.All);

    [Fact]
    public void El_pedido_externo_y_el_pago_externo_son_unicos()
    {
        using var db = InMemoryDbHelper.Create();
        var tipo = db.Model.FindEntityType(typeof(EntraxPayment))!;

        tipo.GetIndexes().Should().Contain(i => i.IsUnique
            && i.Properties.Count == 1 && i.Properties[0].Name == nameof(EntraxPayment.ExternalOrderId));
        tipo.GetIndexes().Should().Contain(i => i.IsUnique
            && i.Properties.Count == 1 && i.Properties[0].Name == nameof(EntraxPayment.ExternalPaymentId));
    }

    [Fact]
    public void La_fila_del_pago_lleva_token_de_concurrencia()
    {
        using var db = InMemoryDbHelper.Create();
        db.Model.FindEntityType(typeof(EntraxPayment))!
            .FindProperty(nameof(EntraxPayment.RowVersion))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void El_pedido_externo_lleva_el_pedido_y_el_intento()
        => EntraxPayment.BuildExternalOrderId("8f0c", 2).Should().Be("SIGNUP-8f0c-2");
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxPaymentModelTests -nologo`
Expected: FAIL de compilación (`EntraxPayment` no existe).

- [ ] **Step 3: Crear `EntraxPaymentStatus.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Estado de un pago del alta con Entrax (SBP, rublos). Los nombres son los de
/// <c>SharedKernel.Billing.EntraxSignupStatuses</c>: la pantalla los recibe tal cual.
/// </summary>
public enum EntraxPaymentStatus
{
    /// <summary>QR emitido (o a punto de emitirse si aún no hay ExternalPaymentId); esperando el pago.</summary>
    AwaitingPayment = 1,

    /// <summary>Entrax dijo "paid", el importe cuadra y el alta se activó con este pago.</summary>
    Paid = 2,

    /// <summary>Entrax dijo "failed", o el QR no llegó a emitirse. El pedido vuelve a Pending.</summary>
    Failed = 3,

    /// <summary>Entrax dijo "expired". El pedido vuelve a Pending; si luego llega "paid", se re-evalúa.</summary>
    Expired = 4,

    /// <summary>
    /// Hubo (o pudo haber) cobro pero NO se aplicó: importe o moneda que no cuadran, pedido ya activado
    /// por otro medio, datos del proveedor que no coinciden. Se mira y se devuelve a mano.
    /// </summary>
    ToReview = 5
}
```

- [ ] **Step 4: Crear `EntraxPayment.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Un intento de pagar un alta en rublos por SBP con Entrax. Un pedido puede tener varios (un QR
/// caduca y se pide otro), pero un pago de Entrax activa como mucho un alta: ExternalPaymentId es
/// único y Consumed solo se pone una vez, bajo el RowVersion de la fila.
///
/// El importe que manda es el del SERVIDOR (Orders.TotalAmount, en USD) congelado en AmountUsd al
/// pedir el QR; Entrax convierte a rublos. Nunca se convierte aquí.
/// </summary>
public class EntraxPayment : AuditChangesStringKey
{
    public string OrderId  { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;

    /// <summary>"SIGNUP-{OrderId}-{intento}". Único. Es el orderId que ve Entrax.</summary>
    public string ExternalOrderId { get; set; } = string.Empty;

    /// <summary>paymentId de Entrax (GUID en minúsculas). Null hasta que Entrax responde. Único.</summary>
    public string? ExternalPaymentId { get; set; }

    public EntraxPaymentStatus Status { get; set; } = EntraxPaymentStatus.AwaitingPayment;

    /// <summary>Importe pedido en USD: el total del pedido al emitir el QR, redondeado a 2 decimales.</summary>
    public decimal AmountUsd { get; set; }

    /// <summary>Lo que Entrax dijo que se pagaría en rublos y con qué cambio. Informativo.</summary>
    public decimal? AmountRub    { get; set; }
    public decimal? ExchangeRate { get; set; }

    /// <summary>amount.original que devolvió GET /payment al liquidarlo como pagado.</summary>
    public decimal? PaidAmountUsd { get; set; }

    /// <summary>Enlace https del QR. La imagen no se guarda (es grande y se puede pedir otra).</summary>
    public string? QrLink { get; set; }

    /// <summary>Caducidad del QR en hora de servidor.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>Este pago ya activó un alta. Nunca vuelve a false.</summary>
    public bool Consumed { get; set; }

    public DateTime? ActivatedAt { get; set; }

    /// <summary>Última vez que se consultó GET /payment. Estrangula la consulta de la pantalla.</summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>Código de por qué falló o quedó a revisar (p. ej. AMOUNT_TOO_LOW).</summary>
    public string? FailureCode { get; set; }

    public string? Notes { get; set; }

    public static string BuildExternalOrderId(string orderId, int attempt) => $"SIGNUP-{orderId}-{attempt}";
}
```

- [ ] **Step 5: Crear `EntraxApiCall.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Una llamada a Entrax o un callback de Entrax. SOLO campos estructurados: ni cuerpos ni cabeceras.
/// Las respuestas de Entrax pueden traer el teléfono del pagador (transactionDetails.payerPhone) y
/// el legado llegó a registrar la contraseña del formulario; aquí no hay nada personal que filtrar.
/// </summary>
public class EntraxApiCall : AuditChangesLongKey
{
    /// <summary>CreateQr, GetPayment o Callback.</summary>
    public string Operation { get; set; } = string.Empty;

    public string? ExternalOrderId   { get; set; }
    public string? ExternalPaymentId { get; set; }

    /// <summary>0 si no hubo respuesta HTTP (timeout, red) o si es un callback entrante.</summary>
    public int HttpStatusCode { get; set; }

    public bool Success { get; set; }

    /// <summary>Código de error de Entrax (RATE_NOT_AVAILABLE…) o nuestro (TRANSPORT, BAD_RESPONSE).</summary>
    public string? ErrorCode { get; set; }

    /// <summary>pending, paid, failed, expired, SUCCESS, FAILED… tal como llegó.</summary>
    public string? ProviderStatus { get; set; }

    public int DurationMs { get; set; }
}
```

- [ ] **Step 6: Crear `EntraxPaymentConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class EntraxPaymentConfiguration : IEntityTypeConfiguration<EntraxPayment>
{
    public void Configure(EntityTypeBuilder<EntraxPayment> builder)
    {
        builder.HasKey(x => x.Id);

        // La carrera entre webhook, pantalla, barrido y admin la decide esta columna: el segundo
        // UPDATE de la misma fila afecta a cero filas y EF lanza DbUpdateConcurrencyException.
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ExternalOrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ExternalPaymentId).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.AmountUsd).HasPrecision(18, 4);
        builder.Property(x => x.AmountRub).HasPrecision(18, 4);
        builder.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        builder.Property(x => x.PaidAmountUsd).HasPrecision(18, 4);
        builder.Property(x => x.QrLink).HasMaxLength(1000);
        builder.Property(x => x.FailureCode).HasMaxLength(60);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Un orderId de Entrax es de un solo intento; un paymentId de Entrax, de un solo alta.
        builder.HasIndex(x => x.ExternalOrderId).IsUnique();
        builder.HasIndex(x => x.ExternalPaymentId).IsUnique().HasFilter("[ExternalPaymentId] IS NOT NULL");

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => new { x.Status, x.CreationDate });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

- [ ] **Step 7: Crear `EntraxApiCallConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class EntraxApiCallConfiguration : IEntityTypeConfiguration<EntraxApiCall>
{
    public void Configure(EntityTypeBuilder<EntraxApiCall> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Operation).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ExternalOrderId).HasMaxLength(100);
        builder.Property(x => x.ExternalPaymentId).HasMaxLength(64);
        builder.Property(x => x.ErrorCode).HasMaxLength(60);
        builder.Property(x => x.ProviderStatus).HasMaxLength(30);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => x.CreationDate);
        builder.HasIndex(x => x.ExternalPaymentId);
    }
}
```

- [ ] **Step 8: Añadir los `DbSet` a `AppDbContext.cs`**, debajo de `CryptoPaymentConfirmations` (línea ~97):

```csharp
    public DbSet<EntraxPayment> EntraxPayments => Set<EntraxPayment>();
    public DbSet<EntraxApiCall> EntraxApiCalls => Set<EntraxApiCall>();
```

(El `using MLMConquerorGlobalEdition.Domain.Entities.Orders;` ya está en la línea 13.)

- [ ] **Step 9: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxPaymentModelTests -nologo`
Expected: PASS (4 pruebas).

- [ ] **Step 10: Generar la migración**

```bash
dotnet ef migrations add EntraxEnElAlta \
  --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.SignupAPI
```

Revisar `Up`: solo `CreateTable("EntraxPayments")`, `CreateTable("EntraxApiCalls")` y sus índices (los dos únicos, el filtrado `[ExternalPaymentId] IS NOT NULL`, `OrderId`, `(Status, CreationDate)`, `CreationDate`, `ExternalPaymentId`). Si aparecen bloques `migrationBuilder.UpdateData(...)` de semillas (`HasData`) que no cambian nada, **borrarlos de `Up` y de `Down`** (no del `.Designer.cs` ni del snapshot). Si aparece cualquier otra operación (columnas o tablas ajenas), el modelo traía deriva: parar y avisar.

Verificar:

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_EntraxEnElAlta.cs`
Expected: `0`

Run: `grep -c "EntraxPayments\|EntraxApiCalls" MLMConquerorGlobalEdition.Repository/Migrations/*_EntraxEnElAlta.cs`
Expected: > 0

- [ ] **Step 11: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 12: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Orders/EntraxPaymentStatus.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Orders/EntraxPayment.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Orders/EntraxApiCall.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/EntraxPaymentConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/EntraxApiCallConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_EntraxEnElAlta.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_EntraxEnElAlta.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxPaymentModelTests.cs
git commit -m "Pagos de Entrax del alta y su registro de llamadas: entidades, configuracion y migracion EntraxEnElAlta"
```

---

### Task 3: Opciones de Entrax y guarda de arranque

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxOptions.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxOptionsGuard.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxOptionsTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class EntraxOptionsTests
{
    private static EntraxOptions Completas() => new()
    {
        Enabled = true, KeyId = "key-1", Secret = "s3cr3t",
        CallbackUrl = "https://alta.ejemplo.com/api/v1/signups/entrax/callback"
    };

    [Fact]
    public void Apagado_no_exige_nada_ni_en_produccion()
        => EntraxOptionsGuard.Problems(new EntraxOptions(), isDevelopment: false).Should().BeEmpty();

    [Fact]
    public void Encendido_en_produccion_sin_credenciales_ni_callback_no_arranca()
    {
        var o = new EntraxOptions { Enabled = true };

        var act = () => EntraxOptionsGuard.ThrowIfInvalid(o, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*KeyId*").And.Message.Should().Contain("Secret").And.Contain("CallbackUrl");
    }

    [Theory]
    [InlineData("http://alta.ejemplo.com/cb")]
    [InlineData("/api/v1/signups/entrax/callback")]
    [InlineData("")]
    public void El_callback_tiene_que_ser_https_absoluto(string url)
    {
        var o = Completas();
        o.CallbackUrl = url;

        EntraxOptionsGuard.Problems(o, isDevelopment: false).Should().ContainSingle(p => p.Contains("CallbackUrl"));
    }

    [Fact]
    public void Encendido_y_completo_arranca()
        => ((Action)(() => EntraxOptionsGuard.ThrowIfInvalid(Completas(), isDevelopment: false))).Should().NotThrow();

    [Fact]
    public void El_fake_fuera_de_Development_no_arranca_aunque_este_apagado()
    {
        var act = () => EntraxOptionsGuard.ThrowIfInvalid(new EntraxOptions { UseFake = true }, isDevelopment: false);
        act.Should().Throw<InvalidOperationException>().WithMessage("*UseFake*");
    }

    [Fact]
    public void En_Development_el_fake_no_necesita_credenciales()
        => EntraxOptionsGuard.Problems(new EntraxOptions { Enabled = true, UseFake = true }, isDevelopment: true)
            .Should().BeEmpty();

    [Fact]
    public void En_Development_avisa_pero_no_tumba_el_arranque()
    {
        var o = new EntraxOptions { Enabled = true };

        EntraxOptionsGuard.Problems(o, isDevelopment: true).Should().NotBeEmpty();
        ((Action)(() => EntraxOptionsGuard.ThrowIfInvalid(o, isDevelopment: true))).Should().NotThrow();
    }

    [Fact]
    public void Por_defecto_solo_se_ofrece_en_Rusia()
    {
        var o = new EntraxOptions { Enabled = true };

        o.IsOfferedFor("RU").Should().BeTrue();
        o.IsOfferedFor("ru").Should().BeTrue();
        o.IsOfferedFor("US").Should().BeFalse();
        o.IsOfferedFor(null).Should().BeFalse();
    }

    [Fact]
    public void Apagado_no_se_ofrece_en_ningun_pais()
        => new EntraxOptions { Enabled = false }.IsOfferedFor("RU").Should().BeFalse();

    [Fact]
    public void Los_paises_de_la_configuracion_sustituyen_a_los_de_por_defecto()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Entrax:Enabled"]      = "true",
            ["Entrax:Countries:0"]  = " kz ",
            ["Entrax:Countries:1"]  = "BY"
        }).Build();

        var o = EntraxOptions.FromConfiguration(config);

        o.EffectiveCountries.Should().Equal("KZ", "BY");
        o.IsOfferedFor("RU").Should().BeFalse();
    }

    [Fact]
    public void La_tolerancia_del_callback_nunca_baja_de_40_minutos()
        => new EntraxOptions { CallbackToleranceMinutes = 5 }.CallbackTolerance.Should().Be(TimeSpan.FromMinutes(40));
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxOptionsTests -nologo`
Expected: FAIL de compilación (`EntraxOptions` no existe).

- [ ] **Step 3: Crear `EntraxOptions.cs`**

```csharp
using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Configuración de Entrax (sección "Entrax"). KeyId y Secret NUNCA van en el appsettings.json base:
/// variables de entorno (Entrax__KeyId, Entrax__Secret) o el almacén de secretos del entorno.
/// </summary>
public sealed class EntraxOptions
{
    public const string Section        = "Entrax";
    public const string DefaultBaseUrl = "https://entrax.pro/api/v1/cash-register";

    public static readonly IReadOnlyList<string> DefaultCountries = ["RU"];

    public bool    Enabled     { get; set; }
    public string  BaseUrl     { get; set; } = DefaultBaseUrl;
    public string? KeyId       { get; set; }
    public string? Secret      { get; set; }

    /// <summary>URL pública https de POST api/v1/signups/entrax/callback.</summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Países ISO2 en los que se ofrece. Null o vacío = <see cref="DefaultCountries"/>. Es null por
    /// defecto a propósito: el binder de configuración AÑADE a un array ya inicializado.
    /// </summary>
    public string[]? Countries { get; set; }

    /// <summary>Solo Development: registra FakeEntraxClient en lugar del cliente real.</summary>
    public bool UseFake { get; set; }

    /// <summary>Antigüedad máxima del timestamp de un callback. Nunca menos de 40 (Entrax reintenta 35 min).</summary>
    public int CallbackToleranceMinutes { get; set; } = 60;

    /// <summary>Durante cuánto tiempo tras caducar se sigue mirando si un QR se pagó tarde.</summary>
    public int LatePaymentWindowMinutes { get; set; } = 120;

    /// <summary>Segundos mínimos entre dos consultas a Entrax del mismo pago desde la pantalla.</summary>
    public int PollThrottleSeconds { get; set; } = 15;

    /// <summary>Pagos que consulta el barrido en cada pasada (límite de Entrax: 100/min por IP).</summary>
    public int SweepBatchSize { get; set; } = 50;

    public TimeSpan CallbackTolerance => TimeSpan.FromMinutes(Math.Max(40, CallbackToleranceMinutes));
    public TimeSpan PollThrottle      => TimeSpan.FromSeconds(Math.Max(5, PollThrottleSeconds));
    public TimeSpan LatePaymentWindow => TimeSpan.FromMinutes(Math.Max(0, LatePaymentWindowMinutes));

    public IReadOnlyList<string> EffectiveCountries =>
        (Countries is { Length: > 0 } ? Countries : DefaultCountries)
            .Select(c => (c ?? string.Empty).Trim().ToUpperInvariant())
            .Where(c => c.Length == 2)
            .Distinct()
            .ToArray();

    public bool IsOfferedFor(string? countryIso2)
        => Enabled
        && !string.IsNullOrWhiteSpace(countryIso2)
        && EffectiveCountries.Contains(countryIso2.Trim().ToUpperInvariant());

    public static EntraxOptions FromConfiguration(IConfiguration configuration)
        => configuration.GetSection(Section).Get<EntraxOptions>() ?? new EntraxOptions();
}
```

- [ ] **Step 4: Crear `EntraxOptionsGuard.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Se ejecuta al arrancar SignupAPI. Fuera de Development, Entrax encendido sin credenciales o sin
/// callback https NO arranca: un alta que cobra sin poder verificar el callback, o con un callback
/// que Entrax no puede alcanzar, es peor que no ofrecer el método. El fake fuera de Development
/// tampoco arranca, esté o no encendido.
/// </summary>
public static class EntraxOptionsGuard
{
    public static IReadOnlyList<string> Problems(EntraxOptions o, bool isDevelopment)
    {
        var problems = new List<string>();

        if (o.UseFake && !isDevelopment)
            problems.Add("Entrax:UseFake solo se permite en Development.");

        if (!o.Enabled || (o.UseFake && isDevelopment))
            return problems;

        if (string.IsNullOrWhiteSpace(o.KeyId))  problems.Add("Entrax:KeyId está vacío.");
        if (string.IsNullOrWhiteSpace(o.Secret)) problems.Add("Entrax:Secret está vacío.");
        if (!IsAbsoluteHttps(o.CallbackUrl))     problems.Add("Entrax:CallbackUrl debe ser una URL https absoluta.");
        if (!IsAbsoluteHttps(o.BaseUrl))         problems.Add("Entrax:BaseUrl debe ser una URL https absoluta.");

        return problems;
    }

    public static void ThrowIfInvalid(EntraxOptions o, bool isDevelopment)
    {
        var problems = Problems(o, isDevelopment);
        if (problems.Count == 0 || isDevelopment) return;

        throw new InvalidOperationException("[Entrax] Configuración inválida: " + string.Join(" ", problems));
    }

    private static bool IsAbsoluteHttps(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
}
```

- [ ] **Step 5: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxOptionsTests -nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxOptions.cs \
        MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxOptionsGuard.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxOptionsTests.cs
git commit -m "Opciones de Entrax: paises en los que se ofrece, tolerancias y guarda de arranque que no deja encenderlo sin credenciales ni callback https"
```

---

### Task 4: Firma HMAC (peticiones y callbacks)

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxSignature.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignatureTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Security.Cryptography;
using System.Text;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// La firma de Entrax según su documentación: canónica METHOD\nPATH\nTIMESTAMP\nNONCE\nbase64(sha256(body))
/// firmada con HMAC-SHA256 y en Base64; el callback, HMAC-SHA256 del cuerpo crudo en X-Signature.
/// Los valores esperados se calculan aquí aparte, sin pasar por la clase que se prueba.
/// </summary>
public class EntraxSignatureTests
{
    private const string Secreto = "secreto-de-pruebas";

    private static string HmacBase64(string secreto, byte[] datos)
        => Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), datos));

    [Fact]
    public void El_hash_de_un_cuerpo_vacio_es_el_de_sha256_vacio()
        => EntraxSignature.BodyHash(string.Empty).Should().Be("47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=");

    [Fact]
    public void La_canonica_sigue_el_formato_de_la_documentacion()
    {
        var body = "{\"amount\":100.50}";
        var esperado = "POST\n/api/v1/cash-register/qr\n1640995200\nabc\n"
                     + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

        EntraxSignature.Canonical("POST", "/api/v1/cash-register/qr", 1640995200, "abc", body).Should().Be(esperado);
    }

    [Fact]
    public void La_firma_es_HMAC_SHA256_en_Base64_de_la_canonica()
    {
        var canonica = "GET\n/api/v1/cash-register/payment/x\n1\nn\n47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=";

        EntraxSignature.Sign(canonica, Secreto).Should().Be(HmacBase64(Secreto, Encoding.UTF8.GetBytes(canonica)));
    }

    [Fact]
    public void La_cabecera_lleva_las_cuatro_partes()
        => EntraxSignature.AuthorizationHeader("key-1", 1640995200, "abc", "c2ln")
            .Should().Be("HMAC-SHA256 KeyId=key-1,Timestamp=1640995200,Nonce=abc,Signature=c2ln");

    [Fact]
    public void Un_callback_firmado_en_Base64_se_acepta()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":\"x\"}");
        EntraxSignature.VerifyCallback(cuerpo, HmacBase64(Secreto, cuerpo), Secreto).Should().BeTrue();
    }

    [Fact]
    public void Un_callback_firmado_en_hexadecimal_tambien_se_acepta()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":\"x\"}");
        var hex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secreto), cuerpo)).ToLowerInvariant();

        EntraxSignature.VerifyCallback(cuerpo, hex, Secreto).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-base64!!")]
    [InlineData("AAAA")]
    public void Sin_firma_o_con_firma_mala_se_rechaza(string? firma)
        => EntraxSignature.VerifyCallback(Encoding.UTF8.GetBytes("{}"), firma, Secreto).Should().BeFalse();

    [Fact]
    public void Un_byte_cambiado_en_el_cuerpo_invalida_la_firma()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"status\":\"SUCCESS\"}");
        var firma = HmacBase64(Secreto, cuerpo);
        cuerpo[^2] = (byte)'X';

        EntraxSignature.VerifyCallback(cuerpo, firma, Secreto).Should().BeFalse();
    }

    [Fact]
    public void Sin_secreto_nada_es_valido()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{}");
        EntraxSignature.VerifyCallback(cuerpo, HmacBase64(string.Empty, cuerpo), string.Empty).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignatureTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxSignature.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Firma HMAC-SHA256 de Entrax (Cash Register API v1).
///
/// Peticiones: canónica "METHOD\nPATH\nTIMESTAMP\nNONCE\nbase64(sha256(body))", firmada con el
/// secreto y en Base64, en la cabecera Authorization: HMAC-SHA256 KeyId=…,Timestamp=…,Nonce=…,Signature=….
///
/// Callbacks: X-Signature = base64(HMAC-SHA256(secreto, cuerpo crudo)). La documentación muestra en
/// un ejemplo un valor que parece hexadecimal; se aceptan las dos codificaciones, siempre sobre el
/// cuerpo crudo y comparando en tiempo constante. El legado comparaba con == y eso no se repite.
/// </summary>
public static class EntraxSignature
{
    public static string BodyHash(string body)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

    public static string Canonical(string method, string path, long timestamp, string nonce, string body)
        => $"{method}\n{path}\n{timestamp}\n{nonce}\n{BodyHash(body)}";

    public static string Sign(string canonical, string secret)
        => Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical)));

    public static string AuthorizationHeader(string keyId, long timestamp, string nonce, string signature)
        => $"HMAC-SHA256 KeyId={keyId},Timestamp={timestamp},Nonce={nonce},Signature={signature}";

    public static bool VerifyCallback(ReadOnlySpan<byte> rawBody, string? signatureHeader, string? secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrEmpty(secret))
            return false;

        var given = Decode(signatureHeader.Trim());
        if (given is null) return false;

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody);
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }

    /// <summary>64 caracteres hexadecimales o Base64. Cualquier otra cosa, null.</summary>
    private static byte[]? Decode(string value)
    {
        if (value.Length == 64 && value.All(Uri.IsHexDigit))
            return Convert.FromHexString(value);

        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out var written) ? buffer[..written] : null;
    }
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignatureTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxSignature.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignatureTests.cs
git commit -m "Firma HMAC de Entrax: canonica de las peticiones y verificacion en tiempo constante del callback sobre el cuerpo crudo"
```

---

### Task 5: Cliente HTTP de Entrax

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxProviderContracts.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxClient.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxClientTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El cliente habla el contrato de la documentación de Entrax: ruta, firma, cuerpo, sobre de
/// respuesta {success, data, error} y errores de red. Nunca lanza: devuelve EntraxCall con el código.
/// </summary>
public class EntraxClientTests
{
    private static readonly DateTime UtcFijo = new(2026, 10, 16, 7, 0, 0, DateTimeKind.Utc);
    private const string PaymentId = "550e8400-e29b-41d4-a716-446655440000";

    private sealed class Manejador(Func<HttpRequestMessage, string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;
        public string Cuerpo = string.Empty;
        public int Llamadas;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Llamadas++;
            Peticion = r;
            Cuerpo = r.Content is null ? string.Empty : await r.Content.ReadAsStringAsync(ct);
            return responder(r, Cuerpo);
        }
    }

    private sealed class Caido : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout"));
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static EntraxOptions Opciones() => new()
    {
        Enabled = true, KeyId = "key-1", Secret = "secreto-de-pruebas",
        CallbackUrl = "https://alta.pruebas/api/v1/signups/entrax/callback"
    };

    private static EntraxClient Cliente(HttpMessageHandler h)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(c => c.UtcNow).Returns(UtcFijo);
        return new EntraxClient(new HttpClient(h), Opciones(), reloj.Object, NullLogger<EntraxClient>.Instance);
    }

    /// <summary>"HMAC-SHA256 KeyId=…,Timestamp=…,Nonce=…,Signature=…" → diccionario. La firma Base64 puede llevar '='.</summary>
    private static Dictionary<string, string> Partes(HttpRequestMessage r)
        => r.Headers.GetValues("Authorization").Single()["HMAC-SHA256 ".Length..]
            .Split(',').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);

    private const string QrOk = """
        {"success":true,"data":{
          "qrCode":{"image":"data:image/png;base64,iVBORw0KGgo=","link":"https://qr.nspk.ru/AS100","paymentId":"550e8400-e29b-41d4-a716-446655440000"},
          "payment":{"amountInRubles":7525.50,"exchangeRate":75.255,"currency":"USD","originalAmount":100.50,"expiresAt":"2026-10-16T07:15:00Z"},
          "order":{"orderId":"SIGNUP-o1-1","status":"pending"}},
         "requestId":"r1"}
        """;

    [Fact]
    public async Task Pide_el_QR_con_POST_firmado_a_la_ruta_de_la_caja()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK, QrOk));

        await Cliente(h).CreateQrAsync(
            new EntraxQrRequest(100.50m, "USD", "SIGNUP-o1-1", "MLM Conqueror signup", "https://alta.pruebas/cb"), default);

        h.Peticion!.Method.Should().Be(HttpMethod.Post);
        h.Peticion.RequestUri!.ToString().Should().Be("https://entrax.pro/api/v1/cash-register/qr");

        using var doc = JsonDocument.Parse(h.Cuerpo);
        doc.RootElement.GetProperty("amount").GetDecimal().Should().Be(100.50m);
        doc.RootElement.GetProperty("currency").GetString().Should().Be("USD");
        doc.RootElement.GetProperty("orderId").GetString().Should().Be("SIGNUP-o1-1");
        doc.RootElement.GetProperty("callbackUrl").GetString().Should().Be("https://alta.pruebas/cb");

        // La firma se recalcula con las piezas de la cabecera: si coincide, se firmó lo que se mandó.
        h.Peticion.Headers.GetValues("Authorization").Single().Should().StartWith("HMAC-SHA256 KeyId=key-1,");
        var partes = Partes(h.Peticion);
        partes["Timestamp"].Should().Be(new DateTimeOffset(UtcFijo).ToUnixTimeSeconds().ToString());
        var canonica = EntraxSignature.Canonical("POST", "/api/v1/cash-register/qr",
            long.Parse(partes["Timestamp"]), partes["Nonce"], h.Cuerpo);
        partes["Signature"].Should().Be(EntraxSignature.Sign(canonica, "secreto-de-pruebas"));
    }

    [Fact]
    public async Task Lee_el_QR_de_la_respuesta()
    {
        var r = await Cliente(new Manejador((_, _) => Json(HttpStatusCode.OK, QrOk)))
            .CreateQrAsync(new EntraxQrRequest(100.50m, "USD", "SIGNUP-o1-1", null, null), default);

        r.Ok.Should().BeTrue();
        r.Value!.PaymentId.Should().Be(PaymentId);
        r.Value.Link.Should().Be("https://qr.nspk.ru/AS100");
        r.Value.ImageDataUri.Should().StartWith("data:image/png;base64,");
        r.Value.AmountInRubles.Should().Be(7525.50m);
        r.Value.ExchangeRate.Should().Be(75.255m);
        r.Value.Currency.Should().Be("USD");
        r.Value.OriginalAmount.Should().Be(100.50m);
        r.Value.ExpiresAt.Should().Be(new DateTimeOffset(2026, 10, 16, 7, 15, 0, TimeSpan.Zero));
        r.HttpStatus.Should().Be(200);
    }

    [Fact]
    public async Task Un_error_de_Entrax_devuelve_su_codigo()
    {
        var r = await Cliente(new Manejador((_, _) => Json(HttpStatusCode.BadRequest,
                """{"success":false,"error":{"code":"RATE_NOT_AVAILABLE","message":"No rate"},"requestId":"r"}""")))
            .CreateQrAsync(new EntraxQrRequest(10m, "USD", "SIGNUP-o1-1", null, null), default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be("RATE_NOT_AVAILABLE");
        r.HttpStatus.Should().Be(400);
    }

    [Fact]
    public async Task Sin_respuesta_es_TRANSPORT_y_no_lanza()
    {
        var r = await Cliente(new Caido())
            .CreateQrAsync(new EntraxQrRequest(10m, "USD", "SIGNUP-o1-1", null, null), default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be(EntraxErrorCodes.Transport);
        r.HttpStatus.Should().Be(0);
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_JSON_es_BAD_RESPONSE()
    {
        var r = await Cliente(new Manejador((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
            { Content = new StringContent("<html>502</html>") }))
            .CreateQrAsync(new EntraxQrRequest(10m, "USD", "SIGNUP-o1-1", null, null), default);

        r.ErrorCode.Should().Be(EntraxErrorCodes.BadResponse);
        r.HttpStatus.Should().Be(502);
    }

    [Fact]
    public async Task Consulta_el_pago_con_GET_firmado_y_cuerpo_vacio()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK, """
            {"success":true,"data":{"paymentId":"550e8400-e29b-41d4-a716-446655440000","status":"paid","orderId":"SIGNUP-o1-1",
             "amount":{"original":100.50,"currency":"USD","rubles":7525.50,"exchangeRate":75.255},
             "timestamps":{"created":"2026-10-16T07:00:00Z","completed":"2026-10-16T07:05:30Z"},
             "transactionDetails":{"sbpTransactionId":null,"payerPhone":"+7 900 000 00 00"}}}
            """));

        var r = await Cliente(h).GetPaymentAsync(PaymentId, default);

        h.Peticion!.Method.Should().Be(HttpMethod.Get);
        h.Peticion.RequestUri!.AbsolutePath.Should().Be($"/api/v1/cash-register/payment/{PaymentId}");
        h.Peticion.Content.Should().BeNull();
        var partes = Partes(h.Peticion);
        var canonica = EntraxSignature.Canonical("GET", $"/api/v1/cash-register/payment/{PaymentId}",
            long.Parse(partes["Timestamp"]), partes["Nonce"], string.Empty);
        partes["Signature"].Should().Be(EntraxSignature.Sign(canonica, "secreto-de-pruebas"));

        r.Ok.Should().BeTrue();
        r.Value!.Status.Should().Be("paid");
        r.Value.OrderId.Should().Be("SIGNUP-o1-1");
        r.Value.Original.Should().Be(100.50m);
        r.Value.Currency.Should().Be("USD");
        r.Value.CompletedAt.Should().Be(new DateTimeOffset(2026, 10, 16, 7, 5, 30, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("../rates")]
    [InlineData("no-es-un-guid")]
    [InlineData("")]
    public async Task Un_paymentId_que_no_es_GUID_no_sale_a_la_red(string id)
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK, "{}"));

        var r = await Cliente(h).GetPaymentAsync(id, default);

        r.ErrorCode.Should().Be(EntraxErrorCodes.InvalidPaymentId);
        h.Llamadas.Should().Be(0);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxClientTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxProviderContracts.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>Lo que se pide a POST /qr. Amount en la moneda indicada (en el alta, USD).</summary>
public sealed record EntraxQrRequest(
    decimal Amount, string Currency, string OrderId, string? Description, string? CallbackUrl);

/// <summary>Lo que devolvió POST /qr, ya fuera del sobre.</summary>
public sealed record EntraxQr(
    string PaymentId,
    string? ImageDataUri,
    string? Link,
    decimal AmountInRubles,
    decimal ExchangeRate,
    string Currency,
    decimal OriginalAmount,
    DateTimeOffset? ExpiresAt,
    string? OrderId);

/// <summary>
/// Lo que devolvió GET /payment/{id}. Status: pending | paid | failed | expired. No lleva
/// transactionDetails (teléfono del pagador): no se necesita y así no puede acabar en ningún sitio.
/// </summary>
public sealed record EntraxPaymentState(
    string PaymentId,
    string Status,
    string? OrderId,
    decimal Original,
    string Currency,
    decimal Rubles,
    decimal ExchangeRate,
    DateTimeOffset? CompletedAt);

/// <summary>Resultado de una llamada. El cliente nunca lanza por un fallo del proveedor o de la red.</summary>
public sealed record EntraxCall<T>(T? Value, int HttpStatus, string? ErrorCode, string? ErrorMessage, long ElapsedMs)
    where T : class
{
    public bool Ok => Value is not null && ErrorCode is null;

    public static EntraxCall<T> Success(T value, int httpStatus, long elapsedMs)
        => new(value, httpStatus, null, null, elapsedMs);

    public static EntraxCall<T> Fail(int httpStatus, string errorCode, string? message, long elapsedMs)
        => new(null, httpStatus, errorCode, message, elapsedMs);
}

public static class EntraxErrorCodes
{
    /// <summary>Sin respuesta: timeout, DNS, conexión rechazada.</summary>
    public const string Transport        = "TRANSPORT";

    /// <summary>Respuesta que no es el sobre esperado o le faltan campos.</summary>
    public const string BadResponse      = "BAD_RESPONSE";

    /// <summary>paymentId que no es un GUID: no se llama (evita rutas inyectadas).</summary>
    public const string InvalidPaymentId = "INVALID_PAYMENT_ID";
}

public interface IEntraxClient
{
    Task<EntraxCall<EntraxQr>> CreateQrAsync(EntraxQrRequest request, CancellationToken ct);
    Task<EntraxCall<EntraxPaymentState>> GetPaymentAsync(string paymentId, CancellationToken ct);
}
```

- [ ] **Step 4: Crear `EntraxClient.cs`**

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Cliente de la API Cash Register v1 de Entrax (HttpClient tipado, timeout 30 s en AddEntrax).
///
/// El Timestamp de la firma es UTC unix porque el protocolo lo exige (Entrax rechaza los que se
/// separan más de 5 minutos de su reloj); es el único uso de UtcNow aquí.
///
/// No registra cuerpos ni cabeceras: el log estructurado lo escribe quien llama (EntraxApiCalls).
/// </summary>
public sealed class EntraxClient : IEntraxClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient            _http;
    private readonly EntraxOptions         _options;
    private readonly IDateTimeProvider     _clock;
    private readonly ILogger<EntraxClient> _logger;

    public EntraxClient(HttpClient http, EntraxOptions options, IDateTimeProvider clock, ILogger<EntraxClient> logger)
    {
        _http    = http;
        _options = options;
        _clock   = clock;
        _logger  = logger;
    }

    private Uri BaseUri => new(_options.BaseUrl.TrimEnd('/') + "/");

    public Task<EntraxCall<EntraxQr>> CreateQrAsync(EntraxQrRequest r, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            amount      = r.Amount,
            currency    = r.Currency,
            orderId     = r.OrderId,
            description = r.Description,
            callbackUrl = r.CallbackUrl
        }, Json);

        return SendAsync<QrData, EntraxQr>(HttpMethod.Post, "qr", body, MapQr, ct);
    }

    public Task<EntraxCall<EntraxPaymentState>> GetPaymentAsync(string paymentId, CancellationToken ct)
    {
        if (!Guid.TryParse(paymentId, out var id))
            return Task.FromResult(EntraxCall<EntraxPaymentState>.Fail(0, EntraxErrorCodes.InvalidPaymentId, "paymentId no es un GUID.", 0));

        return SendAsync<StatusData, EntraxPaymentState>(HttpMethod.Get, $"payment/{id:D}", string.Empty, MapStatus, ct);
    }

    private async Task<EntraxCall<TOut>> SendAsync<TWire, TOut>(
        HttpMethod method, string relative, string body, Func<TWire, TOut?> map, CancellationToken ct)
        where TWire : class
        where TOut : class
    {
        var uri       = new Uri(BaseUri, relative);
        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var nonce     = Guid.NewGuid().ToString("N");
        var signature = EntraxSignature.Sign(
            EntraxSignature.Canonical(method.Method, uri.AbsolutePath, timestamp, nonce, body), _options.Secret ?? string.Empty);

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("Authorization",
            EntraxSignature.AuthorizationHeader(_options.KeyId ?? string.Empty, timestamp, nonce, signature));
        if (method != HttpMethod.Get)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();
        HttpResponseMessage response;
        string text;
        try
        {
            response = await _http.SendAsync(request, ct);
            text     = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning("Entrax {Method} {Path}: sin respuesta ({Error}).", method.Method, uri.AbsolutePath, ex.GetType().Name);
            return EntraxCall<TOut>.Fail(0, EntraxErrorCodes.Transport, ex.GetType().Name, sw.ElapsedMilliseconds);
        }

        using (response)
        {
            var status = (int)response.StatusCode;

            Envelope<TWire>? envelope = null;
            try { envelope = JsonSerializer.Deserialize<Envelope<TWire>>(text, Json); }
            catch (JsonException) { }

            if (envelope is null)
                return EntraxCall<TOut>.Fail(status, EntraxErrorCodes.BadResponse, null, sw.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode || !envelope.Success || envelope.Data is null)
            {
                var code = envelope.Error?.Code ?? (response.IsSuccessStatusCode ? EntraxErrorCodes.BadResponse : $"HTTP_{status}");
                _logger.LogWarning("Entrax {Method} {Path}: {Status} {Code}.", method.Method, uri.AbsolutePath, status, code);
                return EntraxCall<TOut>.Fail(status, code, envelope.Error?.Message, sw.ElapsedMilliseconds);
            }

            var mapped = map(envelope.Data);
            return mapped is null
                ? EntraxCall<TOut>.Fail(status, EntraxErrorCodes.BadResponse, "Respuesta incompleta.", sw.ElapsedMilliseconds)
                : EntraxCall<TOut>.Success(mapped, status, sw.ElapsedMilliseconds);
        }
    }

    private static EntraxQr? MapQr(QrData d)
    {
        if (d.QrCode?.PaymentId is not { } id || !Guid.TryParse(id, out _) || d.Payment is null)
            return null;

        return new EntraxQr(id, d.QrCode.Image, d.QrCode.Link, d.Payment.AmountInRubles, d.Payment.ExchangeRate,
            d.Payment.Currency ?? string.Empty, d.Payment.OriginalAmount, d.Payment.ExpiresAt, d.Order?.OrderId);
    }

    private static EntraxPaymentState? MapStatus(StatusData d)
    {
        if (string.IsNullOrWhiteSpace(d.PaymentId) || string.IsNullOrWhiteSpace(d.Status))
            return null;

        return new EntraxPaymentState(d.PaymentId, d.Status, d.OrderId,
            d.Amount?.Original ?? 0m, d.Amount?.Currency ?? string.Empty,
            d.Amount?.Rubles ?? 0m, d.Amount?.ExchangeRate ?? 0m, d.Timestamps?.Completed);
    }

    // ── Forma del cable (documentación de Entrax, Cash Register API v1) ──────────────────────

    private sealed class Envelope<T>
    {
        public bool       Success { get; set; }
        public T?         Data    { get; set; }
        public ErrorBody? Error   { get; set; }
    }

    private sealed class ErrorBody
    {
        public string? Code    { get; set; }
        public string? Message { get; set; }
    }

    private sealed class QrData
    {
        public QrCodePart?  QrCode  { get; set; }
        public PaymentPart? Payment { get; set; }
        public OrderPart?   Order   { get; set; }
    }

    private sealed class QrCodePart
    {
        public string? Image     { get; set; }
        public string? Link      { get; set; }
        public string? PaymentId { get; set; }
    }

    private sealed class PaymentPart
    {
        public decimal         AmountInRubles { get; set; }
        public decimal         ExchangeRate   { get; set; }
        public string?         Currency       { get; set; }
        public decimal         OriginalAmount { get; set; }
        public DateTimeOffset? ExpiresAt      { get; set; }
    }

    private sealed class OrderPart
    {
        public string? OrderId { get; set; }
        public string? Status  { get; set; }
    }

    private sealed class StatusData
    {
        public string?         PaymentId  { get; set; }
        public string?         Status     { get; set; }
        public string?         OrderId    { get; set; }
        public AmountPart?     Amount     { get; set; }
        public TimestampsPart? Timestamps { get; set; }
    }

    private sealed class AmountPart
    {
        public decimal Original     { get; set; }
        public string? Currency     { get; set; }
        public decimal Rubles       { get; set; }
        public decimal ExchangeRate { get; set; }
    }

    private sealed class TimestampsPart
    {
        public DateTimeOffset? Created   { get; set; }
        public DateTimeOffset? Completed { get; set; }
    }
}
```

- [ ] **Step 5: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxClientTests -nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxProviderContracts.cs \
        MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxClient.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxClientTests.cs
git commit -m "Cliente de Entrax: QR y consulta de pago firmados, sobre de respuesta y errores de red sin excepciones"
```

---

### Task 6: Fake de Entrax para Development

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Entrax/FakeEntraxClient.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/FakeEntraxClientTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class FakeEntraxClientTests
{
    private static readonly DateTime Inicio = new(2026, 10, 16, 7, 0, 0, DateTimeKind.Utc);

    private static (FakeEntraxClient Fake, Mock<IDateTimeProvider> Reloj) Crear()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(c => c.UtcNow).Returns(Inicio);
        return (new FakeEntraxClient(reloj.Object), reloj);
    }

    [Fact]
    public async Task Emite_un_QR_de_prueba_en_rublos_y_queda_pendiente()
    {
        var (fake, _) = Crear();

        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        Guid.TryParse(qr.PaymentId, out _).Should().BeTrue();
        qr.ImageDataUri.Should().StartWith("data:image/png;base64,");
        qr.Link.Should().StartWith("https://");
        qr.AmountInRubles.Should().Be(80m * FakeEntraxClient.Rate);
        qr.OriginalAmount.Should().Be(80m);
        qr.ExpiresAt.Should().Be(new DateTimeOffset(Inicio).Add(FakeEntraxClient.Lifetime));

        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Marcar_pagado_devuelve_paid_por_el_importe_pedido()
    {
        var (fake, _) = Crear();
        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        fake.MarkPaid(qr.PaymentId).Should().BeTrue();

        var estado = (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!;
        estado.Status.Should().Be("paid");
        estado.Original.Should().Be(80m);
        estado.Currency.Should().Be("USD");
        estado.OrderId.Should().Be("SIGNUP-o-1");
    }

    [Fact]
    public async Task Se_puede_simular_un_pago_de_menos()
    {
        var (fake, _) = Crear();
        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        fake.MarkPaid(qr.PaymentId, underpayBy: 1m);

        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Original.Should().Be(79m);
    }

    [Fact]
    public async Task Caduca_solo_si_nadie_lo_pago()
    {
        var (fake, reloj) = Crear();
        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        reloj.Setup(c => c.UtcNow).Returns(Inicio.Add(FakeEntraxClient.Lifetime).AddSeconds(1));

        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Status.Should().Be("expired");
        fake.MarkPaid(qr.PaymentId);
        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Status.Should().Be("paid",
            "un pago tardío es justo lo que el fake tiene que poder simular.");
    }

    [Fact]
    public async Task Un_pago_desconocido_es_PAYMENT_NOT_FOUND()
    {
        var (fake, _) = Crear();
        var r = await fake.GetPaymentAsync(Guid.NewGuid().ToString(), default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be("PAYMENT_NOT_FOUND");
        fake.MarkFailed(Guid.NewGuid().ToString()).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter FakeEntraxClientTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `FakeEntraxClient.cs`**

```csharp
using System.Collections.Concurrent;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Entrax de mentira para Development (Entrax:UseFake = true). Singleton en memoria: emite QR de
/// prueba y deja marcarlos pagados, pagados de menos, fallidos o caducados desde
/// POST /api/v1/dev/entrax/{paymentId}/{resultado} de SignupAPI. EntraxOptionsGuard impide
/// arrancar con él fuera de Development.
/// </summary>
public sealed class FakeEntraxClient : IEntraxClient
{
    public const decimal Rate = 95.50m;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>PNG de 1×1: la pantalla pinta algo; en Development se paga con el endpoint, no escaneando.</summary>
    public const string PlaceholderPng =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private sealed class Entry
    {
        public required string         OrderId   { get; init; }
        public required decimal        Amount    { get; init; }
        public required string         Currency  { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public string   Status       { get; set; } = "pending";
        public decimal? PaidOriginal { get; set; }
    }

    private readonly ConcurrentDictionary<string, Entry> _payments = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDateTimeProvider _clock;

    public FakeEntraxClient(IDateTimeProvider clock) => _clock = clock;

    private DateTimeOffset UtcNow => new(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));

    public Task<EntraxCall<EntraxQr>> CreateQrAsync(EntraxQrRequest request, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("D");
        var expires = UtcNow.Add(Lifetime);
        _payments[id] = new Entry
        {
            OrderId = request.OrderId, Amount = request.Amount, Currency = request.Currency, ExpiresAt = expires
        };

        var qr = new EntraxQr(id, PlaceholderPng, $"https://qr.nspk.ru/FAKE{id.Replace("-", string.Empty)}",
            Math.Round(request.Amount * Rate, 2), Rate, request.Currency, request.Amount, expires, request.OrderId);
        return Task.FromResult(EntraxCall<EntraxQr>.Success(qr, 200, 1));
    }

    public Task<EntraxCall<EntraxPaymentState>> GetPaymentAsync(string paymentId, CancellationToken ct)
    {
        if (!_payments.TryGetValue(paymentId, out var e))
            return Task.FromResult(EntraxCall<EntraxPaymentState>.Fail(404, "PAYMENT_NOT_FOUND", "Payment not found.", 1));

        var status   = e.Status == "pending" && UtcNow > e.ExpiresAt ? "expired" : e.Status;
        var original = e.PaidOriginal ?? e.Amount;
        var state = new EntraxPaymentState(paymentId, status, e.OrderId, original, e.Currency,
            Math.Round(original * Rate, 2), Rate, status == "paid" ? UtcNow : null);
        return Task.FromResult(EntraxCall<EntraxPaymentState>.Success(state, 200, 1));
    }

    public bool MarkPaid(string paymentId, decimal underpayBy = 0m)
    {
        if (!_payments.TryGetValue(paymentId, out var e)) return false;
        e.PaidOriginal = e.Amount - underpayBy;
        e.Status = "paid";
        return true;
    }

    public bool MarkFailed(string paymentId) => Set(paymentId, "failed");

    public bool MarkExpired(string paymentId) => Set(paymentId, "expired");

    private bool Set(string paymentId, string status)
    {
        if (!_payments.TryGetValue(paymentId, out var e)) return false;
        e.Status = status;
        return true;
    }
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter FakeEntraxClientTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Entrax/FakeEntraxClient.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/FakeEntraxClientTests.cs
git commit -m "Entrax de prueba para Development: QR en memoria que se puede marcar pagado, pagado de menos, fallido o caducado"
```

---

### Task 7: Registro de Entrax en el contenedor

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxServiceCollectionExtensions.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxRegistroTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class EntraxRegistroTests
{
    private static ServiceProvider Contenedor(EntraxOptions o)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton(Mock.Of<IDateTimeProvider>());
        s.AddEntrax(o);
        return s.BuildServiceProvider();
    }

    [Fact]
    public void Con_el_fake_el_cliente_es_el_mismo_singleton_que_marca_los_pagos()
    {
        using var sp = Contenedor(new EntraxOptions { UseFake = true });

        var cliente = sp.GetRequiredService<IEntraxClient>();
        cliente.Should().BeOfType<FakeEntraxClient>();
        cliente.Should().BeSameAs(sp.GetRequiredService<FakeEntraxClient>());
        sp.GetRequiredService<IEntraxClient>().Should().BeSameAs(cliente);
    }

    [Fact]
    public void Sin_el_fake_el_cliente_es_el_real_y_el_fake_no_existe()
    {
        using var sp = Contenedor(new EntraxOptions { Enabled = true, KeyId = "k", Secret = "s" });

        sp.GetRequiredService<IEntraxClient>().Should().BeOfType<EntraxClient>();
        sp.GetService<FakeEntraxClient>().Should().BeNull();
    }

    [Fact]
    public void Las_opciones_se_registran_tal_cual()
    {
        var o = new EntraxOptions { Enabled = true };
        using var sp = Contenedor(o);

        sp.GetRequiredService<EntraxOptions>().Should().BeSameAs(o);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxRegistroTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxServiceCollectionExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

public static class EntraxServiceCollectionExtensions
{
    /// <summary>
    /// Registra las opciones (singleton) y el cliente: el fake si <see cref="EntraxOptions.UseFake"/>
    /// (la guarda de arranque ya impidió que eso pase fuera de Development), o el real como
    /// HttpClient tipado con 30 s de timeout, el mismo tope que los clientes de payout.
    /// </summary>
    public static IServiceCollection AddEntrax(this IServiceCollection services, EntraxOptions options)
    {
        services.AddSingleton(options);

        if (options.UseFake)
        {
            services.AddSingleton<FakeEntraxClient>();
            services.AddSingleton<IEntraxClient>(sp => sp.GetRequiredService<FakeEntraxClient>());
            return services;
        }

        services.AddHttpClient<IEntraxClient, EntraxClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
        return services;
    }
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxRegistroTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Entrax/EntraxServiceCollectionExtensions.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxRegistroTests.cs
git commit -m "Registro de Entrax: cliente real tipado con 30 s de timeout o el fake como singleton"
```

---

### Task 8: Activación reutilizable (y la confirmación cripto pasa a usarla)

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Services/ISignupActivationService.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Services/SignupActivationService.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/SignupUserActivation.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/ConfirmCryptoPayment/ConfirmCryptoPaymentHandler.cs:87-191`
- Create: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EscenarioEntrax.cs` (ayudante de las pruebas de las Tareas 8 a 15)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/ActivarAltaPendienteTests.cs`

- [ ] **Step 1: Crear el ayudante `EscenarioEntrax.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Un aspirante ruso con la fase 1 y los productos hechos (miembro, suscripción y usuario pendientes)
/// y las piezas para activarlo. Lo comparten las pruebas del alta con Entrax.
/// </summary>
internal static class EscenarioEntrax
{
    /// <summary>Hora de servidor (Kind sin especificar, como DateTime.Now guardado en SQL).</summary>
    public static readonly DateTime Ahora = new(2026, 10, 16, 10, 0, 0);

    public const string  OrderId   = "ORD-RU-1";
    public const string  MemberId  = "AMB-RU-1";
    public const string  Email     = "ivan@example.com";
    public const string  SubId     = "SUB-RU-1";
    public const decimal Total     = 80m;
    public const string  PaymentId = "550e8400-e29b-41d4-a716-446655440000";

    public static string ExternalOrderId(int intento = 1) => EntraxPayment.BuildExternalOrderId(OrderId, intento);

    public static Mock<IDateTimeProvider> Reloj(DateTime? ahora = null)
    {
        var t = ahora ?? Ahora;
        var m = new Mock<IDateTimeProvider>();
        m.Setup(c => c.Now).Returns(t);
        m.Setup(c => c.UtcNow).Returns(DateTime.SpecifyKind(t, DateTimeKind.Utc));
        return m;
    }

    public static EntraxOptions Opciones(Action<EntraxOptions>? ajustar = null)
    {
        var o = new EntraxOptions
        {
            Enabled = true, KeyId = "key-1", Secret = "secreto-de-pruebas",
            CallbackUrl = "https://alta.pruebas/api/v1/signups/entrax/callback"
        };
        ajustar?.Invoke(o);
        return o;
    }

    public static async Task SembrarAltaAsync(
        AppDbContext db, OrderStatus estadoDelPedido = OrderStatus.Pending, decimal total = Total, string pais = "RU")
    {
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = MemberId, Email = Email, FirstName = "Ivan", LastName = "Petrov",
            DateOfBirth = new DateTime(1990, 1, 1), MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Pending, EnrollDate = Ahora, Country = pais,
            CreatedBy = Email, CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.Products.Add(new Product
        {
            Id = "P-RU", Name = "Pack", Description = "Pack", ImageUrl = "https://cdn.example.com/p.png",
            MonthlyFee = total, SetupFee = 0, QualificationPoins = 10, IsActive = true,
            CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.Orders.Add(new Orders
        {
            Id = OrderId, MemberId = MemberId, OrderNo = "TAE1016RU", TotalAmount = total, Status = estadoDelPedido,
            OrderDate = Ahora, CreatedBy = Email, CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.OrderDetails.Add(new OrderDetail
        {
            OrderId = OrderId, ProductId = "P-RU", Quantity = 1, UnitPrice = total, CreatedBy = Email, CreationDate = Ahora
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = SubId, MemberId = MemberId, MembershipLevelId = 1, ChangeReason = SubscriptionChangeReason.New,
            SubscriptionStatus = MembershipStatus.Pending, StartDate = Ahora, IsAutoRenew = true,
            CreatedBy = Email, CreationDate = Ahora, LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();
    }

    public static async Task<EntraxPayment> SembrarPagoAsync(
        AppDbContext db,
        EntraxPaymentStatus estado = EntraxPaymentStatus.AwaitingPayment,
        string? paymentId = PaymentId,
        int intento = 1,
        decimal importe = Total,
        DateTime? caduca = null,
        DateTime? consultado = null,
        DateTime? creado = null)
    {
        var p = new EntraxPayment
        {
            OrderId = OrderId, MemberId = MemberId, ExternalOrderId = ExternalOrderId(intento),
            ExternalPaymentId = paymentId, Status = estado, AmountUsd = importe,
            AmountRub = importe * 95.5m, ExchangeRate = 95.5m,
            ExpiresAt = caduca ?? Ahora.AddMinutes(15), LastCheckedAt = consultado,
            CreatedBy = Email, CreationDate = creado ?? Ahora, LastUpdateDate = creado ?? Ahora
        };
        db.EntraxPayments.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public static ApplicationUser UsuarioInactivo() => new()
    {
        Id = Guid.NewGuid().ToString(), UserName = Email, NormalizedUserName = Email.ToUpperInvariant(),
        Email = Email, NormalizedEmail = Email.ToUpperInvariant(), EmailConfirmed = false,
        MemberProfileId = MemberId, IsActive = false, CreationDate = Ahora, CreatedBy = Email
    };

    public static Mock<UserManager<ApplicationUser>> Usuarios(ApplicationUser usuario)
    {
        var m = UserManagerHelper.Create();
        m.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(usuario);
        m.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        return m;
    }

    /// <summary>La activación de verdad, con los bonos y el cobro recurrente de mentira.</summary>
    public static SignupActivationService Activacion(AppDbContext db)
        => new(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
               Mock.Of<IRecurringBillingEnrollmentService>());

    public static EntraxPaymentState Remoto(
        string status, decimal original = Total, string currency = "USD", string? orderId = null, string paymentId = PaymentId)
        => new(paymentId, status, orderId ?? ExternalOrderId(), original, currency, original * 95.5m, 95.5m,
               status == "paid" ? new DateTimeOffset(Ahora.AddMinutes(5)) : null);

    public static EntraxCall<T> Ok<T>(T valor) where T : class => EntraxCall<T>.Success(valor, 200, 12);
}

/// <summary>Registro en memoria para comprobar que algo se avisó como Critical.</summary>
internal sealed class RegistroDePrueba<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Texto)> Entradas { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entradas.Add((logLevel, formatter(state, exception)));
}
```

- [ ] **Step 2: Escribir la prueba que falla**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// "El dinero del alta está cobrado" en un solo sitio: lo usan la confirmación cripto y Entrax.
/// </summary>
public class ActivarAltaPendienteTests
{
    [Fact]
    public async Task Activa_pedido_miembro_y_suscripcion_y_devuelve_el_miembro()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var order = await db.Orders.SingleAsync();

        var r = await EscenarioEntrax.Activacion(db).ActivatePendingSignupAsync(order, EscenarioEntrax.Ahora, default);
        await db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue();
        r.Value!.MemberId.Should().Be(EscenarioEntrax.MemberId);
        order.Status.Should().Be(OrderStatus.Completed);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);
        var sub = await db.MembershipSubscriptions.SingleAsync();
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        sub.StartDate.Should().Be(EscenarioEntrax.Ahora, "el mes arranca cuando el dinero está cobrado.");
    }

    [Fact]
    public async Task Las_comisiones_se_calculan_sobre_el_pedido()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var sponsor = new Mock<ISponsorBonusService>();
        var activacion = new SignupActivationService(db, sponsor.Object, Mock.Of<IFastStartBonusService>(),
            Mock.Of<IRecurringBillingEnrollmentService>());

        await activacion.ActivatePendingSignupAsync(await db.Orders.SingleAsync(), EscenarioEntrax.Ahora, default);

        sponsor.Verify(s => s.ComputeAsync(null, EscenarioEntrax.MemberId, EscenarioEntrax.OrderId,
            EscenarioEntrax.Total, EscenarioEntrax.Email, EscenarioEntrax.Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_suscripcion_pendiente_no_toca_nada()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus = MembershipStatus.Active;
        await db.SaveChangesAsync();
        var order = await db.Orders.SingleAsync();

        var r = await EscenarioEntrax.Activacion(db).ActivatePendingSignupAsync(order, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be("SUBSCRIPTION_NOT_FOUND");
        order.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Sin_miembro_es_MEMBER_NOT_FOUND()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        db.MemberProfiles.Remove(await db.MemberProfiles.SingleAsync());
        await db.SaveChangesAsync();

        var r = await EscenarioEntrax.Activacion(db)
            .ActivatePendingSignupAsync(await db.Orders.SingleAsync(), EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Activa_al_usuario_inactivo_y_confirma_el_correo()
    {
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var usuarios = EscenarioEntrax.Usuarios(usuario);

        (await SignupUserActivation.ActivateAsync(usuarios.Object, EscenarioEntrax.Email)).Should().BeTrue();

        usuario.IsActive.Should().BeTrue();
        usuario.EmailConfirmed.Should().BeTrue();
        usuarios.Verify(u => u.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task Un_usuario_ya_activo_no_se_toca()
    {
        var usuario = EscenarioEntrax.UsuarioInactivo();
        usuario.IsActive = true;
        var usuarios = EscenarioEntrax.Usuarios(usuario);

        (await SignupUserActivation.ActivateAsync(usuarios.Object, EscenarioEntrax.Email)).Should().BeFalse();
        usuarios.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }
}
```

- [ ] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter ActivarAltaPendienteTests -nologo`
Expected: FAIL de compilación (`ActivatePendingSignupAsync` y `SignupUserActivation` no existen).

- [ ] **Step 4: Añadir el método a `ISignupActivationService.cs`**

Añadir el `using` y el método dentro de la interfaz, debajo de `ActivateAsync`:

```csharp
using MLMConquerorGlobalEdition.SharedKernel;
```

```csharp
    /// <summary>
    /// Lo mismo que <see cref="ActivateAsync"/>, partiendo solo del pedido: carga el miembro, su
    /// suscripción en Pending y los puntos de los productos del pedido. Es lo que hacen la
    /// confirmación cripto y Entrax cuando el dinero llega más tarde. El actor de la auditoría es el
    /// correo del miembro, como en la vía inmediata.
    ///
    /// No guarda (aunque el alta en el cobro recurrente sí lo hace por dentro: quien llama marca su
    /// propia fila ANTES de llamar, para que viaje en ese guardado). Si falla, no ha tocado nada.
    /// </summary>
    Task<Result<MemberProfile>> ActivatePendingSignupAsync(Orders order, DateTime now, CancellationToken ct);
```

- [ ] **Step 5: Implementarlo en `SignupActivationService.cs`**

Añadir los `using`:

```csharp
using MLMConquerorGlobalEdition.SharedKernel;
```

Y el método, debajo de `ActivateAsync`:

```csharp
    public async Task<Result<MemberProfile>> ActivatePendingSignupAsync(Orders order, DateTime now, CancellationToken ct)
    {
        var member = await _db.MemberProfiles.FirstOrDefaultAsync(m => m.MemberId == order.MemberId, ct);
        if (member is null)
            return Result<MemberProfile>.Failure("MEMBER_NOT_FOUND", "Associated member not found.");

        var subscription = await _db.MembershipSubscriptions.FirstOrDefaultAsync(
            s => s.MemberId == member.MemberId && s.SubscriptionStatus == MembershipStatus.Pending, ct);
        if (subscription is null)
            return Result<MemberProfile>.Failure("SUBSCRIPTION_NOT_FOUND", "Pending subscription not found.");

        var totalQualPoints = await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.OrderId == order.Id)
            .Join(_db.Products.AsNoTracking(), od => od.ProductId, p => p.Id, (od, p) => p.QualificationPoins)
            .SumAsync(ct);

        await ActivateAsync(order, member, subscription, totalQualPoints, now, member.Email, ct);
        return Result<MemberProfile>.Success(member);
    }
```

- [ ] **Step 6: Crear `SignupUserActivation.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// La cuenta del alta puede entrar al portal: IsActive y correo confirmado. Hasta aquí LoginHandler
/// la rechazaba, que es lo que se quiere mientras el dinero no esté cobrado. Idempotente: con un
/// usuario ya activo no hace nada (un Re-check de un pago ya liquidado lo usa para reparar).
/// </summary>
public static class SignupUserActivation
{
    public static async Task<bool> ActivateAsync(UserManager<ApplicationUser> users, string email)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || user.IsActive) return false;

        user.IsActive       = true;
        user.EmailConfirmed = true;
        await users.UpdateAsync(user);
        return true;
    }
}
```

- [ ] **Step 7: `ConfirmCryptoPaymentHandler` usa las dos piezas**

Añadir `using MLMConquerorGlobalEdition.Domain.Entities.Member;` y sustituir el método `Handle` completo (líneas 87-191) por:

```csharp
    public async Task<Result<ConfirmCryptoPaymentResponse>> Handle(
        ConfirmCryptoPaymentCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Solo altas. Las compras de tokens en cripto las confirma Billing (Plan F): aquí no hay
        // suscripción que activar y activar una membresía por una compra de tokens sería un regalo.
        var confirmation = await _db.CryptoPaymentConfirmations
            .FirstOrDefaultAsync(c => c.OrderId == command.OrderId && c.Purpose == CryptoPaymentPurpose.Signup, ct);

        if (confirmation is null)
            return Result<ConfirmCryptoPaymentResponse>.Failure(
                "CRYPTO_PAYMENT_NOT_FOUND",
                "No crypto payment awaiting confirmation was found for that order.");

        // Capa 1 — la máquina de estados.
        if (confirmation.Status != CryptoPaymentConfirmationStatus.AwaitingPayment)
            return Result<ConfirmCryptoPaymentResponse>.Failure(AlreadyConfirmedCode, AlreadyConfirmedText);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);
        if (order is null)
            return Result<ConfirmCryptoPaymentResponse>.Failure(
                "SIGNUP_NOT_FOUND", "The signup order for this crypto payment no longer exists.");

        if (order.Status != OrderStatus.Processing)
            return Result<ConfirmCryptoPaymentResponse>.Failure(AlreadyConfirmedCode, AlreadyConfirmedText);

        // El rastro se marca ANTES de activar, para que la fila de confirmación —con su
        // RowVersion— viaje en el mismo lote de UPDATE que el resto. Es lo que decide la carrera.
        confirmation.Status              = CryptoPaymentConfirmationStatus.Confirmed;
        confirmation.CryptoTransactionId = command.Request.CryptoTransactionId.Trim();
        confirmation.Notes               = command.Request.Notes?.Trim();
        confirmation.ConfirmedByUserId   = command.ConfirmedByUserId;
        confirmation.ConfirmedByEmail    = command.ConfirmedByEmail;
        confirmation.ConfirmedAt         = now;
        confirmation.LastUpdateDate      = now;
        confirmation.LastUpdateBy        = command.ConfirmedByEmail;

        MemberProfile member;
        try
        {
            // Exactamente lo mismo que hacen la vía de tarjeta al completar y Entrax al cobrar:
            // pedido cerrado, miembro y suscripción activos, deltas encolados al upline y
            // comisiones calculadas. Un solo sitio (ISignupActivationService).
            //
            // La llamada va DENTRO del try aunque no sea ella quien persiste: por dentro,
            // RecurringBillingEnrollmentService.EnsureStateForSubscriptionAsync llama a
            // SaveChangesAsync por su cuenta, y ese guardado arrastra todo el rastreador de
            // cambios —la fila de confirmación incluida—. O sea que la carrera se resuelve ahí
            // dentro, no en el SaveChanges de abajo. Verificado en caliente con cuatro
            // confirmaciones simultáneas.
            var activation = await _activation.ActivatePendingSignupAsync(order, now, ct);
            if (!activation.IsSuccess)
            {
                _db.ChangeTracker.Clear();
                return Result<ConfirmCryptoPaymentResponse>.Failure(activation.ErrorCode!, activation.Error!);
            }

            member = activation.Value!;
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "ConfirmCryptoPayment: carrera al confirmar el pedido {OrderId}; gana la primera confirmación.",
                command.OrderId);
            return Result<ConfirmCryptoPaymentResponse>.Failure(AlreadyConfirmedCode, AlreadyConfirmedText);
        }

        // Solo ahora la cuenta puede entrar al portal.
        await SignupUserActivation.ActivateAsync(_userManager, member.Email);

        return Result<ConfirmCryptoPaymentResponse>.Success(new ConfirmCryptoPaymentResponse
        {
            OrderId             = order.Id,
            MemberId            = member.MemberId,
            Email               = member.Email,
            MemberStatus        = member.Status.ToString(),
            CryptoTransactionId = confirmation.CryptoTransactionId!,
            ConfirmedByEmail    = confirmation.ConfirmedByEmail!,
            ConfirmedAt         = now
        });
    }
```

Quitar el `using MLMConquerorGlobalEdition.Domain.Entities.Membership;` si queda sin uso (el compilador avisa; no es error).

- [ ] **Step 8: Ejecutar las pruebas nuevas y las de la confirmación cripto**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "ActivarAltaPendienteTests|AltaPorCriptoTests|CriptoDeTokensNoEsUnAltaTests|ConfirmarCobroEnCriptoRolesTests" -nologo`
Expected: PASS. Si alguna de `AltaPorCriptoTests` falla, el refactor cambió el comportamiento de la confirmación: corregir el refactor, no la prueba.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/ISignupActivationService.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/SignupActivationService.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/SignupUserActivation.cs \
        MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/ConfirmCryptoPayment/ConfirmCryptoPaymentHandler.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EscenarioEntrax.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/ActivarAltaPendienteTests.cs
git commit -m "Activar un alta pendiente desde el pedido en un solo sitio; la confirmacion cripto pasa a usarlo"
```

---

### Task 9: Emitir el QR del alta (`EntraxSignupCheckout`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupErrors.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxCallLog.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxQrSanitizer.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupCheckout.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxQrSanitizerTests.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupCheckoutTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

`EntraxQrSanitizerTests.cs`:

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>Lo que Entrax manda para pintar se valida antes de llegar a un img src o a un href.</summary>
public class EntraxQrSanitizerTests
{
    [Fact]
    public void Una_imagen_PNG_en_base64_pasa()
        => EntraxQrSanitizer.SafeImage(FakeEntraxClient.PlaceholderPng).Should().Be(FakeEntraxClient.PlaceholderPng);

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    [InlineData("data:image/png;base64,")]
    [InlineData("data:image/png;base64,no es base64")]
    [InlineData("https://evil.example.com/qr.png")]
    [InlineData(null)]
    public void Cualquier_otra_cosa_no_se_pinta(string? valor)
        => EntraxQrSanitizer.SafeImage(valor).Should().BeNull();

    [Fact]
    public void Un_enlace_https_pasa_tal_cual()
        => EntraxQrSanitizer.SafeLink("https://qr.nspk.ru/AS100?x=1").Should().Be("https://qr.nspk.ru/AS100?x=1");

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://qr.nspk.ru/AS100")]
    [InlineData("/relativo")]
    [InlineData(null)]
    public void Un_enlace_que_no_es_https_se_descarta(string? valor)
        => EntraxQrSanitizer.SafeLink(valor).Should().BeNull();
}
```

`EntraxSignupCheckoutTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Emitir el QR: el importe es el del servidor y en USD, el orderId es nuestro y estable, el pedido
/// queda en Processing y, si Entrax falla, vuelve a Pending para poder reintentar.
/// </summary>
public class EntraxSignupCheckoutTests
{
    private static EntraxQr Qr(
        decimal original = EscenarioEntrax.Total, string currency = "USD",
        string? image = FakeEntraxClient.PlaceholderPng, string? link = "https://qr.nspk.ru/AS100",
        DateTimeOffset? caduca = null)
        => new(EscenarioEntrax.PaymentId.ToUpperInvariant(), image, link, original * 95.5m, 95.5m, currency, original,
               caduca ?? new DateTimeOffset(EscenarioEntrax.Ahora.AddMinutes(15)), EscenarioEntrax.ExternalOrderId());

    private static async Task<(AppDbContext Db, Orders Order, MemberProfile Member)> AltaAsync(decimal total = EscenarioEntrax.Total)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, total: total);
        return (db, await db.Orders.SingleAsync(), await db.MemberProfiles.SingleAsync());
    }

    private static Mock<IEntraxClient> Cliente(EntraxCall<EntraxQr> respuesta, Action<EntraxQrRequest>? capturar = null)
    {
        var m = new Mock<IEntraxClient>();
        m.Setup(c => c.CreateQrAsync(It.IsAny<EntraxQrRequest>(), It.IsAny<CancellationToken>()))
         .Callback<EntraxQrRequest, CancellationToken>((r, _) => capturar?.Invoke(r))
         .ReturnsAsync(respuesta);
        return m;
    }

    private static EntraxSignupCheckout Checkout(AppDbContext db, Mock<IEntraxClient> cliente, ILogger<EntraxSignupCheckout>? log = null)
        => new(db, cliente.Object, EscenarioEntrax.Opciones(), log ?? NullLogger<EntraxSignupCheckout>.Instance);

    [Fact]
    public async Task Pide_el_QR_en_USD_por_el_total_del_servidor_con_un_orderId_propio()
    {
        var (db, order, member) = await AltaAsync();
        EntraxQrRequest? pedido = null;
        var cliente = Cliente(EscenarioEntrax.Ok(Qr()), r => pedido = r);

        var r = await Checkout(db, cliente).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.IsSuccess.Should().BeTrue();
        pedido!.Amount.Should().Be(80m);
        pedido.Currency.Should().Be("USD");
        pedido.OrderId.Should().Be("SIGNUP-ORD-RU-1-1");
        pedido.CallbackUrl.Should().Be("https://alta.pruebas/api/v1/signups/entrax/callback");
        pedido.Description.Should().NotContain(EscenarioEntrax.Email, "ningún dato personal sale hacia Entrax.");

        var pago = await db.EntraxPayments.SingleAsync();
        pago.Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
        pago.ExternalPaymentId.Should().Be(EscenarioEntrax.PaymentId, "se guarda normalizado, en minúsculas.");
        pago.AmountUsd.Should().Be(80m);
        pago.AmountRub.Should().Be(80m * 95.5m);
        pago.QrLink.Should().Be("https://qr.nspk.ru/AS100");
        pago.ExpiresAt.Should().Be(EscenarioEntrax.Ahora.AddMinutes(15));
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);

        r.Value!.PaymentRef.Should().Be(pago.Id);
        r.Value.QrImage.Should().Be(FakeEntraxClient.PlaceholderPng);
        r.Value.AmountRub.Should().Be(80m * 95.5m);
        r.Value.SecondsRemaining.Should().Be(900);

        var log = await db.EntraxApiCalls.SingleAsync();
        log.Operation.Should().Be("CreateQr");
        log.Success.Should().BeTrue();
        log.ExternalOrderId.Should().Be("SIGNUP-ORD-RU-1-1");
    }

    [Fact]
    public async Task El_segundo_intento_lleva_otro_orderId()
    {
        var (db, order, member) = await AltaAsync();
        await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Expired);
        EntraxQrRequest? pedido = null;

        await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr()), r => pedido = r))
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        pedido!.OrderId.Should().Be("SIGNUP-ORD-RU-1-2");
    }

    [Fact]
    public async Task Si_Entrax_falla_el_pedido_vuelve_a_Pending_y_el_intento_queda_fallido()
    {
        var (db, order, member) = await AltaAsync();
        var cliente = Cliente(EntraxCall<EntraxQr>.Fail(400, "RATE_NOT_AVAILABLE", "No rate", 20));

        var r = await Checkout(db, cliente).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        var pago = await db.EntraxPayments.SingleAsync();
        pago.Status.Should().Be(EntraxPaymentStatus.Failed);
        pago.FailureCode.Should().Be("RATE_NOT_AVAILABLE");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        var log = await db.EntraxApiCalls.SingleAsync();
        log.Success.Should().BeFalse();
        log.ErrorCode.Should().Be("RATE_NOT_AVAILABLE");
        log.HttpStatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Un_QR_por_otro_importe_no_se_muestra_y_se_avisa_como_critico()
    {
        var (db, order, member) = await AltaAsync();
        var log = new RegistroDePrueba<EntraxSignupCheckout>();

        var r = await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(original: 79m))), log)
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        (await db.EntraxPayments.SingleAsync()).FailureCode.Should().Be("QR_AMOUNT_MISMATCH");
        log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Un_QR_en_rublos_tampoco_se_muestra()
    {
        var (db, order, member) = await AltaAsync();

        var r = await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(currency: "RUB"))))
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        (await db.EntraxPayments.SingleAsync()).FailureCode.Should().Be("QR_AMOUNT_MISMATCH");
    }

    [Fact]
    public async Task Un_QR_sin_imagen_ni_enlace_usables_no_se_muestra()
    {
        var (db, order, member) = await AltaAsync();

        var r = await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(image: "data:image/svg+xml;base64,PHN2Zz4=", link: "javascript:alert(1)"))))
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        (await db.EntraxPayments.SingleAsync()).FailureCode.Should().Be("QR_UNUSABLE");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Sin_nada_que_pagar_no_llama_a_Entrax()
    {
        var (db, order, member) = await AltaAsync(total: 0m);
        var cliente = Cliente(EscenarioEntrax.Ok(Qr()));

        var r = await Checkout(db, cliente).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.NothingToPay);
        cliente.Verify(c => c.CreateQrAsync(It.IsAny<EntraxQrRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.EntraxPayments.CountAsync()).Should().Be(0);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task La_caducidad_que_llega_en_UTC_se_guarda_en_hora_de_servidor()
    {
        var (db, order, member) = await AltaAsync();
        var utc = new DateTimeOffset(2026, 10, 16, 7, 15, 0, TimeSpan.Zero);

        await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(caduca: utc)))).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        (await db.EntraxPayments.SingleAsync()).ExpiresAt.Should().Be(utc.LocalDateTime);
    }

    [Fact]
    public void Se_ofrece_segun_las_opciones()
    {
        using var db = InMemoryDbHelper.Create();
        var checkout = Checkout(db, new Mock<IEntraxClient>());

        checkout.IsOfferedFor("RU").Should().BeTrue();
        checkout.IsOfferedFor("US").Should().BeFalse();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "EntraxQrSanitizerTests|EntraxSignupCheckoutTests" -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxSignupErrors.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

/// <summary>Códigos de error del alta con Entrax que ve la pantalla.</summary>
public static class EntraxSignupErrors
{
    /// <summary>Apagado o país no configurado. 400.</summary>
    public const string NotAvailable    = "ENTRAX_NOT_AVAILABLE";

    /// <summary>Entrax no emitió un QR usable. 502: el aspirante puede reintentar u otro método.</summary>
    public const string Unavailable     = "ENTRAX_UNAVAILABLE";

    /// <summary>Otra petición está creando el QR de este alta. 409.</summary>
    public const string InProgress      = "ENTRAX_IN_PROGRESS";

    public const string NothingToPay    = "ENTRAX_NOTHING_TO_PAY";

    public const string PaymentNotFound = "ENTRAX_PAYMENT_NOT_FOUND";
}

public static class EntraxGateway
{
    /// <summary>PaymentHistory.GatewayName de los cobros de Entrax.</summary>
    public const string Name     = "Entrax";

    /// <summary>Moneda en la que se pide el QR del alta: la del pedido. Entrax convierte a rublos.</summary>
    public const string Currency = "USD";

    /// <summary>paymentId de Entrax normalizado (GUID "D" en minúsculas) o null si no es un GUID.</summary>
    public static string? NormalizePaymentId(string? id)
        => Guid.TryParse(id, out var g) ? g.ToString("D") : null;

    public static int SecondsUntil(DateTime? expiresAt, DateTime now)
        => expiresAt is null ? 0 : (int)Math.Max(0, Math.Ceiling((expiresAt.Value - now).TotalSeconds));
}
```

- [ ] **Step 4: Crear `EntraxCallLog.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

/// <summary>Filas de EntraxApiCalls. Solo campos estructurados: nunca cuerpos ni datos del formulario.</summary>
public static class EntraxCallLog
{
    public const string CreateQr   = "CreateQr";
    public const string GetPayment = "GetPayment";
    public const string Callback   = "Callback";

    public static EntraxApiCall From(
        string operation, int httpStatus, string? errorCode, long elapsedMs,
        string? externalOrderId, string? externalPaymentId, string? providerStatus, DateTime now, string actor)
        => new()
        {
            Operation         = operation,
            HttpStatusCode    = httpStatus,
            Success           = errorCode is null,
            ErrorCode         = Cut(errorCode, 60),
            ExternalOrderId   = Cut(externalOrderId, 100),
            ExternalPaymentId = Cut(externalPaymentId, 64),
            ProviderStatus    = Cut(providerStatus, 30),
            DurationMs        = (int)Math.Clamp(elapsedMs, 0, int.MaxValue),
            CreationDate      = now,
            CreatedBy         = Cut(actor, 100) ?? EntraxGateway.Name
        };

    private static string? Cut(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}
```

- [ ] **Step 5: Crear `EntraxQrSanitizer.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

/// <summary>
/// Lo que Entrax devuelve para pintar (imagen y enlace) acaba en un img src y en un href de la
/// pantalla del alta. Solo pasa un PNG en base64 y un enlace https; lo demás se descarta.
/// </summary>
public static class EntraxQrSanitizer
{
    private const string PngPrefix = "data:image/png;base64,";
    public const int MaxImageLength = 200_000;

    public static string? SafeImage(string? dataUri)
    {
        if (string.IsNullOrEmpty(dataUri)
            || dataUri.Length > MaxImageLength
            || dataUri.Length == PngPrefix.Length
            || !dataUri.StartsWith(PngPrefix, StringComparison.Ordinal))
            return null;

        var payload = dataUri.AsSpan(PngPrefix.Length);
        var buffer = new byte[payload.Length];
        return Convert.TryFromBase64Chars(payload, buffer, out _) ? dataUri : null;
    }

    public static string? SafeLink(string? link)
        => Uri.TryCreate(link, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? link : null;
}
```

- [ ] **Step 6: Crear `EntraxSignupCheckout.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public interface IEntraxSignupCheckout
{
    bool IsOfferedFor(string? countryIso2);

    /// <summary>
    /// Reserva el pedido (Processing), guarda el intento y pide el QR a Entrax. GUARDA el contexto:
    /// también lo que el handler dejó pendiente (la fila de estadísticas del alta).
    /// </summary>
    Task<Result<EntraxSignupPaymentDto>> StartAsync(Orders order, MemberProfile member, DateTime now, CancellationToken ct);
}

/// <summary>
/// La emisión del QR del alta. Lo que el legado hacía mal y aquí no:
///  - el importe sale de Orders.TotalAmount (servidor), nunca del cliente, y va en USD: Entrax
///    convierte; nosotros no (el legado usaba el cambio de Spreedly y, si fallaba, mandaba USD
///    como rublos);
///  - el orderId es nuestro, estable y único por intento (SIGNUP-{pedido}-{n}), y se guarda ANTES
///    de llamar, así un callback siempre encuentra a qué alta pertenece;
///  - nada del formulario sale hacia Entrax ni al log.
/// </summary>
public sealed class EntraxSignupCheckout : IEntraxSignupCheckout
{
    private const string UnavailableText =
        "We couldn't create the SBP payment right now. Please try again or choose another payment method.";

    private readonly AppDbContext                    _db;
    private readonly IEntraxClient                   _client;
    private readonly EntraxOptions                   _options;
    private readonly ILogger<EntraxSignupCheckout>   _logger;

    public EntraxSignupCheckout(AppDbContext db, IEntraxClient client, EntraxOptions options, ILogger<EntraxSignupCheckout> logger)
    {
        _db      = db;
        _client  = client;
        _options = options;
        _logger  = logger;
    }

    public bool IsOfferedFor(string? countryIso2) => _options.IsOfferedFor(countryIso2);

    public async Task<Result<EntraxSignupPaymentDto>> StartAsync(Orders order, MemberProfile member, DateTime now, CancellationToken ct)
    {
        var amountUsd = Math.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero);
        if (amountUsd < 0.01m)
            return Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.NothingToPay, "This signup has nothing to pay.");

        var attempt = await _db.EntraxPayments.IgnoreQueryFilters().CountAsync(p => p.OrderId == order.Id, ct) + 1;
        var payment = new EntraxPayment
        {
            OrderId         = order.Id,
            MemberId        = member.MemberId,
            ExternalOrderId = EntraxPayment.BuildExternalOrderId(order.Id, attempt),
            Status          = EntraxPaymentStatus.AwaitingPayment,
            AmountUsd       = amountUsd,
            CreatedBy       = member.Email,
            CreationDate    = now,
            LastUpdateDate  = now,
            LastUpdateBy    = member.Email
        };
        _db.EntraxPayments.Add(payment);

        // Igual que cripto: el pedido EN PROCESO impide que CompleteSignup se repita (solo encuentra
        // Pending) mientras el QR está vivo. Miembro y suscripción siguen en Pending.
        order.Status         = OrderStatus.Processing;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = member.Email;
        member.LastUpdateDate = now;
        member.LastUpdateBy   = member.Email;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Índice único de ExternalOrderId o RowVersion del miembro: otra petición del mismo alta
            // llegó primero. No se llamó a Entrax.
            _db.ChangeTracker.Clear();
            _logger.LogWarning("Entrax: el alta {OrderId} ya tenía un QR en creación (intento {Attempt}).", order.Id, attempt);
            return Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.InProgress,
                "A payment for this signup is already being created. Please wait a moment and try again.");
        }

        // A partir de aquí puede haber un QR vivo en Entrax: nada se cancela con la petición.
        var call = await _client.CreateQrAsync(
            new EntraxQrRequest(amountUsd, EntraxGateway.Currency, payment.ExternalOrderId,
                $"MLM Conqueror signup {order.OrderNo ?? order.Id}", _options.CallbackUrl),
            CancellationToken.None);

        var qr        = call.Value;
        var paymentId = EntraxGateway.NormalizePaymentId(qr?.PaymentId);
        var image     = EntraxQrSanitizer.SafeImage(qr?.ImageDataUri);
        var link      = EntraxQrSanitizer.SafeLink(qr?.Link);

        _db.EntraxApiCalls.Add(EntraxCallLog.From(EntraxCallLog.CreateQr, call.HttpStatus, call.ErrorCode, call.ElapsedMs,
            payment.ExternalOrderId, paymentId, qr is null ? null : "pending", now, member.Email));

        var problem =
              !call.Ok || paymentId is null ? call.ErrorCode ?? EntraxErrorCodes.BadResponse
            : !string.Equals(qr!.Currency, EntraxGateway.Currency, StringComparison.OrdinalIgnoreCase)
              || qr.OriginalAmount != amountUsd ? "QR_AMOUNT_MISMATCH"
            : image is null && link is null ? "QR_UNUSABLE"
            : null;

        if (problem is not null)
        {
            payment.Status            = EntraxPaymentStatus.Failed;
            payment.FailureCode       = problem.Length > 60 ? problem[..60] : problem;
            payment.ExternalPaymentId = paymentId;
            payment.LastUpdateDate    = now;
            payment.LastUpdateBy      = member.Email;
            order.Status              = OrderStatus.Pending;
            order.LastUpdateDate      = now;
            order.LastUpdateBy        = member.Email;
            await _db.SaveChangesAsync(CancellationToken.None);

            if (problem == "QR_AMOUNT_MISMATCH")
                _logger.LogCritical(
                    "Entrax emitió el QR {ExternalOrderId} por {Amount} {Currency} y se pidieron {Expected} USD. No se muestra.",
                    payment.ExternalOrderId, qr!.OriginalAmount, qr.Currency, amountUsd);
            else
                _logger.LogWarning("Entrax: no se pudo crear el QR {ExternalOrderId} ({Code}).", payment.ExternalOrderId, problem);

            return Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.Unavailable, UnavailableText);
        }

        payment.ExternalPaymentId = paymentId;
        payment.AmountRub         = qr!.AmountInRubles;
        payment.ExchangeRate      = qr.ExchangeRate;
        payment.QrLink            = link;
        payment.ExpiresAt         = qr.ExpiresAt?.LocalDateTime ?? now.AddMinutes(15);
        payment.LastUpdateDate    = now;
        payment.LastUpdateBy      = member.Email;
        await _db.SaveChangesAsync(CancellationToken.None);

        return Result<EntraxSignupPaymentDto>.Success(new EntraxSignupPaymentDto
        {
            PaymentRef       = payment.Id,
            QrImage          = image,
            QrLink           = link,
            AmountUsd        = amountUsd,
            AmountRub        = qr.AmountInRubles,
            ExchangeRate     = qr.ExchangeRate,
            ExpiresAt        = payment.ExpiresAt,
            SecondsRemaining = EntraxGateway.SecondsUntil(payment.ExpiresAt, now)
        });
    }
}
```

(`ILogger<T>` llega por los `using` implícitos del SDK web de SignupAPI.)

- [ ] **Step 7: Ejecutar las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "EntraxQrSanitizerTests|EntraxSignupCheckoutTests" -nologo`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupErrors.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxCallLog.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxQrSanitizer.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupCheckout.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxQrSanitizerTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupCheckoutTests.cs
git commit -m "QR de Entrax del alta: importe del servidor en USD, orderId propio por intento, pedido en Processing y vuelta a Pending si Entrax falla"
```

---

### Task 10: `CompleteSignup` con `PaymentMethodType.Entrax`

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/DTOs/PaymentMethodType.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/DTOs/SignupResponse.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/CompleteSignup/CompleteSignupHandler.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Controllers/SignupsController.cs:103-117`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/AltaConEntraxTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Completar el alta con rublos por SBP: solo donde está encendido y para el país del aspirante; deja
/// todo pendiente como la cripto (sin comisiones, sin sesión) y devuelve el QR.
/// </summary>
public class AltaConEntraxTests
{
    private static readonly EntraxSignupPaymentDto Qr = new()
    {
        PaymentRef = "ref-1", QrLink = "https://qr.nspk.ru/AS100", AmountUsd = 80m, AmountRub = 7640m, SecondsRemaining = 900
    };

    private static CompleteSignupRequest Pide() => new() { PaymentMethod = PaymentMethodType.Entrax };

    /// <summary>Un checkout que guarda como el de verdad (StartAsync guarda el contexto) y devuelve el QR.</summary>
    private static Mock<IEntraxSignupCheckout> Checkout(AppDbContext db, Result<EntraxSignupPaymentDto>? resultado = null)
    {
        var m = new Mock<IEntraxSignupCheckout>();
        m.Setup(c => c.IsOfferedFor(It.IsAny<string?>())).Returns<string?>(p => p == "RU");
        m.Setup(c => c.StartAsync(It.IsAny<Orders>(), It.IsAny<MemberProfile>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
         .Returns(async (Orders _, MemberProfile _, DateTime _, CancellationToken _) =>
         {
             await db.SaveChangesAsync();
             return resultado ?? Result<EntraxSignupPaymentDto>.Success(Qr);
         });
        return m;
    }

    private static CompleteSignupHandler Handler(
        AppDbContext db, Mock<UserManager<ApplicationUser>> usuarios, IEntraxSignupCheckout? checkout,
        ISponsorBonusService? sponsor = null)
        => new(db, EscenarioEntrax.Reloj().Object, Mock.Of<IS3FileService>(), usuarios.Object, Mock.Of<IJwtService>(),
               Mock.Of<IEncryptionService>(), Mock.Of<ITokenRedemptionService>(),
               new SignupActivationService(db, sponsor ?? Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
                   Mock.Of<IRecurringBillingEnrollmentService>()),
               Mock.Of<ICryptoDepositAddressReader>(), new PuertaDeFondosDePrueba(db),
               entraxCheckout: checkout);

    [Fact]
    public async Task Fuera_de_los_paises_configurados_es_ENTRAX_NOT_AVAILABLE()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, pais: "US");
        var checkout = Checkout(db);

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), checkout.Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.NotAvailable);
        checkout.Verify(c => c.StartAsync(It.IsAny<Orders>(), It.IsAny<MemberProfile>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Sin_Entrax_registrado_tampoco_esta_disponible()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), checkout: null)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.NotAvailable);
    }

    [Fact]
    public async Task En_Rusia_devuelve_el_QR_y_deja_todo_pendiente_sin_sesion_ni_comisiones()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var usuarios = EscenarioEntrax.Usuarios(usuario);
        var sponsor = new Mock<ISponsorBonusService>();
        var checkout = Checkout(db);

        var r = await Handler(db, usuarios, checkout.Object, sponsor.Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Entrax.Should().BeSameAs(Qr);
        r.Value.AccessToken.Should().BeNull("al pagar, la persona inicia sesión; nadie recibe una sesión por una URL anónima.");
        usuario.IsActive.Should().BeFalse();
        usuarios.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        sponsor.Verify(s => s.ComputeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        checkout.Verify(c => c.StartAsync(
            It.Is<Orders>(o => o.Id == EscenarioEntrax.OrderId), It.Is<MemberProfile>(m => m.MemberId == EscenarioEntrax.MemberId),
            EscenarioEntrax.Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_segundo_intento_no_duplica_las_estadisticas_del_miembro()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        db.MemberStatistics.Add(new MemberStatisticEntity
        {
            MemberId = EscenarioEntrax.MemberId, PersonalPoints = 10, EnrollmentPoints = 10,
            CreatedBy = "primer-intento", CreationDate = EscenarioEntrax.Ahora
        });
        await db.SaveChangesAsync();

        await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), Checkout(db).Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        (await db.MemberStatistics.CountAsync(s => s.MemberId == EscenarioEntrax.MemberId)).Should().Be(1);
    }

    [Fact]
    public async Task El_primer_intento_si_crea_las_estadisticas()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);

        await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), Checkout(db).Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        (await db.MemberStatistics.CountAsync(s => s.MemberId == EscenarioEntrax.MemberId)).Should().Be(1);
    }

    [Fact]
    public async Task El_error_del_QR_llega_tal_cual()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var checkout = Checkout(db, Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.Unavailable, "x"));

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), checkout.Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
    }

    [Theory]
    [InlineData(EntraxSignupErrors.Unavailable, StatusCodes.Status502BadGateway)]
    [InlineData(EntraxSignupErrors.InProgress, StatusCodes.Status409Conflict)]
    [InlineData(EntraxSignupErrors.NotAvailable, StatusCodes.Status400BadRequest)]
    public async Task El_controlador_traduce_los_errores_de_Entrax(string codigo, int http)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(codigo, "x"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        var r = await controller.CompleteSignup(EscenarioEntrax.OrderId, Pide(), default);

        r.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(http);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter AltaConEntraxTests -nologo`
Expected: FAIL de compilación (`PaymentMethodType.Entrax`, `SignupResponse.Entrax`, parámetro `entraxCheckout`).

- [ ] **Step 3: `PaymentMethodType.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public enum PaymentMethodType
{
    CreditCard = 1,
    Crypto = 2,
    Token = 3,
    DiscountCode = 4,

    /// <summary>Rublos por SBP con Entrax (QR). Solo si Entrax:Enabled y el país está en Entrax:Countries.</summary>
    Entrax = 5
}
```

- [ ] **Step 4: `SignupResponse.cs`** — añadir `using MLMConquerorGlobalEdition.SharedKernel.Billing;` arriba y, al final de la clase:

```csharp
    /// <summary>
    /// Solo con PaymentMethod = Entrax: el QR que hay que pagar. Sin tokens: el alta se activa en el
    /// servidor cuando Entrax confirma el cobro, y la persona inicia sesión después.
    /// </summary>
    public EntraxSignupPaymentDto? Entrax { get; set; }
```

- [ ] **Step 5: `CompleteSignupHandler.cs`**

5a. Añadir `using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;` a los `using`.

5b. Añadir al final del comentario `<summary>` de la clase, antes de `/// </summary>`:

```csharp
///
/// CON ENTRAX (rublos por SBP) pasa lo mismo que con cripto —pedido en Processing, miembro y
/// suscripción en Pending, usuario inactivo, ni una comisión— pero el cobro no lo confirma nadie de
/// la casa: lo liquida EntraxSignupSettlement cuando Entrax dice "paid". Aquí solo se emite el QR.
```

5c. Campo y constructor: añadir el campo

```csharp
    private readonly IEntraxSignupCheckout?       _entraxCheckout;
```

y sustituir la firma y el cuerpo del constructor por:

```csharp
    public CompleteSignupHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IS3FileService s3,
        UserManager<ApplicationUser> userManager,
        IJwtService jwtService,
        IEncryptionService encryption,
        ITokenRedemptionService tokenRedemption,
        ISignupActivationService activation,
        ICryptoDepositAddressReader cryptoAddresses,
        IMemberFundsGate gate,
        // Opcional a propósito: sin él (pruebas antiguas, o Entrax sin registrar) el método no está
        // disponible y el alta lo rechaza con ENTRAX_NOT_AVAILABLE. El contenedor lo inyecta.
        IEntraxSignupCheckout? entraxCheckout = null)
    {
        _db              = db;
        _dateTime        = dateTime;
        _s3              = s3;
        _userManager     = userManager;
        _jwtService      = jwtService;
        _encryption      = encryption;
        _tokenRedemption = tokenRedemption;
        _activation      = activation;
        _cryptoAddresses = cryptoAddresses;
        _gate            = gate;
        _entraxCheckout  = entraxCheckout;
    }
```

5d. Disponibilidad. Sustituir:

```csharp
        var appUser = await _userManager.FindByEmailAsync(member.Email);
        if (appUser is null || appUser.IsActive)
            return Result<SignupResponse>.Failure(
                "USER_NOT_FOUND", "Pending user account not found for this signup.");
```

por:

```csharp
        var appUser = await _userManager.FindByEmailAsync(member.Email);
        if (appUser is null || appUser.IsActive)
            return Result<SignupResponse>.Failure(
                "USER_NOT_FOUND", "Pending user account not found for this signup.");

        // Rublos por SBP solo donde está encendido y para el país del aspirante (MemberProfile.Country,
        // ISO2). Se comprueba aquí aunque la pantalla ya esconda la pestaña.
        if (req.PaymentMethod == PaymentMethodType.Entrax
            && (_entraxCheckout is null || !_entraxCheckout.IsOfferedFor(member.Country)))
            return Result<SignupResponse>.Failure(EntraxSignupErrors.NotAvailable,
                "Payment in rubles (SBP) is not available for your country.");
```

5e. Estadísticas sin duplicar. Sustituir:

```csharp
            await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
            {
                MemberId         = member.MemberId,
                PersonalPoints   = totalQualPoints,
                EnrollmentPoints = totalQualPoints,
                CreatedBy        = member.Email,
                CreationDate     = now
            }, ct);
```

por:

```csharp
            // Un alta que volvió a Pending (QR de Entrax fallido o caducado) se completa otra vez: la
            // fila ya existe del primer intento y no se duplica.
            if (!await _db.MemberStatistics.AnyAsync(s => s.MemberId == member.MemberId, ct))
            {
                await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
                {
                    MemberId         = member.MemberId,
                    PersonalPoints   = totalQualPoints,
                    EnrollmentPoints = totalQualPoints,
                    CreatedBy        = member.Email,
                    CreationDate     = now
                }, ct);
            }
```

5f. La rama Entrax, justo **antes** del bloque de cripto. Sustituir:

```csharp
            if (req.PaymentMethod == PaymentMethodType.Crypto)
            {
                // El pedido queda EN PROCESO, no completado: el dinero está anunciado pero no
```

por:

```csharp
            if (req.PaymentMethod == PaymentMethodType.Entrax)
            {
                // StartAsync reserva el pedido (Processing), guarda —también la fila de estadísticas de
                // arriba— y pide el QR. Si Entrax falla, deja el pedido en Pending para reintentar.
                var started = await _entraxCheckout!.StartAsync(order, member, now, ct);
                if (!started.IsSuccess)
                    return Result<SignupResponse>.Failure(started.ErrorCode!, started.Error!);

                return Result<SignupResponse>.Success(new SignupResponse
                {
                    SignupId   = order.Id,
                    MemberId   = member.MemberId,
                    Email      = member.Email,
                    MemberType = member.MemberType.ToString(),
                    EnrollDate = member.EnrollDate,
                    Entrax     = started.Value
                });
            }

            if (req.PaymentMethod == PaymentMethodType.Crypto)
            {
                // El pedido queda EN PROCESO, no completado: el dinero está anunciado pero no
```

- [ ] **Step 6: `SignupsController.CompleteSignup`** — añadir `using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;` y sustituir el bloque `if (!result.IsSuccess) { … }` del método por:

```csharp
        if (!result.IsSuccess)
        {
            // Candado del token ocupado u otro QR en creación: no se hizo nada y se puede repetir.
            // Entrax sin QR usable: 502, el aspirante reintenta o elige otro método.
            var body = ApiResponse<SignupResponse>.Fail(result.ErrorCode!, result.Error!);
            return result.ErrorCode switch
            {
                CompleteSignupHandler.TokenBusyCode or EntraxSignupErrors.InProgress => Conflict(body),
                EntraxSignupErrors.Unavailable => StatusCode(StatusCodes.Status502BadGateway, body),
                _ => BadRequest(body)
            };
        }
```

- [ ] **Step 7: Ejecutar la prueba nueva y las del alta**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "AltaConEntraxTests|CompleteSignupHandlerTests|AltaPorCriptoTests|CanjeDeTokenEnElAltaTests|SignupRankInputsTests|CompleteSignupRequestValidatorTests" -nologo`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/DTOs/PaymentMethodType.cs \
        MLMConquerorGlobalEdition.SignupAPI/DTOs/SignupResponse.cs \
        MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/CompleteSignup/CompleteSignupHandler.cs \
        MLMConquerorGlobalEdition.SignupAPI/Controllers/SignupsController.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/AltaConEntraxTests.cs
git commit -m "Alta con rublos por SBP: PaymentMethodType.Entrax solo en los paises configurados, devuelve el QR y deja el alta pendiente sin sesion; las estadisticas no se duplican al reintentar"
```

---

### Task 11: Liquidar un pago (`EntraxSignupSettlement`), el único camino que activa

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupSettlement.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupSettlementTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El único camino que activa un alta pagada con Entrax. Siempre re-consulta GET /payment (no se fía
/// de quien avisa), exige paid + USD + importe suficiente, y un pago activa un alta una sola vez.
/// </summary>
public class EntraxSignupSettlementTests
{
    private sealed record Montaje(
        AppDbContext Db, Mock<IEntraxClient> Cliente, ApplicationUser Usuario,
        RegistroDePrueba<EntraxSignupSettlement> Log, EntraxSignupSettlement Liquidacion, string PagoId);

    private static async Task<Montaje> MontarAsync(
        OrderStatus pedido = OrderStatus.Processing,
        EntraxPaymentStatus pago = EntraxPaymentStatus.AwaitingPayment,
        EntraxPaymentState? remoto = null,
        Func<AppDbContext, ISignupActivationService>? activacion = null,
        string? paymentId = EscenarioEntrax.PaymentId,
        DateTime? creado = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, pedido);
        var fila = await EscenarioEntrax.SembrarPagoAsync(db, pago, paymentId: paymentId, creado: creado);

        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(remoto ?? EscenarioEntrax.Remoto("paid")));

        var usuario = EscenarioEntrax.UsuarioInactivo();
        var log = new RegistroDePrueba<EntraxSignupSettlement>();
        var liquidacion = new EntraxSignupSettlement(db, cliente.Object,
            (activacion ?? EscenarioEntrax.Activacion)(db), EscenarioEntrax.Usuarios(usuario).Object,
            EscenarioEntrax.Reloj().Object, log);

        return new Montaje(db, cliente, usuario, log, liquidacion, fila.Id);
    }

    private static Task<EntraxPayment> Fila(Montaje m) => m.Db.EntraxPayments.SingleAsync(p => p.Id == m.PagoId);

    // ── Pagado y cuadrado ────────────────────────────────────────────────────────

    [Fact]
    public async Task Pagado_y_cuadrado_activa_el_alta_registra_el_pago_y_da_acceso()
    {
        var m = await MontarAsync();

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.Activated);
        var pago = await Fila(m);
        pago.Status.Should().Be(EntraxPaymentStatus.Paid);
        pago.Consumed.Should().BeTrue();
        pago.ActivatedAt.Should().Be(EscenarioEntrax.Ahora);
        pago.PaidAmountUsd.Should().Be(80m);
        pago.PaidAt.Should().Be(EscenarioEntrax.Ahora.AddMinutes(5));

        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);

        var cobro = await m.Db.PaymentHistories.SingleAsync();
        cobro.GatewayName.Should().Be("Entrax");
        cobro.GatewayTransactionId.Should().Be(EscenarioEntrax.PaymentId);
        cobro.Amount.Should().Be(80m);
        cobro.OrderId.Should().Be(EscenarioEntrax.OrderId);
        cobro.TransactionStatus.Should().Be(PaymentHistoryTransactionStatus.Captured);

        m.Usuario.IsActive.Should().BeTrue("tras cobrar, la persona inicia sesión con su contraseña.");
        (await m.Db.EntraxApiCalls.SingleAsync()).Operation.Should().Be("GetPayment");
    }

    [Fact]
    public async Task Un_segundo_aviso_del_mismo_pago_no_vuelve_a_activar_ni_a_cobrar()
    {
        var m = await MontarAsync();

        await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);
        var segunda = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default);

        segunda.Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(1);
        m.Cliente.Verify(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
            "un pago ya liquidado no se vuelve a consultar.");
    }

    [Fact]
    public async Task Un_pago_ya_liquidado_repara_el_usuario_si_quedo_inactivo()
    {
        var m = await MontarAsync(pago: EntraxPaymentStatus.Paid);

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Admin, default);

        r.Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        m.Usuario.IsActive.Should().BeTrue();
        m.Cliente.Verify(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_carrera_al_activar_se_resuelve_releyendo()
    {
        var veces = 0;
        var m = await MontarAsync(activacion: db =>
        {
            var real = EscenarioEntrax.Activacion(db);
            var act = new Mock<ISignupActivationService>();
            act.Setup(a => a.ActivatePendingSignupAsync(It.IsAny<Orders>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .Returns((Orders o, DateTime n, CancellationToken c) =>
                   ++veces == 1 ? throw new DbUpdateConcurrencyException("carrera") : real.ActivatePendingSignupAsync(o, n, c));
            return act.Object;
        });

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default);

        r.Should().Be(EntraxSettlementOutcome.Activated);
        veces.Should().Be(2);
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(1, "lo del intento perdido se descartó.");
    }

    // ── Pagado pero no se aplica: a revisar ──────────────────────────────────────

    [Fact]
    public async Task Pagado_de_menos_queda_a_revisar_sin_activar_nada()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("paid", original: 79m));

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        var pago = await Fila(m);
        pago.Status.Should().Be(EntraxPaymentStatus.ToReview);
        pago.FailureCode.Should().Be("AMOUNT_TOO_LOW");
        pago.PaidAmountUsd.Should().Be(79m);
        pago.Consumed.Should().BeFalse();
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(0);
        m.Usuario.IsActive.Should().BeFalse();
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Se_compara_tambien_con_el_total_actual_del_pedido()
    {
        var m = await MontarAsync();
        (await m.Db.Orders.SingleAsync()).TotalAmount = 100m;
        await m.Db.SaveChangesAsync();

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("AMOUNT_TOO_LOW");
    }

    [Fact]
    public async Task Pagado_en_otra_moneda_queda_a_revisar()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("paid", currency: "RUB"));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("CURRENCY_MISMATCH");
    }

    [Fact]
    public async Task Pagado_cuando_el_alta_ya_se_activo_por_otro_medio_es_un_doble_cobro()
    {
        var m = await MontarAsync(pedido: OrderStatus.Completed);

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("ORDER_ALREADY_SETTLED");
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(0);
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Un_pago_que_Entrax_atribuye_a_otro_pedido_no_se_aplica()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("paid", orderId: "SIGNUP-OTRO-1"));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("PROVIDER_MISMATCH");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    // ── Fallido, caducado, tardío ────────────────────────────────────────────────

    [Theory]
    [InlineData("failed", EntraxPaymentStatus.Failed, EntraxSettlementOutcome.Failed)]
    [InlineData("expired", EntraxPaymentStatus.Expired, EntraxSettlementOutcome.Expired)]
    public async Task Fallido_o_caducado_devuelve_el_pedido_a_Pending_para_reintentar(
        string remoto, EntraxPaymentStatus esperado, EntraxSettlementOutcome resultado)
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto(remoto));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default)).Should().Be(resultado);

        (await Fila(m)).Status.Should().Be(esperado);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
    }

    [Fact]
    public async Task Si_hay_otro_QR_vivo_del_mismo_pedido_no_vuelve_a_Pending()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("expired"));
        await EscenarioEntrax.SembrarPagoAsync(m.Db, paymentId: Guid.NewGuid().ToString(), intento: 2);

        await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default);

        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Pagado_tarde_tras_caducar_activa_si_el_pedido_sigue_sin_activar()
    {
        var m = await MontarAsync(pedido: OrderStatus.Pending, pago: EntraxPaymentStatus.Expired);

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.Activated);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Pagado_tarde_cancela_la_cripto_que_esperaba_el_mismo_pedido()
    {
        var m = await MontarAsync(pago: EntraxPaymentStatus.Expired);
        m.Db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = EscenarioEntrax.OrderId, MemberId = EscenarioEntrax.MemberId, MemberEmail = EscenarioEntrax.Email,
            CryptoCurrency = "BTC", AmountDue = 80m, Purpose = CryptoPaymentPurpose.Signup,
            Status = CryptoPaymentConfirmationStatus.AwaitingPayment, CreatedBy = "seed", CreationDate = EscenarioEntrax.Ahora
        });
        await m.Db.SaveChangesAsync();

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.Activated);

        var cripto = await m.Db.CryptoPaymentConfirmations.SingleAsync();
        cripto.Status.Should().Be(CryptoPaymentConfirmationStatus.Cancelled,
            "si no, alguien podría confirmar después una transferencia que no existe.");
        cripto.Notes.Should().Contain(EscenarioEntrax.PaymentId);
    }

    // ── Sin respuesta definitiva ─────────────────────────────────────────────────

    [Fact]
    public async Task Pendiente_en_Entrax_solo_anota_la_consulta()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("pending"));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default))
            .Should().Be(EntraxSettlementOutcome.StillPending);

        var pago = await Fila(m);
        pago.Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
        pago.LastCheckedAt.Should().Be(EscenarioEntrax.Ahora);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Entrax_caido_no_cambia_nada()
    {
        var m = await MontarAsync();
        m.Cliente.Setup(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(EntraxCall<EntraxPaymentState>.Fail(0, EntraxErrorCodes.Transport, "timeout", 30000));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ProviderUnavailable);

        (await Fila(m)).Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
        (await m.Db.EntraxApiCalls.SingleAsync()).ErrorCode.Should().Be(EntraxErrorCodes.Transport);
    }

    [Fact]
    public async Task Sin_paymentId_se_espera_un_rato_sin_llamar_a_Entrax()
    {
        var m = await MontarAsync(paymentId: null);

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.StillPending);
        m.Cliente.Verify(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_paymentId_a_los_30_minutos_se_da_por_fallido_y_el_pedido_vuelve_a_Pending()
    {
        var m = await MontarAsync(paymentId: null, creado: EscenarioEntrax.Ahora.AddMinutes(-31));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.Failed);
        (await Fila(m)).FailureCode.Should().Be("NO_PAYMENT_ID");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Un_pago_que_no_existe_es_NotFound()
    {
        var m = await MontarAsync();
        (await m.Liquidacion.SettleAsync("no-existe", EntraxSettlementSource.Admin, default))
            .Should().Be(EntraxSettlementOutcome.NotFound);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupSettlementTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxSignupSettlement.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public enum EntraxSettlementSource { Webhook, Poll, Sweep, Admin }

public enum EntraxSettlementOutcome
{
    NotFound,
    Activated,
    AlreadyResolved,
    StillPending,
    Failed,
    Expired,
    ToReview,
    ProviderUnavailable
}

public interface IEntraxSignupSettlement
{
    /// <summary>Lleva el pago a su estado real según Entrax y, si está pagado y cuadra, activa el alta.</summary>
    Task<EntraxSettlementOutcome> SettleAsync(string entraxPaymentId, EntraxSettlementSource source, CancellationToken ct);
}

/// <summary>
/// EL ÚNICO CAMINO QUE ACTIVA UN ALTA PAGADA CON ENTRAX. Llegan aquí el webhook, la pantalla que
/// consulta el estado, el barrido de Hangfire y el Re-check del admin; ninguno activa por su cuenta.
///
/// Lo que el legado hacía mal y aquí no:
///  - no se fía de quien avisa: SIEMPRE re-consulta GET /payment/{id};
///  - exige paid, USD e importe ≥ lo pedido (y ≥ el total actual del pedido); si no cuadra, ToReview
///    con un Critical en el log y sin activar nada;
///  - un pago activa UNA alta: la fila se marca Paid/Consumed antes de activar, viaja en el mismo
///    guardado con su RowVersion y, si otro camino ganó la carrera, se relee (máx. 3 veces);
///  - el webhook no depende del navegador: el alta se activa aunque nadie tenga la página abierta.
///
/// Activar es exactamente lo de la confirmación cripto (ISignupActivationService.ActivatePendingSignupAsync)
/// más el pago registrado en PaymentHistory con gateway "Entrax" y el paymentId externo.
///
/// Una vez empezado no se cancela con la petición: lo que mueve dinero usa CancellationToken.None.
/// </summary>
public sealed class EntraxSignupSettlement : IEntraxSignupSettlement
{
    /// <summary>Lo que se espera a que una fila sin paymentId reciba uno (por el callback) antes de darla por fallida.</summary>
    public static readonly TimeSpan UnissuedGrace = TimeSpan.FromMinutes(30);

    private const int    MaxAttempts = 3;
    private const string Actor       = EntraxGateway.Name;

    private readonly AppDbContext                     _db;
    private readonly IEntraxClient                    _client;
    private readonly ISignupActivationService         _activation;
    private readonly UserManager<ApplicationUser>     _users;
    private readonly IDateTimeProvider                _clock;
    private readonly ILogger<EntraxSignupSettlement>  _logger;

    public EntraxSignupSettlement(
        AppDbContext db,
        IEntraxClient client,
        ISignupActivationService activation,
        UserManager<ApplicationUser> users,
        IDateTimeProvider clock,
        ILogger<EntraxSignupSettlement> logger)
    {
        _db         = db;
        _client     = client;
        _activation = activation;
        _users      = users;
        _clock      = clock;
        _logger     = logger;
    }

    public async Task<EntraxSettlementOutcome> SettleAsync(
        string entraxPaymentId, EntraxSettlementSource source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SettleOnceAsync(entraxPaymentId, source);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Otro camino (webhook, pantalla, barrido, admin, o la cripto del mismo pedido) guardó
                // la misma fila o el mismo miembro entre la lectura y el guardado. Se relee y se decide
                // con lo que haya ahora: ya liquidado, o el pedido ya activado (→ ToReview).
                _db.ChangeTracker.Clear();
                _logger.LogWarning("Entrax: carrera al liquidar {EntraxPaymentId} ({Source}); se vuelve a leer.",
                    entraxPaymentId, source);
            }
        }
    }

    private async Task<EntraxSettlementOutcome> SettleOnceAsync(string id, EntraxSettlementSource source)
    {
        var none = CancellationToken.None;
        var now  = _clock.Now;

        var payment = await _db.EntraxPayments.FirstOrDefaultAsync(p => p.Id == id, none);
        if (payment is null)
            return EntraxSettlementOutcome.NotFound;

        if (payment.Status == EntraxPaymentStatus.Paid)
        {
            // Ya activó. Si el proceso cayó entre guardar y activar el usuario, esto lo repara.
            await EnsureUserActiveAsync(payment.MemberId);
            return EntraxSettlementOutcome.AlreadyResolved;
        }

        if (payment.Status == EntraxPaymentStatus.ToReview)
            return EntraxSettlementOutcome.AlreadyResolved;

        if (payment.ExternalPaymentId is null)
        {
            if (now - payment.CreationDate < UnissuedGrace)
                return EntraxSettlementOutcome.StillPending;

            return await CloseAsync(payment, EntraxPaymentStatus.Failed, "NO_PAYMENT_ID", now);
        }

        var call = await _client.GetPaymentAsync(payment.ExternalPaymentId, none);
        _db.EntraxApiCalls.Add(EntraxCallLog.From(EntraxCallLog.GetPayment, call.HttpStatus, call.ErrorCode, call.ElapsedMs,
            payment.ExternalOrderId, payment.ExternalPaymentId, call.Value?.Status, now, $"{Actor}:{source}"));
        payment.LastCheckedAt = now;

        if (!call.Ok)
        {
            await _db.SaveChangesAsync(none);
            _logger.LogWarning("Entrax: no se pudo consultar el pago {ExternalPaymentId} ({Code}); se reintentará.",
                payment.ExternalPaymentId, call.ErrorCode);
            return EntraxSettlementOutcome.ProviderUnavailable;
        }

        var remote = call.Value!;
        if (!string.Equals(remote.PaymentId, payment.ExternalPaymentId, StringComparison.OrdinalIgnoreCase)
            || (remote.OrderId is not null && remote.OrderId != payment.ExternalOrderId))
            return await ReviewAsync(payment, "PROVIDER_MISMATCH", now, null);

        switch (remote.Status.Trim().ToLowerInvariant())
        {
            case "paid":
                return await SettlePaidAsync(payment, remote, now, source);
            case "failed":
                return await CloseAsync(payment, EntraxPaymentStatus.Failed, null, now);
            case "expired":
                return await CloseAsync(payment, EntraxPaymentStatus.Expired, null, now);
            default:
                await _db.SaveChangesAsync(none);
                return EntraxSettlementOutcome.StillPending;
        }
    }

    private async Task<EntraxSettlementOutcome> SettlePaidAsync(
        EntraxPayment payment, EntraxPaymentState remote, DateTime now, EntraxSettlementSource source)
    {
        var none = CancellationToken.None;

        payment.PaidAt      ??= remote.CompletedAt?.LocalDateTime ?? now;
        payment.PaidAmountUsd = remote.Original;

        if (!string.Equals(remote.Currency, EntraxGateway.Currency, StringComparison.OrdinalIgnoreCase))
            return await ReviewAsync(payment, "CURRENCY_MISMATCH", now, remote.Original);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, none);
        if (order is null)
            return await ReviewAsync(payment, "ORDER_NOT_FOUND", now, remote.Original);

        var expected = Math.Max(payment.AmountUsd, Math.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero));
        if (remote.Original < expected)
            return await ReviewAsync(payment, "AMOUNT_TOO_LOW", now, remote.Original);

        // Pending (el QR caducó y volvió atrás, o el aspirante está eligiendo otro método) o Processing
        // (este QR, otro intento o una cripto esperando): el alta sigue sin activar y este pago la activa.
        // Completed (u otro): ya se cobró por otro medio. Doble cobro, a devolver a mano.
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Processing))
            return await ReviewAsync(payment, "ORDER_ALREADY_SETTLED", now, remote.Original);

        // Se marca ANTES de activar: el alta en el cobro recurrente guarda por su cuenta dentro de la
        // activación y ese guardado tiene que llevar esta fila, con su RowVersion, en el mismo lote.
        payment.Status         = EntraxPaymentStatus.Paid;
        payment.Consumed       = true;
        payment.ActivatedAt    = now;
        payment.LastUpdateDate = now;
        payment.LastUpdateBy   = Actor;

        _db.PaymentHistories.Add(new PaymentHistory
        {
            OrderId              = order.Id,
            MemberId             = order.MemberId,
            Amount               = remote.Original,
            GatewayName          = EntraxGateway.Name,
            GatewayTransactionId = payment.ExternalPaymentId,
            TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt          = now,
            CreationDate         = now,
            CreatedBy            = Actor,
            LastUpdateDate       = now,
            LastUpdateBy         = Actor
        });

        await CancelWaitingCryptoAsync(order.Id, payment.ExternalPaymentId!, now);

        var activation = await _activation.ActivatePendingSignupAsync(order, now, none);
        if (!activation.IsSuccess)
        {
            // Falló antes de tocar nada (miembro o suscripción que no están): se descarta lo marcado y
            // el pago queda a revisar, con lo que se sabe de él.
            var paidAt = payment.PaidAt;
            _db.ChangeTracker.Clear();
            var again = await _db.EntraxPayments.FirstAsync(p => p.Id == payment.Id, none);
            again.PaidAt        = paidAt;
            again.LastCheckedAt = now;
            return await ReviewAsync(again, activation.ErrorCode ?? "ACTIVATION_FAILED", now, remote.Original);
        }

        await _db.SaveChangesAsync(none);
        await SignupUserActivation.ActivateAsync(_users, activation.Value!.Email);

        _logger.LogInformation("Entrax: alta {OrderId} activada con el pago {ExternalPaymentId} ({Source}).",
            order.Id, payment.ExternalPaymentId, source);
        return EntraxSettlementOutcome.Activated;
    }

    private async Task<EntraxSettlementOutcome> CloseAsync(
        EntraxPayment payment, EntraxPaymentStatus to, string? code, DateTime now)
    {
        var none = CancellationToken.None;

        if (payment.Status != to)
        {
            payment.Status         = to;
            payment.FailureCode    = code ?? payment.FailureCode;
            payment.LastUpdateDate = now;
            payment.LastUpdateBy   = Actor;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, none);
            if (order is not null)
                await ReleaseOrderIfIdleAsync(order, payment.Id, now);
        }

        await _db.SaveChangesAsync(none);
        return to == EntraxPaymentStatus.Failed ? EntraxSettlementOutcome.Failed : EntraxSettlementOutcome.Expired;
    }

    /// <summary>
    /// El pedido vuelve a Pending —y el aspirante puede pedir otro QR o elegir otro método— solo si
    /// nada más lo está esperando: ni otro QR de Entrax vivo ni una cripto pendiente.
    /// </summary>
    private async Task ReleaseOrderIfIdleAsync(Orders order, string exceptPaymentId, DateTime now)
    {
        var none = CancellationToken.None;
        if (order.Status != OrderStatus.Processing) return;

        var otherEntrax = await _db.EntraxPayments.AnyAsync(p => p.OrderId == order.Id && p.Id != exceptPaymentId
            && p.Status == EntraxPaymentStatus.AwaitingPayment, none);
        var crypto = await _db.CryptoPaymentConfirmations.AnyAsync(c => c.OrderId == order.Id
            && c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment, none);
        if (otherEntrax || crypto) return;

        order.Status         = OrderStatus.Pending;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;
    }

    private async Task CancelWaitingCryptoAsync(string orderId, string externalPaymentId, DateTime now)
    {
        var waiting = await _db.CryptoPaymentConfirmations
            .Where(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.Signup
                     && c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment)
            .ToListAsync(CancellationToken.None);

        foreach (var c in waiting)
        {
            c.Status         = CryptoPaymentConfirmationStatus.Cancelled;
            c.Notes          = $"Paid via Entrax ({externalPaymentId}).";
            c.LastUpdateDate = now;
            c.LastUpdateBy   = Actor;
        }
    }

    private async Task<EntraxSettlementOutcome> ReviewAsync(EntraxPayment payment, string reason, DateTime now, decimal? paidUsd)
    {
        payment.Status         = EntraxPaymentStatus.ToReview;
        payment.FailureCode    = reason.Length > 60 ? reason[..60] : reason;
        payment.PaidAmountUsd  = paidUsd ?? payment.PaidAmountUsd;
        payment.LastUpdateDate = now;
        payment.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(CancellationToken.None);

        _logger.LogCritical(
            "Entrax: el pago {ExternalPaymentId} del alta {OrderId} queda A REVISAR ({Reason}). No se activó nada; " +
            "si hubo cobro hay que aplicarlo o devolverlo a mano.",
            payment.ExternalPaymentId, payment.OrderId, reason);
        return EntraxSettlementOutcome.ToReview;
    }

    private async Task EnsureUserActiveAsync(string memberId)
    {
        var email = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.Email)
            .FirstOrDefaultAsync(CancellationToken.None);

        if (email is not null)
            await SignupUserActivation.ActivateAsync(_users, email);
    }
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupSettlementTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupSettlement.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupSettlementTests.cs
git commit -m "Liquidacion de pagos de Entrax del alta: un solo camino que re-consulta el pago, exige paid, USD e importe suficiente, activa una vez y deja a revisar lo que no cuadra"
```

---

### Task 12: Webhook de Entrax (`EntraxCallbackProcessor`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxCallbackProcessor.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxCallbackProcessorTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El callback de Entrax: sin firma o con firma mala no entra; con firma buena solo sirve para
/// disparar la liquidación (que vuelve a preguntar a Entrax). 2xx solo cuando se procesó.
/// </summary>
public class EntraxCallbackProcessorTests
{
    private const string Secreto = "secreto-de-pruebas";

    private static long Ahora() => new DateTimeOffset(DateTime.SpecifyKind(EscenarioEntrax.Ahora, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static byte[] Cuerpo(string paymentId = EscenarioEntrax.PaymentId, string? orderId = null,
        string status = "SUCCESS", long? timestamp = null)
        => Encoding.UTF8.GetBytes(
            $$"""{"paymentId":"{{paymentId}}","orderId":"{{orderId ?? EscenarioEntrax.ExternalOrderId()}}","status":"{{status}}","money":{"summary":80,"currency":"USD","amount":7640,"rate":95.5},"timestamp":{{timestamp ?? Ahora()}}}""");

    private static string Firma(byte[] cuerpo, string secreto = Secreto)
        => Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), cuerpo));

    private sealed record Montaje(AppDbContext Db, Mock<IEntraxSignupSettlement> Liquidacion, EntraxCallbackProcessor Proceso);

    private static async Task<Montaje> MontarAsync(
        EntraxSettlementOutcome resultado = EntraxSettlementOutcome.Activated,
        Action<EntraxOptions>? opciones = null,
        string? paymentIdGuardado = EscenarioEntrax.PaymentId)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await EscenarioEntrax.SembrarPagoAsync(db, paymentId: paymentIdGuardado);

        var liquidacion = new Mock<IEntraxSignupSettlement>();
        liquidacion.Setup(s => s.SettleAsync(It.IsAny<string>(), It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(resultado);

        var proceso = new EntraxCallbackProcessor(db, liquidacion.Object, EscenarioEntrax.Opciones(opciones),
            EscenarioEntrax.Reloj().Object, NullLogger<EntraxCallbackProcessor>.Instance);
        return new Montaje(db, liquidacion, proceso);
    }

    private static void NoSeLiquido(Montaje m)
        => m.Liquidacion.Verify(s => s.SettleAsync(It.IsAny<string>(), It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Sin_secreto_configurado_se_rechaza_aunque_venga_firmado()
    {
        var m = await MontarAsync(opciones: o => o.Secret = null);
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Unauthorized);
        NoSeLiquido(m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Sin_firma_se_rechaza(string? firma)
    {
        var m = await MontarAsync();

        (await m.Proceso.ProcessAsync(Cuerpo(), firma, default)).Should().Be(EntraxCallbackOutcome.Unauthorized,
            "si Entrax no tiene clave manda el callback sin firma: no se acepta.");
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Con_firma_de_otro_secreto_se_rechaza()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo, "otro"), default)).Should().Be(EntraxCallbackOutcome.Unauthorized);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Con_firma_valida_liquida_por_el_camino_unico_y_anota_el_aviso()
    {
        var m = await MontarAsync();
        var pago = await m.Db.EntraxPayments.SingleAsync();
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted);

        m.Liquidacion.Verify(s => s.SettleAsync(pago.Id, EntraxSettlementSource.Webhook, It.IsAny<CancellationToken>()), Times.Once);
        var aviso = await m.Db.EntraxApiCalls.SingleAsync();
        aviso.Operation.Should().Be("Callback");
        aviso.ProviderStatus.Should().Be("SUCCESS");
    }

    [Fact]
    public async Task La_firma_en_hexadecimal_tambien_vale()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo();
        var hex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secreto), cuerpo));

        (await m.Proceso.ProcessAsync(cuerpo, hex, default)).Should().Be(EntraxCallbackOutcome.Accepted);
    }

    [Fact]
    public async Task Un_aviso_de_hace_dos_horas_se_rechaza_sin_tocar_nada()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(timestamp: Ahora() - 2 * 3600);

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Stale);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Un_reintento_de_hace_35_minutos_todavia_entra()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(timestamp: Ahora() - 35 * 60);

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted,
            "Entrax reintenta durante 35 minutos.");
    }

    [Fact]
    public async Task Un_timestamp_en_milisegundos_se_entiende()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(timestamp: Ahora() * 1000);

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted);
    }

    [Fact]
    public async Task Un_pago_que_no_es_nuestro_es_404_para_que_Entrax_reintente()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(paymentId: Guid.NewGuid().ToString(), orderId: "SIGNUP-OTRO-1");

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.NotFound);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Si_aun_no_teniamos_el_paymentId_se_encuentra_por_nuestro_orderId()
    {
        var m = await MontarAsync(paymentIdGuardado: null);
        var cuerpo = Cuerpo(paymentId: EscenarioEntrax.PaymentId.ToUpperInvariant());

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted);

        (await m.Db.EntraxPayments.SingleAsync()).ExternalPaymentId.Should().Be(EscenarioEntrax.PaymentId);
    }

    [Fact]
    public async Task Un_orderId_que_no_es_el_del_pago_es_invalido()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(orderId: "SIGNUP-OTRO-1");

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Invalid);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Un_JSON_roto_con_firma_valida_es_invalido()
    {
        var m = await MontarAsync();
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":");

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Invalid);
    }

    [Theory]
    [InlineData(EntraxSettlementOutcome.Activated, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.AlreadyResolved, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.ToReview, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.Failed, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.Expired, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.StillPending, EntraxCallbackOutcome.RetryLater)]
    [InlineData(EntraxSettlementOutcome.ProviderUnavailable, EntraxCallbackOutcome.RetryLater)]
    public async Task Solo_se_confirma_a_Entrax_lo_que_quedo_resuelto(EntraxSettlementOutcome liquidacion, EntraxCallbackOutcome esperado)
    {
        var m = await MontarAsync(liquidacion);
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(esperado);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxCallbackProcessorTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxCallbackProcessor.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public enum EntraxCallbackOutcome
{
    /// <summary>200: procesado o ya resuelto. Entrax deja de reintentar.</summary>
    Accepted,
    /// <summary>401: sin firma, firma mala o sin secreto con qué comprobarla.</summary>
    Unauthorized,
    /// <summary>400: cuerpo ilegible o que no corresponde al pago.</summary>
    Invalid,
    /// <summary>400: timestamp fuera de la tolerancia.</summary>
    Stale,
    /// <summary>404: el pago no es nuestro o todavía no existe. Entrax reintenta.</summary>
    NotFound,
    /// <summary>503: no se pudo resolver ahora (Entrax caído o aún "pending"). Entrax reintenta.</summary>
    RetryLater
}

public interface IEntraxCallbackProcessor
{
    Task<EntraxCallbackOutcome> ProcessAsync(byte[] rawBody, string? signature, CancellationToken ct);
}

/// <summary>
/// El callback de Entrax. Lo que el legado hacía mal y aquí no: la firma se compara en tiempo
/// constante sobre el cuerpo crudo; sin firma (Entrax la omite si no tiene clave) se rechaza; nunca
/// se devuelve 200 sin haber procesado (para que Entrax reintente si fallamos); y el cuerpo del aviso
/// no decide nada: solo dice QUÉ pago mirar, y EntraxSignupSettlement vuelve a preguntar a Entrax.
///
/// La comprobación del timestamp usa UtcNow porque el campo es un unix UTC del protocolo.
/// </summary>
public sealed class EntraxCallbackProcessor : IEntraxCallbackProcessor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AppDbContext                       _db;
    private readonly IEntraxSignupSettlement            _settlement;
    private readonly EntraxOptions                      _options;
    private readonly IDateTimeProvider                  _clock;
    private readonly ILogger<EntraxCallbackProcessor>   _logger;

    public EntraxCallbackProcessor(
        AppDbContext db, IEntraxSignupSettlement settlement, EntraxOptions options,
        IDateTimeProvider clock, ILogger<EntraxCallbackProcessor> logger)
    {
        _db         = db;
        _settlement = settlement;
        _options    = options;
        _clock      = clock;
        _logger     = logger;
    }

    public async Task<EntraxCallbackOutcome> ProcessAsync(byte[] rawBody, string? signature, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.Secret))
        {
            _logger.LogError("Entrax callback rechazado: no hay Entrax:Secret con el que comprobar la firma.");
            return EntraxCallbackOutcome.Unauthorized;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Entrax callback rechazado: llegó sin X-Signature.");
            return EntraxCallbackOutcome.Unauthorized;
        }

        if (!EntraxSignature.VerifyCallback(rawBody, signature, _options.Secret))
        {
            _logger.LogWarning("Entrax callback rechazado: la firma no es válida.");
            return EntraxCallbackOutcome.Unauthorized;
        }

        Payload? payload;
        try { payload = JsonSerializer.Deserialize<Payload>(rawBody, Json); }
        catch (JsonException) { return EntraxCallbackOutcome.Invalid; }

        var paymentId = EntraxGateway.NormalizePaymentId(payload?.PaymentId);
        if (payload is null || paymentId is null)
            return EntraxCallbackOutcome.Invalid;

        var sent = ToInstant(payload.Timestamp);
        var utcNow = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        if (sent is null || (utcNow - sent.Value).Duration() > _options.CallbackTolerance)
        {
            _logger.LogWarning("Entrax callback del pago {PaymentId} rechazado por antigüedad.", paymentId);
            return EntraxCallbackOutcome.Stale;
        }

        var now = _clock.Now;
        var payment = await _db.EntraxPayments.FirstOrDefaultAsync(p => p.ExternalPaymentId == paymentId, ct);
        if (payment is null && !string.IsNullOrWhiteSpace(payload.OrderId))
        {
            // El QR se emitió pero no llegamos a guardar su paymentId: nuestro orderId lo identifica.
            payment = await _db.EntraxPayments.FirstOrDefaultAsync(
                p => p.ExternalOrderId == payload.OrderId && p.ExternalPaymentId == null, ct);
            if (payment is not null)
            {
                payment.ExternalPaymentId = paymentId;
                payment.LastUpdateDate    = now;
                payment.LastUpdateBy      = EntraxGateway.Name;
            }
        }

        if (payment is null)
        {
            _logger.LogWarning("Entrax callback de un pago que no es nuestro o aún no existe: {PaymentId}.", paymentId);
            return EntraxCallbackOutcome.NotFound;
        }

        if (!string.IsNullOrWhiteSpace(payload.OrderId) && payload.OrderId != payment.ExternalOrderId)
        {
            _logger.LogWarning("Entrax callback del pago {PaymentId} con un orderId que no es el suyo.", paymentId);
            return EntraxCallbackOutcome.Invalid;
        }

        _db.EntraxApiCalls.Add(EntraxCallLog.From(EntraxCallLog.Callback, 0, null, 0,
            payment.ExternalOrderId, paymentId, payload.Status, now, $"{EntraxGateway.Name}:Webhook"));
        await _db.SaveChangesAsync(CancellationToken.None);

        var outcome = await _settlement.SettleAsync(payment.Id, EntraxSettlementSource.Webhook, ct);
        return outcome switch
        {
            EntraxSettlementOutcome.Activated
                or EntraxSettlementOutcome.AlreadyResolved
                or EntraxSettlementOutcome.Failed
                or EntraxSettlementOutcome.Expired
                or EntraxSettlementOutcome.ToReview => EntraxCallbackOutcome.Accepted,
            EntraxSettlementOutcome.NotFound => EntraxCallbackOutcome.NotFound,
            _ => EntraxCallbackOutcome.RetryLater
        };
    }

    /// <summary>Unix UTC en segundos; por encima de 10¹¹ se entiende en milisegundos.</summary>
    private static DateTimeOffset? ToInstant(long timestamp)
    {
        if (timestamp <= 0) return null;
        try
        {
            return timestamp > 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                : DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed class Payload
    {
        public string? PaymentId { get; set; }
        public string? OrderId   { get; set; }
        public string? Status    { get; set; }
        public long    Timestamp { get; set; }
    }
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxCallbackProcessorTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxCallbackProcessor.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxCallbackProcessorTests.cs
git commit -m "Callback de Entrax: firma obligatoria comparada en tiempo constante, tolerancia de 60 minutos, el aviso solo dispara la liquidacion y 2xx solo si quedo resuelto"
```

---

### Task 13: Estado para la pantalla y métodos disponibles

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupStatusService.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupStatusServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// La pantalla pregunta cada pocos segundos; SignupAPI solo vuelve a preguntar a Entrax si hace
/// rato que no lo hace (límite de 100/min por IP). Anónimo, así que sin las dos referencias no dice nada.
/// </summary>
public class EntraxSignupStatusServiceTests
{
    private sealed record Montaje(AppDbContext Db, Mock<IEntraxSignupSettlement> Liquidacion, EntraxSignupStatusService Servicio, EntraxPayment Pago);

    private static async Task<Montaje> MontarAsync(
        EntraxPaymentStatus estado = EntraxPaymentStatus.AwaitingPayment, DateTime? consultado = null,
        DateTime? caduca = null, Action<EntraxOptions>? opciones = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var pago = await EscenarioEntrax.SembrarPagoAsync(db, estado, consultado: consultado, caduca: caduca);
        var liquidacion = new Mock<IEntraxSignupSettlement>();
        var servicio = new EntraxSignupStatusService(db, liquidacion.Object, EscenarioEntrax.Opciones(opciones),
            EscenarioEntrax.Reloj().Object, NullLogger<EntraxSignupStatusService>.Instance);
        return new Montaje(db, liquidacion, servicio, pago);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("otra-referencia")]
    public async Task Sin_la_referencia_del_pago_no_se_dice_nada(string? referencia)
    {
        var m = await MontarAsync();

        (await m.Servicio.GetAsync(EscenarioEntrax.OrderId, referencia, default)).Should().BeNull();
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Con_el_id_de_otra_alta_tampoco()
    {
        var m = await MontarAsync();

        (await m.Servicio.GetAsync("ORD-OTRO", m.Pago.Id, default)).Should().BeNull();
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Si_hace_rato_que_no_se_pregunta_a_Entrax_se_liquida_y_se_devuelve_lo_nuevo()
    {
        var m = await MontarAsync();
        m.Liquidacion.Setup(s => s.SettleAsync(m.Pago.Id, EntraxSettlementSource.Poll, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var fila = m.Db.EntraxPayments.Single();
                fila.Status = EntraxPaymentStatus.Paid;
                m.Db.SaveChanges();
            })
            .ReturnsAsync(EntraxSettlementOutcome.Activated);

        var r = await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default);

        r!.Status.Should().Be(EntraxSignupStatuses.Paid);
        r.SecondsRemaining.Should().Be(0);
    }

    [Fact]
    public async Task Si_se_pregunto_hace_poco_no_se_vuelve_a_preguntar()
    {
        var m = await MontarAsync(consultado: EscenarioEntrax.Ahora.AddSeconds(-5));

        var r = await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default);

        r!.Status.Should().Be(EntraxSignupStatuses.AwaitingPayment);
        r.SecondsRemaining.Should().Be(900);
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_pago_cerrado_no_se_vuelve_a_consultar()
    {
        var m = await MontarAsync(EntraxPaymentStatus.Expired, caduca: EscenarioEntrax.Ahora.AddMinutes(-1));

        var r = await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default);

        r!.Status.Should().Be(EntraxSignupStatuses.Expired);
        r.SecondsRemaining.Should().Be(0);
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Si_la_liquidacion_revienta_se_contesta_con_lo_que_hay()
    {
        var m = await MontarAsync();
        m.Liquidacion.Setup(s => s.SettleAsync(It.IsAny<string>(), It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("carrera"));

        (await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default))!.Status
            .Should().Be(EntraxSignupStatuses.AwaitingPayment);
    }

    [Fact]
    public async Task Los_metodos_disponibles_dicen_en_que_paises_se_ofrece()
    {
        (await MontarAsync()).Servicio.GetPaymentOptions().EntraxCountries.Should().Equal("RU");
        (await MontarAsync(opciones: o => o.Enabled = false)).Servicio.GetPaymentOptions().EntraxCountries.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupStatusServiceTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxSignupStatusService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public interface IEntraxSignupStatusService
{
    /// <summary>Estado del pago para la pantalla del alta, o null si las dos referencias no casan.</summary>
    Task<EntraxSignupStatusDto?> GetAsync(string signupId, string? paymentRef, CancellationToken ct);

    SignupPaymentOptionsDto GetPaymentOptions();
}

/// <summary>
/// Lo que consulta la pantalla del QR. Anónimo (la cuenta aún no puede iniciar sesión), así que:
/// exige el id del alta Y el de nuestro pago (GUID aleatorio), y solo devuelve estado y tiempo.
/// Mientras el pago espera, y si hace más de Entrax:PollThrottleSeconds de la última consulta,
/// lo liquida por el camino único. Los fallos de la liquidación no se le enseñan al navegador.
/// </summary>
public sealed class EntraxSignupStatusService : IEntraxSignupStatusService
{
    private readonly AppDbContext                         _db;
    private readonly IEntraxSignupSettlement              _settlement;
    private readonly EntraxOptions                        _options;
    private readonly IDateTimeProvider                    _clock;
    private readonly ILogger<EntraxSignupStatusService>   _logger;

    public EntraxSignupStatusService(
        AppDbContext db, IEntraxSignupSettlement settlement, EntraxOptions options,
        IDateTimeProvider clock, ILogger<EntraxSignupStatusService> logger)
    {
        _db         = db;
        _settlement = settlement;
        _options    = options;
        _clock      = clock;
        _logger     = logger;
    }

    public async Task<EntraxSignupStatusDto?> GetAsync(string signupId, string? paymentRef, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signupId) || string.IsNullOrWhiteSpace(paymentRef))
            return null;

        var payment = await ReadAsync(signupId, paymentRef, ct);
        if (payment is null)
            return null;

        var now = _clock.Now;
        if (payment.Status == EntraxPaymentStatus.AwaitingPayment
            && payment.ExternalPaymentId is not null
            && (payment.LastCheckedAt is null || now - payment.LastCheckedAt.Value >= _options.PollThrottle))
        {
            try
            {
                await _settlement.SettleAsync(payment.Id, EntraxSettlementSource.Poll, ct);
                payment = await ReadAsync(signupId, paymentRef, ct) ?? payment;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Entrax: falló la liquidación del pago {EntraxPaymentId} desde la pantalla.", payment.Id);
            }
        }

        return new EntraxSignupStatusDto
        {
            Status           = payment.Status.ToString(),
            ExpiresAt        = payment.ExpiresAt,
            SecondsRemaining = payment.Status == EntraxPaymentStatus.AwaitingPayment
                ? EntraxGateway.SecondsUntil(payment.ExpiresAt, now)
                : 0
        };
    }

    public SignupPaymentOptionsDto GetPaymentOptions()
        => new() { EntraxCountries = _options.Enabled ? _options.EffectiveCountries : [] };

    private Task<EntraxPayment?> ReadAsync(string signupId, string paymentRef, CancellationToken ct)
        => _db.EntraxPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentRef && p.OrderId == signupId, ct);
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupStatusServiceTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Entrax/EntraxSignupStatusService.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupStatusServiceTests.cs
git commit -m "Estado del pago con Entrax para la pantalla del alta: exige el alta y la referencia del pago, solo devuelve estado y tiempo y no pregunta a Entrax mas de una vez cada 15 segundos"
```

---

### Task 14: Endpoints de SignupAPI (`EntraxSignupsController`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Controllers/EntraxSignupsController.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupsControllerTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class EntraxSignupsControllerTests
{
    private sealed record Montaje(
        EntraxSignupsController Controlador, Mock<IEntraxCallbackProcessor> Callbacks,
        Mock<IEntraxSignupStatusService> Estado, Mock<IEntraxSignupSettlement> Liquidacion);

    private static Montaje Crear(byte[]? cuerpo = null, string? firma = null)
    {
        var callbacks   = new Mock<IEntraxCallbackProcessor>();
        var estado      = new Mock<IEntraxSignupStatusService>();
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(cuerpo ?? Array.Empty<byte>());
        if (firma is not null) ctx.Request.Headers["X-Signature"] = firma;

        var c = new EntraxSignupsController(callbacks.Object, estado.Object, liquidacion.Object,
            NullLogger<EntraxSignupsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
        return new Montaje(c, callbacks, estado, liquidacion);
    }

    private static int? Codigo(IActionResult r) => r.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;

    // ── Callback ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EntraxCallbackOutcome.Accepted, 200)]
    [InlineData(EntraxCallbackOutcome.Unauthorized, 401)]
    [InlineData(EntraxCallbackOutcome.Invalid, 400)]
    [InlineData(EntraxCallbackOutcome.Stale, 400)]
    [InlineData(EntraxCallbackOutcome.NotFound, 404)]
    [InlineData(EntraxCallbackOutcome.RetryLater, 503)]
    public async Task El_callback_contesta_segun_lo_que_paso(EntraxCallbackOutcome resultado, int http)
    {
        var m = Crear(Encoding.UTF8.GetBytes("{}"), "firma");
        m.Callbacks.Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(resultado);

        Codigo(await m.Controlador.EntraxCallback(default)).Should().Be(http);
    }

    [Fact]
    public async Task Al_procesador_le_llega_el_cuerpo_crudo_y_la_firma()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":\"x\",  \"status\":\"SUCCESS\"}");
        var m = Crear(cuerpo, "abc=");
        byte[]? recibido = null;
        string? firma = null;
        m.Callbacks.Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .Callback<byte[], string?, CancellationToken>((b, f, _) => { recibido = b; firma = f; })
                   .ReturnsAsync(EntraxCallbackOutcome.Accepted);

        await m.Controlador.EntraxCallback(default);

        recibido.Should().Equal(cuerpo, "la firma es sobre los bytes tal cual llegaron, sin re-serializar.");
        firma.Should().Be("abc=");
    }

    [Fact]
    public async Task Un_cuerpo_demasiado_grande_no_se_procesa()
    {
        var m = Crear(new byte[EntraxSignupsController.MaxCallbackBytes + 1], "f");

        Codigo(await m.Controlador.EntraxCallback(default)).Should().Be(StatusCodes.Status413PayloadTooLarge);
        m.Callbacks.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_error_inesperado_es_500_sin_detalles()
    {
        var m = Crear(Encoding.UTF8.GetBytes("{}"), "f");
        m.Callbacks.Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("detalle interno"));

        var r = await m.Controlador.EntraxCallback(default);

        r.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(500,
            "nunca ex.ToString() en una respuesta; el detalle va al log.");
    }

    // ── Estado y métodos ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Un_estado_que_no_casa_es_404_generico()
    {
        var m = Crear();
        m.Estado.Setup(s => s.GetAsync("ORD", "ref", It.IsAny<CancellationToken>())).ReturnsAsync((EntraxSignupStatusDto?)null);

        var r = await m.Controlador.GetEntraxStatus("ORD", "ref", default);

        r.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().BeOfType<ApiResponse<EntraxSignupStatusDto>>()
         .Which.ErrorCode.Should().Be(EntraxSignupErrors.PaymentNotFound);
    }

    [Fact]
    public async Task Un_estado_conocido_se_devuelve()
    {
        var m = Crear();
        m.Estado.Setup(s => s.GetAsync("ORD", "ref", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EntraxSignupStatusDto { Status = EntraxSignupStatuses.Paid });

        var r = await m.Controlador.GetEntraxStatus("ORD", "ref", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<EntraxSignupStatusDto>>()
         .Which.Data!.Status.Should().Be(EntraxSignupStatuses.Paid);
    }

    [Fact]
    public void Los_metodos_disponibles_salen_del_servicio()
    {
        var m = Crear();
        m.Estado.Setup(s => s.GetPaymentOptions()).Returns(new SignupPaymentOptionsDto { EntraxCountries = ["RU"] });

        m.Controlador.GetPaymentOptions().Should().BeOfType<OkObjectResult>()
         .Which.Value.Should().BeOfType<ApiResponse<SignupPaymentOptionsDto>>()
         .Which.Data!.EntraxCountries.Should().Equal("RU");
    }

    // ── Re-check ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Re_check_liquida_por_el_camino_unico_con_origen_Admin()
    {
        var m = Crear();
        m.Liquidacion.Setup(s => s.SettleAsync("pago-1", EntraxSettlementSource.Admin, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(EntraxSettlementOutcome.Activated);

        var r = await m.Controlador.Recheck("pago-1", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value
         .Should().BeOfType<ApiResponse<EntraxSignupsController.EntraxRecheckResponse>>()
         .Which.Data!.Outcome.Should().Be("Activated");
    }

    [Fact]
    public async Task Re_check_de_un_pago_que_no_existe_es_404()
    {
        var m = Crear();
        m.Liquidacion.Setup(s => s.SettleAsync("x", EntraxSettlementSource.Admin, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(EntraxSettlementOutcome.NotFound);

        (await m.Controlador.Recheck("x", default)).Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Quién puede llamar ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(EntraxSignupsController.EntraxCallback))]
    [InlineData(nameof(EntraxSignupsController.GetEntraxStatus))]
    [InlineData(nameof(EntraxSignupsController.GetPaymentOptions))]
    public void Callback_estado_y_metodos_son_anonimos(string metodo)
        => typeof(EntraxSignupsController).GetMethod(metodo)!.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();

    [Fact]
    public void Re_check_exige_los_mismos_roles_que_confirmar_cripto()
    {
        var m = typeof(EntraxSignupsController).GetMethod(nameof(EntraxSignupsController.Recheck))!;

        m.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers);
        m.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupsControllerTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxSignupsController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

/// <summary>
/// Entrax (rublos por SBP) en el alta: métodos disponibles, estado para la pantalla del QR,
/// callback de Entrax y Re-check del admin. Todo lo que activa pasa por EntraxSignupSettlement.
///
/// Ninguna respuesta lleva detalles de una excepción (el legado devolvía ex.ToString() en endpoints
/// anónimos): el detalle va al log.
/// </summary>
[ApiController]
[Route("api/v1/signups")]
public class EntraxSignupsController : ControllerBase
{
    /// <summary>Un callback de Entrax es un JSON de unos cientos de bytes.</summary>
    public const int MaxCallbackBytes = 64 * 1024;

    private readonly IEntraxCallbackProcessor           _callbacks;
    private readonly IEntraxSignupStatusService         _status;
    private readonly IEntraxSignupSettlement            _settlement;
    private readonly ILogger<EntraxSignupsController>   _logger;

    public EntraxSignupsController(
        IEntraxCallbackProcessor callbacks,
        IEntraxSignupStatusService status,
        IEntraxSignupSettlement settlement,
        ILogger<EntraxSignupsController> logger)
    {
        _callbacks  = callbacks;
        _status     = status;
        _settlement = settlement;
        _logger     = logger;
    }

    /// <summary>GET api/v1/signups/payment-options — países donde se ofrece pagar en rublos.</summary>
    [HttpGet("payment-options")]
    [AllowAnonymous]
    public IActionResult GetPaymentOptions()
        => Ok(ApiResponse<SignupPaymentOptionsDto>.Ok(_status.GetPaymentOptions()));

    /// <summary>
    /// GET api/v1/signups/{signupId}/entrax-status?ref={paymentRef} — lo consulta la pantalla del QR
    /// cada pocos segundos. Anónimo: exige las dos referencias y solo devuelve estado y tiempo.
    /// </summary>
    [HttpGet("{signupId}/entrax-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEntraxStatus(
        string signupId, [FromQuery(Name = "ref")] string? paymentRef, CancellationToken ct)
    {
        var dto = await _status.GetAsync(signupId, paymentRef, ct);
        return dto is null
            ? NotFound(ApiResponse<EntraxSignupStatusDto>.Fail(EntraxSignupErrors.PaymentNotFound, "Payment not found."))
            : Ok(ApiResponse<EntraxSignupStatusDto>.Ok(dto));
    }

    /// <summary>
    /// POST api/v1/signups/entrax/callback — el aviso de Entrax. Se lee el cuerpo CRUDO (la firma es
    /// sobre esos bytes) y se devuelve 2xx solo si quedó procesado o ya estaba resuelto: si no,
    /// Entrax reintenta (25 veces en 35 minutos).
    /// </summary>
    [HttpPost("entrax/callback")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxCallbackBytes)]
    public async Task<IActionResult> EntraxCallback(CancellationToken ct)
    {
        var body = await ReadBodyAsync(Request.Body, MaxCallbackBytes, ct);
        if (body is null)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        EntraxCallbackOutcome outcome;
        try
        {
            outcome = await _callbacks.ProcessAsync(body, Request.Headers["X-Signature"].FirstOrDefault(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Entrax callback: error inesperado; Entrax lo reintentará.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return outcome switch
        {
            EntraxCallbackOutcome.Accepted     => Ok(new { received = true }),
            EntraxCallbackOutcome.Unauthorized => Unauthorized(),
            EntraxCallbackOutcome.Invalid      => BadRequest(new { error = "INVALID_CALLBACK" }),
            EntraxCallbackOutcome.Stale        => BadRequest(new { error = "STALE_CALLBACK" }),
            EntraxCallbackOutcome.NotFound     => NotFound(),
            _                                  => StatusCode(StatusCodes.Status503ServiceUnavailable)
        };
    }

    /// <summary>
    /// POST api/v1/signups/entrax/{entraxPaymentId}/recheck — vuelve a preguntar a Entrax y liquida.
    /// Lo llama AdminAPI reenviando el Bearer del administrador; el rol se comprueba aquí también.
    /// </summary>
    [HttpPost("entrax/{entraxPaymentId}/recheck")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> Recheck(string entraxPaymentId, CancellationToken ct)
    {
        var outcome = await _settlement.SettleAsync(entraxPaymentId, EntraxSettlementSource.Admin, ct);
        return outcome == EntraxSettlementOutcome.NotFound
            ? NotFound(ApiResponse<EntraxRecheckResponse>.Fail(EntraxSignupErrors.PaymentNotFound, "Payment not found."))
            : Ok(ApiResponse<EntraxRecheckResponse>.Ok(new EntraxRecheckResponse(outcome.ToString())));
    }

    public sealed record EntraxRecheckResponse(string Outcome);

    /// <summary>Lee el cuerpo entero o devuelve null si pasa de <paramref name="max"/> bytes.</summary>
    internal static async Task<byte[]?> ReadBodyAsync(Stream body, int max, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(buffer, ct)) > 0)
        {
            if (ms.Length + read > max) return null;
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupsControllerTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Controllers/EntraxSignupsController.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupsControllerTests.cs
git commit -m "Endpoints de Entrax en SignupAPI: metodos disponibles, estado anonimo minimo, callback con cuerpo crudo y codigos que hacen reintentar a Entrax, y Re-check con los roles de cripto"
```

---

### Task 15: Barrido de Hangfire (`entrax-signup-sweep`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Jobs/EntraxSignupSweepJob.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupSweepJobTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El barrido liquida lo que el webhook y la pantalla no liquidaron: QR esperando y QR caducados
/// hace poco (un pago tardío). Pocos por pasada, los menos consultados primero.
/// </summary>
public class EntraxSignupSweepJobTests
{
    private static readonly DateTime Ahora = EscenarioEntrax.Ahora;

    private static async Task<AppDbContext> AltaAsync()
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        return db;
    }

    private static Task<EntraxPayment> Pago(AppDbContext db, int intento, EntraxPaymentStatus estado,
        DateTime? caduca = null, DateTime? consultado = null)
        => EscenarioEntrax.SembrarPagoAsync(db, estado, paymentId: Guid.NewGuid().ToString(), intento: intento,
            caduca: caduca, consultado: consultado);

    private static EntraxSignupSweepJob Job(AppDbContext db, Mock<IEntraxSignupSettlement> liquidacion, Action<EntraxOptions>? opciones = null)
        => new(db, liquidacion.Object, EscenarioEntrax.Opciones(opciones), EscenarioEntrax.Reloj().Object,
               NullLogger<EntraxSignupSweepJob>.Instance);

    [Fact]
    public async Task Liquida_los_que_esperan_y_los_caducados_recientes_y_nada_mas()
    {
        await using var db = await AltaAsync();
        var esperando     = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        var caducoHacePoco = await Pago(db, 2, EntraxPaymentStatus.Expired, caduca: Ahora.AddMinutes(-30));
        await Pago(db, 3, EntraxPaymentStatus.Expired, caduca: Ahora.AddHours(-3));
        await Pago(db, 4, EntraxPaymentStatus.Paid);
        await Pago(db, 5, EntraxPaymentStatus.ToReview);
        await Pago(db, 6, EntraxPaymentStatus.Failed);
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        await Job(db, liquidacion).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(esperando.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.Verify(s => s.SettleAsync(caducoHacePoco.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Toma_como_mucho_el_lote_empezando_por_los_menos_consultados()
    {
        await using var db = await AltaAsync();
        var nunca      = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        await Pago(db, 2, EntraxPaymentStatus.AwaitingPayment, consultado: Ahora.AddMinutes(-1));
        var haceDiez   = await Pago(db, 3, EntraxPaymentStatus.AwaitingPayment, consultado: Ahora.AddMinutes(-10));
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        await Job(db, liquidacion, o => o.SweepBatchSize = 2).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(nunca.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.Verify(s => s.SettleAsync(haceDiez.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_fallo_en_uno_no_corta_los_demas()
    {
        await using var db = await AltaAsync();
        var primero = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        var segundo = await Pago(db, 2, EntraxPaymentStatus.AwaitingPayment, consultado: Ahora);
        var liquidacion = new Mock<IEntraxSignupSettlement>();
        liquidacion.Setup(s => s.SettleAsync(primero.Id, It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("boom"));

        await Job(db, liquidacion).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(segundo.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Corre_aunque_Entrax_este_apagado_para_no_dejar_pagos_en_vuelo()
    {
        await using var db = await AltaAsync();
        var esperando = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        await Job(db, liquidacion, o => o.Enabled = false).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(esperando.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupSweepJobTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `EntraxSignupSweepJob.cs`**

```csharp
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Jobs;

/// <summary>
/// HangFire, cada 2 minutos, cola "signups": liquida los pagos de Entrax del alta que nadie más
/// liquidó — el callback no llegó (o se rechazó) y la pantalla se cerró —, y re-mira los QR que
/// caducaron hace menos de Entrax:LatePaymentWindowMinutes por si se pagaron tarde.
///
/// Como mucho Entrax:SweepBatchSize por pasada, los menos consultados primero: Entrax limita a 100
/// peticiones por minuto e IP. Corre aunque Entrax esté apagado: si se apaga con pagos en vuelo,
/// esos pagos tienen que acabar en algún sitio.
/// </summary>
[Queue("signups")]
public class EntraxSignupSweepJob
{
    public const string RecurringJobId = "entrax-signup-sweep";
    public const string Cron           = "*/2 * * * *";

    private readonly AppDbContext                      _db;
    private readonly IEntraxSignupSettlement           _settlement;
    private readonly EntraxOptions                     _options;
    private readonly IDateTimeProvider                 _clock;
    private readonly ILogger<EntraxSignupSweepJob>     _logger;

    public EntraxSignupSweepJob(
        AppDbContext db, IEntraxSignupSettlement settlement, EntraxOptions options,
        IDateTimeProvider clock, ILogger<EntraxSignupSweepJob> logger)
    {
        _db         = db;
        _settlement = settlement;
        _options    = options;
        _clock      = clock;
        _logger     = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now      = _clock.Now;
        var lateFrom = now - _options.LatePaymentWindow;

        var ids = await _db.EntraxPayments.AsNoTracking()
            .Where(p => p.Status == EntraxPaymentStatus.AwaitingPayment
                     || (p.Status == EntraxPaymentStatus.Expired && p.ExpiresAt != null && p.ExpiresAt >= lateFrom))
            .OrderBy(p => p.LastCheckedAt ?? DateTime.MinValue)
            .ThenBy(p => p.CreationDate)
            .Select(p => p.Id)
            .Take(Math.Max(1, _options.SweepBatchSize))
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await _settlement.SettleAsync(id, EntraxSettlementSource.Sweep, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Entrax: el barrido no pudo liquidar el pago {EntraxPaymentId}; se reintentará.", id);
            }
        }
    }
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupSweepJobTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Jobs/EntraxSignupSweepJob.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupSweepJobTests.cs
git commit -m "Barrido de pagos de Entrax del alta cada 2 minutos: los que esperan y los caducados recientes, en lotes y sin que un fallo corte el resto"
```

---

### Task 16: Cableado en SignupAPI (`Program.cs` y `appsettings.json`)

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Program.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/appsettings.json`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxConfiguracionTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El appsettings.json base va al repositorio: Entrax apagado, sin KeyId ni Secret, y con la regla
/// de rate limit del estado que consulta la pantalla.
/// </summary>
public class EntraxConfiguracionTests
{
    private static IConfiguration Base()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.SignupAPI", "appsettings.json"))
            .Build();
    }

    [Fact]
    public void Los_secretos_de_Entrax_no_estan_en_el_appsettings_base()
    {
        var c = Base();

        c["Entrax:KeyId"].Should().BeNullOrEmpty();
        c["Entrax:Secret"].Should().BeNullOrEmpty();
    }

    [Fact]
    public void Entrax_viene_apagado_y_con_la_url_de_la_documentacion()
    {
        var o = EntraxOptions.FromConfiguration(Base());

        o.Enabled.Should().BeFalse();
        o.UseFake.Should().BeFalse();
        o.BaseUrl.Should().Be(EntraxOptions.DefaultBaseUrl);
        o.EffectiveCountries.Should().Equal("RU");
    }

    [Fact]
    public void El_estado_que_consulta_la_pantalla_tiene_su_propio_limite()
    {
        var reglas = Base().GetSection("IpRateLimiting:GeneralRules").GetChildren();

        reglas.Should().Contain(r => r["Endpoint"] == "GET:/api/v1/signups/*/entrax-status" && r["Limit"] == "30");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxConfiguracionTests -nologo`
Expected: FAIL (`El_estado_que_consulta_la_pantalla_tiene_su_propio_limite`).

- [ ] **Step 3: `appsettings.json`**

3a. En `IpRateLimiting.GeneralRules`, justo **antes** de la regla `"Endpoint": "*"`, añadir:

```json
      {
        "//": "La pantalla del QR de Entrax pregunta cada 5 s (12/min). SignupAPI solo pregunta a Entrax una vez cada 15 s por pago.",
        "Endpoint": "GET:/api/v1/signups/*/entrax-status",
        "Period": "1m",
        "Limit": 30
      },
```

3b. Al final del objeto raíz (después de `"Notifications": { … }`, con su coma), añadir:

```json
  "Entrax": {
    "//": "Rublos por SBP en el alta (Plan H). KeyId y Secret NUNCA aqui: variables de entorno Entrax__KeyId y Entrax__Secret. Fuera de Development, Enabled sin KeyId, Secret o CallbackUrl https no arranca. UseFake solo en Development.",
    "Enabled": false,
    "BaseUrl": "https://entrax.pro/api/v1/cash-register",
    "CallbackUrl": "",
    "Countries": [ "RU" ],
    "UseFake": false,
    "CallbackToleranceMinutes": 60,
    "LatePaymentWindowMinutes": 120,
    "PollThrottleSeconds": 15,
    "SweepBatchSize": 50
  }
```

- [ ] **Step 4: `Program.cs`**

4a. `using` nuevos, junto a los demás:

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
```

4b. Debajo de `builder.Services.AddScoped<IEncryptionService, EncryptionService>();` (línea ~122):

```csharp

// Entrax: rublos por SBP en el alta (Plan H). La guarda tumba el arranque fuera de Development si
// está encendido sin KeyId, Secret o CallbackUrl https, o si UseFake está puesto; en Development
// solo avisa. El fake (UseFake) es un singleton en memoria que se paga con /api/v1/dev/entrax/….
var entraxOptions = EntraxOptions.FromConfiguration(builder.Configuration);
EntraxOptionsGuard.ThrowIfInvalid(entraxOptions, builder.Environment.IsDevelopment());
foreach (var problem in EntraxOptionsGuard.Problems(entraxOptions, builder.Environment.IsDevelopment()))
    Console.WriteLine($"[Entrax] {problem}");
builder.Services.AddEntrax(entraxOptions);
builder.Services.AddScoped<IEntraxSignupCheckout, EntraxSignupCheckout>();
builder.Services.AddScoped<IEntraxSignupSettlement, EntraxSignupSettlement>();
builder.Services.AddScoped<IEntraxCallbackProcessor, EntraxCallbackProcessor>();
builder.Services.AddScoped<IEntraxSignupStatusService, EntraxSignupStatusService>();
```

4c. Con los demás trabajos (debajo de `builder.Services.AddScoped<ApplyMemberStatisticDeltasJob>();`):

```csharp
builder.Services.AddScoped<EntraxSignupSweepJob>();
```

4d. Debajo del `RecurringJob.AddOrUpdate<ContestPointsSweepJob>(…)`:

```csharp

// Entrax (Plan H): liquida los pagos del alta que ni el callback ni la pantalla liquidaron, y mira
// los QR caducados hace poco por si se pagaron tarde. QueueName explícito por lo mismo que arriba.
RecurringJob.AddOrUpdate<EntraxSignupSweepJob>(
    EntraxSignupSweepJob.RecurringJobId,
    job => job.ExecuteAsync(CancellationToken.None),
    EntraxSignupSweepJob.Cron,
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "signups" });
```

4e. Debajo del bloque `if (app.Environment.IsDevelopment()) { app.MapPost("/api/v1/dev/drain-deltas", …) }`:

```csharp

// Solo Development con Entrax:UseFake: pagar (o hacer fallar, caducar o pagar de menos) un QR del
// fake, y forzar un barrido. La guarda de arranque impide UseFake fuera de Development.
if (app.Environment.IsDevelopment() && entraxOptions.UseFake)
{
    app.MapPost("/api/v1/dev/entrax/{paymentId}/{outcome}",
        (string paymentId, string outcome, FakeEntraxClient fake) =>
        {
            var ok = outcome switch
            {
                "paid"      => fake.MarkPaid(paymentId),
                "underpaid" => fake.MarkPaid(paymentId, underpayBy: 1m),
                "failed"    => fake.MarkFailed(paymentId),
                "expired"   => fake.MarkExpired(paymentId),
                _           => false
            };
            return ok ? Results.Ok(new { paymentId, outcome }) : Results.NotFound();
        }).AllowAnonymous();

    app.MapPost("/api/v1/dev/entrax/sweep",
        async (EntraxSignupSweepJob job, CancellationToken ct) =>
        {
            await job.ExecuteAsync(ct);
            return Results.Ok(new { swept = true });
        }).AllowAnonymous();
}
```

- [ ] **Step 5: Compilar y ejecutar las pruebas de Entrax**

Run: `dotnet build MLMConquerorGlobalEdition.SignupAPI -v q -nologo`
Expected: `0 Error(s)`

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "FullyQualifiedName~Entrax" -nologo`
Expected: PASS (todas las de la carpeta `Entrax`).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Program.cs \
        MLMConquerorGlobalEdition.SignupAPI/appsettings.json \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxConfiguracionTests.cs
git commit -m "Entrax en SignupAPI: guarda de arranque, servicios, barrido recurrente, endpoints del fake solo en Development y configuracion base apagada y sin secretos"
```

---

### Task 17: Componente único del QR (`SharedComponents.Lite/Payments/EntraxQrPayment.razor`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents.Lite/Payments/EntraxQrPayment.razor`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/EntraxQrPaymentTests.cs`

> Va en `SharedComponents.Lite` porque lo descarga el alta pública (WebAssembly) y no necesita Syncfusion. No inyecta nada: los textos (inglés por defecto, como `CryptoDepositPicker`; decisión H13), la consulta del estado y lo que pasa al pagar entran por parámetro. Las pruebas bUnit van en `BizCenter.Tests`, que ya prueba `CryptoDepositPicker` y tiene bUnit.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Bunit;
using Microsoft.AspNetCore.Components;
using MLMConquerorGlobalEdition.SharedComponents.Lite.Payments;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El QR de rublos por SBP del alta: lo pinta, cuenta hacia atrás, pregunta el estado cada pocos
/// segundos y, según lo que diga el servidor, avisa a la página o le ofrece un QR nuevo u otro método.
/// </summary>
public class EntraxQrPaymentTests : BunitContext
{
    private static readonly TimeSpan Rapido = TimeSpan.FromMilliseconds(10);

    private static EntraxSignupPaymentDto Pago(string referencia = "ref-1") => new()
    {
        PaymentRef = referencia, QrImage = "data:image/png;base64,iVBORw0KGgo=", QrLink = "https://qr.nspk.ru/AS100",
        AmountUsd = 80m, AmountRub = 7640m, ExchangeRate = 95.5m, SecondsRemaining = 900
    };

    private static Func<CancellationToken, Task<EntraxSignupStatusDto?>> Dice(string estado)
        => _ => Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado });

    [Fact]
    public void Muestra_el_QR_el_enlace_seguro_el_importe_en_rublos_y_la_cuenta_atras()
    {
        var cut = Render<EntraxQrPayment>(p => p.Add(c => c.Payment, Pago()));

        cut.Find("img.entrax-qr-image").GetAttribute("src").Should().Be("data:image/png;base64,iVBORw0KGgo=");
        var enlace = cut.Find("a.entrax-link");
        enlace.GetAttribute("href").Should().Be("https://qr.nspk.ru/AS100");
        enlace.GetAttribute("rel").Should().Contain("noopener").And.Contain("noreferrer");
        cut.Find(".entrax-amount").TextContent.Should().Contain("7,640.00").And.Contain("80.00");
        cut.Find(".entrax-countdown").TextContent.Should().MatchRegex("1[45]:[0-5][0-9]");
    }

    [Fact]
    public void Sin_imagen_ni_enlace_no_pinta_nada_roto()
    {
        var pago = Pago();
        pago.QrImage = null;
        pago.QrLink  = null;

        var cut = Render<EntraxQrPayment>(p => p.Add(c => c.Payment, pago));

        cut.FindAll("img").Should().BeEmpty();
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void Cuando_el_servidor_dice_pagado_avisa_a_la_pagina_y_lo_dice()
    {
        var pagado = false;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(EntraxSignupStatuses.Paid))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido)
            .Add(c => c.OnPaid, EventCallback.Factory.Create(this, () => pagado = true)));

        cut.WaitForAssertion(() => pagado.Should().BeTrue(), TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Payment received"));
    }

    [Theory]
    [InlineData(EntraxSignupStatuses.Expired)]
    [InlineData(EntraxSignupStatuses.Failed)]
    public void Caducado_o_fallido_ofrece_un_QR_nuevo_u_otro_metodo(string estado)
    {
        var reintentos = 0;
        var otro = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(estado))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido)
            .Add(c => c.OnRetry, EventCallback.Factory.Create(this, () => reintentos++))
            .Add(c => c.OnChooseAnother, EventCallback.Factory.Create(this, () => otro++)));

        cut.WaitForAssertion(() => cut.Find(".entrax-new-qr"), TimeSpan.FromSeconds(3));
        cut.Find(".entrax-new-qr").Click();
        cut.Find(".entrax-other").Click();

        reintentos.Should().Be(1);
        otro.Should().Be(1);
        cut.FindAll("img").Should().BeEmpty("un QR caducado no se vuelve a enseñar.");
    }

    [Fact]
    public void A_revisar_no_ofrece_pagar_otra_vez()
    {
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(EntraxSignupStatuses.ToReview))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("need to review it"), TimeSpan.FromSeconds(3));
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
    }

    [Fact]
    public void Un_fallo_de_red_no_cambia_nada_y_se_sigue_preguntando()
    {
        var llamadas = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, _ =>
            {
                Interlocked.Increment(ref llamadas);
                return Task.FromException<EntraxSignupStatusDto?>(new HttpRequestException("caido"));
            })
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));

        cut.WaitForAssertion(() => llamadas.Should().BeGreaterThan(1), TimeSpan.FromSeconds(3));
        cut.Markup.Should().Contain("Waiting for your payment");
    }

    [Fact]
    public void Un_QR_nuevo_vuelve_a_esperar()
    {
        var estado = EntraxSignupStatuses.Expired;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago("ref-1"))
            .Add(c => c.CheckStatus, _ => Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado }))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));
        cut.WaitForAssertion(() => cut.Find(".entrax-new-qr"), TimeSpan.FromSeconds(3));

        estado = EntraxSignupStatuses.AwaitingPayment;
        cut.Render(p => p.Add(c => c.Payment, Pago("ref-2")));

        cut.WaitForAssertion(() => cut.Find("img.entrax-qr-image"));
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter EntraxQrPaymentTests -nologo`
Expected: FAIL de compilación (`EntraxQrPayment` no existe).

- [ ] **Step 3: Crear `EntraxQrPayment.razor`**

```razor
@using System.Diagnostics
@using System.Globalization
@using MLMConquerorGlobalEdition.SharedKernel.Billing
@implements IAsyncDisposable
@*
    Pago del alta en rublos por SBP (Entrax): QR, enlace para abrirlo en el móvil, importe en rublos,
    cuenta atrás y consulta del estado cada pocos segundos. Una sola pieza para Signup.razor y
    MemberJoin.razor (y para la compra de tokens si algún día se ofrece Entrax ahí).

    No inyecta nada, como CryptoDepositPicker: corre en el WebAssembly del alta. Lo específico entra
    por parámetro: cómo preguntar el estado (CheckStatus), qué hacer al pagar (OnPaid), al pedir un
    QR nuevo (OnRetry) o al elegir otro método (OnChooseAnother), y los textos (inglés por defecto,
    que es el idioma del alta; la página que tenga localizador pasa los suyos).

    La activación NO depende de esta pantalla: el servidor liquida por callback y por barrido aunque
    la persona cierre la pestaña. Esto solo enseña lo que el servidor ya sabe.

    La cuenta atrás usa los segundos que calculó el servidor y un cronómetro local, nunca una hora:
    el navegador puede estar en otro huso.
*@

<div class="entrax-qr text-center" data-state="@_state">
    @if (_state == EntraxSignupStatuses.Paid)
    {
        <i class="ri-checkbox-circle-fill fs-1 text-success"></i>
        <p class="mt-2 mb-0 fw-semibold">@PaidText</p>
    }
    else if (_state == EntraxSignupStatuses.ToReview)
    {
        <i class="ri-error-warning-line fs-1 text-warning"></i>
        <p class="mt-2 mb-0">@ReviewText</p>
    }
    else if (_state is EntraxSignupStatuses.Expired or EntraxSignupStatuses.Failed)
    {
        <p class="mb-3">@(_state == EntraxSignupStatuses.Expired ? ExpiredText : FailedText)</p>
        <div class="d-flex flex-wrap gap-2 justify-content-center">
            <button type="button" class="btn btn-enroll entrax-new-qr" disabled="@Busy" @onclick="OnRetry">@NewQrText</button>
            <button type="button" class="btn btn-outline-secondary entrax-other" disabled="@Busy" @onclick="OnChooseAnother">@OtherMethodText</button>
        </div>
    }
    else
    {
        <div class="entrax-waiting">
            <h5 class="mb-2">@TitleText</h5>
            <p class="small text-muted mb-3">@ScanText</p>
            @if (!string.IsNullOrEmpty(Payment.QrImage))
            {
                <img class="entrax-qr-image img-fluid mb-3" src="@Payment.QrImage" alt="@QrAltText" width="240" height="240" />
            }
            @if (!string.IsNullOrEmpty(Payment.QrLink))
            {
                <p>
                    <a class="btn btn-outline-primary entrax-link" href="@Payment.QrLink" target="_blank" rel="noopener noreferrer">@OpenLinkText</a>
                </p>
            }
            <p class="fw-semibold mb-1 entrax-amount">
                @string.Format(AmountText,
                    Payment.AmountRub.ToString("N2", CultureInfo.InvariantCulture),
                    Payment.AmountUsd.ToString("N2", CultureInfo.InvariantCulture))
            </p>
            <p class="small text-muted mb-1 entrax-countdown">
                @(Remaining > 0 ? string.Format(ExpiresInText, FormatRemaining(Remaining)) : ExpiringText)
            </p>
            <p class="small text-muted mb-0">
                <span class="spinner-border spinner-border-sm me-1"></span>@WaitingText
            </p>
        </div>
    }
</div>

@code {
    [Parameter, EditorRequired] public EntraxSignupPaymentDto Payment { get; set; } = default!;

    /// <summary>Pregunta el estado al servidor. Null = no se pregunta (solo se pinta).</summary>
    [Parameter] public Func<CancellationToken, Task<EntraxSignupStatusDto?>>? CheckStatus { get; set; }

    [Parameter] public EventCallback OnPaid          { get; set; }
    [Parameter] public EventCallback OnRetry         { get; set; }
    [Parameter] public EventCallback OnChooseAnother { get; set; }

    /// <summary>La página está pidiendo un QR nuevo: los botones se deshabilitan.</summary>
    [Parameter] public bool Busy { get; set; }

    [Parameter] public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    [Parameter] public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(1);

    [Parameter] public string TitleText       { get; set; } = "Pay in rubles with SBP";
    [Parameter] public string ScanText        { get; set; } = "Scan the QR code with your bank's app, or open the link on your phone.";
    [Parameter] public string QrAltText       { get; set; } = "SBP payment QR code";
    [Parameter] public string OpenLinkText    { get; set; } = "Open payment link";
    /// <summary><c>{0}</c> rublos, <c>{1}</c> dólares.</summary>
    [Parameter] public string AmountText      { get; set; } = "Amount to pay: {0} RUB (USD {1})";
    /// <summary><c>{0}</c> mm:ss.</summary>
    [Parameter] public string ExpiresInText   { get; set; } = "This QR code expires in {0}.";
    [Parameter] public string ExpiringText    { get; set; } = "This QR code is about to expire.";
    [Parameter] public string WaitingText     { get; set; } = "Waiting for your payment…";
    [Parameter] public string PaidText        { get; set; } = "Payment received! Your account is active — sign in to get started.";
    [Parameter] public string ExpiredText     { get; set; } = "This QR code expired before we received a payment. If you already paid, don't pay again: your account activates as soon as the payment reaches us.";
    [Parameter] public string FailedText      { get; set; } = "The payment didn't go through. You can try again or choose another payment method.";
    [Parameter] public string ReviewText      { get; set; } = "We received your payment but need to review it before activating your account. Our support team will contact you.";
    [Parameter] public string NewQrText       { get; set; } = "Generate a new QR code";
    [Parameter] public string OtherMethodText { get; set; } = "Choose another payment method";

    private string _state = EntraxSignupStatuses.AwaitingPayment;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private CancellationTokenSource? _cts;
    private string? _paymentRef;

    private int Remaining => Math.Max(0, Payment.SecondsRemaining - (int)_elapsed.Elapsed.TotalSeconds);

    protected override void OnParametersSet()
    {
        // Un QR nuevo (reintento) reinicia el estado, la cuenta atrás y la consulta.
        if (_paymentRef == Payment.PaymentRef) return;

        _paymentRef = Payment.PaymentRef;
        _state      = EntraxSignupStatuses.AwaitingPayment;
        _elapsed.Restart();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TickInterval);
            var sinceCheck = TimeSpan.Zero;
            while (_state == EntraxSignupStatuses.AwaitingPayment && await timer.WaitForNextTickAsync(ct))
            {
                sinceCheck += TickInterval;
                if (CheckStatus is not null && sinceCheck >= PollInterval)
                {
                    sinceCheck = TimeSpan.Zero;
                    await CheckAsync(ct);
                }
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        EntraxSignupStatusDto? status;
        try
        {
            status = await CheckStatus!(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Un fallo de red no cambia nada: se vuelve a preguntar en el siguiente turno.
            return;
        }

        if (status is null || status.Status == _state || !EntraxSignupStatuses.All.Contains(status.Status))
            return;

        _state = status.Status;
        if (_state == EntraxSignupStatuses.Paid)
            await InvokeAsync(() => OnPaid.InvokeAsync());
    }

    private static string FormatRemaining(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 4: Ejecutar las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "EntraxQrPaymentTests|CryptoDepositPickerTests" -nologo`
Expected: PASS.

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupsClientLigeroTests -nologo`
Expected: PASS (la librería ligera sigue sin Syncfusion y solo depende de SharedKernel).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents.Lite/Payments/EntraxQrPayment.razor \
        MLMConquerorGlobalEdition.BizCenter.Tests/EntraxQrPaymentTests.cs
git commit -m "Componente unico del QR de rublos por SBP en la libreria ligera: QR, enlace, importe en rublos, cuenta atras y consulta del estado"
```

---

### Task 18: Pestaña "Rubles (SBP)" en el alta (`Signup.razor` y `MemberJoin.razor`)

> **Desviación respecto a lo implementado (revisión de las tareas 17-18).** El código de los pasos 6 y 7 de abajo
> (estado `_entraxPayment`, `CheckEntraxStatusAsync`, `OnEntraxPaid`, `RetryEntraxAsync`, `ChooseAnotherPaymentMethod`)
> quedó duplicado en las dos páginas y se extrajo a `Signups.Client/Payments/EntraxSignupFlow.razor`, que envuelve el
> formulario (`ChildContent`) y lo sustituye por `EntraxQrPayment` cuando hay un QR. Las páginas solo le pasan
> `SignupId` y escuchan `OnCompleted(bool alreadyPaid)`, `OnChooseAnother` y `OnError(string)`; piden el QR con
> `await _entraxFlow.StartAsync()`. Además:
>
> - `EntraxSignupApi.StartAsync` devuelve `EntraxStartResult` (`Started`, `AlreadySettled`, `InProgress`, `QrAlreadyActive`,
>   `Failed`) y reintenta solo ante `ENTRAX_IN_PROGRESS`.
> - El backend responde `SIGNUP_ALREADY_SETTLED` (409) a `complete` con cualquier método y a `select-products` cuando el
>   pedido ya está `Completed`, y `ENTRAX_QR_ALREADY_ACTIVE` (409) al pedir un QR con otro vivo en otra ventana
>   (no se devuelve el mismo QR: la imagen no se guarda). Las páginas muestran "ya pagada, inicia sesión" con ese primer código en
>   cualquier método.
> - Con el alta creada (`_signupId` no nulo) se bloquean correo, contraseña, país y nivel, se avisa con una nota, el correo
>   de la pantalla final es el que devolvió la fase 1, `EntraxOffered` usa el país guardado en la fase 1 y
>   `select-products` no se repite.
> - `EntraxQrPayment`: suelo de 5 s en `PollInterval`, parámetro `Time` (`TimeProvider`) para las pruebas, y con la cuenta a
>   cero sin cerrar por el servidor se oculta el QR y se sigue preguntando.
> - `Signups.Tests` incorpora bUnit para probar el flujo.

**Files:**
- Create: `MLMConquerorGlobalEdition.Signups.Client/Payments/EntraxSignupApi.cs`
- Modify: `MLMConquerorGlobalEdition.Signups.Client/_Imports.razor`
- Modify: `MLMConquerorGlobalEdition.Signups.Client/Pages/Signup.razor`
- Modify: `MLMConquerorGlobalEdition.Signups.Client/Pages/MemberJoin.razor`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupApiTests.cs`

> `Signups.Tests` ya ve `Signups.Client` a través de `MLMConquerorGlobalEdition.Signups`.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Net;
using System.Text;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.Signups.Client.Payments;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>Las tres llamadas del alta con Entrax que comparten Signup.razor y MemberJoin.razor.</summary>
public class EntraxSignupApiTests
{
    private sealed class Api(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;
        public string? Cuerpo;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticion = r;
            Cuerpo = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            return responder(r);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpClient Cliente(HttpMessageHandler h) => new(h) { BaseAddress = new Uri("https://signup.pruebas/") };

    [Fact]
    public async Task Lee_los_paises_donde_se_ofrece()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"entraxCountries":["RU"]}}"""));

        (await EntraxSignupApi.GetCountriesAsync(Cliente(api))).Should().Equal("RU");
        api.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/payment-options");
    }

    [Fact]
    public async Task Si_no_se_puede_leer_no_se_ofrece()
        => (await EntraxSignupApi.GetCountriesAsync(Cliente(new Api(_ => throw new HttpRequestException("caido")))))
            .Should().BeEmpty();

    [Fact]
    public async Task Completa_el_alta_con_Entrax_y_devuelve_el_QR()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK,
            """{"success":true,"data":{"signupId":"ORD 1","email":"a@x.com","entrax":{"paymentRef":"ref-1","qrLink":"https://qr.nspk.ru/A","amountRub":7640,"amountUsd":80,"secondsRemaining":900}}}"""));

        var (qr, error) = await EntraxSignupApi.StartAsync(Cliente(api), "ORD 1");

        error.Should().BeNull();
        qr!.PaymentRef.Should().Be("ref-1");
        qr.AmountRub.Should().Be(7640m);
        api.Peticion!.Method.Should().Be(HttpMethod.Post);
        api.Peticion.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/ORD%201/complete");
        api.Cuerpo.Should().Contain("\"paymentMethod\":\"Entrax\"", "PostAsJsonAsync usa camelCase; SignupAPI enlaza sin distinguir mayúsculas.");
    }

    [Fact]
    public async Task Un_error_del_alta_llega_como_texto()
    {
        var api = new Api(_ => Json(HttpStatusCode.BadGateway,
            """{"success":false,"errorCode":"ENTRAX_UNAVAILABLE","errors":["We couldn't create the SBP payment right now."]}"""));

        var (qr, error) = await EntraxSignupApi.StartAsync(Cliente(api), "ORD-1");

        qr.Should().BeNull();
        error.Should().Be("We couldn't create the SBP payment right now.");
    }

    [Fact]
    public async Task Pregunta_el_estado_con_las_dos_referencias()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"status":"Paid","secondsRemaining":0}}"""));

        var s = await EntraxSignupApi.GetStatusAsync(Cliente(api), "ORD-1", "ref/1", default);

        s!.Status.Should().Be(EntraxSignupStatuses.Paid);
        api.Peticion!.RequestUri!.PathAndQuery.Should().Be("/api/v1/signups/ORD-1/entrax-status?ref=ref%2F1");
    }

    [Fact]
    public async Task Un_estado_que_no_existe_es_null()
        => (await EntraxSignupApi.GetStatusAsync(Cliente(new Api(_ => Json(HttpStatusCode.NotFound, "{}"))), "ORD-1", "x", default))
            .Should().BeNull();
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupApiTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `Signups.Client/Payments/EntraxSignupApi.cs`**

```csharp
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Signups.Client.Payments;

/// <summary>
/// Las llamadas del alta con Entrax (rublos por SBP) que comparten Signup.razor y MemberJoin.razor,
/// escritas una vez. Corren en el navegador (WebAssembly) contra SignupAPI.
/// </summary>
public static class EntraxSignupApi
{
    /// <summary>Países donde se ofrece. Si no se puede leer, ninguno: la pestaña no aparece.</summary>
    public static async Task<IReadOnlyList<string>> GetCountriesAsync(HttpClient api)
    {
        try
        {
            var r = await api.GetFromJsonAsync<Wrapper<SignupPaymentOptionsDto>>("api/v1/signups/payment-options");
            return r?.Data?.EntraxCountries ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Completa el alta con Entrax: devuelve el QR o el texto del error.</summary>
    public static async Task<(EntraxSignupPaymentDto? Payment, string? Error)> StartAsync(HttpClient api, string signupId)
    {
        var r = await api.PostAsJsonAsync($"api/v1/signups/{Uri.EscapeDataString(signupId)}/complete",
            new { PaymentMethod = "Entrax" });
        var b = await r.Content.ReadFromJsonAsync<Wrapper<StartData>>();

        return b?.Success == true && b.Data?.Entrax is not null
            ? (b.Data.Entrax, null)
            : (null, b?.Message ?? b?.Errors?.FirstOrDefault() ?? "Could not create the SBP payment.");
    }

    /// <summary>Estado del pago, o null si el servidor no lo reconoce.</summary>
    public static async Task<EntraxSignupStatusDto?> GetStatusAsync(
        HttpClient api, string signupId, string paymentRef, CancellationToken ct)
    {
        using var r = await api.GetAsync(
            $"api/v1/signups/{Uri.EscapeDataString(signupId)}/entrax-status?ref={Uri.EscapeDataString(paymentRef)}", ct);
        if (!r.IsSuccessStatusCode) return null;

        var b = await r.Content.ReadFromJsonAsync<Wrapper<EntraxSignupStatusDto>>(ct);
        return b?.Data;
    }

    private sealed record Wrapper<T>(bool Success, T? Data, string? Message, List<string>? Errors);

    private sealed record StartData(EntraxSignupPaymentDto? Entrax);
}
```

- [ ] **Step 4: `_Imports.razor`** — añadir al final:

```razor
@* Llamadas del alta con Entrax, escritas una vez para Signup y MemberJoin. *@
@using MLMConquerorGlobalEdition.Signups.Client.Payments
```

- [ ] **Step 5: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter EntraxSignupApiTests -nologo`
Expected: PASS.

- [ ] **Step 6: `Signup.razor`**

6a. Pantalla del QR. Sustituir (líneas ~114-117):

```razor
        }
        else
        {
        @if (_productsLoading)
```

por:

```razor
        }
        else if (_entraxPayment is not null)
        {
            @* Rublos por SBP (Plan H): el alta está creada y el pedido reservado; se espera el cobro. *@
            <div class="section-card">
                <div class="section-body py-4">
                    <EntraxQrPayment Payment="_entraxPayment"
                                     CheckStatus="CheckEntraxStatusAsync"
                                     Busy="_busy"
                                     OnPaid="OnEntraxPaid"
                                     OnRetry="RetryEntraxAsync"
                                     OnChooseAnother="ChooseAnotherPaymentMethod" />
                </div>
            </div>
        }
        else
        {
        @if (_productsLoading)
```

6b. Pestaña. Sustituir:

```razor
                            <i class="ri-bit-coin-line me-1"></i>Crypto
                        </button>
                    </li>
                </ul>
```

por:

```razor
                            <i class="ri-bit-coin-line me-1"></i>Crypto
                        </button>
                    </li>
                    @if (EntraxOffered)
                    {
                        <li class="nav-item">
                            <button class="nav-link @(_paymentTab == "entrax" ? "active" : "")" type="button"
                                    @onclick='() => _paymentTab = "entrax"'>
                                <i class="ri-qr-code-line me-1"></i>Rubles (SBP)
                            </button>
                        </li>
                    }
                </ul>
```

6c. Contenido de la pestaña. Sustituir:

```razor
                <hr class="my-4" />

                <div class="captcha-placeholder d-flex align-items-center gap-2 mb-3">
```

por:

```razor
                @if (_paymentTab == "entrax" && EntraxOffered)
                {
                    <div class="py-4">
                        <p class="small text-muted mb-0">
                            Pay in rubles through the Russian Fast Payment System (SBP). After you click Join Now we'll show
                            a QR code for @AmountToday.ToString("C") converted to rubles at the current rate. Your account
                            activates as soon as the payment arrives.
                        </p>
                    </div>
                }

                <hr class="my-4" />

                <div class="captcha-placeholder d-flex align-items-center gap-2 mb-3">
```

6d. Botón. En el `disabled` del botón *Join Now*, sustituir:

```razor
(_paymentTab == "crypto" && _cryptoDeposit is null))"
```

por:

```razor
(_paymentTab == "crypto" && _cryptoDeposit is null) || (_paymentTab == "entrax" && !EntraxOffered))"
```

6e. Estado. Debajo de `private string _paymentTab    = "card";` añadir:

```csharp

    // Rublos por SBP con Entrax (Plan H): países donde se ofrece y el QR en curso.
    private IReadOnlyList<string> _entraxCountries = [];
    private EntraxSignupPaymentDto? _entraxPayment;
    private bool EntraxOffered => _entraxCountries.Contains(_countryIso2);
```

6f. Carga. En `OnInitializedAsync`, sustituir:

```csharp
        catch { _cryptoDeposits = new(); }
```

por:

```csharp
        catch { _cryptoDeposits = new(); }

        // Rublos por SBP (Plan H): países con Entrax encendido. Si falla, la pestaña no aparece.
        _entraxCountries = await EntraxSignupApi.GetCountriesAsync(api);
```

6g. La fase 1 no se repite. Sustituir:

```csharp
            var r1 = await api.PostAsJsonAsync("api/v1/signups/ambassador", new
```

por:

```csharp
            // Un alta ya creada (p. ej. tras un QR de Entrax caducado y "otro método") no se vuelve a
            // crear: el correo ya existe. Se sigue desde la selección de productos.
            if (_signupId is null)
            {
            var r1 = await api.PostAsJsonAsync("api/v1/signups/ambassador", new
```

y sustituir:

```csharp
            _signupId = b1.Data.SignupId;
```

por:

```csharp
            _signupId = b1.Data.SignupId;
            }
```

6h. La rama Entrax. Sustituir:

```csharp
            var paymentTab = _paymentTab;
            object payment;
```

por:

```csharp
            var paymentTab = _paymentTab;

            // Rublos por SBP (Plan H): el servidor emite el QR y la pantalla espera el cobro. Todavía no
            // hay _success: la cuenta se activa cuando Entrax confirma, no al pulsar.
            if (paymentTab == "entrax")
            {
                var (qr, entraxError) = await EntraxSignupApi.StartAsync(api, _signupId!);
                if (qr is null) { _error = entraxError ?? "Could not create the SBP payment."; return; }
                _completedEmail = _email;
                _entraxPayment  = qr;
                return;
            }

            object payment;
```

6i. Los manejadores. Justo **antes** de `    // ── Local models ──` añadir:

```csharp
    // ── Rublos por SBP (Plan H) ──────────────────────────────────────────────
    private Task<EntraxSignupStatusDto?> CheckEntraxStatusAsync(CancellationToken ct)
        => _signupId is null || _entraxPayment is null
            ? Task.FromResult<EntraxSignupStatusDto?>(null)
            : EntraxSignupApi.GetStatusAsync(HttpFactory.CreateClient("SignupsInternal"), _signupId, _entraxPayment.PaymentRef, ct);

    /// <summary>El servidor ya activó la cuenta: se enseña el "bienvenido" de siempre, que manda a iniciar sesión.</summary>
    private void OnEntraxPaid()
    {
        _entraxPayment          = null;
        _completedPendingCrypto = false;
        _success                = true;
    }

    private async Task RetryEntraxAsync()
    {
        if (_signupId is null) return;
        _error = string.Empty;
        _busy  = true;
        try
        {
            var (qr, entraxError) = await EntraxSignupApi.StartAsync(HttpFactory.CreateClient("SignupsInternal"), _signupId);
            if (qr is not null) _entraxPayment = qr;
            else _error = entraxError ?? "Could not create the SBP payment.";
        }
        catch (Exception ex) { _error = $"Connection error: {ex.Message}"; }
        finally { _busy = false; }
    }

    private void ChooseAnotherPaymentMethod()
    {
        _entraxPayment = null;
        _paymentTab    = "card";
    }

```

- [ ] **Step 7: `MemberJoin.razor`** — los mismos nueve cambios, con estas cadenas exactas:

7a. Sustituir:

```razor
        }
        else
        {
        @if (_productsLoading)
```

por:

```razor
        }
        else if (_entraxPayment is not null)
        {
            @* Rublos por SBP (Plan H): el alta está creada y el pedido reservado; se espera el cobro. *@
            <div class="section-card">
                <div class="section-body py-4">
                    <EntraxQrPayment Payment="_entraxPayment"
                                     CheckStatus="CheckEntraxStatusAsync"
                                     Busy="_busy"
                                     OnPaid="OnEntraxPaid"
                                     OnRetry="RetryEntraxAsync"
                                     OnChooseAnother="ChooseAnotherPaymentMethod" />
                </div>
            </div>
        }
        else
        {
        @if (_productsLoading)
```

7b. Sustituir:

```razor
                            <i class="ri-bit-coin-line me-1"></i>Crypto
                        </button>
                    </li>
                </ul>
```

por:

```razor
                            <i class="ri-bit-coin-line me-1"></i>Crypto
                        </button>
                    </li>
                    @if (EntraxOffered)
                    {
                        <li class="nav-item">
                            <button class="nav-link @(_paymentTab == "entrax" ? "active" : "")" type="button"
                                    @onclick='() => _paymentTab = "entrax"'>
                                <i class="ri-qr-code-line me-1"></i>Rubles (SBP)
                            </button>
                        </li>
                    }
                </ul>
```

7c. Sustituir:

```razor
                <hr class="my-4" />

                <div class="captcha-placeholder d-flex align-items-center gap-2 mb-3">
```

por:

```razor
                @if (_paymentTab == "entrax" && EntraxOffered)
                {
                    <div class="py-4">
                        <p class="small text-muted mb-0">
                            Pay in rubles through the Russian Fast Payment System (SBP). After you click Join Now we'll show
                            a QR code for @AmountToday.ToString("C") converted to rubles at the current rate. Your membership
                            activates as soon as the payment arrives.
                        </p>
                    </div>
                }

                <hr class="my-4" />

                <div class="captcha-placeholder d-flex align-items-center gap-2 mb-3">
```

7d. Sustituir:

```razor
(_paymentTab == "crypto" && _cryptoDeposit is null))"
```

por:

```razor
(_paymentTab == "crypto" && _cryptoDeposit is null) || (_paymentTab == "entrax" && !EntraxOffered))"
```

7e. Debajo de `private string _paymentTab     = "card";` (con cinco espacios antes del `=`):

```csharp

    // Rublos por SBP con Entrax (Plan H): países donde se ofrece y el QR en curso.
    private IReadOnlyList<string> _entraxCountries = [];
    private EntraxSignupPaymentDto? _entraxPayment;
    private bool EntraxOffered => _entraxCountries.Contains(_countryIso2);
```

7f. Sustituir:

```csharp
        catch { _cryptoDeposits = new(); }
```

por:

```csharp
        catch { _cryptoDeposits = new(); }

        // Rublos por SBP (Plan H): países con Entrax encendido. Si falla, la pestaña no aparece.
        _entraxCountries = await EntraxSignupApi.GetCountriesAsync(api);
```

7g. Sustituir:

```csharp
            var r1 = await api.PostAsJsonAsync("api/v1/signups/member", new
```

por:

```csharp
            // Un alta ya creada (p. ej. tras un QR de Entrax caducado y "otro método") no se vuelve a
            // crear: el correo ya existe. Se sigue desde la selección de productos.
            if (_signupId is null)
            {
            var r1 = await api.PostAsJsonAsync("api/v1/signups/member", new
```

y sustituir:

```csharp
            _signupId = b1.Data.SignupId;
```

por:

```csharp
            _signupId = b1.Data.SignupId;
            }
```

7h. Sustituir:

```csharp
            var paymentTab = _paymentTab;
            object payment;
```

por:

```csharp
            var paymentTab = _paymentTab;

            // Rublos por SBP (Plan H): el servidor emite el QR y la pantalla espera el cobro. Todavía no
            // hay _success: la cuenta se activa cuando Entrax confirma, no al pulsar.
            if (paymentTab == "entrax")
            {
                var (qr, entraxError) = await EntraxSignupApi.StartAsync(api, _signupId!);
                if (qr is null) { _error = entraxError ?? "Could not create the SBP payment."; return; }
                _completedEmail = _email;
                _entraxPayment  = qr;
                return;
            }

            object payment;
```

7i. Justo **antes** de `    // ── Local models ──` añadir:

```csharp
    // ── Rublos por SBP (Plan H) ──────────────────────────────────────────────
    private Task<EntraxSignupStatusDto?> CheckEntraxStatusAsync(CancellationToken ct)
        => _signupId is null || _entraxPayment is null
            ? Task.FromResult<EntraxSignupStatusDto?>(null)
            : EntraxSignupApi.GetStatusAsync(HttpFactory.CreateClient("SignupsInternal"), _signupId, _entraxPayment.PaymentRef, ct);

    /// <summary>El servidor ya activó la cuenta: se enseña el "bienvenido" de siempre, que manda a iniciar sesión.</summary>
    private void OnEntraxPaid()
    {
        _entraxPayment          = null;
        _completedPendingCrypto = false;
        _success                = true;
    }

    private async Task RetryEntraxAsync()
    {
        if (_signupId is null) return;
        _error = string.Empty;
        _busy  = true;
        try
        {
            var (qr, entraxError) = await EntraxSignupApi.StartAsync(HttpFactory.CreateClient("SignupsInternal"), _signupId);
            if (qr is not null) _entraxPayment = qr;
            else _error = entraxError ?? "Could not create the SBP payment.";
        }
        catch (Exception ex) { _error = $"Connection error: {ex.Message}"; }
        finally { _busy = false; }
    }

    private void ChooseAnotherPaymentMethod()
    {
        _entraxPayment = null;
        _paymentTab    = "card";
    }

```

- [ ] **Step 8: Compilar el alta y vigilar la librería ligera**

Run: `dotnet build MLMConquerorGlobalEdition.Signups -v q -nologo`
Expected: `0 Error(s)` (compila también `Signups.Client`).

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SignupsClientLigeroTests|EntraxSignupApiTests|ConfiguracionEnganosaTests" -nologo`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Signups.Client/Payments/EntraxSignupApi.cs \
        MLMConquerorGlobalEdition.Signups.Client/_Imports.razor \
        MLMConquerorGlobalEdition.Signups.Client/Pages/Signup.razor \
        MLMConquerorGlobalEdition.Signups.Client/Pages/MemberJoin.razor \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupApiTests.cs
git commit -m "Pestana Rubles (SBP) en el alta de embajador y de miembro con el componente unico del QR; el alta ya creada no se repite al cambiar de metodo"
```

---

### Task 19: Admin — lista de pagos de Entrax y *Re-check* (AdminAPI)

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminEntraxSignupsController.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CryptoSignups/AdminEntraxSignupsControllerTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CryptoSignups;

/// <summary>
/// Pagos de Entrax del alta en el admin: los que esperan, los que hay que revisar y los recientes;
/// Re-check se reenvía a SignupAPI con el Bearer del administrador (SignupAPI vuelve a comprobar el rol).
/// </summary>
public class AdminEntraxSignupsControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 10, 0, 0);

    private sealed class Captura : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticion = r;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":{\"outcome\":\"Activated\"}}",
                    System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Caido : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(new HttpRequestException("caido"));
    }

    private static Mock<IHttpClientFactory> Fabrica(HttpMessageHandler h)
    {
        var f = new Mock<IHttpClientFactory>();
        f.Setup(x => x.CreateClient("SignupApi")).Returns(new HttpClient(h) { BaseAddress = new Uri("https://signup.pruebas/") });
        return f;
    }

    private static AdminEntraxSignupsController Build(AppDbContext db, IHttpClientFactory? f = null, string? bearer = null)
    {
        var ctx = new DefaultHttpContext();
        if (bearer is not null) ctx.Request.Headers.Authorization = bearer;
        return new AdminEntraxSignupsController(db, f ?? Mock.Of<IHttpClientFactory>())
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    private static async Task SeedAsync(AppDbContext db, string id, EntraxPaymentStatus status, string memberId = "AMB-RU-1")
    {
        if (!db.MemberProfiles.Any())
            db.MemberProfiles.Add(new MemberProfile
            {
                MemberId = "AMB-RU-1", Email = "ivan@example.com", FirstName = "Ivan", LastName = "Petrov",
                MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Pending, EnrollDate = Now,
                Country = "RU", CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
            });
        db.EntraxPayments.Add(new EntraxPayment
        {
            Id = id, OrderId = $"ORD-{id}", MemberId = memberId, ExternalOrderId = $"SIGNUP-ORD-{id}-1",
            ExternalPaymentId = Guid.NewGuid().ToString(), Status = status, AmountUsd = 80m, AmountRub = 7640m,
            FailureCode = status == EntraxPaymentStatus.ToReview ? "AMOUNT_TOO_LOW" : null,
            CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
        });
        await db.SaveChangesAsync();
    }

    private static IEnumerable<AdminEntraxSignupsController.EntraxSignupRowDto> Filas(IActionResult r)
        => ((OkObjectResult)r).Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminEntraxSignupsController.EntraxSignupRowDto>>>().Which.Data!;

    [Fact]
    public void Exige_los_roles_de_cripto_y_nada_es_anonimo()
    {
        typeof(AdminEntraxSignupsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.CryptoPaymentApprovers);
        typeof(AdminEntraxSignupsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().OnlyContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() == null);
    }

    [Fact]
    public async Task Por_defecto_muestra_los_que_esperan_pago()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);
        await SeedAsync(db, "p2", EntraxPaymentStatus.ToReview);
        await SeedAsync(db, "p3", EntraxPaymentStatus.Paid);

        var fila = Filas(await Build(db).List()).Should().ContainSingle().Subject;

        fila.Id.Should().Be("p1");
        fila.Status.Should().Be("AwaitingPayment");
        fila.FullName.Should().Be("Ivan Petrov");
        fila.Country.Should().Be("RU");
    }

    [Fact]
    public async Task A_revisar_no_pierde_un_pago_sin_perfil()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.ToReview, memberId: "AMB-BORRADO");

        var fila = Filas(await Build(db).List(AdminEntraxSignupsController.ViewReview)).Should()
            .ContainSingle("un pago a revisar tiene dinero en juego: no puede desaparecer de la lista.").Subject;

        fila.Reason.Should().Be("AMOUNT_TOO_LOW");
        fila.FullName.Should().BeNull();
    }

    [Fact]
    public async Task Recientes_muestra_todos()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);
        await SeedAsync(db, "p2", EntraxPaymentStatus.Paid);

        Filas(await Build(db).List(AdminEntraxSignupsController.ViewRecent)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Re_check_se_reenvia_a_SignupAPI_con_el_Bearer_del_administrador()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);
        var captura = new Captura();

        var r = await Build(db, Fabrica(captura).Object, "Bearer token-admin").Recheck("p1");

        captura.Peticion!.Method.Should().Be(HttpMethod.Post);
        captura.Peticion.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/entrax/p1/recheck");
        captura.Peticion.Headers.Authorization!.ToString().Should().Be("Bearer token-admin");
        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Re_check_de_un_pago_que_no_existe_no_llama_a_nadie()
    {
        await using var db = InMemoryDbHelper.Create();
        var fabrica = new Mock<IHttpClientFactory>(MockBehavior.Strict);

        (await Build(db, fabrica.Object).Recheck("no-existe")).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Con_SignupAPI_caido_es_502()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);

        var r = await Build(db, Fabrica(new Caido()).Object).Recheck("p1");

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminEntraxSignupsControllerTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `AdminEntraxSignupsController.cs`**

```csharp
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Altas pagadas en rublos por SBP con Entrax (Plan H): las que esperan pago, las que quedaron a
/// revisar (pagos que no cuadran o dobles cobros, con dinero en juego) y las recientes.
///
/// Re-check NO liquida aquí: reenvía a SignupAPI, que es quien activa el alta por el camino único
/// (EntraxSignupSettlement), con el Bearer del administrador. Los mismos cuatro roles que confirmar
/// una cripto, comprobados aquí y otra vez en SignupAPI.
/// </summary>
[ApiController]
[Route("api/v1/admin/entrax-signups")]
[Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
public class AdminEntraxSignupsController : ControllerBase
{
    public const string ViewOpen   = "open";
    public const string ViewReview = "review";
    public const string ViewRecent = "recent";

    private readonly AppDbContext       _db;
    private readonly IHttpClientFactory _httpFactory;

    public AdminEntraxSignupsController(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db          = db;
        _httpFactory = httpFactory;
    }

    /// <summary>GET api/v1/admin/entrax-signups?view=open|review|recent&amp;take=100</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? view = ViewOpen, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        if (take is < 1 or > 500) take = 100;

        var payments = _db.EntraxPayments.AsNoTracking();
        payments = view switch
        {
            ViewReview => payments.Where(p => p.Status == EntraxPaymentStatus.ToReview),
            ViewRecent => payments,
            _          => payments.Where(p => p.Status == EntraxPaymentStatus.AwaitingPayment)
        };

        // Uniones a la izquierda: un pago a revisar tiene dinero en juego y no puede desaparecer de la
        // lista porque falte el perfil o el pedido.
        var raw = await (
            from p in payments
            join m in _db.MemberProfiles.AsNoTracking() on p.MemberId equals m.MemberId into ms
            from m in ms.DefaultIfEmpty()
            join o in _db.Orders.AsNoTracking() on p.OrderId equals o.Id into os
            from o in os.DefaultIfEmpty()
            orderby p.CreationDate descending
            select new
            {
                Payment  = p,
                OrderNo  = o != null ? o.OrderNo : null,
                FullName = m != null ? (m.FirstName + " " + m.LastName).Trim() : null,
                Email    = m != null ? m.Email : null,
                Country  = m != null ? m.Country : null
            }
        ).Take(take).ToListAsync(ct);

        var rows = raw.Select(x => new EntraxSignupRowDto(
            x.Payment.Id, x.Payment.OrderId, x.OrderNo, x.Payment.MemberId, x.FullName, x.Email, x.Country,
            x.Payment.Status.ToString(), x.Payment.AmountUsd, x.Payment.AmountRub, x.Payment.PaidAmountUsd,
            x.Payment.ExternalOrderId, x.Payment.ExternalPaymentId, x.Payment.ExpiresAt, x.Payment.PaidAt,
            x.Payment.LastCheckedAt, x.Payment.FailureCode, x.Payment.CreationDate)).ToList();

        return Ok(ApiResponse<IEnumerable<EntraxSignupRowDto>>.Ok(rows));
    }

    /// <summary>POST api/v1/admin/entrax-signups/{id}/recheck — vuelve a preguntar a Entrax y liquida.</summary>
    [HttpPost("{id}/recheck")]
    public async Task<IActionResult> Recheck(string id, CancellationToken ct = default)
    {
        var exists = await _db.EntraxPayments.AsNoTracking().AnyAsync(p => p.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<object>.Fail("ENTRAX_PAYMENT_NOT_FOUND", "Payment not found."));

        var http = _httpFactory.CreateClient("SignupApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/signups/entrax/{Uri.EscapeDataString(id)}/recheck");

        var bearer = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(bearer) && AuthenticationHeaderValue.TryParse(bearer, out var parsed))
            request.Headers.Authorization = parsed;

        try
        {
            using var resp = await http.SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new ContentResult { Content = body, ContentType = "application/json", StatusCode = (int)resp.StatusCode };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse<object>.Fail("SIGNUP_API_UNAVAILABLE", "The signup service is not reachable. Try again."));
        }
    }

    public sealed record EntraxSignupRowDto(
        string    Id,
        string    OrderId,
        string?   OrderNo,
        string    MemberId,
        string?   FullName,
        string?   Email,
        string?   Country,
        string    Status,
        decimal   AmountUsd,
        decimal?  AmountRub,
        decimal?  PaidAmountUsd,
        string    ExternalOrderId,
        string?   ExternalPaymentId,
        DateTime? ExpiresAt,
        DateTime? PaidAt,
        DateTime? LastCheckedAt,
        string?   Reason,
        DateTime  CreatedAt);
}
```

- [ ] **Step 4: Ejecutar la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminEntraxSignupsControllerTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminEntraxSignupsController.cs \
        MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CryptoSignups/AdminEntraxSignupsControllerTests.cs
git commit -m "Admin de pagos de Entrax del alta: esperando, a revisar y recientes sin perder pagos huerfanos, y Re-check reenviado a SignupAPI con el Bearer del administrador"
```

---

### Task 20: Admin — pantalla y menú (AdminWeb)

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminEntraxSignups.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor:316-333`

- [ ] **Step 1: Crear `AdminEntraxSignups.razor`**

```razor
@page "/admin/billing/entrax-signups"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
@inject HttpClient Http

@*
    Altas pagadas en rublos por SBP con Entrax (Plan H). Re-check vuelve a preguntar a Entrax y, si el
    pago está hecho y cuadra, activa el alta por el mismo camino que el webhook (lo hace SignupAPI).
    Los "To review" tienen dinero en juego: o se aplican a mano o se devuelven desde Entrax.

    El atributo de arriba solo esconde la pantalla: AdminEntraxSignupsController y SignupAPI
    comprueban los mismos cuatro roles.
*@

<PageTitle>Entrax Signups — Admin</PageTitle>

<BreadcrumbNav Items="@(new List<BreadcrumbNav.BreadcrumbItem> {
    new("Admin Dashboard", "/admin"), new("Billing"), new("Entrax Signups") })" />

@if (_alertMsg is not null)
{
    <div class="alert @(_alertIsError ? "alert-danger" : "alert-success") alert-dismissible mb-3">
        @_alertMsg <button type="button" class="btn-close" @onclick="() => _alertMsg = null"></button>
    </div>
}

<div class="card border-0 shadow-sm">
    <div class="card-header py-3 d-flex justify-content-between align-items-center flex-wrap gap-2">
        <div>
            <h5 class="mb-0 fw-bold">Ruble (SBP) signups — Entrax</h5>
            <small class="text-muted">
                Paid signups activate automatically. "To review" means money may have been received but was not applied.
            </small>
        </div>
        <div class="d-flex gap-2">
            <div class="btn-group btn-group-sm">
                <button class="btn @(_view == "open" ? "btn-primary" : "btn-outline-primary")" @onclick='() => ChangeView("open")'>Awaiting payment</button>
                <button class="btn @(_view == "review" ? "btn-primary" : "btn-outline-primary")" @onclick='() => ChangeView("review")'>To review</button>
                <button class="btn @(_view == "recent" ? "btn-primary" : "btn-outline-primary")" @onclick='() => ChangeView("recent")'>Recent</button>
            </div>
            <button class="btn btn-sm btn-outline-secondary" @onclick="LoadAsync" disabled="@_loading">
                <i class="ri-refresh-line"></i> Refresh
            </button>
        </div>
    </div>
    <div class="card-body p-0">
        @if (_loading)
        {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
        }
        else if (_rows.Count == 0)
        {
            <div class="text-center py-5 text-muted">
                <i class="ri-qr-code-line" style="font-size:2.5rem;opacity:.4"></i>
                <p class="mt-2 mb-0">Nothing here.</p>
            </div>
        }
        else
        {
            <SfGrid TValue="EntraxSignupRowDto" DataSource="@_rows"
                    AllowFiltering="true" AllowSorting="true" AllowResizing="true" GridLines="GridLine.Horizontal">
                <GridFilterSettings Type="Syncfusion.Blazor.Grids.FilterType.Excel" />
                <GridColumns>
                    <GridColumn HeaderText="Created" Width="140">
                        <Template>
                            <span class="small text-muted">@(((EntraxSignupRowDto)context).CreatedAt.ToString("yyyy-MM-dd HH:mm"))</span>
                        </Template>
                    </GridColumn>
                    <GridColumn Field="@nameof(EntraxSignupRowDto.MemberId)" HeaderText="Member ID" Width="120" />
                    <GridColumn HeaderText="Name" Width="170">
                        <Template>@(((EntraxSignupRowDto)context).FullName ?? "—")</Template>
                    </GridColumn>
                    <GridColumn HeaderText="Email" Width="200">
                        <Template>@(((EntraxSignupRowDto)context).Email ?? "—")</Template>
                    </GridColumn>
                    <GridColumn HeaderText="Status" Width="130" TextAlign="TextAlign.Center">
                        <Template>
                            @{ var s = ((EntraxSignupRowDto)context).Status; }
                            <span class="badge @StatusBadge(s)">@s</span>
                        </Template>
                    </GridColumn>
                    <GridColumn HeaderText="USD" Width="90" TextAlign="TextAlign.Right">
                        <Template>@(((EntraxSignupRowDto)context).AmountUsd.ToString("N2"))</Template>
                    </GridColumn>
                    <GridColumn HeaderText="RUB" Width="110" TextAlign="TextAlign.Right">
                        <Template>@(((EntraxSignupRowDto)context).AmountRub?.ToString("N2") ?? "—")</Template>
                    </GridColumn>
                    <GridColumn HeaderText="Paid USD" Width="100" TextAlign="TextAlign.Right">
                        <Template>@(((EntraxSignupRowDto)context).PaidAmountUsd?.ToString("N2") ?? "—")</Template>
                    </GridColumn>
                    <GridColumn HeaderText="Reason" Width="170">
                        <Template><code class="small">@(((EntraxSignupRowDto)context).Reason ?? "—")</code></Template>
                    </GridColumn>
                    <GridColumn HeaderText="Entrax payment id" Width="240">
                        <Template><code class="small">@(((EntraxSignupRowDto)context).ExternalPaymentId ?? "—")</code></Template>
                    </GridColumn>
                    <GridColumn HeaderText="Last check" Width="140">
                        <Template>
                            <span class="small text-muted">@(((EntraxSignupRowDto)context).LastCheckedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—")</span>
                        </Template>
                    </GridColumn>
                    <GridColumn HeaderText="Actions" Width="120" TextAlign="TextAlign.Center">
                        <Template>
                            @{ var row = (EntraxSignupRowDto)context; }
                            <button class="btn btn-sm btn-outline-primary py-0" disabled="@(_busyId is not null)"
                                    @onclick="() => RecheckAsync(row)" title="Ask Entrax again and settle">
                                @if (_busyId == row.Id) { <span class="spinner-border spinner-border-sm me-1"></span> }
                                Re-check
                            </button>
                        </Template>
                    </GridColumn>
                </GridColumns>
            </SfGrid>
        }
    </div>
</div>

@code {
    private List<EntraxSignupRowDto> _rows = new();
    private string  _view = "open";
    private bool    _loading = true;
    private string? _busyId;
    private string? _alertMsg;
    private bool    _alertIsError;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task ChangeView(string view)
    {
        _view = view;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        StateHasChanged();
        try
        {
            var r = await Http.GetFromJsonAsync<ApiResp<List<EntraxSignupRowDto>>>(
                $"api/v1/admin/entrax-signups?view={_view}&take=200");
            _rows = r?.Data ?? new();
        }
        catch (Exception ex)
        {
            _alertMsg = $"Error: {ex.Message}";
            _alertIsError = true;
        }
        finally { _loading = false; }
    }

    private async Task RecheckAsync(EntraxSignupRowDto row)
    {
        _busyId = row.Id;
        try
        {
            var resp = await Http.PostAsync($"api/v1/admin/entrax-signups/{Uri.EscapeDataString(row.Id)}/recheck", null);
            var payload = await resp.Content.ReadFromJsonAsync<ApiResp<RecheckResult>>();
            if (resp.IsSuccessStatusCode && payload?.Data is not null)
            {
                _alertMsg = $"{row.MemberId}: {payload.Data.Outcome}.";
                _alertIsError = payload.Data.Outcome is "ToReview" or "ProviderUnavailable";
                await LoadAsync();
            }
            else
            {
                _alertMsg = payload?.Errors?.FirstOrDefault() ?? payload?.Message ?? $"Re-check failed ({(int)resp.StatusCode}).";
                _alertIsError = true;
            }
        }
        catch (Exception ex)
        {
            _alertMsg = $"Error: {ex.Message}";
            _alertIsError = true;
        }
        finally { _busyId = null; }
    }

    private static string StatusBadge(string status) => status switch
    {
        "Paid"            => "bg-success",
        "ToReview"        => "bg-danger",
        "AwaitingPayment" => "bg-warning text-dark",
        _                 => "bg-secondary"
    };

    // AdminWeb no referencia AdminAPI: el contrato se duplica por forma del JSON, como en AdminCryptoSignups.razor.
    private record ApiResp<T>(bool Success, T? Data, string? Message, IEnumerable<string>? Errors);

    private record RecheckResult(string Outcome);

    private record EntraxSignupRowDto(
        string Id, string OrderId, string? OrderNo, string MemberId, string? FullName, string? Email, string? Country,
        string Status, decimal AmountUsd, decimal? AmountRub, decimal? PaidAmountUsd, string ExternalOrderId,
        string? ExternalPaymentId, DateTime? ExpiresAt, DateTime? PaidAt, DateTime? LastCheckedAt, string? Reason,
        DateTime CreatedAt);
}
```

- [ ] **Step 2: Menú.** En `AdminSidebarMenu.razor`, dentro del `<AuthorizeView Roles="@AppRoles.CryptoPaymentApprovers">`, justo después del `<li>` de *Crypto Signups* (el que termina en `<span class="title">Crypto Signups</span></a></li>`), añadir:

```razor
                @* Altas pagadas en rublos por SBP con Entrax (Plan H): mismos cuatro roles que la cripto. *@
                <li class="menu-item @(IsActive("/admin/billing/entrax-signups") == "active" ? "active" : "")">
                    <a href="/admin/billing/entrax-signups"
                       class="menu-link @IsActive("/admin/billing/entrax-signups")">
                        <i class="ri-qr-code-line menu-icon"></i>
                        <span class="title">Entrax Signups</span>
                    </a>
                </li>
```

- [ ] **Step 3: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminEntraxSignups.razor \
        MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor
git commit -m "Pantalla de admin de altas con Entrax: esperando, a revisar y recientes con Re-check, y su entrada en el menu de cobros en cripto"
```

---

### Task 21: Documentación de seguridad y condiciones de despliegue

**Files:**
- Modify: `docs/deployment/pruebas-de-seguridad.md` (sección nueva al final)

- [x] **Step 1: Añadir la sección 10 al final del archivo**

> **Hecho, con la sección contrastada contra el código (no copiada de esta plantilla).** La §10 real de
> `docs/deployment/pruebas-de-seguridad.md` sustituye a la plantilla de abajo, que se escribió antes de
> las tareas 10 a 20 y se queda corta: numera las pruebas de 10.1 a 10.47 en siete bloques (callback,
> estado, liquidación, otros métodos, admin, configuración y fallos del legado), añade `413` por encima de
> 64 KB, firma en hexadecimal, freno de 15 s reservado con `UPDATE` condicional, `Superseded` (H21),
> `LOCAL_EXPIRY` (H20), los 409 `ENTRAX_QR_ALREADY_ACTIVE`, `ENTRAX_PAYMENT_CONFIRMING` y
> `SIGNUP_ALREADY_SETTLED` (este solo para el pedido de un alta), el Re-check auditado con `CLIENT_CANCELLED`
> y su tope de 20 s, y que un Re-check no reactiva a quien desactivó un administrador. Condiciones de
> despliegue que la plantilla no tenía: `Entrax__CallbackUrl` también por variable de entorno,
> `Services__SignupAPI` en AdminAPI (sin ella AdminAPI entero no arranca), la migración
> `20261016045914_EntraxEnElAlta` antes de que SignupAPI atienda tráfico, NTP por la tolerancia del callback
> y la primera prueba real con importe mínimo y aprobación de Nick (no hay sandbox de PaymentQr). Las
> pruebas quedan pendientes de verificar (Tarea 22).

````markdown

## 10. Entrax en el alta: rublos por SBP (Plan H)

El alta de un aspirante de un país de `Entrax:Countries` (por defecto `RU`) puede pagarse con un QR
de SBP. **El alta solo se activa en el servidor**, por un único camino (`EntraxSignupSettlement`) al
que llegan el callback de Entrax, la pantalla que consulta el estado, el barrido
`entrax-signup-sweep` y *Re-check* del admin. Ese camino **siempre** vuelve a preguntar
`GET /payment/{id}` a Entrax y exige `paid`, `USD` e importe ≥ lo pedido; si no cuadra, el pago
queda *To review* sin activar nada.

### Condiciones de despliegue

- **Credenciales fuera del repositorio.** `Entrax__KeyId` y `Entrax__Secret` como variables de
  entorno (o el almacén de secretos del entorno). El `appsettings.json` base no las lleva (lo vigila
  `EntraxConfiguracionTests`). Fuera de Development, `Entrax:Enabled = true` sin ellas **no arranca**.
- **`Entrax:CallbackUrl` pública y https**, apuntando a `https://{host-público-de-SignupAPI}/api/v1/signups/entrax/callback`.
  Fuera de Development, sin https absoluta no arranca.
- **`Entrax:UseFake` nunca fuera de Development** (si está puesto, no arranca).
- **Apagar Entrax es dejar de ofrecerlo, no quitar credenciales.** Se apaga con `Entrax:Enabled = false`
  y se dejan `KeyId` y `Secret`: el barrido corre apagado y sigue liquidando los pagos en vuelo con
  ellas. Fuera de Development, unas credenciales **a medias** (solo `KeyId` o solo `Secret`) o una
  `BaseUrl` sin https **no arrancan** aunque esté apagado. Sin credenciales, el cliente no llama
  (`NOT_CONFIGURED`): el barrido cierra esos QR en local como caducados pasado `ExpiresAt` +
  `Entrax:LocalExpiryGraceMinutes` y el pedido vuelve a `Pending`, pero un pago real ya no se puede
  comprobar hasta que vuelvan. Con Entrax apagado y pagos esperando, el log lleva un `Critical`.
- **El barrido vivo.** En `/hangfire` de SignupAPI debe existir el recurrente `entrax-signup-sweep`
  (cada 2 minutos, cola `signups`). Es lo que activa las altas cuyo callback no llegó.
- **IP de Entrax en `IpRateLimiting:IpWhitelist`** de SignupAPI. Si no, la regla general (60/min por
  IP) puede cortar callbacks cuando haya muchas altas; Entrax reintenta y el barrido liquida, pero
  con retraso.
- **Prueba real con Entrax antes de anunciar el método** (en este orden, con un alta de prueba de RU):
  1. el QR se emite **en USD** por el total del pedido (si el comercio no acepta USD, `502 ENTRAX_UNAVAILABLE`:
     pedir a Entrax que lo habilite; **no** convertir nosotros);
  2. pagar el QR de verdad y comprobar que llega el callback, que **su firma valida** (si se rechaza con
     401, revisar con Entrax qué firman: el cuerpo crudo en Base64 o hexadecimal es lo que se acepta),
     y que el alta queda activa con un `PaymentHistory` `Entrax`;
  3. dejar caducar otro QR sin pagarlo y comprobar que la pantalla ofrece uno nuevo.
- **Revisar el log de SignupAPI por `Critical` de Entrax** tras el despliegue: cada uno es un pago
  que quedó *To review* (importe, moneda, doble cobro). Se resuelven en *Admin → Billing → Entrax
  Signups → To review* (devolución manual con Entrax).

### Pruebas

Preparación (Development, con el fake): arrancar SignupAPI con
`Entrax__Enabled=true`, `Entrax__UseFake=true` y `Entrax__Secret=secreto-dev`. Para firmar un
callback a mano (PowerShell; `body.json` sin BOM):

```powershell
$secret = "secreto-dev"
$ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$body = '{"paymentId":"' + $paymentId + '","orderId":"' + $externalOrderId + '","status":"SUCCESS","money":{"summary":80,"currency":"USD","amount":7640,"rate":95.5},"timestamp":' + $ts + '}'
[IO.File]::WriteAllText("$PWD\body.json", $body)
$hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$sig = [Convert]::ToBase64String($hmac.ComputeHash([IO.File]::ReadAllBytes("$PWD\body.json")))
curl.exe -k -i -X POST https://localhost:7005/api/v1/signups/entrax/callback -H "Content-Type: application/json" -H "X-Signature: $sig" --data-binary "@body.json"
```

| # | Prueba | Resultado esperado | ✔ |
|---|---|---|---|
| 10.1 | Callback **sin** `X-Signature` | `401`, sin cambios en `EntraxPayments` ni en el alta | ☐ |
| 10.2 | Callback con firma de otro secreto (o un byte del cuerpo cambiado tras firmar) | `401` | ☐ |
| 10.3 | Callback bien firmado con `timestamp` de hace 2 h | `400 STALE_CALLBACK`, sin cambios | ☐ |
| 10.4 | **Replay**: el mismo callback bien firmado dos veces seguidas | las dos `200`; un solo `PaymentHistory` `Entrax`, una sola activación | ☐ |
| 10.5 | Callback bien firmado de un pago que **no** está pagado en Entrax (`status: SUCCESS` falso) | `503` y el alta sigue pendiente: manda `GET /payment`, no el cuerpo del aviso | ☐ |
| 10.6 | **Monto menor**: `POST /api/v1/dev/entrax/{paymentId}/underpaid` | el pago queda `ToReview` `AMOUNT_TOO_LOW`, alta sin activar, `Critical` en el log, visible en *To review* | ☐ |
| 10.7 | **Pago reutilizado**: callback con el `paymentId` de un alta y el `orderId` de otra | `400 INVALID_CALLBACK`; ninguna de las dos altas cambia | ☐ |
| 10.8 | **Doble cobro**: activar un alta y luego pagar un segundo QR del mismo pedido | el segundo queda `ToReview` `ORDER_ALREADY_SETTLED` | ☐ |
| 10.9 | `GET /api/v1/signups/{signupId}/entrax-status` sin `ref`, con un `ref` de otra alta o con un `signupId` inventado | `404 ENTRAX_PAYMENT_NOT_FOUND` sin más datos | ☐ |
| 10.10 | Alta con `PaymentMethod: Entrax` desde un país que no está en `Entrax:Countries` | `400 ENTRAX_NOT_AVAILABLE` | ☐ |
| 10.11 | `POST /api/v1/signups/entrax/{id}/recheck` con un token de `SupportLevel1` | `403` | ☐ |
| 10.12 | `entrax-signup-sweep` en `/hangfire` | existe, cada 2 minutos, cola `signups` | ☐ |
````

- [x] **Step 2: Commit**

```bash
git add docs/deployment/pruebas-de-seguridad.md
git commit -m "Pruebas de seguridad de Entrax en el alta (firma, replay, monto menor, pago reutilizado, doble cobro) y sus condiciones de despliegue"
```

---

### Task 22: Prueba completa y prueba manual de punta a punta con el fake

**Files:** ninguno (verificación).

- [ ] **Step 1: Compilar la solución**

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 2: Todas las pruebas de los proyectos tocados**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo`
Expected: PASS (todas, también las anteriores al plan).

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests -nologo`
Expected: PASS.

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`
Expected: PASS.

- [ ] **Step 3: Arrancar con el fake** (dos terminales PowerShell; SignupAPI aplica la migración `EntraxEnElAlta` al arrancar)

```powershell
$env:Entrax__Enabled = "true"; $env:Entrax__UseFake = "true"; $env:Entrax__Secret = "secreto-dev"
dotnet run --project MLMConquerorGlobalEdition.SignupAPI --launch-profile https
```

```powershell
dotnet run --project MLMConquerorGlobalEdition.Signups --launch-profile https
```

Expected: en la consola de SignupAPI no sale ningún `[Entrax] …` (configuración válida para el fake) y arranca; `https://localhost:7005/hangfire` → *Recurring jobs* muestra `entrax-signup-sweep`.

- [ ] **Step 4: Alta pagada (camino feliz)**

1. Abrir `https://localhost:7147/ambassador-join/{slug-de-un-patrocinador}`, elegir **Russia**, rellenar el formulario (correo nuevo) y elegir un plan.
2. Debe aparecer la pestaña **Rubles (SBP)** (no aparece si se elige otro país). Elegirla, aceptar términos y pulsar *Join Now*.
3. Se ve el QR (cuadrado de 1×1 del fake), el botón *Open payment link*, el importe en rublos y la cuenta atrás.
4. En SQL: `SELECT TOP 1 Id, ExternalOrderId, ExternalPaymentId, Status, AmountUsd FROM EntraxPayments ORDER BY CreationDate DESC` → `AwaitingPayment`, `SIGNUP-{OrderId}-1`, `AmountUsd` = total del pedido. `Orders.Status` = 3 (Processing); `MemberProfiles.Status` = Pending.
5. `curl.exe -k -X POST https://localhost:7005/api/v1/dev/entrax/{ExternalPaymentId}/paid`
6. En ≤ 20 s la pantalla muestra *Payment received* y luego el "Welcome… Go to BizCenter". Iniciar sesión con el correo y la contraseña del alta: **entra**.
7. En SQL: `EntraxPayments.Status = 2` (Paid), `Consumed = 1`; `Orders.Status = 4` (Completed); `PaymentHistories` con `GatewayName = 'Entrax'` y `GatewayTransactionId` = el paymentId; `EntraxApiCalls` con `CreateQr` y `GetPayment` y sin cuerpos.

- [ ] **Step 5: QR caducado → QR nuevo → otro método**

1. Otra alta de RU con Entrax. `curl.exe -k -X POST https://localhost:7005/api/v1/dev/entrax/{paymentId}/expired`.
2. La pantalla muestra el aviso de caducado con *Generate a new QR code* y *Choose another payment method*. `Orders.Status` vuelve a 1 (Pending).
3. *Generate a new QR code* → QR nuevo; en SQL aparece `SIGNUP-{OrderId}-2`.
4. Volver a caducarlo, pulsar *Choose another payment method*, elegir *Crypto* y *Join Now*: el alta **no** se vuelve a crear (no hay error de correo duplicado) y queda en "Thank you… crypto transfer".
5. **Pago tardío**: `curl.exe -k -X POST https://localhost:7005/api/v1/dev/entrax/{paymentId-del-intento-2}/paid` y `curl.exe -k -X POST https://localhost:7005/api/v1/dev/entrax/sweep` → el alta se activa con Entrax y la `CryptoPaymentConfirmation` del pedido pasa a `Cancelled` con la nota "Paid via Entrax (…)".

- [ ] **Step 6: Webhook y casos de seguridad** — ejecutar las pruebas de `docs/deployment/pruebas-de-seguridad.md` §10 que se pueden hacer con el fake (10.1 a 10.33 y 10.36 a 10.47; 10.34, 10.35, 10.37 y 10.38 arrancan en `Production`) con el script de firma de esa sección y marcar las que pasen.

- [ ] **Step 7: Carrera contra SQL Server (R8)**

Con un alta en `AwaitingPayment` ya marcada `paid` en el fake, lanzar a la vez varias liquidaciones:

```powershell
1..4 | ForEach-Object -Parallel { curl.exe -k -s "https://localhost:7005/api/v1/signups/$using:signupId/entrax-status?ref=$using:paymentRef" } -ThrottleLimit 4
curl.exe -k -X POST https://localhost:7005/api/v1/dev/entrax/sweep
```

(`ForEach-Object -Parallel` es de PowerShell 7; en Windows PowerShell 5.1, abrir cuatro consolas.)

Expected: un solo `PaymentHistory` `Entrax` para el pedido, una sola fila de cada bono en `CommissionEarnings` con ese `SourceOrderId`, y el pago `Paid` una vez.

- [ ] **Step 8: Admin**

Entrar a AdminWeb con un usuario `BillingManager` → menú lateral → *Entrax Signups* (junto a *Crypto Signups*): las tres vistas cargan; *Re-check* sobre un pago `AwaitingPayment` pagado en el fake lo activa; el de la prueba 10.16 aparece en *To review* con `AMOUNT_TOO_LOW`. Con `SupportLevel1` la entrada del menú no aparece y la API devuelve `403`.

- [ ] **Step 9: Cerrar**

Parar los dos procesos y borrar las variables de entorno de la consola (`Remove-Item Env:Entrax__*`). No hay nada que commitear en esta tarea.

---

## Autorrevisión

- **Cobertura del encargo:** (1) Tareas 3, 10, 13, 16, 18; (2) Tareas 2, 9, 10; (3) Tareas 8, 11, 12, 13, 15; (4) Tarea 11; (5) Tareas 12, 14; (6) Tareas 13, 14, 18; (7) Tareas 3–7, 16; (8) Tareas 17, 18; (9) Tareas 14, 19, 20; (10) Tarea 21; (11) *Fuera de alcance*.
- **Nombres usados en varias tareas:** `EntraxPayment`, `EntraxPaymentStatus`, `EntraxApiCall`, `EntraxOptions`, `IEntraxClient`, `EntraxCall<T>`, `EntraxQr`, `EntraxPaymentState`, `IEntraxSignupCheckout.StartAsync/IsOfferedFor`, `IEntraxSignupSettlement.SettleAsync`, `EntraxSettlementSource`, `EntraxSettlementOutcome`, `IEntraxCallbackProcessor.ProcessAsync`, `EntraxCallbackOutcome`, `IEntraxSignupStatusService.GetAsync/GetPaymentOptions`, `EntraxSignupErrors`, `EntraxGateway`, `EntraxCallLog`, `EntraxQrSanitizer`, `ISignupActivationService.ActivatePendingSignupAsync`, `SignupUserActivation.ActivateAsync`, `EntraxSignupApi`, `EntraxQrPayment`.
