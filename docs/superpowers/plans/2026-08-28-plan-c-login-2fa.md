# Plan C — Login con dos factores y enrolamiento forzado

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que un administrador entre al portal con dos factores: elija canal, reciba el código,
lo verifique, y que quien no tenga 2FA configurado quede atrapado en el enrolamiento antes de
poder navegar.

**Architecture:** `LoginHandler` deja de generar y enviar el código a mano y pasa a delegar en
`ITwoFactorService`, la librería construida en el Plan B. El `TwoFactorChallengeService` local
de SignupAPI se retira. En AdminWeb, `AuthEndpoints.LoginAsync` deja de ignorar
`RequiresTwoFactor` —que hoy rompe el login de cualquier admin con 2FA— y encamina hacia
verificación o enrolamiento según lo que responda la API. Las páginas nuevas viven en
`SharedComponents` para que BizCenterWeb las herede.

**Tech Stack:** .NET 10, Blazor (InteractiveServer), ASP.NET Identity, xUnit + Moq +
FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-08-27-admin-2fa-step-up-design.md` §6.1, §6.2, §10.2.

**Depende de:** Plan A y Plan B completos.

---

## Contexto que el implementador necesita

**El estado real hoy, verificado.** El 2FA por correo nunca funcionó, por tres causas
independientes que se tapaban unas a otras:

1. `IEmailService` estaba registrado como `NullEmailService`: el código nunca salía. Resuelto
   en el Plan B, pero el proveedor sigue en `Null` por configuración hasta que haya credenciales.
2. `JwtSecurityTokenHandler` renombraba `sub` y `email`, así que el servicio rechazaba su propio
   challenge. Resuelto en el Plan A (`1459bb7`).
3. **La tabla `EmailTemplates` está vacía.** No hay ninguna fila `TWO_FACTOR_CODE`. Este plan la
   siembra.

**Lo que este plan cambia y a quién afecta.** `SignupAPI` sirve al portal de administración y al
BizCenter de los miembros. Tocar `LoginHandler` los toca a los dos. La red de seguridad es que
`PreferredTwoFactorChannel` vale `Email` para todos los usuarios existentes —lo garantiza el
backfill de la migración del Plan B—, así que un miembro con 2FA activo sigue recibiendo su
código por correo exactamente igual que antes.

**El enrolamiento forzado se activa por configuración, y arranca desactivado.**
`Auth:TwoFactor:MandatoryRoles` sale **vacío**. Con la lista vacía nadie es forzado a enrolarse
y el comportamiento no cambia para nadie. Se llena **después** de verificar en producción que
SES y Twilio entregan de verdad. Si se llenara antes, un administrador cuyo canal sea correo se
quedaría fuera en cuanto el transporte fallara, y con 2FA obligatorio eso es perder el acceso al
portal.

**El challenge nunca viaja en la URL.** En la URL queda en el historial del navegador, en los
registros del proxy y en la cabecera `Referer` hacia cualquier recurso externo que cargue la
página. Va en una cookie HttpOnly de vida corta.

---

## Estructura de archivos

| Archivo | Cambio |
|---|---|
| `SignupAPI/DTOs/Auth/AuthResponse.cs` | Campos de enrolamiento y canal |
| `SignupAPI/Features/Auth/Commands/Login/LoginHandler.cs` | Delega en `ITwoFactorService` |
| `SignupAPI/Features/Auth/Commands/VerifyTwoFactor/VerifyTwoFactorHandler.cs` | Íd. |
| `SignupAPI/Features/Auth/Commands/ResendTwoFactor/ResendTwoFactorHandler.cs` | Íd. |
| `SignupAPI/Features/Auth/Commands/Enrollment/*` (nuevos) | Begin y Confirm |
| `SignupAPI/Controllers/AuthController.cs` | Dos endpoints nuevos |
| `SignupAPI/Services/TwoFactorChallengeService.cs` + su interfaz | **Se eliminan** |
| `SignupAPI/Program.cs` | `AddAuthn()`, retirar el registro viejo |
| `Repository/Migrations/*` (nueva) | Semillas de plantillas |
| `AdminWeb/Services/AuthEndpoints.cs` | Reescrito |
| `AdminWeb/Program.cs` | Endpoints nuevos |
| `SharedComponents/Components/Account/*` (nuevos) | Páginas de verificación y enrolamiento |
| `AdminWeb/Components/Pages/*` | Montaje de las páginas |

---

## Task 1: `LoginHandler` sobre la librería

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/DTOs/Auth/AuthResponse.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Auth/Commands/Login/LoginHandler.cs`
- Modify: `MLMConquerorGlobalEdition.Signups.Tests/Features/Auth/LoginHandlerTests.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Program.cs`

- [ ] **Step 1: Extender `AuthResponse`**

Añadir, conservando lo existente:

```csharp
    /// <summary>
    /// True cuando el usuario tiene un rol que exige 2FA pero no lo ha configurado. El cliente
    /// debe llevarlo al enrolamiento con <see cref="EnrollmentToken"/>; no hay tokens de acceso
    /// hasta que lo complete.
    /// </summary>
    public bool RequiresEnrollment { get; set; }

    /// <summary>JWT de propósito Enrollment. No es un token de acceso: no abre ningún endpoint de negocio.</summary>
    public string? EnrollmentToken { get; set; }

    /// <summary>Canal por el que se envió el código, para que la interfaz sepa qué decir.</summary>
    public TwoFactorChannel? Channel { get; set; }

    /// <summary>Destino enmascarado: correo o teléfono según el canal. Vacío para Authenticator.</summary>
    public string? MaskedTarget { get; set; }
```

`MaskedEmail` se **conserva** y se rellena igual que hoy cuando el canal es correo. Quitarlo
rompería a cualquier cliente que ya lo lea; `MaskedTarget` lo generaliza y `MaskedEmail` queda
como alias del mismo valor para ese canal. Márcalo `[Obsolete]` con un comentario que apunte a
`MaskedTarget`.

- [ ] **Step 2: Escribir las pruebas que fallan**

En `LoginHandlerTests`, mockeando `ITwoFactorService`:

1. `Handle_WhenTwoFactorEnabled_DelegatesToTwoFactorService` — no genera ni envía el código a mano
2. `Handle_WhenTwoFactorEnabled_ReturnsChannelAndMaskedTarget`
3. `Handle_WhenRoleIsMandatoryAndNotEnrolled_ReturnsRequiresEnrollment` — con `MandatoryRoles` conteniendo el rol del usuario
4. `Handle_WhenRoleIsNotMandatoryAndNotEnrolled_LogsInNormally` — la red de seguridad: sin la lista, nada cambia
5. `Handle_WhenMandatoryRolesEmpty_NeverRequiresEnrollment` — el default de producción
6. `Handle_WhenIssueFails_ReturnsThatError` — si `IssueAsync` devuelve `CHANNEL_UNAVAILABLE`, el login lo propaga en vez de dar tokens
7. **Regresión:** `Handle_MemberWithEmailTwoFactor_BehavesAsBefore` — un miembro con `PreferredTwoFactorChannel = Email` recibe `RequiresTwoFactor`, `ChallengeToken` y `MaskedEmail` como hasta ahora

La 7 es la que protege a los miembros del BizCenter. No la omitas.

- [ ] **Step 3: Implementar**

Reemplazar el bloque de dos factores actual (líneas ~75-95, el que llama a `_twoFactor.GenerateCode()`
y `_email.SendAsync`) por:

```csharp
        var mandatoryRoles = _config.GetSection("Auth:TwoFactor:MandatoryRoles").Get<string[]>() ?? [];
        var requiresTwoFactor = roles.Any(r => mandatoryRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

        // Rol que exige 2FA pero sin configurar: no hay tokens de acceso hasta enrolarse.
        // El token de enrolamiento no abre ningún endpoint de negocio.
        if (requiresTwoFactor && !user.TwoFactorEnabled)
        {
            var enrollment = _twoFactor.IssueEnrollmentToken(user);
            return Result<AuthResponse>.Success(new AuthResponse
            {
                UserId = user.Id, Email = user.Email!,
                RequiresEnrollment = true, EnrollmentToken = enrollment
            });
        }

        if (user.TwoFactorEnabled)
        {
            var issued = await _twoFactor.IssueAsync(
                user, TwoFactorPurpose.Login, languageCode: defaultLanguage, ct: ct);

            if (!issued.IsSuccess)
                return Result<AuthResponse>.Failure(issued.ErrorCode!, issued.Error!);

            return Result<AuthResponse>.Success(new AuthResponse
            {
                UserId = user.Id, Email = user.Email!,
                RequiresTwoFactor = true,
                ChallengeToken = issued.Value!.ChallengeToken,
                Channel        = issued.Value.Channel,
                MaskedTarget   = issued.Value.MaskedTarget,
                MaskedEmail    = issued.Value.Channel == TwoFactorChannel.Email
                                     ? issued.Value.MaskedTarget : null
            });
        }
```

`IEmailService` y `ITwoFactorChallengeService` salen del constructor de `LoginHandler`: ya no los
usa. `IConfiguration` entra.

**`IssueEnrollmentToken` no existe todavía**: verificado, `ITwoFactorService` solo expone
`IssueAsync` y `VerifyAsync`. Añádelo a la librería `Authn` como parte de esta tarea:

```csharp
/// <summary>
/// Emite el token que autoriza a enrolarse, y nada más. No es un token de acceso: no abre
/// ningún endpoint de negocio, de modo que quien tiene 2FA obligatorio pendiente queda
/// atrapado en el enrolamiento en vez de poder navegar el portal a medias.
/// </summary>
string IssueEnrollmentToken(ApplicationUser user);
```

Implementación: un challenge con `TwoFactorPurpose.Enrollment` y canal `Authenticator`, sin
código ni hash —no se envía nada, el usuario todavía no tiene dónde recibirlo—. Es síncrono
porque no despacha ni toca la caché. Con su prueba en `Authn.Tests`: que el token emitido valide
con propósito `Enrollment` y **no** valide con propósito `Login`.

- [ ] **Step 4: Verificar y commitear**

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests
dotnet build MLMConquerorGlobalEdition.slnx
```

Línea base: **1544 pruebas**. Commit sin firma de AI.

---

## Task 2: Verificación, reenvío y enrolamiento

**Files:**
- Modify: `VerifyTwoFactorHandler.cs`, `ResendTwoFactorHandler.cs`
- Create: `Features/Auth/Commands/Enrollment/BeginEnrollment{Command,Handler}.cs`
- Create: `Features/Auth/Commands/Enrollment/ConfirmEnrollment{Command,Handler}.cs`
- Modify: `Controllers/AuthController.cs`
- Delete: `Services/TwoFactorChallengeService.cs`, `Services/ITwoFactorChallengeService.cs`
- Delete: `Signups.Tests/Services/TwoFactorChallengeServiceTests.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Migrar los dos handlers existentes**

`VerifyTwoFactorHandler` pasa a llamar `_twoFactor.VerifyAsync(challengeToken, code, TwoFactorPurpose.Login)`
y, si va bien, emite los tokens de acceso como hoy.

`ResendTwoFactorHandler` valida el challenge con `allowExpired: true` dentro de la ventana de
gracia y llama `IssueAsync` de nuevo. **Debe rechazar el reenvío cuando el canal es
`Authenticator`**: no hay nada que reenviar, el código lo genera la aplicación del usuario.
Añade una prueba para ese caso.

- [ ] **Step 2: Endpoints de enrolamiento**

`POST /api/v1/auth/two-factor/enroll/begin` recibe el `EnrollmentToken`, lo valida con propósito
`Enrollment`, y devuelve lo que da `ITotpEnrollmentService.BeginAsync`: clave, URI y QR.

`POST /api/v1/auth/two-factor/enroll/confirm` recibe el token y el primer código de 6 dígitos.
Si `ConfirmAsync` va bien, **emite los tokens de acceso**: el usuario queda dentro.

Ambos son anónimos: el `EnrollmentToken` es la credencial. Añade `[AllowAnonymous]` explícito si
el controlador tiene `[Authorize]` a nivel de clase.

- [ ] **Step 3: Retirar el servicio viejo**

Borra `TwoFactorChallengeService.cs`, su interfaz, sus pruebas, y el registro en `Program.cs`.
Añade `builder.Services.AddAuthn();`.

**Comprueba que no queda ninguna referencia** antes de borrar:

```bash
git grep -n "ITwoFactorChallengeService\|TwoFactorChallengeService"
```

Si algo fuera de los archivos a borrar lo referencia, migra ese sitio primero.

- [ ] **Step 4: Verificar y commitear**

Batería completa. El número bajará al borrar las pruebas del servicio viejo; **reporta el número
exacto** y de dónde sale la diferencia.

---

## Task 3: Semillas de plantillas

**Files:**
- Create: `Repository/Migrations/*_SeedTwoFactorTemplates.cs` (vía `dotnet ef migrations add`)

- [ ] **Step 1: Sembrar la plantilla de correo**

Una migración con `InsertData` sobre `EmailTemplates` y `EmailTemplateLocalizations`:

- `EmailTemplate`: `EventType = "TWO_FACTOR_CODE"`, `Name = "Código de verificación"`,
  `Category = "Security"`, `IsActive = true`
- Localizaciones con **asunto y cuerpo HTML** usando marcadores `{{Code}}` y `{{ExpiresInMinutes}}`

**Escribe solo `en` y `es`.** Los otros siete idiomas —`pt`, `fr`, `de`, `zh`, `it`, `kr`, `ge`—
**no se siembran**: el servicio ya respalda a inglés cuando falta el idioma pedido, y una
traducción automática de un correo de seguridad que llega a los usuarios reales es peor que el
inglés, porque parece revisada y no lo está. Deja un comentario en la migración diciendo qué
idiomas faltan y que requieren revisión de un hablante nativo.

- [ ] **Step 2: Sembrar la plantilla de SMS**

Lo mismo sobre `SmsTemplates` y `SmsTemplateLocalizations`, con el cuerpo en un solo texto.
Máximo 480 caracteres —tres segmentos GSM-7—; para un código de verificación deberías estar muy
por debajo. Recuerda que Twilio cobra por segmento.

- [ ] **Step 3: Verificar**

La migración no se aplica a ninguna base en este plan. Confirma que aparece como pendiente y que
contiene solo `InsertData`.

---

## Task 4: `AuthEndpoints` de AdminWeb

**Files:**
- Modify: `AdminWeb/Services/AuthEndpoints.cs`
- Modify: `AdminWeb/Program.cs`

Este archivo es la razón de que hoy activar 2FA a un administrador lo deje sin poder entrar:
lee `apiResponse.Data.AccessToken` directamente y, cuando la API responde `RequiresTwoFactor`,
ese campo viene vacío, `CanReadToken` falla y redirige a `/admin/login?error=invalid`. El
usuario ve "credenciales inválidas" cuando sus credenciales eran correctas.

- [ ] **Step 1: Reescribir `LoginAsync`**

Tras recibir la respuesta de la API, ramificar **antes** de tocar el token:

```csharp
if (apiResponse.Data.RequiresEnrollment)
{
    SetChallengeCookie(httpContext, EnrollmentCookie, apiResponse.Data.EnrollmentToken!);
    return Results.Redirect("/admin/enroll-authenticator");
}

if (apiResponse.Data.RequiresTwoFactor)
{
    SetChallengeCookie(httpContext, ChallengeCookie, apiResponse.Data.ChallengeToken!);
    return Results.Redirect("/admin/login-2fa");
}
```

`SetChallengeCookie` escribe una cookie `HttpOnly`, `Secure`, `SameSite=Strict`, con expiración
de 10 minutos. **El token no va en la URL** por las razones del preámbulo.

La comprobación de rol admin que ya existe se **mantiene** y sigue aplicándose sobre el token
final, no sobre el challenge.

- [ ] **Step 2: Endpoints nuevos**

`POST /account/login-2fa` — lee la cookie, llama `api/v1/auth/two-factor/verify`, y con los
tokens hace `SignInAsync` igual que el login normal. Borra la cookie. Si falla, redirige de
vuelta con el código de error para que la página lo muestre.

`POST /account/login-2fa/resend` — llama al reenvío y vuelve a la página.

`POST /account/enroll-authenticator` — llama a `enroll/confirm`, y con los tokens hace
`SignInAsync`.

Regístralos en `Program.cs` junto a los existentes, con `.DisableAntiforgery()` como los otros
—son formularios servidos por la propia aplicación y el flujo actual ya lo hace así—.

**Extrae la parte común**: la construcción del `ClaimsPrincipal` a partir del access token y el
`SignInAsync` se repiten en tres sitios. Un método privado.

- [ ] **Step 3: Pruebas**

`AdminWeb` no tiene proyecto de pruebas. **No crees uno solo para esto**; dilo en el reporte y
verifica este paso manualmente en la Task 6.

---

## Task 5: Páginas de verificación y enrolamiento

**Files:**
- Create: `SharedComponents/Components/Account/TwoFactorVerify.razor`
- Create: `SharedComponents/Components/Account/EnrollAuthenticator.razor`
- Create: `AdminWeb/Components/Pages/LoginTwoFactor.razor`, `EnrollAuthenticator.razor`

**Referencia obligatoria:** `BizCenterWeb/Components/Pages/TwoFactor.razor` (116 líneas). Calca
su estructura y su estilo; no inventes uno nuevo.

- [ ] **Step 1: `TwoFactorVerify.razor`**

Componente en `SharedComponents`, parametrizado para que sirva a los dos portales: recibe el
canal, el destino enmascarado, la ruta del formulario y el mensaje de error. Muestra un campo de
6 dígitos, un botón de reenvío —**oculto cuando el canal es `Authenticator`**, porque no hay nada
que reenviar— y el destino enmascarado.

`inputmode="numeric"` y `autocomplete="one-time-code"`: sin eso, en el móvil sale el teclado
alfabético y el sistema no ofrece autocompletar el código del SMS.

- [ ] **Step 2: `EnrollAuthenticator.razor`**

Muestra el QR como `<img src="@QrCodePngDataUri" />`, la clave en texto para entrada manual
—quien no pueda escanear la necesita—, y el campo del primer código.

Explica en una frase qué tiene que hacer el usuario. Alguien que nunca ha usado una aplicación
de autenticación no sabe qué es ese cuadrado.

- [ ] **Step 3: Montaje en AdminWeb**

Páginas en `/admin/login-2fa` y `/admin/enroll-authenticator`, con `BlankLayout` como hace
`Login.razor`, que envuelven los componentes compartidos y apuntan a los endpoints de la Task 4.

- [ ] **Step 4: Commit**

---

## Task 6: Verificación de extremo a extremo

Esta es la tarea que demuestra que el plan funciona. Requiere base de datos y ejecutar servicios.

- [ ] **Step 1: Aplicar migraciones a la base de desarrollo**

```bash
dotnet ef database update --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.SignupAPI
```

**Confirma con el usuario antes de ejecutarlo.** Es la primera vez en estos tres planes que se
toca una base de datos.

- [ ] **Step 2: Verificar que nada cambió con la configuración por defecto**

Con `MandatoryRoles` vacío y `Notifications:Email:Provider = Null`, arranca SignupAPI y AdminWeb
y entra con un administrador **sin** 2FA. Debe entrar exactamente como antes. Si no, el plan
rompió el login y hay que parar.

- [ ] **Step 3: Verificar el 2FA por autenticador**

Activa `TwoFactorEnabled` a un usuario de prueba con `PreferredTwoFactorChannel = Authenticator`,
entra, escanea el QR con una aplicación real, y verifica el código. Es el único canal que se
puede probar de punta a punta sin credenciales externas.

- [ ] **Step 4: Verificar el enrolamiento forzado**

Añade el rol del usuario de prueba a `MandatoryRoles` en `appsettings.Development.json`, quítale
el 2FA, y comprueba que al entrar queda en la pantalla de enrolamiento y **no puede navegar a
ninguna otra ruta** hasta completarlo.

- [ ] **Step 5: Verificar el canal correo con el proveedor nulo**

Con `Provider = Null`, el código aparece en el log en vez de enviarse. Cópialo del log y
verifica que valida. Esto prueba la cadena completa —plantilla, sustitución, challenge,
verificación— sin necesitar SES.

Si la plantilla no se encontró, el log dirá qué `eventType` faltaba.

- [ ] **Step 6: Batería completa y commit final**

---

## Qué queda para después

- **Plan D:** el resto de la superficie de cuenta (§7 del spec): recuperación de contraseña,
  confirmación de correo, alta de teléfono, gestión de 2FA, datos personales
- **Plan E:** step-up en operaciones críticas
- **Traducciones** de las plantillas a los siete idiomas restantes, con revisión nativa
- **Activar `MandatoryRoles` en producción**, solo después de verificar que SES y Twilio entregan
