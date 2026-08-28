# Plan D — Superficie de cuenta en los dos portales

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que un usuario —administrador o miembro— pueda gestionar su cuenta sin que nadie
toque la base de datos por él: recuperar la contraseña, confirmar su correo, dar de alta un
teléfono, y activar, cambiar o desactivar su segundo factor.

**Architecture:** Los componentes viven en `SharedComponents/Components/Account/`, junto a los
dos que ya existen, y los dos portales los montan con su propio layout. El backend que falta se
añade a `SignupAPI/Features/Auth/`, que ya es el servicio de autenticación de ambos. La única
pieza exclusiva de administración es desactivar el 2FA de **otro** usuario, que va en AdminAPI
porque es una operación sobre terceros.

**Tech Stack:** .NET 10, Blazor SSR, ASP.NET Identity, EF Core, xUnit + Moq + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-08-27-admin-2fa-step-up-design.md` §7.

**Depende de:** Planes A, B y C completos.

---

## Qué existe ya, y qué no

Esta es la razón de ser del plan: el inventario acordado en el spec §7 se diseñó entero pero
solo se construyeron dos páginas.

| Página | Backend | Página admin | Página miembro |
|---|---|---|---|
| Login | ✅ | ✅ | ✅ |
| Verificación de código | ✅ | ✅ | ✅ |
| Enrolamiento con QR | ✅ | ✅ | ❌ |
| ForgotPassword / ResetPassword | ✅ | ❌ | ✅ |
| ChangePassword | ✅ | ❌ | ❌ |
| Lockout | ✅ | ❌ | ❌ |
| ConfirmEmail / ResendEmailConfirmation | ❌ | ❌ | ❌ |
| Manage/Index | ❌ | ❌ | ❌ |
| AddPhoneNumber / VerifyPhoneNumber | ❌ | ❌ | ❌ |
| TwoFactorAuthentication (panel) | ❌ | ❌ | ❌ |
| SetPassword | ❌ | ❌ | ❌ |
| PersonalData / Download | ❌ | ❌ | ❌ |
| Disable2fa (sobre otro usuario) | ❌ | ❌ | — |

**Fuera de alcance por decisión previa:** `LoginWithRecoveryCode` y `GenerateRecoveryCodes` —se
eligió caída a correo/SMS en vez de códigos de respaldo— y `DeletePersonalData`, porque una
cuenta de staff está referenciada por auditoría y comisiones aprobadas: lo correcto ahí es
desactivar, no borrar.

**Las tres páginas que BizCenterWeb ya tiene** —`ForgotPassword`, `ResetPassword`,
`TwoFactor`— **no se tocan**. Funcionan y sirven a miembros reales; migrarlas a los
componentes compartidos es refactor sin valor inmediato y con riesgo de regresión. Se anota
como deuda al final.

---

## Task 1: Backend — confirmación de correo

**Files:**
- Create: `SignupAPI/Features/Auth/Commands/EmailConfirmation/{SendEmailConfirmation,ConfirmEmail}{Command,Handler}.cs`
- Create: `SignupAPI/DTOs/Auth/{SendEmailConfirmationRequest,ConfirmEmailRequest}.cs` + validadores
- Modify: `SignupAPI/Controllers/AuthController.cs`
- Create: pruebas en `Signups.Tests/Features/Auth/`
- Modify: migración de semillas — plantilla `EMAIL_CONFIRMATION`

**Contexto:** hoy los registros crean el usuario con `EmailConfirmed = false` y **nada lo
confirma nunca**, salvo los sembradores de desarrollo. No existe endpoint ni correo.

- [ ] **Step 1: Pruebas primero**

`SendEmailConfirmationHandler`:
1. Usuario existente y sin confirmar → genera token con `GenerateEmailConfirmationTokenAsync` y manda el correo
2. Usuario ya confirmado → **devuelve éxito sin mandar nada**. No revelar que ya estaba confirmado
3. Usuario inexistente → **devuelve éxito sin mandar nada**. Igual que `forgot-password`: no confirmar si un correo existe

El caso 3 es el importante. Un endpoint que responde distinto según exista o no la cuenta es
un oráculo para enumerar usuarios.

`ConfirmEmailHandler`:
4. Token válido → `ConfirmEmailAsync` y éxito
5. Token de otro usuario → falla
6. Token manipulado → falla
7. Ya confirmado → éxito idempotente

- [ ] **Step 2: Implementar**

Endpoints, ambos `[AllowAnonymous]`:
- `POST /api/v1/auth/email/send-confirmation` — cuerpo `{ email }`
- `POST /api/v1/auth/email/confirm` — cuerpo `{ userId, token }`

El token de Identity va en la URL del correo, así que **codifícalo en base64url** antes de
mandarlo y decodifícalo al recibir: contiene caracteres que se corrompen al viajar en una query.

Variables de la plantilla: `{{ConfirmationUrl}}` y `{{ExpiresInHours}}`. La URL base sale de
configuración (`Auth:PortalBaseUrl` para miembros, `Auth:AdminPortalBaseUrl` para staff);
decide cuál según si el usuario tiene `MemberProfileId`.

- [ ] **Step 3: Plantilla**

Migración nueva con `InsertData`: `EMAIL_CONFIRMATION` en inglés y español, con `TextBody`.
Mismo criterio que `TWO_FACTOR_CODE`: los otros siete idiomas caen a inglés.

- [ ] **Step 4: Verificar y commitear**

---

## Task 2: Backend — teléfono

**Files:**
- Create: `SignupAPI/Features/Auth/Commands/Phone/{AddPhone,VerifyPhone,RemovePhone}{Command,Handler}.cs`
- Create: DTOs y validadores
- Modify: `AuthController.cs`
- Create: pruebas

- [ ] **Step 1: Pruebas primero**

1. `AddPhone` cifra el número con `IEncryptionService`, guarda `TwoFactorPhoneLast4` y deja `TwoFactorPhoneConfirmed = false`
2. `AddPhone` con formato no E.164 → `INVALID_PHONE`
3. `AddPhone` **manda un código por SMS al número nuevo** usando `ITwoFactorService` con canal forzado `Sms`
4. `VerifyPhone` con código correcto → `TwoFactorPhoneConfirmed = true`
5. `VerifyPhone` con código incorrecto → falla y **no** confirma
6. `RemovePhone` limpia los tres campos y, **si el canal preferido era SMS, lo devuelve a correo**
7. Añadir un teléfono nuevo cuando ya había uno confirmado → el anterior queda sin confirmar hasta verificar el nuevo

El caso 6 evita dejar al usuario con un canal preferido que ya no tiene destino: sin eso,
quitar el teléfono lo dejaría sin poder recibir el código.

El caso 3 tiene un problema de huevo y gallina: `ITwoFactorService.IssueAsync` resuelve el
destino leyendo `TwoFactorPhoneEncrypted` del usuario, y aquí el teléfono aún no está
confirmado. Resuélvelo guardando el teléfono **antes** de emitir, con
`TwoFactorPhoneConfirmed = false`, y que `ResolveTarget` acepte SMS sin confirmar **solo**
para el propósito de verificación de teléfono. Si eso exige tocar `Authn`, hazlo y dilo en el
reporte; no lo rodees con una llamada directa a `ISmsService`, que se saltaría los límites de
emisión.

- [ ] **Step 2: Endpoints**

Todos `[Authorize]` — es gestión de la propia cuenta:
- `POST /api/v1/auth/phone` — cuerpo `{ phoneE164 }`
- `POST /api/v1/auth/phone/verify` — cuerpo `{ code }`
- `DELETE /api/v1/auth/phone`

- [ ] **Step 3: Verificar y commitear**

---

## Task 3: Backend — datos personales, contraseña y estado del 2FA

**Files:**
- Create: `SignupAPI/Features/Auth/Queries/GetAccountStatus/`
- Create: `SignupAPI/Features/Auth/Queries/GetPersonalData/`
- Create: `SignupAPI/Features/Auth/Commands/SetPassword/`
- Modify: `AuthController.cs`
- Create: pruebas

- [ ] **Step 1: `GET /api/v1/auth/account-status`**

Lo que necesita el panel de gestión para pintarse. Devuelve: correo y si está confirmado,
teléfono enmascarado y si está confirmado, si el 2FA está activo, canal preferido, fecha de
enrolamiento, y si la cuenta tiene contraseña (`HasPasswordAsync`).

**Nunca devuelve el teléfono en claro**, solo los últimos cuatro dígitos que ya están
almacenados sin cifrar para eso.

- [ ] **Step 2: `GET /api/v1/auth/personal-data`**

Los datos personales de la cuenta en JSON: los campos de `ApplicationUser` que son del usuario
—correo, teléfono enmascarado, fechas, roles— y, si tiene `MemberProfileId`, los de su
`MemberProfile`.

**No incluyas hashes de contraseña, tokens de refresco, la clave del autenticador ni el
teléfono cifrado.** Son datos del sistema, no del usuario, y exportarlos crearía una vía de
extracción de material sensible.

`GET /api/v1/auth/personal-data/download` devuelve lo mismo como descarga con
`Content-Disposition: attachment`.

- [ ] **Step 3: `POST /api/v1/auth/set-password`**

Para cuentas sin contraseña. `AddPasswordAsync`, no `ChangePasswordAsync`. Si la cuenta ya
tiene contraseña, devuelve un error que dirija a cambiarla en vez de fijarla.

Hoy no hay logins externos, así que este endpoint no tiene usuarios reales. Se construye
porque estaba en el inventario acordado y porque un SSO futuro lo necesitaría.

- [ ] **Step 4: Verificar y commitear**

---

## Task 4: Componentes — recuperación de contraseña y bloqueo

**Files:**
- Create: `SharedComponents/Components/Account/{ForgotPassword,ForgotPasswordConfirmation,ResetPassword,ResetPasswordConfirmation,Lockout}.razor`

Páginas anónimas. **Referencia:** `BizCenterWeb/Components/Pages/ForgotPassword.razor` y
`ResetPassword.razor`, que ya existen y funcionan — calca su flujo, no lo reinventes.

Textos a `SharedResources.resx` en los 9 idiomas, como se hizo con las pantallas de 2FA.
Nada en duro.

- [ ] **Step 1: `ForgotPassword`**

Un campo de correo. **La respuesta es siempre la misma exista o no la cuenta**, y la página
de confirmación no dice si se envió algo: decirlo convertiría el formulario en un
comprobador de correos registrados.

- [ ] **Step 2: `ResetPassword`**

Recibe `userId` y `token` por query. Dos campos de contraseña con confirmación. Muestra los
requisitos —mínimo 8, un dígito, una mayúscula, según `Program.cs`— **antes** de que el
usuario escriba, no como error después.

- [ ] **Step 3: `Lockout`**

Estática. Explica que la cuenta está bloqueada temporalmente por intentos fallidos y que se
desbloquea sola. **No digas cuántos minutos quedan**: sería decirle a quien está probando
contraseñas cuánto falta para poder seguir.

- [ ] **Step 4: Commit**

---

## Task 5: Componentes — layout de gestión, perfil y contraseña

**Files:**
- Create: `SharedComponents/Components/Account/AccountLayout.razor` — navegación lateral
- Create: `SharedComponents/Components/Account/{ManageIndex,ChangePassword,SetPassword}.razor`

- [ ] **Step 1: `AccountLayout`**

Navegación entre las secciones: perfil, contraseña, seguridad, datos personales. Parametrizado
con la sección activa y las rutas base, para que cada portal use las suyas.

- [ ] **Step 2: `ManageIndex`**

Correo con su estado de confirmación y un botón de reenviar si no lo está. Teléfono
enmascarado con su estado, y enlaces para añadir, verificar o quitar.

- [ ] **Step 3: `ChangePassword` y `SetPassword`**

`ChangePassword` pide la actual y la nueva dos veces. `SetPassword` solo la nueva, y la ruta
**solo se muestra si `account-status` dice que la cuenta no tiene contraseña** — enseñarla a
quien sí tiene una es ofrecerle una operación que va a fallar.

- [ ] **Step 4: Commit**

---

## Task 6: Componentes — panel de 2FA y teléfono

**Files:**
- Create: `SharedComponents/Components/Account/{TwoFactorPanel,AddPhoneNumber,VerifyPhoneNumber}.razor`

- [ ] **Step 1: `TwoFactorPanel`**

El centro de la gestión del segundo factor. Muestra el estado —activo o no, canal preferido,
fecha de enrolamiento— y ofrece:

- Activar el 2FA, que lleva al enrolamiento con QR que ya existe
- Cambiar el canal preferido, **ofreciendo solo los que están disponibles**: SMS solo si hay
  teléfono confirmado, autenticador solo si hay enrolamiento. Ofrecer un canal sin destino
  deja al usuario sin poder entrar en el siguiente inicio de sesión
- Volver a enrolar el autenticador, avisando de que **la entrada anterior de su aplicación
  dejará de funcionar**
- Desactivar el 2FA, **solo si su rol no lo exige**. Si está en `MandatoryRoles`, el botón no
  aparece y se explica por qué

- [ ] **Step 2: `AddPhoneNumber` y `VerifyPhoneNumber`**

Campo de teléfono con el formato esperado indicado (`+14155552671`) y un ejemplo. El de
verificación reutiliza el mismo campo de seis dígitos que la pantalla de login, con
`inputmode="numeric"` y `autocomplete="one-time-code"`.

- [ ] **Step 3: Commit**

---

## Task 7: Componentes — datos personales

**Files:**
- Create: `SharedComponents/Components/Account/PersonalData.razor`

Lista los datos que el sistema guarda de la cuenta y un botón de descarga en JSON.

**No hay borrado.** Está fuera de alcance por decisión previa: una cuenta de staff está
referenciada por auditoría y comisiones aprobadas, y borrarla rompería la trazabilidad. Si la
página necesita decir algo al respecto, que dirija a solicitar la baja al administrador.

- [ ] **Step 1: Commit**

---

## Task 8: Montaje en AdminWeb

**Files:**
- Create: páginas en `AdminWeb/Components/Pages/Account/`
- Modify: `AdminWeb/Services/AuthEndpoints.cs` y `Program.cs`

Rutas bajo `/admin/account/…` para las autenticadas y `/admin/…` para las anónimas, con
`BlankLayout` en estas últimas como hace `Login.razor`.

Los formularios postean a endpoints de AdminWeb que llaman a la API, siguiendo el patrón que
ya usan `login-2fa` y `enroll-authenticator`. **Extrae lo común**: a estas alturas hay muchos
manejadores que hacen lo mismo —leer el formulario, llamar a la API, redirigir con el código
de error—; si el archivo pasa de unas 400 líneas, sepáralo por área.

- [ ] **Step 1: Rutas anónimas** — forgot-password, reset-password, lockout, confirm-email
- [ ] **Step 2: Rutas de gestión** — perfil, contraseña, seguridad, teléfono, datos personales
- [ ] **Step 3: Enlace desde el menú** para llegar a la gestión de cuenta sin escribir la URL
- [ ] **Step 4: Verificar que AdminWeb arranca y las rutas responden. Commit**

---

## Task 9: Montaje en BizCenterWeb

**Files:**
- Create: páginas en `BizCenterWeb/Components/Pages/Account/`
- Modify: `BizCenterWeb/Services/AuthEndpoints.cs` y `Program.cs`

Lo mismo para el portal de miembros, **sin tocar** `ForgotPassword.razor`,
`ResetPassword.razor` ni `TwoFactor.razor`, que ya existen y funcionan.

Las páginas nuevas para miembros son: confirmación de correo, gestión de cuenta, contraseña,
panel de 2FA, teléfono, datos personales y enrolamiento con QR —que hoy solo tiene
administración—.

- [ ] **Step 1: Rutas y endpoints**
- [ ] **Step 2: Enlace desde el perfil**, que es donde un miembro buscaría esto
- [ ] **Step 3: Verificar que BizCenterWeb arranca y que el login de miembro sigue funcionando igual. Commit**

El segundo punto del paso 3 es la comprobación que importa: este plan toca el portal que usan
miembros reales.

---

## Task 10: Desactivar el 2FA de otro usuario

**Files:**
- Create: `AdminAPI/Features/SystemUsers/DisableTwoFactor/`
- Modify: `AdminAPI/Controllers/SystemUsersController.cs`
- Modify: `AdminWeb/Components/Pages/SystemUsers.razor`

Es la única pieza exclusiva de administración, y **no es autoservicio**: solo un SuperAdmin
puede desactivar el 2FA de otra cuenta. Si fuera autoservicio, cualquiera con 2FA obligatorio
podría quitárselo y la obligatoriedad no valdría nada.

- [ ] **Step 1: Endpoint** `POST /api/v1/admin/system-users/{id}/two-factor/disable`, con
  `[Authorize(Roles = "SuperAdmin")]`. Desactiva el 2FA, limpia el enrolamiento, y **registra
  un `AuthSecurityEvent`** con `TwoFactorDisabledByAdmin`, quién lo hizo y desde qué IP.

  Es una operación que reduce la seguridad de la cuenta de otra persona: sin rastro, no hay
  forma de reconstruir quién dejó a quién sin segundo factor.

- [ ] **Step 2: Botón en System Users**, con confirmación que diga qué implica.
- [ ] **Step 3: Pruebas** — un no-SuperAdmin recibe 403; se registra el evento; el usuario
  queda sin 2FA y sin enrolamiento.
- [ ] **Step 4: Commit**

---

## Task 11: Verificación final

- [ ] **Step 1: Build y batería completa.** Línea base: **1565 pruebas**.
- [ ] **Step 2: Los dos portales arrancan.**
- [ ] **Step 3: Recorrido manual en administración** — recuperar contraseña, confirmar correo,
  añadir y verificar teléfono, cambiar canal preferido, re-enrolar, descargar datos personales.
- [ ] **Step 4: Recorrido manual en BizCenter**, empezando por comprobar que un miembro
  **entra exactamente igual que antes**.
- [ ] **Step 5: Que un SuperAdmin pueda desactivar el 2FA de otra cuenta y quede en auditoría.**

---

## Qué queda después

- **Plan E: confirmación por código en operaciones críticas** — la segunda mitad del encargo
  original, todavía sin construir
- Migrar las tres páginas de BizCenterWeb a los componentes compartidos, para no mantener dos
  versiones del mismo formulario
- Traducciones revisadas por hablante nativo
