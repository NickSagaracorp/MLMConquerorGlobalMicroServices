# Plan B — Núcleo de autenticación de dos factores y transportes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir la capacidad de emitir y verificar códigos de verificación por tres
canales —aplicación de autenticación, correo y SMS— con su modelo de datos y sus transportes
reales, sin que ninguna interfaz de usuario la consuma todavía.

**Architecture:** Una librería nueva `MLMConquerorGlobalEdition.Authn` concentra la
verificación de factores y la emisión del challenge firmado; es la generalización del
`TwoFactorChallengeService` que hoy vive dentro de SignupAPI y solo sabe de correo. Una
segunda librería `MLMConquerorGlobalEdition.Notifications` aporta los transportes reales
(SES y Twilio) sin contaminar `SharedKernel`, que es referenciado por todo. El modelo de
datos añade las columnas de preferencia de canal al usuario y tres tablas nuevas: política
de step-up, auditoría de eventos de seguridad y catálogo de plantillas de SMS.

**Tech Stack:** .NET 10, ASP.NET Identity (proveedor `Authenticator` de TOTP, ya
registrado), EF Core 10.0.11, AWSSDK.SimpleEmailV2, Twilio, xUnit + Moq +
FluentAssertions 6.12, QRCoder.

**Spec:** `docs/superpowers/specs/2026-08-27-admin-2fa-step-up-design.md` §4, §5.

**Depende de:** Plan A (completo). `JwtKeyGuard` ya existe y está cableado.

---

## Contexto que el implementador necesita

**Qué existe hoy y hay que reutilizar, no reescribir.**
`MLMConquerorGlobalEdition.SignupAPI/Services/TwoFactorChallengeService.cs` ya implementa un
challenge JWT stateless: genera un código de 6 dígitos, guarda su SHA-256 dentro de un token
firmado con la llave RSA del sistema, y lo valida al redimir. Funciona y está probado en
`Signups.Tests/Features/Auth/`. Este plan lo **mueve** a la librería nueva y lo generaliza en
dos ejes: el propósito (login / enrolamiento / step-up) y el canal (TOTP / correo / SMS).

**Lo que NO hace este plan.** No toca `LoginHandler`, no crea páginas, no cablea nada en
AdminWeb ni en BizCenterWeb. Al terminar, la librería existe y está probada, pero el login
sigue comportándose exactamente igual que hoy. Eso es deliberado: el Plan C consume esta
librería, y separarlo permite verificar el núcleo sin arrastrar la interfaz.

**El riesgo principal.** `SignupAPI` sirve tanto al portal de administración como al
BizCenter de los miembros. Cualquier cambio en el camino de autenticación afecta a los dos.
Por eso el default de `PreferredTwoFactorChannel` es `Email`, que es lo que hacen hoy los
miembros con 2FA activo.

**Identity ya trae TOTP.** `AddDefaultTokenProviders()` está registrado en AdminAPI,
SignupAPI y BizCenter, e incluye el `AuthenticatorTokenProvider`. Es decir,
`GetAuthenticatorKeyAsync`, `ResetAuthenticatorKeyAsync` y
`VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code)` funcionan
sin añadir dependencias. **No implementes TOTP a mano.**

**Credenciales de SES y Twilio.** El código lee de configuración; los valores reales van en
`appsettings.Production.json`, fuera de git. Mientras no existan, la selección por
configuración deja activos los proveedores `Null`, que registran en el log lo que habrían
enviado. Las pruebas de esta fase son unitarias y no requieren credenciales: la entrega real
se verifica en el despliegue, siguiendo `docs/deployment/`.

---

## Estructura de archivos

### Proyecto nuevo: `MLMConquerorGlobalEdition.Authn`

Referencia a `Repository` (por `ApplicationUser`) y `SharedKernel`.

Los enums `TwoFactorChannel` y `TwoFactorPurpose` **no** viven aquí: van en `Domain`
(Task 1). `ApplicationUser` necesita `TwoFactorChannel` y está en `Repository`, que no puede
depender de `Authn` porque `Authn` ya depende de `Repository`. `Domain` es la base que no
referencia nada, así que es el único sitio donde los tres proyectos pueden verlos.

| Archivo | Responsabilidad |
|---|---|
| `Abstractions/IChallengeTokenService.cs` | Emitir y validar el JWT de challenge |
| `Abstractions/ITotpEnrollmentService.cs` | Iniciar, confirmar y reiniciar el enrolamiento |
| `Abstractions/ITwoFactorService.cs` | Orquestar: elegir canal, despachar, verificar |
| `Models/ChallengeClaims.cs` | Datos extraídos de un challenge validado |
| `Models/ChallengeIssued.cs` | Resultado de emitir: token, canal, destino enmascarado |
| `Services/ChallengeTokenService.cs` | Implementación, generalizada desde SignupAPI |
| `Services/TotpEnrollmentService.cs` | Envuelve los helpers de Identity + QR |
| `Services/TwoFactorService.cs` | Orquestación, límites de intentos, antirreplay |
| `AuthnServiceCollectionExtensions.cs` | `AddAuthn()` para registrar todo |

### Proyecto nuevo: `MLMConquerorGlobalEdition.Notifications`

Referencia a `SharedKernel` y `Repository` (por el catálogo de plantillas).

| Archivo | Responsabilidad |
|---|---|
| `Email/SesEmailService.cs` | `IEmailService` sobre AWSSDK.SimpleEmailV2 |
| `Sms/TwilioSmsService.cs` | `ISmsService` sobre Twilio |
| `Sms/NullSmsService.cs` | Registra en el log; equivalente de `NullEmailService` |
| `NotificationsServiceCollectionExtensions.cs` | Selección por configuración |

### Modificaciones

| Archivo | Cambio |
|---|---|
| `SharedKernel/Interfaces/ISmsService.cs` (nuevo) | Par de `IEmailService` |
| `Repository/Identity/ApplicationUser.cs` | Cinco columnas nuevas |
| `Domain/Entities/Security/*` (nuevos) | `StepUpPolicy`, `AuthSecurityEvent` |
| `Domain/Entities/Sms/*` (nuevos) | `SmsTemplate`, `SmsTemplateLocalization` |
| `Repository/Context/AppDbContext.cs` | Cuatro `DbSet` nuevos |
| `Repository/Configurations/**` | Configuración EF de las entidades nuevas |
| `MLMConquerorGlobalEdition.slnx` | Registrar los cuatro proyectos nuevos |

### Proyectos de prueba nuevos

`MLMConquerorGlobalEdition.Authn.Tests` y `MLMConquerorGlobalEdition.Notifications.Tests`,
con las mismas versiones de paquete que `Signups.Tests` (xUnit 2.9.2, Moq 4.20.70,
FluentAssertions 6.12.0, Microsoft.NET.Test.Sdk 17.12.0, coverlet.collector 6.0.2).

---

## Task 1: Modelo de datos

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Identity/ApplicationUser.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Security/StepUpPolicy.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Security/AuthSecurityEvent.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Security/SecurityEnums.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Sms/SmsTemplate.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Sms/SmsTemplateLocalization.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/Security/StepUpPolicyConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/Security/AuthSecurityEventConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/Sms/SmsTemplateConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/Sms/SmsTemplateLocalizationConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs`

- [ ] **Step 1: Añadir las columnas a `ApplicationUser`**

Al final de la clase, antes de la llave de cierre:

```csharp
    /// <summary>Canal preferido para los códigos de verificación. Email por defecto:
    /// preserva el comportamiento de los miembros del BizCenter que ya tienen 2FA.</summary>
    public TwoFactorChannel PreferredTwoFactorChannel { get; set; } = TwoFactorChannel.Email;

    public DateTime? TwoFactorEnrolledAt { get; set; }

    /// <summary>Teléfono para SMS, cifrado con IEncryptionService. No se reutiliza
    /// IdentityUser.PhoneNumber porque está en texto plano y aquí es a la vez PII y
    /// factor de autenticación.</summary>
    public string? TwoFactorPhoneEncrypted { get; set; }

    /// <summary>Últimos 4 dígitos, para enmascarar en la interfaz sin desencriptar.</summary>
    public string? TwoFactorPhoneLast4 { get; set; }

    public bool TwoFactorPhoneConfirmed { get; set; }
```

`TwoFactorChannel` se declara en `Domain` (Step 2), no en `Authn`: `Repository` lo necesita
para `ApplicationUser` y no puede depender de `Authn`, que a su vez depende de `Repository`.
`Repository` ya referencia `Domain`, y `Authn` lo obtiene de forma transitiva. Añade a
`ApplicationUser.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Security;
```

- [ ] **Step 2: Crear los enums en Domain**

`MLMConquerorGlobalEdition.Domain/Entities/Security/SecurityEnums.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Security;

public enum TwoFactorChannel
{
    Authenticator = 0,
    Email         = 1,
    Sms           = 2
}

public enum TwoFactorPurpose
{
    Login      = 0,
    Enrollment = 1,
    StepUp     = 2
}

public enum StepUpCategory
{
    Money           = 0,
    Identity        = 1,
    FinancialConfig = 2,
    BusinessData    = 3
}

public enum AuthEventOutcome
{
    Issued   = 0,
    Verified = 1,
    Failed   = 2,
    Denied   = 3
}

public enum AuthEventType
{
    LoginTwoFactorIssued    = 0,
    LoginTwoFactorVerified  = 1,
    LoginTwoFactorFailed    = 2,
    EnrollmentStarted       = 3,
    EnrollmentCompleted     = 4,
    TwoFactorDisabledByAdmin = 5,
    PhoneAdded              = 6,
    PhoneVerified           = 7,
    EmailConfirmed          = 8,
    PasswordChanged         = 9,
    StepUpIssued            = 10,
    StepUpVerified          = 11,
    StepUpFailed            = 12,
    StepUpDenied            = 13,
    StepUpPolicyChanged     = 14
}
```

El valor `Email = 1` y no `0` es deliberado: el default de una columna `int` no anulable en
SQL Server es `0`, y quiero que el default explícito de C# (`Email`) sea el que mande, no un
accidente del motor. Si `Authenticator` fuera `1` y `Email` `0`, una fila insertada por SQL
directo quedaría en `Email` por casualidad y no por decisión. Tal como está, una fila con `0`
queda en `Authenticator`, que exige enrolamiento y por tanto falla de forma visible en vez de
silenciosa.

- [ ] **Step 3: Crear `StepUpPolicy`**

`MLMConquerorGlobalEdition.Domain/Entities/Security/StepUpPolicy.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Security;

/// <summary>
/// Política por operación crítica: si exige código, por qué canal, y cuántos minutos dura
/// la confirmación antes de volver a pedirla. Se siembra desde el catálogo en código al
/// arrancar; las claves que desaparecen del código se marcan obsoletas, nunca se borran,
/// porque los registros de auditoría las referencian.
/// </summary>
public class StepUpPolicy
{
    /// <summary>Clave estable, ej. "PAYOUT_BATCH_RELEASE". Es la PK.</summary>
    public string OperationKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public StepUpCategory Category { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Null = usar el canal preferido del usuario.</summary>
    public TwoFactorChannel? RequiredChannel { get; set; }

    /// <summary>0 = pedir código en cada operación, sin ventana.</summary>
    public int FreshnessWindowMinutes { get; set; } = 15;

    /// <summary>La clave ya no existe en el catálogo del código.</summary>
    public bool IsObsolete { get; set; }

    public DateTime? LastUpdateDate { get; set; }
    public string? LastUpdateBy { get; set; }
}
```

- [ ] **Step 4: Crear `AuthSecurityEvent`**

`MLMConquerorGlobalEdition.Domain/Entities/Security/AuthSecurityEvent.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Security;

/// <summary>
/// Bitácora de eventos de seguridad de autenticación: 2FA de login, enrolamiento, alta de
/// teléfono, cambios de contraseña, resets por administrador, step-up y cambios de política.
/// Un solo lugar donde mirar cuando hay que reconstruir qué pasó con una cuenta.
/// </summary>
public class AuthSecurityEvent : AuditChangesLongKey
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>Desnormalizado a propósito: sobrevive a la baja de la cuenta.</summary>
    public string UserEmail { get; set; } = string.Empty;

    public AuthEventType EventType { get; set; }

    public AuthEventOutcome Outcome { get; set; }

    /// <summary>Null en eventos que no son de step-up.</summary>
    public string? OperationKey { get; set; }

    public TwoFactorChannel? Channel { get; set; }

    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestPath { get; set; }

    /// <summary>Identificador del challenge, para correlacionar emisión con verificación.</summary>
    public string? ChallengeJti { get; set; }
}
```

- [ ] **Step 5: Crear el catálogo de SMS**

`MLMConquerorGlobalEdition.Domain/Entities/Sms/SmsTemplate.cs` y
`SmsTemplateLocalization.cs`, espejando `EmailTemplate` / `EmailTemplateLocalization`
(mira esos dos archivos y calca la forma):

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Sms;

public class SmsTemplate : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<SmsTemplateLocalization> Localizations { get; set; } = [];
}
```

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Sms;

public class SmsTemplateLocalization : AuditChangesIntKey
{
    public int SmsTemplateId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Cuerpo del mensaje. Máximo 480 caracteres: tres segmentos GSM-7.
    /// Más allá de eso Twilio cobra por segmento adicional y algunos operadores truncan.</summary>
    public string Body { get; set; } = string.Empty;

    public SmsTemplate? SmsTemplate { get; set; }
}
```

- [ ] **Step 6: Configuraciones EF**

Cuatro archivos siguiendo el patrón de
`Repository/Configurations/Email/EmailTemplateConfiguration.cs` (léelo primero).

`StepUpPolicyConfiguration`: `HasKey(p => p.OperationKey)`, `OperationKey` con
`HasMaxLength(64)`, `DisplayName` con `HasMaxLength(128)`.

`AuthSecurityEventConfiguration`: `UserId` con `HasMaxLength(450)` y clave foránea a
`AspNetUsers` con `DeleteBehavior.Restrict` —no queremos que borrar un usuario borre su
rastro de auditoría—; `UserEmail` `HasMaxLength(256)`; `FailureReason` `HasMaxLength(128)`;
`IpAddress` `HasMaxLength(45)` (cabe una IPv6); `UserAgent` `HasMaxLength(512)`;
`RequestPath` `HasMaxLength(256)`; `ChallengeJti` `HasMaxLength(64)`. Tres índices:
`(UserId, CreationDate)`, `(OperationKey, CreationDate)`, `(EventType, CreationDate)`.

`SmsTemplateConfiguration`: índice único en `EventType`.
`SmsTemplateLocalizationConfiguration`: índice único en `(SmsTemplateId, LanguageCode)`,
`Body` con `HasMaxLength(480)`, relación con `SmsTemplate` en cascada.

- [ ] **Step 6b: Configuración EF de `ApplicationUser`**

Sin esto, EF genera las dos columnas de teléfono como `nvarchar(max)`: almacenamiento
potencialmente fuera de fila, imposible de indexar, y absurdo para un campo de 4 caracteres.

Crea `Repository/Configurations/Identity/ApplicationUserConfiguration.cs` como
`IEntityTypeConfiguration<ApplicationUser>`, restringiendo **solo** las columnas nuevas:

- `TwoFactorPhoneEncrypted` → `HasMaxLength(256)`
- `TwoFactorPhoneLast4` → `HasMaxLength(4)`

No toques las columnas que Identity ya define. Verifica que `ApplyConfigurationsFromAssembly`
la recoge; si `IdentityDbContext` la ignora, asegúrate de que se aplica **después** de
`base.OnModelCreating`.

**No uses `HasDefaultValue(TwoFactorChannel.Email)`.** Es la solución que parece obvia y
tiene una trampa: EF decide si incluye una columna en el INSERT comparándola con un valor
centinela, que por defecto es el valor CLR por defecto — `0` para un enum. Con
`HasDefaultValue(1)`, un usuario cuyo canal sea legítimamente `Authenticator` (`0`) haría
que EF omitiera la columna y la base de datos aplicara `1` = `Email`: se guardaría un canal
distinto del que el usuario eligió, en silencio. El default de C# en la propiedad ya cubre a
los usuarios nuevos.

- [ ] **Step 7: Registrar los `DbSet`**

En `AppDbContext.cs`, junto a los demás:

```csharp
    public DbSet<StepUpPolicy> StepUpPolicies => Set<StepUpPolicy>();
    public DbSet<AuthSecurityEvent> AuthSecurityEvents => Set<AuthSecurityEvent>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<SmsTemplateLocalization> SmsTemplateLocalizations => Set<SmsTemplateLocalization>();
```

Con los `using` correspondientes. Comprueba cómo se aplican las configuraciones en
`OnModelCreating` —si usa `ApplyConfigurationsFromAssembly`, no hace falta registrar cada una.

- [ ] **Step 8: Generar la migración**

```bash
dotnet ef migrations add Add2faStepUpAndSmsTemplates \
  --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.SignupAPI
```

**Revisa el archivo generado antes de seguir.** Debe contener exactamente: cinco columnas
nuevas en `AspNetUsers` y cuatro tablas nuevas. Si aparece cualquier otro cambio —columnas
renombradas, tablas alteradas que no tocaste— **para y reporta**: significa que el snapshot
del modelo estaba desincronizado y ese cambio ajeno se colaría en esta migración.

Comprueba que `TwoFactorPhoneEncrypted` salió como `nvarchar(256)` y `TwoFactorPhoneLast4`
como `nvarchar(4)`. Si salieron `nvarchar(max)`, falta el Step 6b.

**Añade a mano el backfill**, justo después del `AddColumn` de `PreferredTwoFactorChannel`
(EF no lo genera solo):

```csharp
// Las filas existentes quedan en Email, que es el canal que ya usan hoy los miembros
// con 2FA activo. Sin esto heredarian Authenticator (valor 0 del enum) y se les
// pediria un codigo TOTP que nunca enrolaron.
migrationBuilder.Sql("UPDATE AspNetUsers SET PreferredTwoFactorChannel = 1;");
```

Sin este `UPDATE`, la columna se crea con `defaultValue: 0` y **todos los usuarios que hoy
tienen 2FA por correo quedarían apuntando a la aplicación de autenticación**, un canal en el
que nunca se enrolaron. Al cablearse la selección de canal en el Plan C se les pediría un
código TOTP que no pueden generar. El fallo aparecería semanas después y lejos de su causa.

- [ ] **Step 9: Verificar que compila**

```bash
dotnet build MLMConquerorGlobalEdition.slnx
dotnet test MLMConquerorGlobalEdition.slnx
```

Esperado: 0 errores, 1462 pruebas en verde. No apliques la migración a ninguna base de datos
en este paso.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(2fa): modelo de datos para 2FA, step-up y plantillas de SMS

Cinco columnas en ApplicationUser para canal preferido y telefono cifrado;
tablas StepUpPolicy, AuthSecurityEvent y el catalogo SmsTemplate.

El telefono va cifrado con IEncryptionService en vez de reutilizar
IdentityUser.PhoneNumber, que esta en texto plano y aqui es a la vez PII y
factor de autenticacion."
```

---

## Task 2: Proyecto Authn y el challenge firmado

**Files:**
- Create: `MLMConquerorGlobalEdition.Authn/MLMConquerorGlobalEdition.Authn.csproj`
- Create: `MLMConquerorGlobalEdition.Authn/Models/ChallengeClaims.cs`
- Create: `MLMConquerorGlobalEdition.Authn/Abstractions/IChallengeTokenService.cs`
- Create: `MLMConquerorGlobalEdition.Authn/Services/ChallengeTokenService.cs`
- Create: `MLMConquerorGlobalEdition.Authn.Tests/...`
- Modify: `MLMConquerorGlobalEdition.slnx`

**Punto de partida obligatorio:** lee
`MLMConquerorGlobalEdition.SignupAPI/Services/TwoFactorChallengeService.cs` completo antes de
escribir nada. Este servicio es esa clase, generalizada. No lo reinventes.

- [ ] **Step 1: Crear el proyecto y registrarlo**

```bash
dotnet new classlib -n MLMConquerorGlobalEdition.Authn -f net10.0
dotnet add MLMConquerorGlobalEdition.Authn reference MLMConquerorGlobalEdition.SharedKernel MLMConquerorGlobalEdition.Repository
dotnet add MLMConquerorGlobalEdition.Authn package System.IdentityModel.Tokens.Jwt
dotnet add MLMConquerorGlobalEdition.Authn package Microsoft.Extensions.Configuration.Abstractions
dotnet add MLMConquerorGlobalEdition.Authn package Microsoft.Extensions.DependencyInjection.Abstractions
```

Borra el `Class1.cs` que genera la plantilla.

Añade a `MLMConquerorGlobalEdition.slnx`, en orden alfabético entre las líneas existentes:

```xml
  <Project Path="MLMConquerorGlobalEdition.Authn/MLMConquerorGlobalEdition.Authn.csproj" />
  <Project Path="MLMConquerorGlobalEdition.Authn.Tests/MLMConquerorGlobalEdition.Authn.Tests.csproj" />
```

Crea también el proyecto de pruebas, copiando los `PackageReference` de
`MLMConquerorGlobalEdition.Signups.Tests/MLMConquerorGlobalEdition.Signups.Tests.csproj`
(mismas versiones) y su `Usings.cs`/`GlobalUsings` si lo tiene.

- [ ] **Step 2: El modelo de claims**

`Models/ChallengeClaims.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.Authn.Models;

/// <summary>Datos extraídos de un challenge ya validado.</summary>
/// <param name="Jti">Identificador único, usado para el antirreplay y la auditoría.</param>
/// <param name="CodeHash">SHA-256 del código enviado. Null cuando el canal es Authenticator:
/// ahí el código lo genera la aplicación del usuario y lo verifica Identity.</param>
public sealed record ChallengeClaims(
    string           Jti,
    string           UserId,
    string           Email,
    TwoFactorPurpose Purpose,
    string?          OperationKey,
    TwoFactorChannel Channel,
    string?          CodeHash,
    DateTime         IssuedAt,
    DateTime         ExpiresAt);
```

- [ ] **Step 3: Escribir las pruebas que fallan**

En `MLMConquerorGlobalEdition.Authn.Tests/Services/ChallengeTokenServiceTests.cs`. Genera un
par RSA al vuelo en el `IConfiguration` de prueba, igual que hace
`Signups.Tests/Services/JwtServiceTests.cs` (léelo y calca `GeneratePrivateKeyBase64`).

Casos obligatorios:

1. `Issue_ThenValidate_RoundTrips` — emitir y validar devuelve los mismos claims
2. `Validate_WhenPurposeDiffers_Fails` — un challenge emitido para `Login` **no** valida cuando se espera `StepUp`. Es la separación que impide que un código pedido para entrar autorice un pago
3. `Validate_WhenOperationKeyDiffers_Fails` — un challenge de `StepUp:PAYOUT_BATCH_RELEASE` no vale para `StepUp:SYSTEM_USER_DELETE`
4. `Validate_WhenExpired_ReturnsCodeExpired` — pasado el tiempo de vida, código de error `CODE_EXPIRED`
5. `Validate_WhenTamperedSignature_Fails` — alterar un carácter del token lo invalida
6. `Validate_WhenIssuedByAnotherKey_Fails` — un token firmado con otro par RSA no valida
7. `Issue_WhenChannelIsAuthenticator_HasNoCodeHash` — el challenge de TOTP no lleva hash
8. `Validate_AllowExpired_WithinGraceWindow_Succeeds` — para el reenvío
9. `Validate_AllowExpired_BeyondGraceWindow_Fails` — pasada la ventana de gracia

Usa `Result<T>` de `SharedKernel` para los retornos, igual que hace el servicio actual.

- [ ] **Step 4: Ejecutar y confirmar que fallan**

```bash
dotnet test MLMConquerorGlobalEdition.Authn.Tests
```

Esperado: falla de compilación, `ChallengeTokenService` no existe.

- [ ] **Step 5: Implementar**

`Abstractions/IChallengeTokenService.cs`:

```csharp
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Authn.Abstractions;

public interface IChallengeTokenService
{
    TimeSpan ChallengeLifetime { get; }
    TimeSpan ResendGraceWindow { get; }

    string GenerateCode();
    string HashCode(string code);

    string Issue(
        string           userId,
        string           email,
        TwoFactorPurpose purpose,
        TwoFactorChannel channel,
        string?          codeHash,
        string?          operationKey = null);

    /// <summary>
    /// Valida firma, vigencia y que el propósito y la operación coincidan con lo esperado.
    /// </summary>
    Result<ChallengeClaims> Validate(
        string           challengeToken,
        TwoFactorPurpose expectedPurpose,
        string?          expectedOperationKey = null,
        bool             allowExpired = false);
}
```

`Services/ChallengeTokenService.cs` es el actual de SignupAPI con estos cambios:

- El claim `purpose` deja de ser la constante `"2fa-challenge"` y pasa a llevar el valor del
  enum; para `StepUp` se combina con la operación en la forma `step_up:{OPERATION_KEY}`
- Claims nuevos: `channel` y, cuando aplica, `operation_key`
- `code_hash` pasa a ser opcional: ausente cuando el canal es `Authenticator`
- `Validate` recibe el propósito esperado y **rechaza** si no coincide
- Las llaves RSA se leen con `JwtKeyGuard.ValidatePrivateKey` / `ValidatePublicKey` en vez
  del `?? throw` actual, igual que hicimos en el resto del sistema

Conserva sin cambios: la generación del código de 6 dígitos con `RandomNumberGenerator`, el
hash SHA-256, y `ChallengeLifetime` / `ResendGraceWindow` leídos de
`Auth:TwoFactor:ChallengeLifetimeMinutes` y `Auth:TwoFactor:ResendGraceWindowMinutes`.

`MaskEmail` **no** va aquí: se mueve a `TwoFactorService` en la Task 4, junto a `MaskPhone`.

- [ ] **Step 6: Ejecutar y confirmar que pasan**

```bash
dotnet test MLMConquerorGlobalEdition.Authn.Tests
dotnet build MLMConquerorGlobalEdition.slnx
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(2fa): libreria Authn con el challenge firmado generalizado

Generaliza el TwoFactorChallengeService de SignupAPI en dos ejes: proposito
(login/enrolamiento/step-up) y canal (TOTP/correo/SMS).

El claim de proposito impide que un codigo pedido para iniciar sesion sirva
para autorizar una operacion critica, y viceversa: el mismo token redimido
contra otro endpoint. Para step-up el proposito incluye la operacion, asi que
un codigo para liberar un lote de payout no vale para borrar un usuario."
```

---

## Task 3: Enrolamiento TOTP

**Files:**
- Create: `MLMConquerorGlobalEdition.Authn/Abstractions/ITotpEnrollmentService.cs`
- Create: `MLMConquerorGlobalEdition.Authn/Models/TotpEnrollment.cs`
- Create: `MLMConquerorGlobalEdition.Authn/Services/TotpEnrollmentService.cs`
- Create: `MLMConquerorGlobalEdition.Authn.Tests/Services/TotpEnrollmentServiceTests.cs`

- [ ] **Step 1: Añadir QRCoder**

```bash
dotnet add MLMConquerorGlobalEdition.Authn package QRCoder
```

QRCoder genera el PNG en proceso, sin llamadas a ningún servicio externo. **No uses una API
de generación de QR por URL:** enviaría el secreto TOTP del usuario a un tercero.

- [ ] **Step 2: Escribir las pruebas que fallan**

Necesitas un `UserManager<ApplicationUser>` mockeado; usa
`Signups.Tests/Helpers/UserManagerHelper.cs` como referencia y crea el equivalente en
`Authn.Tests/Helpers/`.

Casos:

1. `BeginAsync_ResetsKeyAndReturnsUri` — llama `ResetAuthenticatorKeyAsync`, y el URI
   devuelto empieza por `otpauth://totp/` y contiene el secreto y el emisor
2. `BeginAsync_ReturnsQrAsPngDataUri` — el QR devuelto empieza por `data:image/png;base64,`
3. `ConfirmAsync_WhenCodeValid_EnablesTwoFactor` — llama `SetTwoFactorEnabledAsync(user, true)`,
   fija `TwoFactorEnrolledAt` y `PreferredTwoFactorChannel = Authenticator`
4. `ConfirmAsync_WhenCodeInvalid_DoesNotEnable` — devuelve error `CODE_INVALID` y **no**
   llama `SetTwoFactorEnabledAsync`
5. `ResetAsync_DisablesAndClearsEnrollment` — desactiva 2FA, limpia `TwoFactorEnrolledAt`

- [ ] **Step 3: Implementar**

`Models/TotpEnrollment.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Authn.Models;

/// <param name="SharedKey">El secreto en base32, para entrada manual si no se puede escanear.</param>
/// <param name="AuthenticatorUri">URI otpauth:// que codifica el QR.</param>
/// <param name="QrCodePngDataUri">PNG en data-URI, listo para un &lt;img src&gt;.</param>
public sealed record TotpEnrollment(string SharedKey, string AuthenticatorUri, string QrCodePngDataUri);
```

`Services/TotpEnrollmentService.cs`:

```csharp
public async Task<Result<TotpEnrollment>> BeginAsync(ApplicationUser user, CancellationToken ct = default)
{
    await _userManager.ResetAuthenticatorKeyAsync(user);
    var key = await _userManager.GetAuthenticatorKeyAsync(user);
    if (string.IsNullOrEmpty(key))
        return Result<TotpEnrollment>.Failure("ENROLLMENT_FAILED", "No se pudo generar la clave del autenticador.");

    var uri = $"otpauth://totp/{Uri.EscapeDataString(_issuer)}:{Uri.EscapeDataString(user.Email!)}" +
              $"?secret={key}&issuer={Uri.EscapeDataString(_issuer)}&digits=6&period=30";

    using var generator = new QRCodeGenerator();
    using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
    using var png = new PngByteQRCode(data);
    var dataUri = "data:image/png;base64," + Convert.ToBase64String(png.GetGraphic(10));

    return Result<TotpEnrollment>.Success(new TotpEnrollment(key, uri, dataUri));
}
```

`ConfirmAsync` verifica con
`_userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code)`
y, si es válido, activa 2FA y actualiza el usuario. `ResetAsync` desactiva y limpia.

`_issuer` sale de `Auth:TwoFactor:Issuer`, con `"MLMConqueror"` por defecto. Es lo que verá
el usuario en su aplicación de autenticación, así que debe ser reconocible.

- [ ] **Step 4: Ejecutar, verificar, commitear**

```bash
dotnet test MLMConquerorGlobalEdition.Authn.Tests
git add -A
git commit -m "feat(2fa): enrolamiento TOTP sobre el proveedor de Identity

Usa GetAuthenticatorKeyAsync/VerifyTwoFactorTokenAsync de Identity, ya
disponibles via AddDefaultTokenProviders. El QR se genera en proceso con
QRCoder: una API externa de QR recibiria el secreto TOTP del usuario."
```

---

## Task 4: Orquestación, límites y antirreplay

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Interfaces/ISmsService.cs` (ver nota de orden)
- Create: `MLMConquerorGlobalEdition.Authn/Abstractions/ITwoFactorService.cs`
- Create: `MLMConquerorGlobalEdition.Authn/Models/ChallengeIssued.cs`
- Create: `MLMConquerorGlobalEdition.Authn/Services/TwoFactorService.cs`
- Create: `MLMConquerorGlobalEdition.Authn/AuthnServiceCollectionExtensions.cs`
- Create: `MLMConquerorGlobalEdition.Authn.Tests/Services/TwoFactorServiceTests.cs`

Esta es la pieza con más lógica del plan. Léela entera antes de empezar.

> **Corrección de orden respecto a la versión original del plan.** La Task 5 creaba
> `ISmsService`, pero las pruebas de esta tarea lo mockean: tal como estaba escrito, esta
> tarea no compilaba. La **interfaz** se crea aquí, en `SharedKernel`, junto a
> `IEmailService`. La Task 5 se queda solo con las implementaciones y el proyecto
> `Notifications`.

- [ ] **Step 1: El contrato**

```csharp
public interface ITwoFactorService
{
    /// <summary>Elige el canal, genera y despacha el código, y devuelve el challenge.</summary>
    Task<Result<ChallengeIssued>> IssueAsync(
        ApplicationUser   user,
        TwoFactorPurpose  purpose,
        string?           operationKey = null,
        TwoFactorChannel? forcedChannel = null,
        CancellationToken ct = default);

    /// <summary>Verifica el código contra el challenge. Consume el challenge: un mismo
    /// token no puede redimirse dos veces.</summary>
    Task<Result<ChallengeClaims>> VerifyAsync(
        string            challengeToken,
        string            code,
        TwoFactorPurpose  expectedPurpose,
        string?           expectedOperationKey = null,
        CancellationToken ct = default);
}
```

```csharp
public sealed record ChallengeIssued(
    string           ChallengeToken,
    TwoFactorChannel Channel,
    string           MaskedTarget,
    DateTime         ExpiresAt);
```

- [ ] **Step 2: Escribir las pruebas que fallan**

Mockea `IChallengeTokenService`, `UserManager<ApplicationUser>`, `IEmailService`,
`ISmsService`, `ICacheService` y `IDateTimeProvider`.

**Selección de canal:**
1. `IssueAsync_UsesPreferredChannel_WhenNoneForced`
2. `IssueAsync_UsesForcedChannel_WhenGiven`
3. `IssueAsync_WhenSmsRequestedButPhoneNotConfirmed_ReturnsChannelUnavailable`
4. `IssueAsync_WhenAuthenticatorRequestedButNotEnrolled_ReturnsChannelUnavailable`

**Despacho:**
5. `IssueAsync_WhenEmail_SendsEmailWithCode` — verifica que `IEmailService.SendAsync` recibe
   el evento `TWO_FACTOR_CODE` y el código en las variables
6. `IssueAsync_WhenSms_SendsSms`
7. `IssueAsync_WhenAuthenticator_SendsNothing` — no se llama ni al correo ni al SMS
8. `IssueAsync_WhenTransportThrows_ReturnsChannelUnavailable_AndDoesNotIssue` — si SES o
   Twilio fallan, **no** se devuelve un challenge: el usuario esperaría un código que no va
   a llegar

**Enmascarado:**
9. `IssueAsync_MasksEmail` — `usuario@dominio.com` → `u*******@dominio.com`
10. `IssueAsync_MasksPhone` — muestra solo los últimos 4 dígitos

**Verificación:**
11. `VerifyAsync_WhenEmailCodeCorrect_Succeeds`
12. `VerifyAsync_WhenEmailCodeWrong_Fails`
13. `VerifyAsync_WhenAuthenticatorCode_DelegatesToIdentity`
14. `VerifyAsync_WhenChallengeAlreadyConsumed_Fails` — antirreplay del challenge
15. `VerifyAsync_WhenTotpCodeReused_Fails` — antirreplay del código TOTP

**Límites:**
16. `VerifyAsync_AfterFiveFailedAttempts_BurnsChallenge` — el sexto intento devuelve
    `TOO_MANY_ATTEMPTS` aunque el código sea correcto
17. `IssueAsync_AfterThreeIssuesInWindow_ReturnsTooManyRequests`

- [ ] **Step 3: Implementar**

Puntos que importan y son fáciles de hacer mal:

**Comparación del código en tiempo constante.** Al comparar el hash del código recibido con
el del challenge, usa `CryptographicOperations.FixedTimeEquals` sobre los bytes, no `==`
sobre las cadenas. Es barato y elimina la clase entera de ataque por temporización.

**Antirreplay del challenge.** Tras una verificación exitosa, guarda el `jti` en caché con
`ICacheService` hasta la expiración del challenge, y recházalo si ya está. Sin esto el mismo
token sirve dos veces dentro de su ventana de vida.

**Antirreplay del código TOTP.** Identity acepta el mismo código durante unos 90 segundos
por tolerancia de reloj. Para step-up sobre operaciones de dinero eso significa que un código
puede autorizar dos veces. Guarda `2fa:totp:{userId}:{sha256(code)}` durante 90 segundos.

**Límite de intentos.** Contador en caché por `jti`, máximo 5. Al superarlo, marca el
challenge como consumido: hay que pedir uno nuevo.

**Límite de emisiones.** Contador por `userId`, máximo 3 cada 15 minutos. **Sin esto, un
atacante que conozca un correo puede bombardear con SMS a costa de la empresa**, porque
Twilio cobra por mensaje. Los valores salen de configuración
(`Auth:TwoFactor:MaxAttemptsPerChallenge`, `Auth:TwoFactor:MaxIssuesPerWindow`,
`Auth:TwoFactor:IssueWindowMinutes`) con esos defaults.

**Claves de caché.** Añádelas a `SharedKernel/CacheKeys.cs`, donde ya viven las demás; no las
escribas en línea.

**El idioma del mensaje.** `IEmailService.SendAsync` recibe `languageCode`. Para un usuario
con `MemberProfileId`, léelo de `MemberProfile.DefaultLanguage` igual que hace hoy
`LoginHandler`; para staff, usa `"en"`. No dupliques esa consulta: recíbela como parámetro
opcional y deja que quien llama la resuelva.

- [ ] **Step 4: Registro en DI**

`AuthnServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddAuthn(this IServiceCollection services)
{
    services.AddScoped<IChallengeTokenService, ChallengeTokenService>();
    services.AddScoped<ITotpEnrollmentService, TotpEnrollmentService>();
    services.AddScoped<ITwoFactorService, TwoFactorService>();
    return services;
}
```

- [ ] **Step 5: Ejecutar, verificar, commitear**

```bash
dotnet test MLMConquerorGlobalEdition.Authn.Tests
dotnet build MLMConquerorGlobalEdition.slnx
git add -A
git commit -m "feat(2fa): orquestacion de los tres canales con limites y antirreplay

Selecciona el canal, despacha el codigo y verifica, con una sola superficie
para TOTP, correo y SMS.

Antirreplay en dos niveles: el challenge se consume al verificarse, y el
codigo TOTP se marca durante 90 segundos porque Identity lo acepta ese
tiempo por tolerancia de reloj -para una operacion de dinero eso significa
autorizar dos veces con el mismo codigo-.

Limite de 3 emisiones cada 15 minutos por usuario: sin el, quien conozca un
correo puede bombardear con SMS a costa de la empresa, porque Twilio cobra
por mensaje."
```

---

## Task 5: `ISmsService` y el transporte de Twilio

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Interfaces/ISmsService.cs`
- Create: `MLMConquerorGlobalEdition.Notifications/` (proyecto)
- Create: `MLMConquerorGlobalEdition.Notifications/Sms/NullSmsService.cs`
- Create: `MLMConquerorGlobalEdition.Notifications/Sms/TwilioSmsService.cs`
- Create: `MLMConquerorGlobalEdition.Notifications.Tests/...`
- Modify: `MLMConquerorGlobalEdition.slnx`

> **La interfaz `ISmsService` ya se creó en la Task 4** (ver la corrección de orden allí).
> Este paso queda como referencia de su contrato; si ya existe, no la dupliques.

- [ ] **Step 1: La interfaz, junto a `IEmailService`**

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Envía SMS transaccionales usando el catálogo SmsTemplate, igual que IEmailService usa
/// EmailTemplate: la implementación busca la plantilla por eventType + languageCode,
/// sustituye variables y entrega por el transporte configurado.
/// </summary>
public interface ISmsService
{
    /// <param name="toPhoneE164">Teléfono en formato E.164, ej. "+14155552671".</param>
    Task SendAsync(
        string toPhoneE164,
        string languageCode,
        string eventType,
        Dictionary<string, string> variables,
        CancellationToken ct = default);
}
```

`SharedKernel` **no** recibe la dependencia de Twilio: solo la interfaz.

- [ ] **Step 2: El proyecto Notifications**

```bash
dotnet new classlib -n MLMConquerorGlobalEdition.Notifications -f net10.0
dotnet add MLMConquerorGlobalEdition.Notifications reference MLMConquerorGlobalEdition.SharedKernel MLMConquerorGlobalEdition.Repository
dotnet add MLMConquerorGlobalEdition.Notifications package Twilio
dotnet add MLMConquerorGlobalEdition.Notifications package AWSSDK.SimpleEmailV2
```

Registra los dos proyectos nuevos en `MLMConquerorGlobalEdition.slnx`.

- [ ] **Step 3: `NullSmsService`**

Calca `SharedKernel/Services/NullEmailService.cs`: registra en el log lo que habría enviado.
Es lo que queda activo mientras no haya credenciales, y lo que usan las pruebas.

- [ ] **Step 4: `TwilioSmsService` con sus pruebas**

Configuración: `Notifications:Sms:Twilio:AccountSid`, `:AuthToken`, `:FromNumber`.

El envío en sí es una llamada a `MessageResource.CreateAsync`. Lo que sí hay que probar y
tiene lógica propia:

1. `SendAsync_LooksUpTemplateByEventTypeAndLanguage`
2. `SendAsync_FallsBackToEnglish_WhenLanguageMissing` — mismo comportamiento que el catálogo
   de correo
3. `SendAsync_SubstitutesVariables` — `{{Code}}` en el cuerpo se reemplaza
4. `SendAsync_WhenTemplateMissing_Throws` — con un mensaje que nombre el `eventType`; un SMS
   silenciosamente no enviado es peor que un error
5. `SendAsync_WhenPhoneNotE164_Throws` — valida el formato antes de gastar una llamada a
   Twilio

Para probar sin pegarle a Twilio, aísla la llamada tras una interfaz interna
(`ITwilioMessageSender`) con una implementación real y una falsa. Que la lógica de plantillas
y validación sea probable sin red.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(notifications): ISmsService y transporte de Twilio

La interfaz vive en SharedKernel junto a IEmailService; la implementacion en
un proyecto aparte para no arrastrar el SDK de Twilio a la libreria que
referencian todos los servicios.

El catalogo de plantillas espeja el de correo, asi que el texto del SMS es
localizable en los 9 idiomas y las futuras notificaciones por SMS tienen
donde vivir."
```

---

## Task 6: Transporte de correo real (SES)

**Files:**
- Create: `MLMConquerorGlobalEdition.Notifications/Email/SesEmailService.cs`
- Create: `MLMConquerorGlobalEdition.Notifications/NotificationsServiceCollectionExtensions.cs`
- Create: `MLMConquerorGlobalEdition.Notifications.Tests/Email/SesEmailServiceTests.cs`
- Modify: `Program.cs` de AdminAPI, SignupAPI, RankEngine y Billing

- [ ] **Step 1: `SesEmailService`**

Implementa `IEmailService` leyendo el catálogo `EmailTemplate` + `EmailTemplateLocalization`
que ya existe en la base de datos, y enviando con `AmazonSimpleEmailServiceV2Client`.

Configuración: `Notifications:Email:Ses:Region`, `:FromAddress`, `:FromName`. Las
credenciales las toma la cadena de proveedores del SDK de AWS —rol de la instancia, variables
de entorno o perfil— igual que hace el cliente de S3 que ya se usa en el repositorio.

Misma estructura de pruebas que Twilio: aísla el envío tras una interfaz interna y prueba la
resolución de plantilla, el fallback a inglés, la sustitución de variables y el error cuando
falta la plantilla.

- [ ] **Step 2: Selección por configuración**

```csharp
public static IServiceCollection AddNotifications(
    this IServiceCollection services, IConfiguration config)
{
    var emailProvider = config["Notifications:Email:Provider"] ?? "Null";
    if (emailProvider.Equals("Ses", StringComparison.OrdinalIgnoreCase))
        services.AddScoped<IEmailService, SesEmailService>();
    else
        services.AddTransient<IEmailService, NullEmailService>();

    var smsProvider = config["Notifications:Sms:Provider"] ?? "Null";
    if (smsProvider.Equals("Twilio", StringComparison.OrdinalIgnoreCase))
        services.AddScoped<ISmsService, TwilioSmsService>();
    else
        services.AddTransient<ISmsService, NullSmsService>();

    return services;
}
```

**El default es `Null` a propósito.** Un despliegue sin configurar no debe intentar enviar
por un transporte a medio configurar; debe registrar en el log lo que habría enviado y
seguir.

- [ ] **Step 3: Reemplazar los registros existentes**

En los cuatro `Program.cs` que hoy hacen
`builder.Services.AddTransient<IEmailService, NullEmailService>()` —AdminAPI:170,
SignupAPI:123, RankEngine:62 y Billing:99— sustituir por
`builder.Services.AddNotifications(builder.Configuration);`.

Añade a los `appsettings.json` rastreados, con los proveedores en `Null`:

```json
"Notifications": {
  "Email": { "Provider": "Null", "Ses": { "Region": "us-east-1", "FromAddress": "", "FromName": "MLM Conqueror" } },
  "Sms":   { "Provider": "Null", "Twilio": { "AccountSid": "", "AuthToken": "", "FromNumber": "" } }
}
```

Los valores reales van en `appsettings.Production.json`, que está fuera de git.

- [ ] **Step 4: Verificar y commitear**

```bash
dotnet build MLMConquerorGlobalEdition.slnx
dotnet test MLMConquerorGlobalEdition.slnx
```

Esperado: 0 errores, y la batería completa en verde incluyendo los dos proyectos de prueba
nuevos. **El total debe ser 1462 más las pruebas nuevas**; si alguna preexistente falla,
repórtalo: significa que el cambio de registro de `IEmailService` rompió una expectativa.

```bash
git add -A
git commit -m "feat(notifications): transporte de correo por SES y seleccion por configuracion

Los cuatro servicios que registraban NullEmailService a mano pasan por
AddNotifications, que elige el proveedor segun configuracion. El default
sigue siendo Null: un despliegue sin credenciales registra en el log lo que
habria enviado en vez de fallar."
```

---

## Task 7: Verificación final del plan

- [ ] **Step 1: Compilar y probar todo**

```bash
dotnet build MLMConquerorGlobalEdition.slnx
dotnet test MLMConquerorGlobalEdition.slnx
```

- [ ] **Step 2: Confirmar que el comportamiento actual no cambió**

Este plan **no debe alterar** cómo se comporta el login hoy. Verifica:

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "FullyQualifiedName~Auth"
```

Las pruebas de `LoginHandler`, `VerifyTwoFactorHandler` y `ResendTwoFactorHandler` deben
seguir pasando sin haberlas tocado. Si alguna necesitó cambios, es señal de que el plan se
salió de su alcance.

- [ ] **Step 3: Confirmar que la migración no se aplicó a ninguna base**

```bash
dotnet ef migrations list --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.SignupAPI
```

`Add2faStepUpAndSmsTemplates` debe aparecer como pendiente. Aplicarla es parte del
despliegue, no de este plan.

---

## Qué queda para el Plan C

- `LoginHandler` con los tres canales y la rama de enrolamiento obligatorio
- Las páginas de cuenta en `SharedComponents`
- El arreglo de `AuthEndpoints.LoginAsync` en AdminWeb, que hoy ignora `RequiresTwoFactor`
- Semillas de las plantillas `TWO_FACTOR_CODE` y `STEP_UP_CODE` en los 9 idiomas
