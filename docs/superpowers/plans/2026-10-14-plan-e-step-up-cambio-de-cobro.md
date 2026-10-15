# Plan E — Step-up al cambiar el método de cobro (BizCenter)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que ningún miembro pueda cambiar a dónde se le pagan las comisiones sin confirmar un código de un solo uso (correo, SMS o autenticador), cerrando la fila "OTP al cambiar método de cobro" de la matriz de paridad.

**Architecture:** Es el §6.3 de `docs/superpowers/specs/2026-08-27-admin-2fa-step-up-design.md` aplicado primero al BizCenter. SignupAPI —el único anfitrión de miembros con la llave privada— emite el reto (`step-up/challenge`) y, al verificar el código, un **permiso de step-up** firmado (`step-up/verify`). El BizCenter lo valida con la llave pública en un filtro `[RequiresStepUp]`, lo quema al primer uso (ventana 0) y deja pasar la petición. La UI reintenta con el permiso cuando la API responde `403 STEP_UP_REQUIRED`.

**Tech Stack:** .NET 10, ASP.NET Core MVC filters, System.IdentityModel.Tokens.Jwt 8.22, MediatR, Blazor Server (SharedComponents), xUnit + FluentAssertions + Moq + bUnit.

---

## Hoja de ruta de la ronda (decisiones del 2026-10-14)

| Plan | Alcance | Decisiones tomadas |
|---|---|---|
| **E (este)** | OTP / step-up al cambiar el método de cobro | Emite SignupAPI, valida el BizCenter con llave pública. Un permiso, un uso. 24 h de espera tras cambiar un factor (Tarea 4b). |
| F | Cobro desde el BizCenter + compra de tokens | Billing es el dueño del dinero; el BizCenter lo llama con el JWT del miembro. Precio del token = producto que otorga (`TokenTypeProduct.Role = Granted` → `Product`). Límite 50/mes, no reembolsable. Cripto por aprobación manual en el Admin, como el alta. |
| G | Upgrade de membresía | Se cobra el **precio completo** del nivel nuevo. Tarjeta, token de upgrade (`TokenTypeUpgradeValidator`) o cripto con aprobación manual. |

F y G se escriben cuando E esté integrado: comparten la pieza de cobro y conviene diseñarla una vez.

## Fuera de alcance de E

- **Frecuencia de pago** (`PUT profile`): cambia *cuándo* se paga, no *a dónde*; no desvía dinero. Queda sin step-up.
- **`StepUpPolicy` en BD y `AuthSecurityEvent`**: la tabla existe pero nada la lee ni la escribe. Aquí la política va en código (siempre activa, ventana 0). El historial de wallet (`MemberProfilesWalletHistory`) sigue registrando cada cambio. Leer la política de BD llega con el step-up del Admin.
- **Admin y MAUI**: el `WalletPanel` en modo admin habla con AdminAPI, que no responde `STEP_UP_REQUIRED`, así que no cambia. La MAUI no registra el cliente de step-up todavía.

## Mapa de archivos

| Archivo | Responsabilidad |
|---|---|
| `SharedKernel/Security/StepUpOperations.cs` (nuevo) | Catálogo de operaciones protegidas. |
| `SharedKernel/Security/StepUpGrant.cs` (nuevo) | Contrato del permiso: cabecera, audiencia, claims, vida, códigos de error. |
| `SharedKernel/CacheKeys.cs` | Clave de "permiso consumido". |
| `Authn/Abstractions/IStepUpGrantService.cs`, `Authn/Services/StepUpGrantService.cs` (nuevos) | Firma el permiso (llave privada). |
| `Authn/AuthnServiceCollectionExtensions.cs` | `AddAuthnStepUpGrants()`. |
| `SharedKernel.Server/Security/StepUpGrantValidator.cs` (nuevo) | Valida el permiso con la llave pública. |
| `SharedKernel.Server/Security/RequiresStepUpAttribute.cs`, `StepUpAuthorizationFilter.cs` (nuevos) | El filtro de MVC. |
| `SignupAPI/Features/Auth/Commands/StepUp/*` (nuevo) | Emitir reto y canjear código por permiso. |
| `SignupAPI/Services/TwoFactorLanguage.cs` (nuevo) | Idioma del código, compartido con `AddPhoneHandler`. |
| `SignupAPI/Controllers/AuthController.cs` | Dos endpoints `step-up/*`. |
| `BizCenter/Controllers/WalletController.cs` | `[RequiresStepUp]` en las dos puertas; se borra `PUT wallet`. |
| `BizCenter/Features/Wallet/UpdateWallet/*`, `BizCenter/DTOs/Wallet/UpdateWalletRequest.cs` + validador | Se borran (puerta trasera sin uso). |
| `SharedComponents/Components/Account/StepUp/*` (nuevo) | `StepUpClient`, `StepUpHttp`, `StepUpDialog.razor`. |
| `SharedComponents.Server/Extensions/StepUpClientRegistration.cs` (nuevo) | Cliente HTTP a SignupAPI con `ApiAuthHandler`. |
| `SharedComponents/Components/Profile/WalletPanel.razor`, `CommissionsCard.razor` | Reintento con permiso. |
| `SharedComponents/Resources/SharedResources*.resx` (9) | Textos `StepUp.*`. |
| `BizCenterWeb/Program.cs` | Registra el cliente. |

---

### Task 1: Contrato compartido (SharedKernel)

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Security/StepUpOperations.cs`
- Create: `MLMConquerorGlobalEdition.SharedKernel/Security/StepUpGrant.cs`
- Modify: `MLMConquerorGlobalEdition.SharedKernel/CacheKeys.cs` (junto a las claves `TwoFactor*`, ~línea 93)

- [x] **Step 1: Crear `StepUpOperations.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// Operaciones que exigen step-up. La clave viaja dentro del reto y del permiso, así que un
/// código confirmado para una operación no autoriza otra.
/// </summary>
/// <remarks>
/// Es un catálogo cerrado a propósito: SignupAPI se niega a emitir retos para claves que no estén
/// aquí, para que el endpoint no sirva de generador de códigos con propósitos inventados.
/// Agregar una operación es una constante más y un atributo en el endpoint.
/// </remarks>
public static class StepUpOperations
{
    /// <summary>Cambiar a dónde se pagan las comisiones: alta o edición de una wallet, o cambio de la preferida.</summary>
    public const string PayoutMethodChange = "member.payout-method.change";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        PayoutMethodChange
    };

    public static bool IsKnown(string? operationKey) =>
        operationKey is not null && Known.Contains(operationKey);
}
```

- [x] **Step 2: Crear `StepUpGrant.cs`**

```csharp
using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// Contrato del permiso de step-up: lo que SignupAPI firma al verificar el código y lo que los
/// demás servicios aceptan en <see cref="HeaderName"/>. Vive aquí para que quien firma y quien
/// valida no puedan divergir.
/// </summary>
/// <remarks>
/// <b>Audiencia propia</b>, por la misma razón que <see cref="ChallengeAudience"/>: los anfitriones
/// validan <c>Jwt:Audience</c>, así que un permiso presentado como token de acceso muere en la
/// validación estándar sin que nadie tenga que acordarse de nada. Lleva además el claim de
/// propósito, que es el segundo cinturón que ya rechazan todos los anfitriones.
/// </remarks>
public static class StepUpGrant
{
    public const string HeaderName        = "X-Step-Up-Token";
    public const string AudienceSuffix    = ".stepup";
    public const string OperationClaim    = "operation_key";
    public const string PurposePrefix     = "step_up_grant:";
    public const string RequiredErrorCode = "STEP_UP_REQUIRED";
    public const string InvalidErrorCode  = "STEP_UP_INVALID";

    /// <summary>
    /// Corta a propósito: el permiso se pide justo antes de reintentar la acción, y dentro de su
    /// vida no se puede revocar. Un solo uso lo garantiza el validador, no la vida.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    public static string AudienceFor(string accessAudience) => accessAudience + AudienceSuffix;

    public static string PurposeFor(string operationKey) => PurposePrefix + operationKey;
}
```

- [x] **Step 3: Agregar la clave de caché en `CacheKeys.cs`**, después de `TwoFactorTotpUsed`:

```csharp
    /// <summary>
    /// Marca de permiso de step-up ya usado. Se incrementa al presentarlo: el primero que llega a 1
    /// pasa, cualquier otro con el mismo jti es una repetición.
    /// </summary>
    public static string StepUpGrantConsumed(string jti) => $"stepup:grant:{jti}:consumed";
```

- [x] **Step 4: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.SharedKernel -v q -nologo`
Expected: `0 Error(s)`

- [x] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel
git commit -m "Contrato del permiso de step-up y catalogo de operaciones protegidas"
```

---

### Task 2: Firmar y validar el permiso (Authn + SharedKernel.Server)

**Files:**
- Create: `MLMConquerorGlobalEdition.Authn/Abstractions/IStepUpGrantService.cs`
- Create: `MLMConquerorGlobalEdition.Authn/Services/StepUpGrantService.cs`
- Modify: `MLMConquerorGlobalEdition.Authn/AuthnServiceCollectionExtensions.cs`
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Security/StepUpGrantValidator.cs`
- Modify: `MLMConquerorGlobalEdition.SharedKernel.Server/MLMConquerorGlobalEdition.SharedKernel.Server.csproj` (package `System.IdentityModel.Tokens.Jwt` 8.22.0)
- Modify: `MLMConquerorGlobalEdition.Authn.Tests/MLMConquerorGlobalEdition.Authn.Tests.csproj` (ProjectReference a SharedKernel.Server)
- Test: `MLMConquerorGlobalEdition.Authn.Tests/Services/StepUpGrantServiceTests.cs`

- [x] **Step 1: Escribir la prueba de ida y vuelta (falla: los tipos no existen)**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

/// <summary>
/// El permiso lo firma Authn y lo valida SharedKernel.Server en otro servicio. Estas pruebas son
/// el contrato entre los dos: si uno cambia audiencia, propósito o claims, se rompen aquí.
/// </summary>
public class StepUpGrantServiceTests
{
    private const string Issuer   = "MLMConqueror";
    private const string Audience = "MLMConquerorUsers";
    private const string UserId   = "user-001";
    private const string Op       = StepUpOperations.PayoutMethodChange;

    private static readonly DateTime FixedNow = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private static IConfiguration Config(out string publicKey)
    {
        using var rsa = RSA.Create(2048);
        publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyBase64"] = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
            ["Jwt:PublicKeyBase64"]  = publicKey,
            ["Jwt:Issuer"]           = Issuer,
            ["Jwt:Audience"]         = Audience
        }).Build();
    }

    private static Mock<IDateTimeProvider> Clock(DateTime now)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(now);
        clock.Setup(c => c.Now).Returns(now);
        return clock;
    }

    [Fact]
    public void Permiso_recien_firmado_valida_para_su_usuario_y_operacion()
    {
        var config    = Config(out _);
        var clock     = Clock(FixedNow);
        var token     = new StepUpGrantService(config, clock.Object).Issue(UserId, Op);
        var validator = new StepUpGrantValidator(config, clock.Object);

        var result = validator.Validate(token, UserId, Op);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace(); // el jti
    }

    [Fact]
    public void Permiso_no_sirve_para_otro_usuario()
    {
        var config = Config(out _);
        var clock  = Clock(FixedNow);
        var token  = new StepUpGrantService(config, clock.Object).Issue(UserId, Op);

        new StepUpGrantValidator(config, clock.Object).Validate(token, "otro-usuario", Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Permiso_no_sirve_para_otra_operacion()
    {
        var config = Config(out _);
        var clock  = Clock(FixedNow);
        var token  = new StepUpGrantService(config, clock.Object).Issue(UserId, Op);

        new StepUpGrantValidator(config, clock.Object).Validate(token, UserId, "otra.operacion")
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Permiso_vencido_no_valida()
    {
        var config = Config(out _);
        var token  = new StepUpGrantService(config, Clock(FixedNow).Object).Issue(UserId, Op);
        var later  = Clock(FixedNow.Add(StepUpGrant.Lifetime).AddSeconds(1));

        new StepUpGrantValidator(config, later.Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Permiso_no_lleva_la_audiencia_de_acceso_y_si_el_claim_de_proposito()
    {
        var config = Config(out _);
        var token  = new StepUpGrantService(config, Clock(FixedNow).Object).Issue(UserId, Op);
        var jwt    = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Audiences.Should().ContainSingle().Which.Should().Be(StepUpGrant.AudienceFor(Audience));
        jwt.Claims.Should().Contain(c => c.Type == "purpose" && c.Value == StepUpGrant.PurposeFor(Op));
    }

    [Fact]
    public void Un_reto_de_step_up_no_vale_como_permiso()
    {
        // El reto se emite antes de verificar nada; si valiera como permiso, pedir el código
        // bastaría para cambiar la wallet sin saberlo.
        var config    = Config(out _);
        var clock     = Clock(FixedNow);
        var challenge = new ChallengeTokenService(config, clock.Object).Issue(
            UserId, "u@d.com", Domain.Entities.Security.TwoFactorPurpose.StepUp,
            Domain.Entities.Security.TwoFactorChannel.Email, "hash", Op);

        new StepUpGrantValidator(config, clock.Object).Validate(challenge, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }
}
```

- [x] **Step 2: Agregar la referencia de prueba y correr (debe fallar al compilar)**

En `MLMConquerorGlobalEdition.Authn.Tests.csproj`, dentro del `ItemGroup` de `ProjectReference`:

```xml
    <ProjectReference Include="..\MLMConquerorGlobalEdition.SharedKernel.Server\MLMConquerorGlobalEdition.SharedKernel.Server.csproj" />
```

Run: `dotnet test MLMConquerorGlobalEdition.Authn.Tests --filter StepUpGrantServiceTests -nologo`
Expected: FAIL — `StepUpGrantService` / `StepUpGrantValidator` no existen.

- [x] **Step 3: Crear `IStepUpGrantService.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Authn.Abstractions;

/// <summary>
/// Firma el permiso que demuestra que el usuario acaba de confirmar un código para una operación.
/// Solo lo registra el anfitrión que verifica códigos (SignupAPI); quien lo consume lo valida con
/// la llave pública mediante <c>StepUpGrantValidator</c>.
/// </summary>
public interface IStepUpGrantService
{
    string Issue(string userId, string operationKey);
}
```

- [x] **Step 4: Crear `StepUpGrantService.cs`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Authn.Services;

/// <inheritdoc cref="IStepUpGrantService"/>
public sealed class StepUpGrantService : IStepUpGrantService
{
    private readonly RsaSecurityKey    _signingKey;
    private readonly string            _issuer;
    private readonly string            _audience;
    private readonly IDateTimeProvider _dateTime;

    public StepUpGrantService(IConfiguration config, IDateTimeProvider dateTime)
    {
        _dateTime = dateTime;
        _issuer   = config["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        var accessAudience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
        _audience = StepUpGrant.AudienceFor(accessAudience);

        var privateKeyBase64 = JwtKeyGuard.ValidatePrivateKey(config["Jwt:PrivateKeyBase64"]);
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _signingKey = new RsaSecurityKey(rsa);
    }

    public string Issue(string userId, string operationKey)
    {
        if (!StepUpOperations.IsKnown(operationKey))
            throw new ArgumentException($"Operación de step-up desconocida: {operationKey}.", nameof(operationKey));

        var now = _dateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ChallengeAudience.PurposeClaim, StepUpGrant.PurposeFor(operationKey)),
            new Claim(StepUpGrant.OperationClaim, operationKey)
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          now,
            expires:            now.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

> `JwtKeyGuard` es el mismo que usa `ChallengeTokenService`; confirmar su namespace con `grep -rn "class JwtKeyGuard"` y ajustar el `using` si no está en `SharedKernel.Configuration`.

- [x] **Step 5: Registrar en `AuthnServiceCollectionExtensions.cs`**, después de `AddAuthnChallengeTokens`:

```csharp
    /// <summary>
    /// Registra el firmante del permiso de step-up. Solo en el anfitrión que verifica códigos: es
    /// el único que puede afirmar que el usuario acaba de demostrar el segundo factor.
    /// </summary>
    public static IServiceCollection AddAuthnStepUpGrants(this IServiceCollection services)
    {
        services.AddScoped<IStepUpGrantService, StepUpGrantService>();
        return services;
    }
```

- [x] **Step 6: Agregar el paquete a SharedKernel.Server**

En `MLMConquerorGlobalEdition.SharedKernel.Server.csproj`, junto a los demás `PackageReference`:

```xml
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.22.0" />
```

- [x] **Step 7: Crear `SharedKernel.Server/Security/StepUpGrantValidator.cs`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Security;

/// <summary>
/// Valida un permiso de step-up con la llave PÚBLICA. Cualquier servicio que valide tokens de
/// acceso puede validar esto sin tener con qué firmarlo.
/// </summary>
/// <remarks>
/// No comprueba el uso único: eso necesita estado y lo hace el filtro con la caché. Aquí solo
/// firma, emisor, audiencia, vigencia, propósito, usuario y operación.
/// </remarks>
public sealed class StepUpGrantValidator
{
    private readonly RsaSecurityKey    _validationKey;
    private readonly string            _issuer;
    private readonly string            _audience;
    private readonly IDateTimeProvider _dateTime;

    public StepUpGrantValidator(IConfiguration config, IDateTimeProvider dateTime)
    {
        _dateTime = dateTime;
        _issuer   = config["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        var accessAudience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
        _audience = StepUpGrant.AudienceFor(accessAudience);

        var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(config["Jwt:PublicKeyBase64"]);
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        _validationKey = new RsaSecurityKey(rsa);
    }

    /// <returns>El jti del permiso, para que quien llama lo marque como consumido.</returns>
    public Result<string> Validate(string token, string expectedUserId, string expectedOperationKey)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedUserId))
            return Invalid();

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _issuer,
            ValidAudience            = _audience,
            IssuerSigningKey         = _validationKey,
            ClockSkew                = TimeSpan.Zero,
            // Mismo reloj que firma: ver ChallengeTokenService.ValidateLifetimeAgainstInjectedClock.
            LifetimeValidator        = (nbf, exp, _, _) =>
            {
                var now = _dateTime.UtcNow;
                return exp is not null && now < exp.Value && (nbf is null || now >= nbf.Value);
            }
        };

        System.Security.Claims.ClaimsPrincipal principal;
        try { principal = handler.ValidateToken(token, parameters, out _); }
        catch { return Invalid(); }

        var purpose = principal.FindFirst(ChallengeAudience.PurposeClaim)?.Value;
        var op      = principal.FindFirst(StepUpGrant.OperationClaim)?.Value;
        var sub     = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var jti     = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (!string.Equals(purpose, StepUpGrant.PurposeFor(expectedOperationKey), StringComparison.Ordinal) ||
            !string.Equals(op, expectedOperationKey, StringComparison.Ordinal) ||
            !string.Equals(sub, expectedUserId, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(jti))
            return Invalid();

        return Result<string>.Success(jti);
    }

    private static Result<string> Invalid() =>
        Result<string>.Failure(StepUpGrant.InvalidErrorCode, "El permiso de step-up no es válido.");
}
```

- [x] **Step 8: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.Authn.Tests --filter StepUpGrantServiceTests -nologo`
Expected: PASS (6 pruebas).

- [x] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Authn MLMConquerorGlobalEdition.SharedKernel.Server MLMConquerorGlobalEdition.Authn.Tests
git commit -m "Permiso de step-up firmado: lo emite Authn y lo valida cualquier servicio con la llave publica"
```

---

### Task 3: El filtro `[RequiresStepUp]`

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Security/RequiresStepUpAttribute.cs`
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Security/StepUpAuthorizationFilter.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/StepUpAuthorizationFilterTests.cs`

- [x] **Step 1: Escribir las pruebas (fallan: el filtro no existe)**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El filtro es la única barrera entre una sesión robada y la wallet de cobro. Cada prueba
/// comprueba que la petición NO llega a la acción (<c>context.Result</c> queda puesto), salvo la
/// del permiso bueno.
/// </summary>
public class StepUpAuthorizationFilterTests
{
    private const string Issuer   = "MLMConqueror";
    private const string Audience = "MLMConquerorUsers";
    private const string UserId   = "user-001";
    private const string Op       = StepUpOperations.PayoutMethodChange;

    private static readonly DateTime FixedNow = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly Dictionary<string, long> _counters = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public StepUpAuthorizationFilterTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(FixedNow);
        _clock.Setup(c => c.Now).Returns(FixedNow);
        _cache.Setup(c => c.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string k, TimeSpan _, CancellationToken _) =>
                  _counters[k] = _counters.GetValueOrDefault(k) + 1);
    }

    private StepUpAuthorizationFilter Filter()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PublicKeyBase64"] = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo()),
            ["Jwt:Issuer"]          = Issuer,
            ["Jwt:Audience"]        = Audience
        }).Build();
        return new StepUpAuthorizationFilter(Op, new StepUpGrantValidator(config, _clock.Object), _cache.Object);
    }

    /// <summary>Firma un permiso igual que StepUpGrantService, sin depender de Authn.</summary>
    private string Grant(string userId = UserId, string op = Op, string? jti = null, DateTime? now = null)
    {
        var issuedAt = now ?? FixedNow;
        var token = new JwtSecurityToken(
            issuer: Issuer, audience: StepUpGrant.AudienceFor(Audience),
            claims: new[]
            {
                new Claim("sub", userId),
                new Claim("jti", jti ?? Guid.NewGuid().ToString()),
                new Claim("purpose", StepUpGrant.PurposeFor(op)),
                new Claim(StepUpGrant.OperationClaim, op)
            },
            notBefore: issuedAt, expires: issuedAt.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthorizationFilterContext Context(string? header, string userId = UserId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Bearer"))
        };
        if (header is not null) http.Request.Headers[StepUpGrant.HeaderName] = header;
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(action, new List<IFilterMetadata>());
    }

    private static string? ErrorCode(AuthorizationFilterContext ctx) =>
        ((ctx.Result as ObjectResult)?.Value as ApiResponse<StepUpRequiredData>)?.ErrorCode;

    [Fact]
    public async Task Sin_cabecera_corta_con_403_STEP_UP_REQUIRED_y_dice_que_operacion()
    {
        var ctx = Context(header: null);
        await Filter().OnAuthorizationAsync(ctx);

        var result = ctx.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
        var body = result.Value.Should().BeOfType<ApiResponse<StepUpRequiredData>>().Subject;
        body.ErrorCode.Should().Be(StepUpGrant.RequiredErrorCode);
        body.Data!.OperationKey.Should().Be(Op);
    }

    [Fact]
    public async Task Permiso_valido_deja_pasar()
    {
        var ctx = Context(Grant());
        await Filter().OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task El_mismo_permiso_dos_veces_la_segunda_se_corta()
    {
        var token = Grant();
        var first = Context(token);
        await Filter().OnAuthorizationAsync(first);
        var second = Context(token);
        await Filter().OnAuthorizationAsync(second);

        first.Result.Should().BeNull();
        ErrorCode(second).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Permiso_de_otro_usuario_se_corta()
    {
        var ctx = Context(Grant(userId: "otro-usuario"));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Permiso_de_otra_operacion_se_corta()
    {
        var ctx = Context(Grant(op: "otra.operacion"));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Permiso_vencido_se_corta()
    {
        var ctx = Context(Grant(now: FixedNow.AddMinutes(-3)));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Basura_en_la_cabecera_se_corta()
    {
        var ctx = Context("no-es-un-jwt");
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter StepUpAuthorizationFilterTests -nologo`
Expected: FAIL al compilar — `StepUpAuthorizationFilter` / `StepUpRequiredData` no existen.

- [x] **Step 2: Crear `RequiresStepUpAttribute.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Security;

/// <summary>
/// Exige un permiso de step-up vigente y sin usar para <paramref name="operationKey"/> en la
/// cabecera <c>X-Step-Up-Token</c>. Ver <see cref="StepUpAuthorizationFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresStepUpAttribute : TypeFilterAttribute
{
    public RequiresStepUpAttribute(string operationKey) : base(typeof(StepUpAuthorizationFilter))
    {
        OperationKey = operationKey;
        Arguments    = new object[] { operationKey };
    }

    public string OperationKey { get; }
}
```

- [x] **Step 3: Crear `StepUpAuthorizationFilter.cs`**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Security;

/// <summary>Lo que la UI necesita para saber qué código pedir.</summary>
public sealed record StepUpRequiredData(string OperationKey);

/// <summary>
/// Deja pasar la acción solo con un permiso de step-up válido para esta operación y este usuario,
/// y lo quema en el acto: un permiso, un uso.
/// </summary>
/// <remarks>
/// Corre como filtro de autorización, antes del model binding y de la acción: sin permiso, la
/// acción no llega a ejecutarse ni a leer el cuerpo.
///
/// El permiso se consume aunque la acción falle después (por ejemplo, un identificador mal
/// escrito). Es a propósito: consumir después dejaría una ventana en la que dos peticiones
/// simultáneas con el mismo permiso pasan las dos. Repetir el código es el precio.
/// </remarks>
public sealed class StepUpAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string               _operationKey;
    private readonly StepUpGrantValidator _validator;
    private readonly ICacheService        _cache;

    public StepUpAuthorizationFilter(string operationKey, StepUpGrantValidator validator, ICacheService cache)
    {
        _operationKey = operationKey;
        _validator    = validator;
        _cache        = cache;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http   = context.HttpContext;
        var header = http.Request.Headers[StepUpGrant.HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            context.Result = Forbidden(StepUpGrant.RequiredErrorCode,
                "Esta operación requiere confirmar un código de verificación.");
            return;
        }

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? http.User.FindFirstValue("sub")
                  ?? string.Empty;

        var validated = _validator.Validate(header, userId, _operationKey);
        if (!validated.IsSuccess)
        {
            context.Result = Forbidden(StepUpGrant.InvalidErrorCode, validated.Error!);
            return;
        }

        // Incremento atómico: el primero que lo lleva a 1 se queda el permiso. La marca dura más
        // que el permiso para que no pueda reaparecer "sin usar" antes de vencer.
        var uses = await _cache.IncrementAsync(
            CacheKeys.StepUpGrantConsumed(validated.Value!),
            StepUpGrant.Lifetime + TimeSpan.FromMinutes(1),
            http.RequestAborted);

        if (uses > 1)
            context.Result = Forbidden(StepUpGrant.InvalidErrorCode, "El permiso de step-up ya se usó.");
    }

    private ObjectResult Forbidden(string code, string message)
    {
        var body = ApiResponse<StepUpRequiredData>.Fail(code, message);
        body.Data = new StepUpRequiredData(_operationKey);
        return new ObjectResult(body) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
```

> `StatusCodes` está en `Microsoft.AspNetCore.Http`; agregar ese `using` si el compilador lo pide.

- [x] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter StepUpAuthorizationFilterTests -nologo`
Expected: PASS (7 pruebas).

- [x] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel.Server MLMConquerorGlobalEdition.BizCenter.Tests/StepUpAuthorizationFilterTests.cs
git commit -m "Filtro RequiresStepUp: sin permiso valido y sin usar, la accion no se ejecuta"
```

---

### Task 4: Emitir reto y canjear código por permiso (SignupAPI)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/TwoFactorLanguage.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Auth/Commands/Phone/AddPhoneHandler.cs` (usar `TwoFactorLanguage`)
- Create: `MLMConquerorGlobalEdition.SignupAPI/DTOs/Auth/StepUpChallengeRequest.cs`, `StepUpChallengeResponse.cs`, `StepUpVerifyRequest.cs`, `StepUpGrantResponse.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Features/Auth/Commands/StepUp/IssueStepUpCommand.cs`, `IssueStepUpHandler.cs`, `VerifyStepUpCommand.cs`, `VerifyStepUpHandler.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Controllers/AuthController.cs` (después de `RemovePhone`)
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Program.cs:134` (`AddAuthnStepUpGrants()`)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Features/Auth/StepUpHandlersTests.cs`

- [x] **Step 1: Escribir las pruebas (fallan: los tipos no existen)**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Reto y canje del step-up. Lo que importa: no se emite para operaciones inventadas, y el
/// permiso solo sale para el mismo usuario y la misma operación del reto.
/// </summary>
public class StepUpHandlersTests : IDisposable
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";
    private const string Op     = StepUpOperations.PayoutMethodChange;

    private static readonly DateTime FixedNow = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<ITwoFactorService>   _twoFactor = new();
    private readonly Mock<IStepUpGrantService> _grants    = new();
    private readonly ApplicationUser _user = new() { Id = UserId, Email = Email, IsActive = true };

    public void Dispose() => _db.Dispose();

    private Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> Users()
    {
        var um = UserManagerHelper.Create();
        um.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(_user);
        return um;
    }

    private static ChallengeClaims Claims(string userId = UserId, string? op = Op) => new(
        Jti: "jti", UserId: userId, Email: Email, Purpose: TwoFactorPurpose.StepUp,
        OperationKey: op, Channel: TwoFactorChannel.Email, CodeHash: "h",
        IssuedAt: FixedNow, ExpiresAt: FixedNow.AddMinutes(5));

    [Fact]
    public async Task Reto_para_operacion_desconocida_no_emite_nada()
    {
        var handler = new IssueStepUpHandler(Users().Object, _twoFactor.Object, _db);

        var result = await handler.Handle(
            new IssueStepUpCommand(UserId, new StepUpChallengeRequest { OperationKey = "inventada" }), default);

        result.ErrorCode.Should().Be("UNKNOWN_OPERATION");
        _twoFactor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reto_se_emite_con_proposito_step_up_y_la_operacion()
    {
        _twoFactor.Setup(t => t.IssueAsync(_user, TwoFactorPurpose.StepUp, Op, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeIssued>.Success(
                      new ChallengeIssued("reto", TwoFactorChannel.Email, "u***@dominio.com", FixedNow.AddMinutes(5))));
        var handler = new IssueStepUpHandler(Users().Object, _twoFactor.Object, _db);

        var result = await handler.Handle(
            new IssueStepUpCommand(UserId, new StepUpChallengeRequest { OperationKey = Op }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ChallengeToken.Should().Be("reto");
        result.Value.Channel.Should().Be("Email");
        result.Value.MaskedTarget.Should().Be("u***@dominio.com");
    }

    [Fact]
    public async Task Codigo_bueno_devuelve_permiso_para_el_usuario_y_la_operacion()
    {
        _twoFactor.Setup(t => t.VerifyAsync("reto", "123456", TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims()));
        _grants.Setup(g => g.Issue(UserId, Op)).Returns("permiso");
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = Op, ChallengeToken = "reto", Code = "123456" }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StepUpToken.Should().Be("permiso");
    }

    [Fact]
    public async Task Reto_de_otro_usuario_no_da_permiso()
    {
        _twoFactor.Setup(t => t.VerifyAsync("reto", "123456", TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims(userId: "otro")));
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = Op, ChallengeToken = "reto", Code = "123456" }), default);

        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
        _grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Codigo_malo_propaga_el_error_y_no_da_permiso()
    {
        _twoFactor.Setup(t => t.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Failure("CODE_INVALID", "x"));
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = Op, ChallengeToken = "reto", Code = "000000" }), default);

        result.ErrorCode.Should().Be("CODE_INVALID");
        _grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Canje_para_operacion_desconocida_no_verifica_nada()
    {
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = "inventada", ChallengeToken = "reto", Code = "1" }), default);

        result.ErrorCode.Should().Be("UNKNOWN_OPERATION");
        _twoFactor.VerifyNoOtherCalls();
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter StepUpHandlersTests -nologo`
Expected: FAIL al compilar.

- [x] **Step 2: Extraer `TwoFactorLanguage.cs`** (mismo cuerpo que `AddPhoneHandler.ResolveLanguageAsync`)

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Idioma de la plantilla del código. El personal no tiene MemberProfile: devuelve null y la
/// librería cae a "en".
/// </summary>
public static class TwoFactorLanguage
{
    public static async Task<string?> ResolveAsync(AppDbContext db, ApplicationUser user, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(user.MemberProfileId))
            return null;

        var language = await db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == user.MemberProfileId)
            .Select(m => m.DefaultLanguage)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(language) ? null : language;
    }
}
```

En `AddPhoneHandler.cs`: borrar el método privado `ResolveLanguageAsync` y cambiar la llamada a `var languageCode = await TwoFactorLanguage.ResolveAsync(_db, user, ct);` (con `using MLMConquerorGlobalEdition.SignupAPI.Services;`).

- [x] **Step 3: Crear los DTOs** (`DTOs/Auth/`)

```csharp
// StepUpChallengeRequest.cs
namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
public class StepUpChallengeRequest
{
    public string OperationKey { get; set; } = string.Empty;
}

// StepUpChallengeResponse.cs
namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
/// <summary>El reto a redimir en <c>step-up/verify</c>. Channel va como texto: la UI no referencia Domain.</summary>
public class StepUpChallengeResponse
{
    public string   ChallengeToken { get; set; } = string.Empty;
    public string   Channel        { get; set; } = string.Empty;
    public string   MaskedTarget   { get; set; } = string.Empty;
    public DateTime ExpiresAt      { get; set; }
}

// StepUpVerifyRequest.cs
namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
public class StepUpVerifyRequest
{
    public string OperationKey   { get; set; } = string.Empty;
    public string ChallengeToken { get; set; } = string.Empty;
    public string Code           { get; set; } = string.Empty;
}

// StepUpGrantResponse.cs
namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
/// <summary>El permiso que va en la cabecera X-Step-Up-Token del reintento.</summary>
public class StepUpGrantResponse
{
    public string StepUpToken { get; set; } = string.Empty;
}
```

(Un archivo por clase, como el resto de `DTOs/Auth`.)

- [x] **Step 4: Crear comandos y handlers** (`Features/Auth/Commands/StepUp/`)

```csharp
// IssueStepUpCommand.cs
using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;
/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record IssueStepUpCommand(string UserId, StepUpChallengeRequest Request)
    : IRequest<Result<StepUpChallengeResponse>>;

// VerifyStepUpCommand.cs
using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;
/// <param name="UserId">Sale de las claims del token de acceso, nunca del cuerpo.</param>
public record VerifyStepUpCommand(string UserId, StepUpVerifyRequest Request)
    : IRequest<Result<StepUpGrantResponse>>;
```

```csharp
// IssueStepUpHandler.cs
using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

/// <summary>
/// Manda el código de step-up por el canal preferido del usuario. El tope de emisiones y la
/// elección de destino los aplica <see cref="ITwoFactorService"/>: un SMS sin confirmar no es un
/// canal para step-up.
/// </summary>
public class IssueStepUpHandler : IRequestHandler<IssueStepUpCommand, Result<StepUpChallengeResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITwoFactorService            _twoFactor;
    private readonly AppDbContext                 _db;

    public IssueStepUpHandler(UserManager<ApplicationUser> userManager, ITwoFactorService twoFactor, AppDbContext db)
    {
        _userManager = userManager;
        _twoFactor   = twoFactor;
        _db          = db;
    }

    public async Task<Result<StepUpChallengeResponse>> Handle(IssueStepUpCommand command, CancellationToken ct)
    {
        var op = command.Request.OperationKey;
        if (!StepUpOperations.IsKnown(op))
            return Result<StepUpChallengeResponse>.Failure("UNKNOWN_OPERATION", "Operación de step-up desconocida.");

        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<StepUpChallengeResponse>.Failure("USER_NOT_FOUND", "User not found.");

        var language = await TwoFactorLanguage.ResolveAsync(_db, user, ct);
        var issued = await _twoFactor.IssueAsync(user, TwoFactorPurpose.StepUp, op, languageCode: language, ct: ct);
        if (!issued.IsSuccess)
            return Result<StepUpChallengeResponse>.Failure(issued.ErrorCode!, issued.Error!);

        return Result<StepUpChallengeResponse>.Success(new StepUpChallengeResponse
        {
            ChallengeToken = issued.Value!.ChallengeToken,
            Channel        = issued.Value.Channel.ToString(),
            MaskedTarget   = issued.Value.MaskedTarget,
            ExpiresAt      = issued.Value.ExpiresAt
        });
    }
}
```

```csharp
// VerifyStepUpHandler.cs
using MediatR;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

/// <summary>
/// Canjea el código por el permiso. La librería comprueba propósito y operación dentro del reto,
/// aplica el límite de intentos y quema el reto; aquí falta solo que el reto sea de quien llama.
/// </summary>
public class VerifyStepUpHandler : IRequestHandler<VerifyStepUpCommand, Result<StepUpGrantResponse>>
{
    private readonly ITwoFactorService   _twoFactor;
    private readonly IStepUpGrantService _grants;

    public VerifyStepUpHandler(ITwoFactorService twoFactor, IStepUpGrantService grants)
    {
        _twoFactor = twoFactor;
        _grants    = grants;
    }

    public async Task<Result<StepUpGrantResponse>> Handle(VerifyStepUpCommand command, CancellationToken ct)
    {
        var req = command.Request;
        if (!StepUpOperations.IsKnown(req.OperationKey))
            return Result<StepUpGrantResponse>.Failure("UNKNOWN_OPERATION", "Operación de step-up desconocida.");

        var verified = await _twoFactor.VerifyAsync(
            req.ChallengeToken, req.Code, TwoFactorPurpose.StepUp, req.OperationKey, ct);
        if (!verified.IsSuccess)
            return Result<StepUpGrantResponse>.Failure(verified.ErrorCode!, verified.Error!);

        // Mismo corte que VerifyPhoneHandler: un reto ajeno no puede autorizar a esta cuenta.
        if (!string.Equals(verified.Value!.UserId, command.UserId, StringComparison.Ordinal))
            return Result<StepUpGrantResponse>.Failure("INVALID_CHALLENGE", "Este challenge no es de esta cuenta.");

        return Result<StepUpGrantResponse>.Success(new StepUpGrantResponse
        {
            StepUpToken = _grants.Issue(command.UserId, req.OperationKey)
        });
    }
}
```

- [x] **Step 5: Endpoints en `AuthController.cs`**, después de `RemovePhone` (y `using ...Features.Auth.Commands.StepUp;`):

```csharp
    /// <summary>
    /// Manda un código de step-up para <c>OperationKey</c> por el canal preferido del usuario.
    /// </summary>
    /// <remarks>Autenticado: el usuario sale de las claims, nunca del cuerpo.</remarks>
    [HttpPost("step-up/challenge")]
    [Authorize]
    public async Task<IActionResult> StepUpChallenge(
        [FromBody] StepUpChallengeRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new IssueStepUpCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<StepUpChallengeResponse>.Ok(result.Value!))
            : BadRequest(ApiResponse<StepUpChallengeResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Canjea el código por el permiso que va en <c>X-Step-Up-Token</c>.</summary>
    [HttpPost("step-up/verify")]
    [Authorize]
    public async Task<IActionResult> StepUpVerify(
        [FromBody] StepUpVerifyRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new VerifyStepUpCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<StepUpGrantResponse>.Ok(result.Value!))
            : BadRequest(ApiResponse<StepUpGrantResponse>.Fail(result.ErrorCode!, result.Error!));
    }
```

- [x] **Step 6: Registrar en `SignupAPI/Program.cs`**, después de `builder.Services.AddAuthnTwoFactor();`:

```csharp
builder.Services.AddAuthnStepUpGrants();
```

- [x] **Step 7: Correr las pruebas de SignupAPI completas** (incluye `AddPhoneHandlerTests`, que cubre la extracción del idioma)

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo`
Expected: PASS, sin regresiones.

- [x] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI MLMConquerorGlobalEdition.Signups.Tests
git commit -m "SignupAPI emite el reto de step-up y canjea el codigo por un permiso de un solo uso"
```

---

### Task 4b: Endurecer el step-up de SignupAPI (agregada tras la revisión de la Tarea 4)

**Por qué.** La revisión de la Tarea 4 encontró que, con un token de acceso robado (vive hasta 15 min tras revocar), se puede primero reapuntar el canal del código —cambiar el correo (no pide contraseña), enrolar un autenticador propio, o dar de alta y confirmar un teléfono propio y cambiar el canal preferido— y después pedir el step-up, que llega al atacante. Decisión del dueño (2026-10-14): **espera de 24 h** tras cualquier cambio de factor antes de emitir o canjear un step-up, más **aviso al dueño** del cambio. La revisión encontró además una carrera en el canje del reto, que la plantilla del código no dice qué autoriza, y que el step-up comparte el cupo de emisiones del login.

**Requisitos:**

1. **Marca de cambio de factor, centralizada.** Columna nueva `ApplicationUser.SecurityFactorsChangedAt` (`DateTime?`, hora de servidor `Now`, no `UtcNow`) con migración. Se sella en **un solo sitio** que no dependa de que cada handler se acuerde: al guardar un `ApplicationUser` cuyo `Email`/`NormalizedEmail`, `TwoFactorPhoneEncrypted`, `TwoFactorPhoneConfirmed`, `PreferredTwoFactorChannel`, `TwoFactorEnrolledAt` (o la clave del autenticador en `AspNetUserTokens`) cambió. Preferir un `SaveChangesInterceptor` o el override existente de `AppDbContext`. Pruebas que cubran cada propiedad y que un cambio ajeno (p. ej. `LastLoginDate`) no sella.
2. **Espera.** `IssueStepUpHandler` y `VerifyStepUpHandler` rechazan con `STEP_UP_COOLDOWN` si `SecurityFactorsChangedAt` es posterior a `Now - Auth:StepUp:FactorChangeCooldownHours` (por defecto 24); el mensaje incluye hasta cuándo. `VerifyStepUpHandler` recarga el usuario y comprueba además `IsActive`. La UI (Tarea 6) traduce `STEP_UP_COOLDOWN` con su propia clave.
3. **Aviso al dueño.** Evento nuevo `SECURITY_FACTOR_CHANGED` (plantilla de correo sembrada por migración en los mismos idiomas que `TWO_FACTOR_CODE`): al correo **anterior** cuando cambia el correo (BizCenter `UpdateEmailHandler`, que hoy no envía correo: agregar `AddNotifications` al BizCenter igual que SignupAPI/AdminAPI) y al correo actual cuando cambia teléfono confirmado, autenticador o canal (handlers de SignupAPI). Un fallo de envío se registra y no revierte el cambio.
4. **Reto de un solo canje, atómico.** En `TwoFactorService.VerifyAsync`, sustituir el par `IsConsumedAsync`/`MarkConsumedAsync` por `ICacheService.TryClaimOnceAsync` sobre `TwoFactorChallengeConsumed(jti)` **después** de verificar el código: solo quien gana el claim recibe éxito. Mismo tratamiento para el antirreplay TOTP. Prueba de carrera: dos verificaciones simultáneas del mismo reto, una sola gana.
5. **Plantilla propia.** `NotificationEvents.StepUpCode = "STEP_UP_CODE"`, sembrada (correo y SMS, mismos idiomas) con un texto que diga que el código confirma un cambio sensible en la cuenta, como a dónde se pagan las comisiones, y qué hacer si no lo pidió. `DispatchAsync` elige el evento según el propósito.
6. **Cupo separado.** El step-up cuenta en una clave propia (`CacheKeys.StepUpIssueWindow(userId)`), para que pedir step-ups no agote los códigos de login del dueño.
7. **Rastro.** `ILogger` en ambos handlers: emisión, canje, `INVALID_CHALLENGE`, `TOO_MANY_ATTEMPTS`, `STEP_UP_COOLDOWN` (usuario, operación, nunca el código ni el token).
8. **Pruebas faltantes** de la revisión: usuario inactivo o inexistente al emitir, fallos de `IssueAsync` propagados, el idioma llega a `IssueAsync`, la operación del request llega a `VerifyAsync`, validadores de los dos DTOs.

**Commits:** uno por bloque (marca + espera, aviso, reto atómico + plantilla + cupo).

---

### Task 5: Proteger las puertas de la wallet (BizCenter)

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter/Controllers/WalletController.cs` (`SaveWallet` ~:126, `SetDefault` ~:148, borrar `UpdateWallet` :40-48)
- Delete: `MLMConquerorGlobalEdition.BizCenter/Features/Wallet/UpdateWallet/` (carpeta completa)
- Delete: `MLMConquerorGlobalEdition.BizCenter/DTOs/Wallet/UpdateWalletRequest.cs`, `MLMConquerorGlobalEdition.BizCenter/DTOs/Wallet/Validators/UpdateWalletRequestValidator.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Validators/BizCenterDtoValidatorTests.cs` (quitar las pruebas de `UpdateWalletRequestValidator`)
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (registrar `StepUpGrantValidator`)
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/PuertasDeLaWalletTests.cs`

- [x] **Step 1: Prueba de inventario (falla: las acciones no llevan el atributo y `UpdateWallet` existe)**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Toda acción del BizCenter que escribe en las wallets de cobro tiene que exigir step-up. Esta
/// prueba recorre el controlador entero, no una lista de nombres: si mañana alguien agrega una
/// puerta nueva sin el atributo, falla aquí y no en producción.
/// </summary>
public class PuertasDeLaWalletTests
{
    private static IEnumerable<MethodInfo> AccionesQueEscribenWallets() =>
        typeof(WalletController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .Any(a => a.HttpMethods.Any(v => v != "GET")
                       && (a.Template ?? string.Empty).StartsWith("wallet", StringComparison.OrdinalIgnoreCase)));

    [Fact]
    public void Hay_al_menos_las_dos_puertas_conocidas()
    {
        AccionesQueEscribenWallets().Select(m => m.Name)
            .Should().Contain(new[] { nameof(WalletController.SaveWallet), nameof(WalletController.SetDefault) });
    }

    [Fact]
    public void Toda_accion_que_escribe_wallets_exige_step_up_de_cambio_de_cobro()
    {
        foreach (var action in AccionesQueEscribenWallets())
            action.GetCustomAttribute<RequiresStepUpAttribute>()?.OperationKey
                .Should().Be(StepUpOperations.PayoutMethodChange, $"{action.Name} cambia a dónde se paga");
    }

    [Fact]
    public void La_ruta_vieja_PUT_wallet_ya_no_existe()
    {
        typeof(WalletController).GetMethod("UpdateWallet").Should().BeNull(
            "sobrescribía la cuenta de cobro sin historial y sin step-up");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter PuertasDeLaWalletTests -nologo`
Expected: FAIL (`Toda_accion...` y `La_ruta_vieja...`).

- [x] **Step 2: Borrar la ruta vieja**

```bash
git rm -r MLMConquerorGlobalEdition.BizCenter/Features/Wallet/UpdateWallet
git rm MLMConquerorGlobalEdition.BizCenter/DTOs/Wallet/UpdateWalletRequest.cs MLMConquerorGlobalEdition.BizCenter/DTOs/Wallet/Validators/UpdateWalletRequestValidator.cs
```

En `WalletController.cs`: borrar la acción `UpdateWallet` (líneas 40-48) y el `using ...Features.Wallet.UpdateWallet;`. En `BizCenterDtoValidatorTests.cs`: borrar las pruebas y el campo que usan `UpdateWalletRequest` / `UpdateWalletRequestValidator`.

Confirmar que nadie más los usa:
Run: `grep -rn "UpdateWalletRequest\|UpdateWalletCommand\|UpdateWalletHandler" --include=*.cs --include=*.razor . | grep -v /obj/`
Expected: sin resultados.

- [x] **Step 3: Poner el atributo en las dos puertas** (`using MLMConquerorGlobalEdition.SharedKernel.Security; using MLMConquerorGlobalEdition.SharedKernel.Server.Security;`)

```csharp
    /// <summary>PUT /api/v1/bizcenter/wallets/{walletType} — create/update an account for that gateway.</summary>
    /// <remarks>Exige step-up: cambia la cuenta a la que se pagan las comisiones.</remarks>
    [HttpPut("wallets/{walletType}")]
    [RequiresStepUp(StepUpOperations.PayoutMethodChange)]
    public async Task<IActionResult> SaveWallet(
```

```csharp
    /// <summary>PUT /api/v1/bizcenter/wallets/{walletType}/default — mark this gateway as default.</summary>
    /// <remarks>Exige step-up: cambia a dónde se pagan las comisiones y, si la wallet no existe, la crea.</remarks>
    [HttpPut("wallets/{walletType}/default")]
    [RequiresStepUp(StepUpOperations.PayoutMethodChange)]
    public async Task<IActionResult> SetDefault(
```

- [x] **Step 4: Registrar el validador en `BizCenter/Program.cs`**, junto a las demás líneas `AddSingleton/AddScoped` de servicios (después de `ICacheService`, ~:192):

```csharp
// Valida el permiso de step-up con la llave pública. El BizCenter no firma nada: el permiso lo
// emite SignupAPI al verificar el código. Ver StepUpGrant.
builder.Services.AddSingleton<MLMConquerorGlobalEdition.SharedKernel.Server.Security.StepUpGrantValidator>();
```

> El filtro resuelve de DI `StepUpGrantValidator` (esta línea) e `ICacheService` (ya registrado); sin cualquiera de los dos, el atributo revienta al construir el filtro. `IConnectionMultiplexer` ya quedó registrado en `BizCenter/Program.cs` (arreglo de la revisión, igual que AdminAPI): sin él, `TryClaimOnceAsync` marca el permiso solo dentro del proceso y se podría repetir en otra instancia.
>
> Singleton: solo guarda la llave y lee `IDateTimeProvider`. Confirmar con `grep -n "IDateTimeProvider" MLMConquerorGlobalEdition.BizCenter/Program.cs` que el de SharedKernel está registrado como singleton; si es scoped, registrar el validador `AddScoped`.

- [x] **Step 5: Correr todo BizCenter.Tests**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`
Expected: PASS, sin regresiones.

- [x] **Step 6: Commit**

```bash
git add -A MLMConquerorGlobalEdition.BizCenter MLMConquerorGlobalEdition.BizCenter.Tests
git commit -m "Cambiar la wallet de cobro exige step-up, y se borra la ruta vieja que la sobrescribia sin rastro"
```

---

### Task 6: La UI pide el código y reintenta

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Account/StepUp/StepUpClient.cs`
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Account/StepUp/StepUpHttp.cs`
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Account/StepUp/StepUpDialog.razor`
- Create: `MLMConquerorGlobalEdition.SharedComponents.Server/Extensions/StepUpClientRegistration.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenterWeb/Program.cs` (después de `AddAuthApiClient`)
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Profile/WalletPanel.razor` (`SaveEditAsync` :522, `SetDefaultAsync` :621)
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Profile/CommissionsCard.razor` (`SaveAsync`, paso 2)
- Modify: los 9 `SharedResources*.resx`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/StepUpHttpTests.cs`

- [x] **Step 1: Prueba de la detección (falla: `StepUpHttp` no existe)**

```csharp
using System.Net;
using System.Text;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class StepUpHttpTests
{
    private static HttpResponseMessage Resp(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Un_403_STEP_UP_REQUIRED_pide_codigo()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "{\"success\":false,\"errorCode\":\"STEP_UP_REQUIRED\"}");
        (await StepUpHttp.IsStepUpRequiredAsync(resp)).Should().BeTrue();
    }

    [Fact]
    public async Task Un_403_de_otra_cosa_no_pide_codigo()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "{\"success\":false,\"errorCode\":\"FORBIDDEN\"}");
        (await StepUpHttp.IsStepUpRequiredAsync(resp)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_cuerpo_que_no_es_json_no_revienta()
    {
        var resp = Resp(HttpStatusCode.Forbidden, "<html>");
        (await StepUpHttp.IsStepUpRequiredAsync(resp)).Should().BeFalse();
    }

    [Fact]
    public void El_reintento_lleva_el_permiso_en_la_cabecera()
    {
        var req = StepUpHttp.WithGrant(new HttpRequestMessage(HttpMethod.Put, "x"), "permiso");
        req.Headers.GetValues("X-Step-Up-Token").Should().ContainSingle().Which.Should().Be("permiso");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter StepUpHttpTests -nologo`
Expected: FAIL al compilar.

- [x] **Step 2: Crear `StepUpHttp.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;

/// <summary>Lo mínimo para que un componente reconozca el 403 de step-up y reintente con el permiso.</summary>
public static class StepUpHttp
{
    private sealed class Envelope { public string? ErrorCode { get; set; } }

    /// <remarks>Lee el cuerpo con LoadIntoBufferAsync para que quien llama pueda volver a leerlo si no era step-up.</remarks>
    public static async Task<bool> IsStepUpRequiredAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Forbidden) return false;
        try
        {
            await response.Content.LoadIntoBufferAsync();
            var body = await response.Content.ReadFromJsonAsync<Envelope>();
            return string.Equals(body?.ErrorCode, StepUpGrant.RequiredErrorCode, StringComparison.Ordinal);
        }
        catch { return false; }
    }

    public static HttpRequestMessage WithGrant(HttpRequestMessage request, string? stepUpToken)
    {
        if (!string.IsNullOrEmpty(stepUpToken))
            request.Headers.TryAddWithoutValidation(StepUpGrant.HeaderName, stepUpToken);
        return request;
    }
}
```

- [x] **Step 3: Crear `StepUpClient.cs`**

```csharp
using System.Net.Http.Json;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;

public sealed record StepUpChallenge(string ChallengeToken, string Channel, string MaskedTarget, DateTime ExpiresAt);

public sealed record StepUpOutcome<T>(bool Success, string? ErrorCode, T? Data);

/// <summary>
/// Habla con <c>api/v1/auth/step-up/*</c> de SignupAPI. El cliente con nombre lo registra cada
/// anfitrión con su manejador de autenticación (ver <c>StepUpClientRegistration</c>): este
/// componente no sabe de dónde sale el token de acceso.
/// </summary>
public sealed class StepUpClient
{
    public const string HttpClientName = "StepUpApi";

    private readonly IHttpClientFactory _factory;

    public StepUpClient(IHttpClientFactory factory) => _factory = factory;

    private sealed class Wrapper<T> { public bool Success { get; set; } public string? ErrorCode { get; set; } public T? Data { get; set; } }
    private sealed class GrantDto { public string StepUpToken { get; set; } = string.Empty; }

    public Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string operationKey, CancellationToken ct = default) =>
        PostAsync<StepUpChallenge>("api/v1/auth/step-up/challenge", new { operationKey }, ct);

    public async Task<StepUpOutcome<string>> VerifyAsync(
        string operationKey, string challengeToken, string code, CancellationToken ct = default)
    {
        var r = await PostAsync<GrantDto>("api/v1/auth/step-up/verify",
            new { operationKey, challengeToken, code }, ct);
        return new StepUpOutcome<string>(r.Success, r.ErrorCode, r.Data?.StepUpToken);
    }

    private async Task<StepUpOutcome<T>> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        try
        {
            var resp = await _factory.CreateClient(HttpClientName).PostAsJsonAsync(path, body, ct);
            var env  = await resp.Content.ReadFromJsonAsync<Wrapper<T>>(cancellationToken: ct);
            return env is { Success: true }
                ? new StepUpOutcome<T>(true, null, env.Data)
                : new StepUpOutcome<T>(false, env?.ErrorCode, default);
        }
        catch
        {
            return new StepUpOutcome<T>(false, null, default);
        }
    }
}
```

- [x] **Step 4: Crear `StepUpDialog.razor`**

```razor
@namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp
@using Microsoft.Extensions.Localization
@using MLMConquerorGlobalEdition.SharedComponents.Components.Account
@inject StepUpClient Client
@inject IStringLocalizer<SharedResources> L

@* Modal de confirmación por código. Un componente lo monta una vez y llama a RequireAsync cuando
   la API responde STEP_UP_REQUIRED; devuelve el permiso, o null si la persona cancela. *@
@if (_open)
{
    <div class="modal d-block" tabindex="-1" role="dialog" aria-modal="true" style="background:rgba(0,0,0,.5)">
        <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">@L["StepUp.Title"]</h5>
                </div>
                <div class="modal-body">
                    <p class="text-muted small">@L["StepUp.Why"]</p>
                    @if (_challenge is not null)
                    {
                        <p>
                            @(TwoFactorMessages.IsAuthenticator(_challenge.Channel)
                                ? L["StepUp.AuthenticatorPrompt"].Value
                                : string.Format(L["StepUp.SentTo"].Value, _challenge.MaskedTarget))
                        </p>
                        <label class="form-label" for="stepup-code">@L["StepUp.CodeLabel"]</label>
                        <input id="stepup-code" class="form-control" inputmode="numeric" autocomplete="one-time-code"
                               maxlength="6" @bind="_code" @bind:event="oninput" disabled="@_busy" />
                    }
                    @if (_errorKey is not null)
                    {
                        <div class="alert alert-danger mt-3 mb-0" role="alert">@L[_errorKey]</div>
                    }
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-link me-auto" @onclick="ResendAsync" disabled="@_busy">@L["StepUp.Resend"]</button>
                    <button type="button" class="btn btn-outline-secondary" @onclick="Cancel" disabled="@_busy">@L["StepUp.Cancel"]</button>
                    <button type="button" class="btn btn-primary" @onclick="ConfirmAsync"
                            disabled="@(_busy || _challenge is null || string.IsNullOrWhiteSpace(_code))">@L["StepUp.Confirm"]</button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    private bool _open, _busy;
    private string? _operationKey, _code, _errorKey;
    private StepUpChallenge? _challenge;
    private TaskCompletionSource<string?>? _pending;

    /// <summary>Abre el modal, manda el código y espera. Devuelve el permiso o null si se cancela.</summary>
    public async Task<string?> RequireAsync(string operationKey)
    {
        _pending?.TrySetResult(null);
        _pending      = new TaskCompletionSource<string?>();
        _operationKey = operationKey;
        _open         = true;
        await ResendAsync();
        return await _pending.Task;
    }

    private async Task ResendAsync()
    {
        _busy = true; _errorKey = null; _code = null; StateHasChanged();
        var r = await Client.RequestChallengeAsync(_operationKey!);
        _challenge = r.Success ? r.Data : _challenge;
        if (!r.Success) _errorKey = TwoFactorMessages.ErrorKeyFor(r.ErrorCode);
        _busy = false; StateHasChanged();
    }

    private async Task ConfirmAsync()
    {
        _busy = true; _errorKey = null; StateHasChanged();
        var r = await Client.VerifyAsync(_operationKey!, _challenge!.ChallengeToken, _code!.Trim());
        _busy = false;
        if (r.Success && !string.IsNullOrEmpty(r.Data))
        {
            Close();
            _pending?.TrySetResult(r.Data);
            return;
        }
        _errorKey = TwoFactorMessages.ErrorKeyFor(r.ErrorCode);
        StateHasChanged();
    }

    private void Cancel()
    {
        Close();
        _pending?.TrySetResult(null);
    }

    private void Close()
    {
        _open = false; _challenge = null; _code = null; _errorKey = null;
        StateHasChanged();
    }
}
```

> Confirmar que `SharedResources` resuelve sin `using` extra (lo usan los demás componentes de `Components/Account`); si no, agregar `@using MLMConquerorGlobalEdition.SharedComponents.Resources`.

- [x] **Step 5: Crear `StepUpClientRegistration.cs`** (SharedComponents.Server)

```csharp
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;

public static class StepUpClientRegistration
{
    /// <summary>
    /// Cliente a SignupAPI para el step-up, autenticado con <see cref="ApiAuthHandler"/> —el que
    /// funciona dentro del circuito de Blazor— y con las cookies del manejador apagadas por la
    /// misma razón que <c>AddAuthApiClient</c>: el manejador primario es compartido entre usuarios.
    /// </summary>
    public static IServiceCollection AddStepUpClient(this IServiceCollection services, string authApiBaseAddress)
    {
        services.AddHttpClient(StepUpClient.HttpClientName, c => c.BaseAddress = new Uri(authApiBaseAddress))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
                .AddHttpMessageHandler<ApiAuthHandler>();
        services.AddScoped<StepUpClient>();
        return services;
    }
}
```

- [x] **Step 6: Registrar en `BizCenterWeb/Program.cs`**, después de `builder.Services.AddAuthApiClient(...)`:

```csharp
// Cliente del step-up: el modal que pide el código antes de cambiar la wallet de cobro.
builder.Services.AddStepUpClient(
    builder.Configuration["AuthApiBaseUrl"] ?? "https://localhost:7005");
```

- [x] **Step 7: Integrar en `WalletPanel.razor`**

Arriba del markup (junto al alert de :11-12) montar el modal, y agregar `@using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp` y `@using MLMConquerorGlobalEdition.SharedKernel.Security`:

```razor
<StepUpDialog @ref="_stepUp" />
```

En `@code`: el campo y un helper que envía, y si la API pide step-up, pide el código y reintenta una vez:

```csharp
    private StepUpDialog _stepUp = default!;

    /// <summary>
    /// Envía la petición; si el BizCenter responde STEP_UP_REQUIRED, pide el código y la repite con
    /// el permiso. Null si la persona canceló. En modo admin la API no pide step-up y esto es un
    /// envío normal.
    /// </summary>
    private async Task<HttpResponseMessage?> SendWithStepUpAsync(Func<HttpRequestMessage> build)
    {
        var resp = await Http.SendAsync(build());
        if (!await StepUpHttp.IsStepUpRequiredAsync(resp)) return resp;

        var grant = await _stepUp.RequireAsync(StepUpOperations.PayoutMethodChange);
        if (grant is null) return null;
        return await Http.SendAsync(StepUpHttp.WithGrant(build(), grant));
    }
```

En `SaveEditAsync`, reemplazar `var resp = await Http.PutAsJsonAsync(url, payload);` por:

```csharp
            var resp = await SendWithStepUpAsync(() =>
                new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(payload) });
            if (resp is null) return; // canceló el código: el modal de edición sigue abierto
```

En `SetDefaultAsync`, reemplazar `var resp = await Http.PutAsync($"{ListUrl}/{walletType}/default", null);` por:

```csharp
            var resp = await SendWithStepUpAsync(() =>
                new HttpRequestMessage(HttpMethod.Put, $"{ListUrl}/{walletType}/default"));
            if (resp is null) return;
```

(`JsonContent` está en `System.Net.Http.Json`; agregar el `@using` si falta.)

- [x] **Step 8: Integrar en `CommissionsCard.razor`** — mismo `<StepUpDialog @ref="_stepUp" />`, mismos `@using`, mismo helper `SendWithStepUpAsync` (con `Http` del componente), y en `SaveAsync` paso 2:

```csharp
                var walletResp = await SendWithStepUpAsync(() =>
                    new HttpRequestMessage(HttpMethod.Put, $"{WalletsBase}/{_editWalletType}/default"));
                if (walletResp is null) return;
```

> El helper queda duplicado en dos componentes. Si aparece un tercero, subirlo a un método de extensión sobre `HttpClient` en `StepUpHttp` que reciba el `StepUpDialog`.

- [x] **Step 9: Textos en los 9 `.resx`** — agregar estas claves `StepUp.*` en cada archivo (`<data name="..." xml:space="preserve"><value>...</value></data>`):

| Clave | en (`SharedResources.resx`) | es | fr | de | it | pt | ko | ka | zh |
|---|---|---|---|---|---|---|---|---|---|
| StepUp.Title | Confirm it's you | Confirma que eres tú | Confirmez votre identité | Bestätigen Sie, dass Sie es sind | Conferma la tua identità | Confirme que é você | 본인 확인 | დაადასტურეთ, რომ თქვენ ხართ | 确认是您本人 |
| StepUp.Why | For your security, enter a verification code before changing where your commissions are paid. | Por seguridad, ingresa un código de verificación antes de cambiar a dónde se pagan tus comisiones. | Pour votre sécurité, saisissez un code de vérification avant de modifier le mode de versement de vos commissions. | Geben Sie zu Ihrer Sicherheit einen Bestätigungscode ein, bevor Sie ändern, wohin Ihre Provisionen ausgezahlt werden. | Per sicurezza, inserisci un codice di verifica prima di cambiare dove vengono pagate le tue commissioni. | Por segurança, digite um código de verificação antes de alterar onde suas comissões são pagas. | 보안을 위해 수수료 지급처를 변경하기 전에 인증 코드를 입력하세요. | უსაფრთხოებისთვის, საკომისიოს გადახდის ადგილის შეცვლამდე შეიყვანეთ დამადასტურებელი კოდი. | 为了您的安全，更改佣金收款方式前请输入验证码。 |
| StepUp.SentTo | We sent a code to {0}. | Te enviamos un código a {0}. | Nous avons envoyé un code à {0}. | Wir haben einen Code an {0} gesendet. | Abbiamo inviato un codice a {0}. | Enviamos um código para {0}. | {0}(으)로 코드를 보냈습니다. | კოდი გაიგზავნა: {0}. | 我们已将验证码发送至 {0}。 |
| StepUp.AuthenticatorPrompt | Enter the code from your authenticator app. | Ingresa el código de tu aplicación de autenticación. | Saisissez le code de votre application d'authentification. | Geben Sie den Code aus Ihrer Authenticator-App ein. | Inserisci il codice della tua app di autenticazione. | Digite o código do seu aplicativo autenticador. | 인증 앱의 코드를 입력하세요. | შეიყვანეთ კოდი ავთენტიფიკატორის აპიდან. | 请输入身份验证器应用中的验证码。 |
| StepUp.CodeLabel | Verification code | Código de verificación | Code de vérification | Bestätigungscode | Codice di verifica | Código de verificação | 인증 코드 | დამადასტურებელი კოდი | 验证码 |
| StepUp.Confirm | Confirm | Confirmar | Confirmer | Bestätigen | Conferma | Confirmar | 확인 | დადასტურება | 确认 |
| StepUp.Cancel | Cancel | Cancelar | Annuler | Abbrechen | Annulla | Cancelar | 취소 | გაუქმება | 取消 |
| StepUp.Resend | Send a new code | Enviar otro código | Envoyer un nouveau code | Neuen Code senden | Invia un nuovo codice | Enviar novo código | 새 코드 보내기 | ახალი კოდის გაგზავნა | 重新发送验证码 |

Verificar que las 8 claves están en los 9 archivos:
Run: `for f in MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources*.resx; do echo "$f $(grep -c 'name="StepUp\.' $f)"; done`
Expected: `8` en cada línea.

- [x] **Step 10: Correr pruebas y compilar la solución**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo` → PASS.
Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo` → `0 Error(s)` (incluye AdminWeb y las MAUI, que comparten los componentes).

- [x] **Step 11: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents MLMConquerorGlobalEdition.SharedComponents.Server MLMConquerorGlobalEdition.BizCenterWeb MLMConquerorGlobalEdition.BizCenter.Tests
git commit -m "El BizCenter pide el codigo de verificacion antes de cambiar la wallet de cobro y reintenta con el permiso"
```

---

### Task 7: Verificación en la app levantada y documentación

**Files:**
- Modify: `docs/deployment/pruebas-de-seguridad.md` (~:341, donde step-up figura como "sin construir")
- Modify: `MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md:78` (estado → Cubierto)

- [x] **Step 1: Reiniciar SignupAPI, BizCenter API y BizCenterWeb** con el código nuevo (los demás servicios pueden seguir).

- [x] **Step 2: Probar con `curl` que la puerta está cerrada** (token de acceso de un miembro de prueba en `$T`):

```bash
curl -sk -X PUT https://localhost:7003/api/v1/bizcenter/wallets/6/default -H "Authorization: Bearer $T" -w "\n%{http_code}\n"
```
Expected: `403` con `"errorCode":"STEP_UP_REQUIRED"`. La wallet preferida no cambia (comprobar con `GET wallets`).

```bash
curl -sk -X PUT https://localhost:7003/api/v1/bizcenter/wallet -H "Authorization: Bearer $T" -H "Content-Type: application/json" -d "{}" -w "\n%{http_code}\n"
```
Expected: `404` / `405` — la ruta vieja ya no existe.

- [x] **Step 3: Recorrido en el navegador** (https://localhost:7004, miembro de prueba): Perfil → Wallets → editar una cuenta → Guardar. Debe abrirse el modal, llegar el código al correo (en desarrollo, revisar el log de SignupAPI o el buzón configurado), y al confirmarlo guardarse el cambio. Repetir con "cambiar preferida" desde la tarjeta de Comisiones. Cancelar el modal no debe guardar nada. Un código malo muestra el error traducido.

- [x] **Step 4: Actualizar los documentos** — en `pruebas-de-seguridad.md` cambiar "sin construir" por la descripción del step-up del BizCenter y los pasos 2-3 como prueba; en la matriz de paridad, fila 78: `SaveWallet y SetDefault exigen step-up (código por correo, SMS o autenticador) | Cubierto`, y ajustar el conteo del resumen (Cubierto 30, Falta 16).

- [x] **Step 5: Commit**

```bash
git add docs "MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md"
git commit -m "Documenta el step-up del cambio de cobro y lo marca cubierto en la matriz de paridad"
```

## Estado final

Las Tareas 1 a 7 están hechas. Además del alcance original, la rama incluye:

- **Tarea 4b, endurecimiento del step-up**: espera de 24 h tras cambiar un factor
  (`STEP_UP_COOLDOWN`, `SecurityFactorsChangedAt` sellado por interceptor), aviso al dueño con
  `SECURITY_FACTOR_CHANGED` (también al re-enrolar el autenticador) y canje del reto de un solo uso
  y atómico con `TryClaimOnceAsync`, fallando cerrado.
- **Corrección de la huella del código**: HMAC-SHA256 con clave derivada por HKDF de la llave
  privada JWT y atada al jti, en lugar de un SHA-256 sin clave invertible fuera de línea; el
  servicio de retos pasa a Singleton y deriva sus claves de la llave canónica.
- **Tope diario de códigos fallidos** por usuario (`TWO_FACTOR_LOCKED`; 15 en login y 10 en
  step-up por 24 h) con aviso al dueño y código de exactamente seis dígitos ASCII.
- **Redacción de los logs de los transportes nulos**: ya no escriben códigos ni enlaces, solo el
  evento, el destino enmascarado y los nombres de las variables.
- **Proveedor SMTP para MailHog** en desarrollo (y, en la revisión final, rechazado fuera de
  Development).
- **Revisión final**: tolerancia de 30 s en `nbf` del permiso, texto propio del diálogo tras
  `INVALID_CHALLENGE`/`CODE_EXPIRED`/`TOO_MANY_ATTEMPTS`, step-up rechazado bajo suplantación
  (`IMPERSONATION_NOT_ALLOWED`), firmante del permiso como Singleton y las condiciones de
  despliegue (SES/Twilio reales antes de producción, NTP) en `docs/deployment/pruebas-de-seguridad.md`.
