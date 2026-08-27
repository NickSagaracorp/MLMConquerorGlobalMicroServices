# 2FA de administración y confirmación de operaciones críticas

**Fecha:** 2026-08-27
**Estado:** aprobado, pendiente de plan de implementación
**Alcance:** AdminWeb / AdminAPI / SignupAPI / SharedComponents / SharedKernel

---

## 1. Objetivo

Dos entregables acoplados:

1. **2FA obligatorio en el portal de administración**, con tres canales: aplicación de
   autenticación (TOTP), correo y SMS.
2. **Confirmación por código en operaciones críticas de negocio** (step-up), con la
   política de qué operación exige código y por qué canal configurable sin desplegar.

Como consecuencia del punto 1 se construye además **toda la superficie de páginas de
cuenta de Identity**, para no volver a construirlas de a una.

---

## 2. Estado actual del código

Hallazgos de la exploración previa que condicionan el diseño:

| Hallazgo | Ubicación | Consecuencia |
|---|---|---|
| El login de admin **no** usa el `AuthController` del AdminAPI; va contra SignupAPI | `AdminWeb/Program.cs` (cliente `AuthApi` → `AuthApiBaseUrl`), `AdminWeb/Services/AuthEndpoints.cs` | El `AuthController` del AdminAPI es código muerto |
| Ya existe 2FA por correo, con challenge JWT stateless que carga el SHA-256 del código | `SignupAPI/Services/TwoFactorChallengeService.cs`, `Features/Auth/Commands/{Login,VerifyTwoFactor,ResendTwoFactor}` | Base reutilizable; se generaliza en vez de reescribir |
| `AuthEndpoints.LoginAsync` **ignora** `RequiresTwoFactor` | `AdminWeb/Services/AuthEndpoints.cs:30-37` | Hoy, activar `TwoFactorEnabled` a un admin lo deja sin poder entrar |
| `IEmailService` está registrado como `NullEmailService` en los 4 hosts | `AdminAPI/Program.cs:170`, `SignupAPI/Program.cs:123`, `RankEngine`, `Billing` | El 2FA por correo existente nunca entregó un código |
| El lockout está configurado pero **nunca se invoca** en el login real | `SignupAPI/Program.cs:47-48` vs `Features/Auth/Commands/Login/LoginHandler.cs` | No hay freno de fuerza bruta en el camino de login que se usa |
| `EmailConfirmed=false` al registrarse y nada lo confirma nunca | `SignupAmbassadorHandler.cs:350`, `SignupMemberHandler.cs:237` | No existe flujo de confirmación de correo |
| No hay proveedor de SMS en el repositorio | — | `ISmsService` y su implementación son nuevos |
| No hay tabla de auditoría de acciones de staff | — | `AuthSecurityEvent` es nueva |
| `AddDefaultTokenProviders()` ya registrado | `AdminAPI/Program.cs:52`, `SignupAPI/Program.cs:52`, `BizCenter/Program.cs:51` | El `AuthenticatorTokenProvider` (TOTP) está disponible sin dependencias nuevas |
| `PhoneNumber` / `PhoneNumberConfirmed` de Identity sin usar en todo el repo | — | Libres, pero se dejan intactos (ver §5) |
| `IEncryptionService` disponible, con precedente `SsnEncrypted` | `SharedKernel/Interfaces/IEncryptionService.cs` | Se usa para el teléfono |
| La llave privada RSA del JWT está commiteada en texto plano | `AdminAPI/appsettings.json:26`, `SignupAPI/appsettings.json:21` | **Bypass total de autenticación.** Ver §10 |
| `Billing` y `CommissionEngine` validan con `SymmetricSecurityKey` sobre `Jwt:Key`, no con la pública RSA; y su `Audience` es `MLMConquerorGlobalEditionClients`, sin el punto | `Billing/Program.cs:156,170`, `CommissionEngine/Program.cs:89,101` | **Su autenticación está rota hoy.** Ver §10.6 |

---

## 3. Decisiones tomadas

| Tema | Decisión |
|---|---|
| Arquitectura | Núcleo compartido (`Authn`) + step-up en AdminAPI |
| Canales | TOTP, correo (SES) y SMS (Twilio), los tres end-to-end en este alcance |
| Obligatoriedad | Obligatorio para roles admin, con enrolamiento forzado en el primer login |
| Política de step-up | Catálogo de operaciones en código + configuración en base de datos |
| Frescura | Ventana configurable por operación; `0` significa pedir siempre |
| Recuperación | Caída automática del TOTP a correo/SMS. **Sin códigos de respaldo** |
| Operaciones protegidas | Los cuatro grupos: dinero saliente, identidad y acceso, configuración financiera, datos de negocio de alto impacto |
| Credenciales | `appsettings.{Environment}.json`, ya cubierto por `.gitignore:92-93` |
| Llave RSA | Rotación como paso 0 del plan |
| `DeletePersonalData` | Fuera de alcance |

---

## 4. Componentes

### 4.1 `MLMConquerorGlobalEdition.Authn` (nuevo, class library)

Referencia a `Repository` y `SharedKernel`. Lo consumen SignupAPI y AdminAPI; BizCenter
puede adoptarlo después sin trabajo adicional.

| Tipo | Responsabilidad |
|---|---|
| `TwoFactorChannel` | `Authenticator`, `Email`, `Sms` |
| `TwoFactorPurpose` | `Login`, `Enrollment`, `StepUp` |
| `ITwoFactorService` | `IssueAsync(user, purpose, operationKey?, forcedChannel?)`; `VerifyAsync(challengeToken, code)` |
| `IChallengeTokenService` | Emite y valida el JWT de challenge. Generalización del `TwoFactorChallengeService` actual |
| `ITotpEnrollmentService` | `BeginAsync`, `ConfirmAsync`, `ResetAsync` |

**El challenge lleva un claim `purpose`.** Hoy está fijo en `"2fa-challenge"`. Pasa a ser
`login`, `enrollment` o `step_up:{OPERATION_KEY}`. Sin esta separación, el código pedido
para iniciar sesión serviría para autorizar un lote de payout: el mismo token, redimido
contra otro endpoint.

**Un solo `VerifyAsync` para los tres canales.** Correo y SMS generan un código propio,
así que el challenge carga su `code_hash`. TOTP no tiene código que hashear —lo genera la
aplicación del usuario— así que el challenge marca `channel=authenticator` sin hash y la
verificación cae en `UserManager.VerifyTwoFactorTokenAsync`. Quien llama no distingue.

**Antirreplay.** Identity acepta el mismo código TOTP durante unos 90 segundos por
tolerancia de reloj. Para operaciones de dinero eso significa que un código puede
autorizar dos veces. Se guarda en Redis (`ICacheService`) el `jti` del challenge consumido
hasta su expiración, y el hash del código TOTP usado durante su ventana de validez.

### 4.2 `MLMConquerorGlobalEdition.Notifications` (nuevo, class library)

`SesEmailService` (AWSSDK.SimpleEmailV2, lee el catálogo `EmailTemplate` existente) y
`TwilioSmsService`. Van en su propia librería porque `SharedKernel` lo referencia todo y no
debe arrastrar el SDK de Twilio. `ISmsService` sí vive en `SharedKernel/Interfaces`, junto
a `IEmailService`.

Selección por configuración: `Notifications:Email:Provider = Ses|Null`,
`Notifications:Sms:Provider = Twilio|Null`. `NullEmailService` se conserva para pruebas y
desarrollo sin credenciales.

### 4.3 Cambios en proyectos existentes

- **SignupAPI** — `LoginHandler`, `VerifyTwoFactorHandler` y `ResendTwoFactorHandler`
  delegan en `ITwoFactorService`. Endpoints nuevos de enrolamiento, confirmación de
  correo, teléfono y datos personales.
- **AdminAPI** — `Features/StepUp/`, `StepUpController`, `[RequiresStepUp]` con su filtro,
  `StepUpPolicyController` (solo SuperAdmin), reset de 2FA sobre otro usuario.
- **AdminWeb** — `AuthEndpoints.LoginAsync` reescrito; montaje de las páginas de cuenta;
  `StepUpDialog` en `MainLayout`.
- **SharedComponents** — todas las páginas de cuenta, para que BizCenterWeb las herede.

---

## 5. Modelo de datos

Migración única: `Add2faStepUpAndAccountSurface`.

### `ApplicationUser` — columnas nuevas

| Columna | Tipo | Nota |
|---|---|---|
| `PreferredTwoFactorChannel` | `int NOT NULL` default `Email` | El default preserva el comportamiento actual de los miembros del BizCenter |
| `TwoFactorEnrolledAt` | `datetime2 NULL` | |
| `TwoFactorPhoneEncrypted` | `nvarchar(256) NULL` | Vía `IEncryptionService`, precedente `SsnEncrypted` |
| `TwoFactorPhoneLast4` | `nvarchar(4) NULL` | Enmascarar en UI sin desencriptar |
| `TwoFactorPhoneConfirmed` | `bit NOT NULL` default `0` | |

No se reutiliza `PhoneNumber` de Identity: está en texto plano y el teléfono de un
administrador es a la vez PII y factor de autenticación. Las columnas de Identity quedan
intactas y sin uso.

### `StepUpPolicy` (nueva)

`OperationKey` (PK, `nvarchar(64)`), `DisplayName`, `Category`, `IsEnabled`,
`RequiredChannel` (`int NULL`; null = canal preferido del usuario),
`FreshnessWindowMinutes` (`0` = pedir siempre), `IsObsolete`, `UpdatedAt`, `UpdatedBy`.

Se siembra desde el catálogo en código al arrancar. Las claves nuevas entran con defaults
seguros (habilitada, 15 minutos, canal libre). Las claves que desaparecen del código se
marcan `IsObsolete`, **no se borran** — si se borraran, los registros de auditoría que las
referencian quedarían huérfanos.

### `AuthSecurityEvent` (nueva)

`Id` (bigint PK), `UserId` (FK a `AspNetUsers`), `UserEmail` (desnormalizado, sobrevive a
la baja de la cuenta), `EventType`, `OperationKey` (null en eventos de login), `Channel`,
`Outcome` (`Issued` / `Verified` / `Failed` / `Denied`), `FailureReason`, `IpAddress`,
`UserAgent`, `RequestPath`, `ChallengeJti`, `CreatedAt`.

Índices: `(UserId, CreatedAt)`, `(OperationKey, CreatedAt)`, `(EventType, CreatedAt)`.

Cubre login 2FA, enrolamiento, alta y verificación de teléfono, confirmación de correo,
cambio de contraseña, resets por SuperAdmin, step-up y cambios de política. Un solo lugar
donde mirar.

### `SmsTemplate` + `SmsTemplateLocalization` (nuevas)

Espejo de la estructura de `EmailTemplate` / `EmailTemplateLocalization`, para que el texto
del SMS sea localizable en los 9 idiomas de `LanguageCodeMapper` y futuras notificaciones
por SMS tengan dónde vivir. Semilla: `TWO_FACTOR_CODE`, `STEP_UP_CODE`.

### `EmailTemplate` — filas semilla nuevas

`STEP_UP_CODE`, `EMAIL_CONFIRMATION`, `PASSWORD_CHANGED`.

### Sin tabla, en Redis (`ICacheService`)

- Máximo 5 intentos por challenge; al sexto se quema y hay que reiniciar.
- Máximo 3 emisiones por usuario cada 15 minutos. Sin este límite, un atacante bombardea
  por SMS a costa de la empresa, porque Twilio cobra por mensaje.
- `jti` consumido y hash de código TOTP usado, para el antirreplay descrito en §4.1.

---

## 6. Flujos

### 6.1 Login

`LoginHandler` de SignupAPI:

1. `FindByEmailAsync` → null o `!IsActive` → `INVALID_CREDENTIALS`
2. **nuevo** `IsLockedOutAsync` → `ACCOUNT_LOCKED`
3. `CheckPasswordAsync` falla → **nuevo** `AccessFailedAsync`, luego `INVALID_CREDENTIALS`
4. **nuevo** `ResetAccessFailedCountAsync`
5. Rol en `Auth:TwoFactor:MandatoryRoles` y `!TwoFactorEnabled` → `RequiresEnrollment` + `EnrollmentToken`
6. `TwoFactorEnabled` → `IssueAsync(user, Login)` → `RequiresTwoFactor` + `ChallengeToken` + `Channel` + `MaskedTarget`
7. En cualquier otro caso → tokens, como hoy

Los pasos 2 a 4 reparan el lockout muerto y benefician también a los miembros del
BizCenter.

En AdminWeb, **el challenge viaja en una cookie HttpOnly de vida corta, nunca en la URL**:
en la URL quedaría en el historial del navegador, en los registros del proxy y en la
cabecera `Referer` hacia cualquier recurso externo. `RequiresTwoFactor` redirige a
`/account/login-2fa`; `RequiresEnrollment` a `/account/enroll-authenticator`. Al verificar,
`SignInAsync` y se borra la cookie.

La comprobación de rol admin que hoy vive en `AuthEndpoints` se mantiene y sigue
aplicándose sobre el token final.

### 6.2 Enrolamiento forzado

`enroll/begin` ejecuta `ResetAuthenticatorKeyAsync` + `GetAuthenticatorKeyAsync`, arma el
URI `otpauth://totp/MLMConqueror:{email}?secret=…&issuer=MLMConqueror&digits=6&period=30`
y devuelve el QR como PNG en data-URI (QRCoder, sin llamadas a servicios externos) más la
clave en texto para entrada manual.

`enroll/confirm` valida el primer código con `VerifyTwoFactorTokenAsync`, activa
`TwoFactorEnabled`, fija `PreferredTwoFactorChannel = Authenticator` y **recién entonces**
emite los tokens de acceso.

El `EnrollmentToken` no es un access token y no abre ningún endpoint de negocio. Hasta que
el usuario no confirme, queda atrapado en la pantalla de enrolamiento.

El teléfono es opcional y posterior, desde `AddPhoneNumber`. Exigirlo durante el
enrolamiento dejaría fuera a un administrador sin señal el día del despliegue.

### 6.3 Step-up

```csharp
[HttpPost("{batchId}/release")]
[RequiresStepUp(StepUpOperations.PayoutBatchRelease)]
public async Task<IActionResult> Release(...)
```

`StepUpAuthorizationFilter` (`IAsyncAuthorizationFilter`):

1. Lee la clave del atributo y resuelve la `StepUpPolicy` (cacheada, invalidada al editar).
2. Si `!IsEnabled` → deja pasar.
3. Sin cabecera `X-Step-Up-Token` → corta con `403` y `ApiResponse.Fail("STEP_UP_REQUIRED")`,
   cuyo `Data` lleva `operationKey`, `displayName` y `channel`. La UI sabe qué pedir sin
   tener nada codificado a mano.
4. Con token: valida firma, `purpose == step_up:{clave}`, `sub == usuario autenticado` y
   expiración. Si `FreshnessWindowMinutes == 0`, exige además que el `jti` no esté
   consumido y lo marca.
5. Registra el `AuthSecurityEvent` correspondiente.

**Por qué token firmado y no estado en servidor.** Las cuatro categorías de operaciones
protegidas (§6.4) viven todas en AdminAPI, que valida RSA correctamente. Pero AdminWeb
también consume RankEngine y TicketManagementSystem, que igualmente validan RSA con la
misma llave pública: un token firmado funciona en los tres sin estado compartido, y sirve
para extender el step-up a esos servicios sin trabajo adicional. Con estado en Redis
dependeríamos de `Cache:Mode`, que en desarrollo cae a memoria en proceso y se rompe con
más de una instancia.

`Billing` y `CommissionEngine` quedan explícitamente fuera del alcance del step-up hasta
que se repare su validación (§10.6). Ninguna operación del §6.4 vive en ellos, así que no
bloquea este trabajo.

El precio es que dentro de la ventana de frescura el token no se puede revocar. Por eso las
operaciones más graves se configuran con ventana `0`: un token, un uso.

Emisión: `POST api/v1/admin/step-up/challenge { operationKey }` despacha el código por el
canal de la política (o el preferido del usuario) y devuelve el challenge.
`POST api/v1/admin/step-up/verify { challengeToken, code }` devuelve el `X-Step-Up-Token`.

En AdminWeb, un `DelegatingHandler` detecta `STEP_UP_REQUIRED`, levanta el `StepUpDialog`
montado en `MainLayout`, espera el token y reintenta la petición original. Ninguna página
necesita saber que existe step-up.

**Riesgo conocido:** ese handler está registrado `AddTransient` y resuelve contexto vía
`IHttpContextAccessor`, que en Blazor Server interactivo no es fiable. Si el reintento
automático no sale limpio, el respaldo es explícito —
`await StepUp.RequireAsync(StepUpOperations.PayoutBatchRelease)` antes de la acción en cada
página. Menos elegante, igual de seguro. **Se resuelve al principio de la implementación,
no al final.**

### 6.4 Operaciones protegidas en el primer corte

| Grupo | Controladores |
|---|---|
| Dinero saliente | `AdminPayoutsController`, `AdminCommissionsController`, `AdminMemberWalletsController` |
| Identidad y acceso | `SystemUsersController`, `ImpersonationController` |
| Configuración financiera | `AdminBillingCredentials`, `AdminBillingGateways`, `AdminPayoutDefaultsController`, `AdminRecurringBillingController` |
| Datos de negocio de alto impacto | `GhostPointsController`, `AdminTokensController`, `AdminMemberRanksController`, `AdminPlacementController` |

El catálogo `StepUpOperations` queda extensible: agregar una operación es una constante más
y un atributo.

---

## 7. Superficie de páginas de cuenta

Componentes Blazor en `SharedComponents/Components/Account/`, contra endpoints de la API.
AdminWeb y BizCenterWeb las montan con sus propios layouts.

**No se scaffoldea el Identity UI de Razor Pages.** Ese scaffold asume `SignInManager` y la
cookie de Identity dentro del proyecto web. AdminWeb no registra Identity en absoluto: usa
cookie propia (`mlm_admin_cookie`) y delega en la API, y `SignInManager` solo está
registrado en SignupAPI (`Program.cs:53`). Scaffoldear dejaría **dos caminos de
autenticación en paralelo**, y el de Identity iría directo al `DbContext` saltándose el 2FA.

### Anónimas

`Login`, `LoginWith2fa`, `Lockout`, `ForgotPassword`, `ForgotPasswordConfirmation`,
`ResetPassword`, `ResetPasswordConfirmation`, `ConfirmEmail`, `ResendEmailConfirmation`

### Gestión de cuenta (layout `/account/manage` con navegación lateral)

`Index` (correo y teléfono), `ChangePassword`, `SetPassword`, `TwoFactorAuthentication`
(hub), `EnableAuthenticator`, `AddPhoneNumber`, `VerifyPhoneNumber`, `PersonalData`,
`DownloadPersonalData`, `Logout`

### Solo administración

`Disable2fa` (SuperAdmin sobre otro usuario, dentro de System Users; no autoservicio, para
no anular la obligatoriedad), enrolamiento forzado, `StepUpDialog`, pantalla de política de
step-up, pantalla de auditoría.

### No se construyen

- `LoginWithRecoveryCode` y `GenerateRecoveryCodes` — se decidió caída a correo/SMS en
  lugar de códigos de respaldo.
- `DeletePersonalData` — una cuenta de staff está referenciada por auditoría, comisiones
  aprobadas e impersonaciones; el borrado por autoservicio rompe la trazabilidad. Lo
  correcto en staff es desactivar.

### Backend nuevo que estas páginas requieren

1. `ConfirmEmail` y `ResendEmailConfirmation`: no existe nada hoy. Endpoints nuevos más
   envío del correo.
2. Reparación del lockout (§6.1), sin la cual la página `Lockout` decora un freno
   inexistente.
3. Cambio de correo, que exige confirmar la dirección nueva antes de aplicarla.
4. Alta y verificación de teléfono.
5. Exportación de datos personales.
6. `SetPassword`, variante de change-password para cuentas sin contraseña. Hoy no aplica
   —no hay logins externos— y se construye para un SSO futuro.

---

## 8. Errores

Códigos consistentes con `ApiResponse.Fail`: `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`,
`INVALID_CHALLENGE`, `CODE_EXPIRED`, `CODE_INVALID`, `TOO_MANY_ATTEMPTS`,
`TOO_MANY_REQUESTS`, `ENROLLMENT_REQUIRED`, `STEP_UP_REQUIRED`, `STEP_UP_TOKEN_INVALID`,
`CHANNEL_UNAVAILABLE`.

El mensaje al usuario nunca revela si el correo existe ni si la cuenta es de
administración. El detalle real va al `AuthSecurityEvent`.

Si SES o Twilio fallan, **no se emite el challenge**: se responde `CHANNEL_UNAVAILABLE` y
la UI ofrece otro canal, en vez de dejar al usuario esperando un código que no va a llegar.

---

## 9. Pruebas

Siguiendo el patrón existente (`Signups.Tests/Features/Auth/*HandlerTests.cs`,
`AdminAPI.Tests/Features/*`).

**`Authn.Tests` (nuevo):** separación por `purpose` (un challenge de login no verifica en
step-up y viceversa), expiración, ventana de reenvío, TOTP válido / de ventana adyacente /
ya usado, selección de canal, límites de intentos y de emisiones.

**`Signups.Tests` (regresión, obligatoria):** el login de un miembro con 2FA por correo se
comporta exactamente igual que hoy. Es el riesgo real de haber elegido un núcleo
compartido. Más: lockout a los 5 fallos, reseteo del contador al acertar, y que la rama de
enrolamiento solo dispare para roles en `MandatoryRoles`.

**`AdminAPI.Tests`:** el filtro rechaza sin token; rechaza un token emitido para otra
operación; rechaza el token de otro usuario; con ventana `0` exige reemisión; una política
deshabilitada deja pasar; el CRUD de política exige SuperAdmin.

---

## 10. Riesgos y prerrequisitos

### 10.1 Llave RSA commiteada — paso 0 del plan

`AdminAPI/appsettings.json:26` y `SignupAPI/appsettings.json:21` contienen
`Jwt:PrivateKeyBase64` en texto plano, en un archivo rastreado por git. Cualquiera con
acceso al repositorio puede firmar un access token con rol `SuperAdmin` sin contraseña y
sin 2FA, y también forjar el challenge de 2FA, que usa la misma llave.

Montar 2FA encima de esto no aumenta la seguridad real: el bypass sigue abierto por debajo.

Antes de tocar 2FA: generar un par de llaves nuevo, moverlo del `appsettings.json` base a
`appsettings.Production.json` (ya cubierto por `.gitignore:92-93`) y purgar el actual.

### 10.2 El despliegue del día 1

En cuanto `Auth:TwoFactor:MandatoryRoles` se llene, todo administrador queda enrolando en
su siguiente inicio de sesión. Si un administrador elige correo y SES no funciona, no
entra.

Mitigación: `MandatoryRoles` sale vacío en el despliegue y se llena **después** de
verificar SES y Twilio en producción.

### 10.3 SES arranca en sandbox

Las cuentas nuevas de SES solo envían a direcciones verificadas y con cuota reducida. Hay
que confirmar que la cuenta salió del sandbox antes de que el login dependa de ella.

### 10.4 Caída de TOTP a correo/SMS

Reduce la seguridad efectiva a la del canal más débil: quien controle el correo del
administrador entra igual. Decisión tomada conscientemente; queda documentada.

Como escape operativo se mantiene el reset de 2FA por SuperAdmin, necesario de todos modos
para bajas de personal.

### 10.5 Secreto TOTP en texto plano

Identity guarda la clave del autenticador en `AspNetUserTokens` sin cifrar. Cifrarla obliga
a renunciar a los helpers de Identity (`GetAuthenticatorKeyAsync` y compañía). Se acepta el
comportamiento por defecto y se documenta.

### 10.6 Autenticación rota en Billing y CommissionEngine

Hallazgo lateral, anterior a este trabajo y no causado por él.

`Billing/Program.cs:156,170` y `CommissionEngine/Program.cs:89,101` construyen la llave de
validación como `SymmetricSecurityKey` sobre `Jwt:Key` (HMAC), mientras que los tokens los
firman `SignupAPI` y `AdminAPI` con RSA/RS256. Además, la audiencia configurada en ambos es
`MLMConquerorGlobalEditionClients` —sin punto— contra el `MLMConquerorGlobalEdition.Clients`
que llevan los tokens emitidos.

Algoritmo equivocado y audiencia equivocada: `api/v1/billing/charge`, `api/v1/billing/refund`
y `api/v1/commissions/*` responden 401 a cualquier token legítimo. `SharedAPICenter` usa la
misma configuración simétrica pero su único controlador es de webhooks y es público a
propósito, así que no se ve afectado.

La reparación es un cambio pequeño —copiar el bloque de validación RSA que ya usan AdminAPI,
SignupAPI, BizCenter, RankEngine y TicketManagementSystem— pero **no entra en este alcance**
porque afecta a servicios de cobro y hay que verificar qué depende del comportamiento actual.
Requiere su propio ticket.

---

## 11. Orden de implementación

0. **Rotación de la llave RSA** (§10.1)
1. Librería `Authn` con sus pruebas, sin tocar nada existente
2. Librería `Notifications` (SES + Twilio) y reemplazo de `NullEmailService`
3. Migración y entidades
4. SignupAPI: tres canales, reparación del lockout, enrolamiento
5. Superficie de páginas de cuenta en `SharedComponents`
6. Step-up en AdminAPI: catálogo, filtro, endpoints, política
7. Marcado de los cuatro grupos de endpoints con `[RequiresStepUp]`
8. Auditoría y pantallas de administración

El punto 6.3 (viabilidad del `DelegatingHandler` en Blazor Server) se valida al inicio del
paso 6, no al final.
