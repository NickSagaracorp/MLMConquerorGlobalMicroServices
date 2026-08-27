# Plan A — Prerrequisitos de seguridad

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sacar la llave privada RSA del repositorio, impedir que se vuelva a usar la llave
filtrada, y reparar el freno de fuerza bruta que hoy está configurado pero nunca se invoca
en el login real.

**Architecture:** Un guarda estático en `SharedKernel` valida la configuración de llaves al
construir los servicios que firman tokens, y rechaza tanto la llave ausente como la llave
revocada por huella SHA-256. La llave privada sale del `appsettings.json` rastreado hacia
`appsettings.Development.json` (ya cubierto por `.gitignore`), con un archivo plantilla
commiteado que documenta qué claves hacen falta. Por separado, `LoginHandler` de SignupAPI
pasa a invocar el lockout de Identity que ya estaba configurado.

**Tech Stack:** .NET 10, ASP.NET Identity, xUnit 2.9 + Moq 4.20 + FluentAssertions 6.12.

**Spec:** `docs/superpowers/specs/2026-08-27-admin-2fa-step-up-design.md` §10.1 y §6.1.

---

## Contexto que el implementador necesita

**Por qué esto va antes que el 2FA.** `Jwt:PrivateKeyBase64` está commiteado en texto plano
en `MLMConquerorGlobalEdition.AdminAPI/appsettings.json:26` y
`MLMConquerorGlobalEdition.SignupAPI/appsettings.json:21` (la misma llave en ambos,
SHA-256 `2ddf53d1…`). Con esa llave se puede firmar un access token con rol `SuperAdmin`
sin contraseña, y también forjar el challenge de 2FA, que usa la misma llave. Montar 2FA
encima no aumenta la seguridad real.

**La llave vieja queda en el historial de git para siempre.** Este plan no reescribe
historia. Por eso el guarda rechaza esa llave por huella: aunque alguien la restaure desde
un commit viejo, el servicio no arranca.

**Rotar la llave invalida todas las sesiones activas.** En desarrollo no importa. En
producción hay que hacerlo en una ventana de mantenimiento, y todos los usuarios vuelven a
iniciar sesión.

**Quién firma y quién solo valida.** Firman con la privada: `SignupAPI/Services/JwtService.cs`,
`SignupAPI/Services/TwoFactorChallengeService.cs` y `AdminAPI/Services/JwtService.cs`. Solo
validan con la pública: AdminAPI, SignupAPI, BizCenter, RankEngine y TicketManagementSystem
(en sus `Program.cs`). **La llave pública no es secreta y se queda en el `appsettings.json`
rastreado** — así una clonación limpia sigue pudiendo validar, y solo la privada requiere
configuración local.

**Fuera de alcance.** `Billing` y `CommissionEngine` validan con llave simétrica y audiencia
equivocada (spec §10.6). No se toca aquí.

---

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `SharedKernel/Configuration/JwtKeyGuard.cs` (nuevo) | Validar llave presente y no revocada; huella SHA-256 |
| `Signups.Tests/Configuration/JwtKeyGuardTests.cs` (nuevo) | Pruebas del guarda |
| `SignupAPI/Services/JwtService.cs` (modificar) | Usar el guarda |
| `SignupAPI/Services/TwoFactorChallengeService.cs` (modificar) | Usar el guarda |
| `AdminAPI/Services/JwtService.cs` (modificar) | Usar el guarda |
| `SignupAPI/appsettings.json` (modificar) | Vaciar `PrivateKeyBase64`, poner la pública nueva |
| `AdminAPI/appsettings.json` (modificar) | Igual |
| `SignupAPI/appsettings.Development.json` (nuevo, ignorado) | Llave privada de desarrollo |
| `AdminAPI/appsettings.Development.json` (nuevo, ignorado) | Llave privada de desarrollo |
| `docs/deployment/jwt-keys.template.json` (nuevo, commiteado) | Documenta las claves de producción |
| `SignupAPI/Features/Auth/Commands/Login/LoginHandler.cs` (modificar) | Invocar el lockout |
| `Signups.Tests/Features/Auth/LoginHandlerTests.cs` (modificar) | Pruebas del lockout |

---

## Task 1: Guarda de configuración de llaves JWT

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Configuration/JwtKeyGuard.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Configuration/JwtKeyGuardTests.cs`

Nota de ubicación: no existe un proyecto `SharedKernel.Tests`. Las pruebas van en
`Signups.Tests`, que ya referencia SharedKernel y ya prueba configuración JWT en
`Services/JwtServiceTests.cs`.

- [ ] **Step 1: Escribir las pruebas que fallan**

Crear `MLMConquerorGlobalEdition.Signups.Tests/Configuration/JwtKeyGuardTests.cs`:

```csharp
using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Configuration;

public class JwtKeyGuardTests
{
    /// <summary>La llave que estuvo commiteada en appsettings.json hasta 2026-08-27.</summary>
    private const string RevokedKeySample =
        "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQC4bmDlbzdUGJJPYtJf4o7XCayVP0ml8D6GUaWp8DYWHp2qGnRSKK/W3SpQOk527sR3n0lorN/pDuWn0McP9AaZcwJNr69C0haT59VnzhMf8pjT1FH8aFZMyEMStKBF8eQx8BMsMXBm7Ks04t3NzOpiBywR3drnBU/USJjQ0S9m+CrHod/Wqpc39X37NXF4bA2LI/pd6SLhwe1fsN+IU7SVuJCbn0G3+URCswKCZEL21lIYeZgXPk1cgc91TVJ33T4JDBe9CQIlFkRqCBwKxfvplaiT8VM3j9g5DBPs9ODZKUPgev/ZaaIXGQxLBLEC+z1nWWvgs+UeziL/jMF/Rf9hAgMBAAECggEAE+Pzl0rzUKKFxQIHZkfs18w3TLFSpA7Q73OGxlkdvCz5HAtWWDYDIM0hbx2asMD3d186b4uVanEs23hsv4+11n7M1MwJvs8hmDn8jgFvlpZ3XQEdBnfKNuWyNiY80s5PqgMWTkHWuYL0w/NjsHpVHNVhL1wZ66hq/54EvSCSWCxkhD81HOkGzyd8nlubv2kDOtP+MT+L+/gdu/7XcBItKs6qpZNPq36qBKvFogWsecnXGJc0roT+WsgOnY1kcE1+cUL80Nnj4lhE1NkE1FIGIH3+bGDOJeLZ6vI79TCqfa/zXr05nUusHqy1LHEFcS0rDUb6IwWDUQBzA6mPXY7+cQKBgQDtLwHUi3EZ7AaA/WfjAm1ekul2azz3hBOovwZiRC+XJ4Xbw+E1nzKBQM/vpuGbM8ilo7Dg0Jk6TTmuXXaX43hZwAcTSAv6dxVInNyWVmI1CL0dgBVAvqOTunWbCFeDJeweu0powpS3sxa/yyVhqo/yxsw1wKFQISADEHYx/hTghwKBgQDHEAPQ51NuzV8h3lsr1ZZGhKTRs+CSGinutfE14Wuj14fXmtKTKqgjOwyR/uLDmRmFim6zTT+gwkOWYuKufk13htSym0h8J0Lu3eisMZstN1V3uQyD0InB5N3KxSdNPmDkSYhfp4S/IcUFUpc8uKsssZLiBgHWGFilXOKVnxii1wKBgC1tO2SB8H+OfRBneGY6KMhcSuqrT1n4qes/6vEXLiY9I54bvh6PBxVKXIkB5WPcXymaWF42laJc+Bc1P2mH350Q8kn3GpQ2CpWFtZn1oYmWyuHDjk6ANMQuiifPSTONJ9Qa+v1lhyZH3quNNPOnvzo4aRRCeKLoNUFg/cJFb6oxAoGAfWlYjh9/T/pxafwVwnsQ7yKYWEmUPVfgfTUBX6nCT/n06l/vNKqWPYPxOnWz1fk5vAloDuynHpflTvTDzZ1jHt/CVzg/pYByydzivsGN+yG2ZfQer1kNwSt4lEw7o42eK5UsJt96YZRb9SuAfs/1f/XsDRwtwf2as6veUNdoBQcCgYBEAahv2JzK6Rt3E/3/R4ujdIxz92cwATJK3q18NjCFS0J7Rpm9ObFFlC3n2tIY+HwT+Dh9yglGLoZOsvadSvWOyGRcZ4jTgJbJomfQB+s1gsLm5uK6BGYPSAA50udUr2OQQvxzgXEHy4/lfjg8PeC7cU9DqV1jsPYc2RzoWLLOmw==";

    [Fact]
    public void ValidatePrivateKey_WhenNull_Throws()
    {
        var act = () => JwtKeyGuard.ValidatePrivateKey(null);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Jwt:PrivateKeyBase64*no está configurada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenWhitespace_Throws()
    {
        var act = () => JwtKeyGuard.ValidatePrivateKey("   ");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*no está configurada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenRevokedKey_Throws()
    {
        var act = () => JwtKeyGuard.ValidatePrivateKey(RevokedKeySample);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*llave revocada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenRevokedKeyHasSurroundingWhitespace_StillThrows()
    {
        var act = () => JwtKeyGuard.ValidatePrivateKey($"  {RevokedKeySample}\n");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*llave revocada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenValidKey_ReturnsIt()
    {
        const string fresh = "MIIEvQIBADANBgkq-esta-no-es-la-revocada";

        JwtKeyGuard.ValidatePrivateKey(fresh).Should().Be(fresh);
    }

    [Fact]
    public void ValidatePrivateKey_UsesGivenConfigKeyInMessage()
    {
        var act = () => JwtKeyGuard.ValidatePrivateKey(null, "Jwt:OtraLlave");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Jwt:OtraLlave*");
    }
}
```

- [ ] **Step 2: Ejecutar y verificar que fallan**

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "FullyQualifiedName~JwtKeyGuardTests"
```

Esperado: FALLA de compilación — `JwtKeyGuard` no existe.

- [ ] **Step 3: Implementar el guarda**

Crear `MLMConquerorGlobalEdition.SharedKernel/Configuration/JwtKeyGuard.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.SharedKernel.Configuration;

/// <summary>
/// Valida la configuración de llaves JWT al construir los servicios que firman tokens.
///
/// Rechaza dos casos: la llave ausente, y la llave que estuvo commiteada en
/// appsettings.json hasta 2026-08-27. Esa llave sigue en el historial de git y debe
/// considerarse comprometida de forma permanente; el rechazo por huella impide que
/// alguien la restaure desde un commit viejo y arranque el servicio con ella.
/// </summary>
public static class JwtKeyGuard
{
    /// <summary>
    /// SHA-256 en hexadecimal de la llave privada revocada.
    /// Se guarda la huella, no la llave: la huella no sirve para firmar nada.
    /// </summary>
    private const string RevokedPrivateKeyFingerprint =
        "2ddf53d71674a46e97fcfcb513a5b804aed7eb9f6df3a43ee72e03f4789f0fe5";

    /// <summary>
    /// Devuelve la llave si es utilizable; si no, lanza con un mensaje accionable.
    /// </summary>
    /// <param name="base64">Valor leído de configuración.</param>
    /// <param name="configKey">Clave de configuración, para el mensaje de error.</param>
    public static string ValidatePrivateKey(string? base64, string configKey = "Jwt:PrivateKeyBase64")
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException(
                $"{configKey} no está configurada. En desarrollo va en appsettings.Development.json; " +
                "en producción, en appsettings.Production.json. " +
                "Plantilla: docs/deployment/jwt-keys.template.json.");

        if (Fingerprint(base64) == RevokedPrivateKeyFingerprint)
            throw new InvalidOperationException(
                $"{configKey} contiene la llave revocada que estuvo commiteada en el repositorio. " +
                "Esa llave es pública de forma permanente porque sigue en el historial de git. " +
                "Genera un par nuevo: ver docs/deployment/jwt-keys.template.json.");

        return base64;
    }

    /// <summary>SHA-256 en hexadecimal minúscula del valor, ignorando espacios alrededor.</summary>
    public static string Fingerprint(string base64) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(base64.Trim()))).ToLowerInvariant();
}
```

- [ ] **Step 4: Ejecutar y verificar que pasan**

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "FullyQualifiedName~JwtKeyGuardTests"
```

Esperado: PASS, 6 pruebas.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel/Configuration/JwtKeyGuard.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Configuration/JwtKeyGuardTests.cs
git commit -m "feat(security): guarda de configuracion de llaves JWT

Rechaza la llave ausente y la llave que estuvo commiteada en appsettings.json,
identificada por huella SHA-256. La llave sigue en el historial de git, asi que
el rechazo por huella impide restaurarla desde un commit viejo."
```

---

## Task 2: Cablear el guarda en los tres servicios que firman

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Services/JwtService.cs:23-24`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Services/TwoFactorChallengeService.cs:29-30`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Services/JwtService.cs:23-24`

- [ ] **Step 1: Modificar `SignupAPI/Services/JwtService.cs`**

Agregar el `using` junto a los demás al inicio del archivo:

```csharp
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
```

Reemplazar las líneas 23-24:

```csharp
        var privateKeyBase64 = config["Jwt:PrivateKeyBase64"]
            ?? throw new InvalidOperationException("Jwt:PrivateKeyBase64 not configured.");
```

por:

```csharp
        var privateKeyBase64 = JwtKeyGuard.ValidatePrivateKey(config["Jwt:PrivateKeyBase64"]);
```

- [ ] **Step 2: Modificar `SignupAPI/Services/TwoFactorChallengeService.cs`**

Agregar el mismo `using`, y reemplazar las líneas 29-30:

```csharp
        var privateKeyBase64 = config["Jwt:PrivateKeyBase64"]
            ?? throw new InvalidOperationException("Jwt:PrivateKeyBase64 not configured.");
```

por:

```csharp
        var privateKeyBase64 = JwtKeyGuard.ValidatePrivateKey(config["Jwt:PrivateKeyBase64"]);
```

- [ ] **Step 3: Modificar `AdminAPI/Services/JwtService.cs`**

Agregar el mismo `using`, y reemplazar las líneas 23-24 (idénticas a las de SignupAPI):

```csharp
        var privateKeyBase64 = config["Jwt:PrivateKeyBase64"]
            ?? throw new InvalidOperationException("Jwt:PrivateKeyBase64 not configured.");
```

por:

```csharp
        var privateKeyBase64 = JwtKeyGuard.ValidatePrivateKey(config["Jwt:PrivateKeyBase64"]);
```

- [ ] **Step 4: Verificar que la prueba existente de llave ausente sigue pasando**

`Signups.Tests/Services/JwtServiceTests.cs:40-42` ya cubre el caso de
`Jwt:PrivateKeyBase64` omitida y espera `InvalidOperationException`. El guarda lanza el
mismo tipo, así que debe seguir pasando sin tocarla.

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "FullyQualifiedName~JwtServiceTests"
```

Esperado: PASS.

Si esa prueba afirma sobre el **texto** del mensaje (`"Jwt:PrivateKeyBase64 not configured."`),
actualizar la aserción a `.WithMessage("*Jwt:PrivateKeyBase64*")`, que sigue siendo cierta
con el mensaje nuevo.

- [ ] **Step 5: Compilar la solución completa**

```bash
dotnet build MLMConquerorGlobalEdition.slnx
```

Esperado: 0 errores. `SharedKernel` ya es referencia de AdminAPI y SignupAPI, así que no
hace falta agregar `ProjectReference`.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/JwtService.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/TwoFactorChallengeService.cs \
        MLMConquerorGlobalEdition.AdminAPI/Services/JwtService.cs
git commit -m "refactor(security): validar la llave privada con JwtKeyGuard

Los tres servicios que firman tokens pasan por el guarda, que rechaza la llave
ausente y la revocada."
```

---

## Task 3: Generar el par nuevo y sacar la privada del repositorio

Esta tarea no tiene pruebas automatizadas: es configuración. La verificación es arrancar
los servicios.

- [ ] **Step 1: Generar el par de llaves de desarrollo**

Desde la raíz del repositorio:

```bash
python -c "
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives import serialization
import base64
k = rsa.generate_private_key(public_exponent=65537, key_size=2048)
priv = k.private_bytes(serialization.Encoding.DER, serialization.PrivateFormat.PKCS8, serialization.NoEncryption())
pub  = k.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
print('PRIVATE:', base64.b64encode(priv).decode())
print('PUBLIC :', base64.b64encode(pub).decode())
"
```

Si falta el paquete: `pip install cryptography`.

Alternativa sin Python, con OpenSSL:

```bash
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER -out priv.der
openssl rsa -in priv.der -inform DER -pubout -outform DER -out pub.der
base64 -w0 priv.der && echo && base64 -w0 pub.der
rm priv.der pub.der
```

Guardar las dos cadenas: se usan en los pasos 2 y 3.

- [ ] **Step 2: Crear los `appsettings.Development.json` con la llave privada**

Estos dos archivos **no se commitean** — `.gitignore:92` ya cubre `appsettings.*.json`.

Crear `MLMConquerorGlobalEdition.SignupAPI/appsettings.Development.json`:

```json
{
  "Jwt": {
    "PrivateKeyBase64": "<PRIVATE del paso 1>"
  }
}
```

Crear `MLMConquerorGlobalEdition.AdminAPI/appsettings.Development.json` con el mismo
contenido. La llave debe ser **la misma en los dos**: ambos firman tokens que el otro
valida.

- [ ] **Step 3: Vaciar la privada y actualizar la pública en los `appsettings.json` rastreados**

En `MLMConquerorGlobalEdition.SignupAPI/appsettings.json`, el bloque `Jwt` (líneas 20-27)
queda así:

```json
  "Jwt": {
    "PrivateKeyBase64": "",
    "PublicKeyBase64": "<PUBLIC del paso 1>",
    "Issuer": "MLMConquerorGlobalEdition",
    "Audience": "MLMConquerorGlobalEdition.Clients",
    "AccessTokenExpiryMinutes": 120,
    "RefreshTokenExpiryDays": 30
  },
```

Hacer lo mismo en `MLMConquerorGlobalEdition.AdminAPI/appsettings.json`: `PrivateKeyBase64`
en cadena vacía y `PublicKeyBase64` con el valor nuevo.

La pública se queda en el archivo rastreado a propósito: no es secreta, y así una clonación
limpia sigue pudiendo **validar** tokens. Solo firmar requiere configuración local.

- [ ] **Step 4: Actualizar la pública en los servicios que solo validan**

Los tres restantes leen `Jwt:PublicKeyBase64` de su propio `appsettings.json` y fallan al
arrancar si no coincide con la llave que firmó el token. Poner el mismo `<PUBLIC del paso 1>`
en el bloque `Jwt` de:

- `MLMConquerorGlobalEdition.BizCenter/appsettings.json`
- `MLMConquerorGlobalEdition.RankEngine/appsettings.json`
- `MLMConquerorGlobalEdition.TicketManagementSystem/appsettings.json`

- [ ] **Step 5: Verificar que ningún archivo rastreado conserva la llave vieja**

```bash
git grep -n "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQC4bmDl" -- '*.json'
```

Esperado: **sin resultados**. Ningún `.json` rastreado debe conservar la llave revocada.

La llave sí sigue apareciendo, a propósito, en la constante de prueba de
`Signups.Tests/Configuration/JwtKeyGuardTests.cs` — es lo que verifica que el guarda la
rechaza. Por eso el filtro `-- '*.json'` la excluye del chequeo.

```bash
git status --short
```

Esperado: no aparece ningún `appsettings.Development.json`.

- [ ] **Step 6: Arrancar SignupAPI y verificar que firma con la llave nueva**

```bash
dotnet run --project MLMConquerorGlobalEdition.SignupAPI
```

Esperado: arranca sin excepción. Si lanza `InvalidOperationException` con "no está
configurada", falta el `appsettings.Development.json` del paso 2.

Detener con Ctrl+C y repetir con AdminAPI:

```bash
dotnet run --project MLMConquerorGlobalEdition.AdminAPI
```

- [ ] **Step 7: Verificar que el guarda rechaza la llave vieja**

Pegar temporalmente la llave revocada en
`MLMConquerorGlobalEdition.SignupAPI/appsettings.Development.json` y arrancar:

```bash
dotnet run --project MLMConquerorGlobalEdition.SignupAPI
```

Esperado: `InvalidOperationException` con "contiene la llave revocada".

Restaurar la llave nueva antes de continuar.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/appsettings.json \
        MLMConquerorGlobalEdition.AdminAPI/appsettings.json \
        MLMConquerorGlobalEdition.BizCenter/appsettings.json \
        MLMConquerorGlobalEdition.RankEngine/appsettings.json \
        MLMConquerorGlobalEdition.TicketManagementSystem/appsettings.json
git commit -m "security!: rotar el par RSA y sacar la llave privada del repositorio

La llave privada sale de los appsettings.json rastreados hacia
appsettings.Development.json, ignorado por git. La publica se queda: no es
secreta y permite validar en una clonacion limpia.

BREAKING: invalida todas las sesiones activas. En produccion requiere ventana de
mantenimiento y colocar la llave nueva en appsettings.Production.json antes de
desplegar.

La llave anterior sigue en el historial de git y esta permanentemente
comprometida; JwtKeyGuard la rechaza por huella."
```

---

## Task 4: Plantilla de despliegue

**Files:**
- Create: `docs/deployment/jwt-keys.template.json`

- [ ] **Step 1: Verificar que el `.gitignore` no bloquea la carpeta nueva**

`.gitignore:5` ignora `*.md` con excepciones; los `.json` bajo `docs/` no están ignorados.
Confirmar:

```bash
mkdir -p docs/deployment
git check-ignore -v docs/deployment/jwt-keys.template.json
```

Esperado: sin salida y código de salida 1 — significa que **no** está ignorado.

- [ ] **Step 2: Crear la plantilla**

Crear `docs/deployment/jwt-keys.template.json`:

```json
{
  "_comment": [
    "Plantilla de las llaves JWT. Copiar el bloque Jwt a appsettings.Production.json",
    "de MLMConquerorGlobalEdition.SignupAPI y MLMConquerorGlobalEdition.AdminAPI.",
    "appsettings.*.json esta ignorado por .gitignore: nunca se commitea con valores.",
    "",
    "PrivateKeyBase64: RSA 2048 en PKCS#8 DER, codificada en base64. Solo la necesitan",
    "SignupAPI y AdminAPI, que firman tokens. Debe ser LA MISMA en ambos.",
    "",
    "PublicKeyBase64: SubjectPublicKeyInfo DER en base64. La necesitan ademas BizCenter,",
    "RankEngine y TicketManagementSystem para validar. No es secreta.",
    "",
    "Generar un par nuevo:",
    "  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER -out priv.der",
    "  openssl rsa -in priv.der -inform DER -pubout -outform DER -out pub.der",
    "  base64 -w0 priv.der  # -> PrivateKeyBase64",
    "  base64 -w0 pub.der   # -> PublicKeyBase64",
    "  rm priv.der pub.der",
    "",
    "Rotar la llave invalida todas las sesiones activas: hacerlo en ventana de",
    "mantenimiento. La llave anterior a 2026-08-27 esta commiteada en el historial de",
    "git y JwtKeyGuard la rechaza; no se puede reutilizar."
  ],
  "Jwt": {
    "PrivateKeyBase64": "",
    "PublicKeyBase64": "",
    "Issuer": "MLMConquerorGlobalEdition",
    "Audience": "MLMConquerorGlobalEdition.Clients",
    "AccessTokenExpiryMinutes": 120,
    "RefreshTokenExpiryDays": 30
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add docs/deployment/jwt-keys.template.json
git commit -m "docs: plantilla de configuracion de llaves JWT para despliegue"
```

---

## Task 5: Reparar el lockout en el login

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Auth/Commands/Login/LoginHandler.cs:41-48`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Features/Auth/LoginHandlerTests.cs`

Contexto: `SignupAPI/Program.cs:47-48` configura `MaxFailedAccessAttempts = 5` y
`DefaultLockoutTimeSpan = 15 min`, pero `LoginHandler` nunca llama a `IsLockedOutAsync`,
`AccessFailedAsync` ni `ResetAccessFailedCountAsync`. El contador nunca sube, así que el
bloqueo nunca ocurre. Este es el camino de login que usan tanto AdminWeb como BizCenterWeb.

- [ ] **Step 1: Escribir las pruebas que fallan**

Añadir a `MLMConquerorGlobalEdition.Signups.Tests/Features/Auth/LoginHandlerTests.cs`,
dentro de la clase existente. Usan los helpers `UserManagerHelper.Create()` y
`BuildHandler(...)` que ya están en el archivo:

```csharp
    [Fact]
    public async Task Handle_WhenUserLockedOut_ReturnsAccountLocked()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Email = "locked@test.com", UserName = "locked@test.com", IsActive = true
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("locked@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(true);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "locked@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_LOCKED");

        // No debe llegar a comprobar la contraseña de una cuenta bloqueada.
        userManager.Verify(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
                           Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordInvalid_IncrementsAccessFailedCount()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Email = "user@test.com", UserName = "user@test.com", IsActive = true
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(m => m.CheckPasswordAsync(user, "wrong")).ReturnsAsync(false);
        userManager.Setup(m => m.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "user@test.com", Password = "wrong" }),
            CancellationToken.None);

        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        userManager.Verify(m => m.AccessFailedAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPasswordValid_ResetsAccessFailedCount()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Email = "user@test.com", UserName = "user@test.com",
            IsActive = true, TwoFactorEnabled = false
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(m => m.CheckPasswordAsync(user, "correct")).ReturnsAsync(true);
        userManager.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "user@test.com", Password = "correct" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        userManager.Verify(m => m.ResetAccessFailedCountAsync(user), Times.Once);
    }
```

Añadir al inicio del archivo el `using` de `IdentityResult` si no está:

```csharp
using Microsoft.AspNetCore.Identity;
```

(ya está en la línea 1 del archivo).

- [ ] **Step 2: Ejecutar y verificar que fallan**

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "FullyQualifiedName~LoginHandlerTests"
```

Esperado: FALLAN las tres nuevas. `Handle_WhenUserLockedOut_ReturnsAccountLocked` falla
porque el handler ignora el bloqueo y sigue a `CheckPasswordAsync`; las otras dos fallan en
el `Verify` porque nunca se invocan.

Las pruebas preexistentes siguen pasando: `IsLockedOutAsync` sin `Setup` devuelve `false`
por defecto en Moq, que es el camino que ya ejercitan.

- [ ] **Step 3: Implementar en `LoginHandler`**

En `MLMConquerorGlobalEdition.SignupAPI/Features/Auth/Commands/Login/LoginHandler.cs`,
reemplazar las líneas 41-48:

```csharp
        var user = await _userManager.FindByEmailAsync(req.Email);

        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, req.Password);
        if (!passwordValid)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");
```

por:

```csharp
        var user = await _userManager.FindByEmailAsync(req.Email);

        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");

        // Lockout de Identity: configurado en Program.cs (5 intentos / 15 min) pero hasta
        // ahora nunca invocado desde aquí, así que el contador no subía y el bloqueo no
        // ocurría. Este es el camino de login de AdminWeb y BizCenterWeb.
        if (await _userManager.IsLockedOutAsync(user))
            return Result<AuthResponse>.Failure("ACCOUNT_LOCKED", "Account is temporarily locked.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, req.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Result<AuthResponse>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);
```

El mensaje de `ACCOUNT_LOCKED` no dice cuántos intentos quedan ni cuándo expira: sería
decirle a quien está probando contraseñas cuánto falta para poder seguir.

- [ ] **Step 4: Ejecutar y verificar que pasan**

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "FullyQualifiedName~LoginHandlerTests"
```

Esperado: PASS, incluidas las preexistentes.

- [ ] **Step 5: Ejecutar la batería completa de SignupAPI**

```bash
dotnet test MLMConquerorGlobalEdition.Signups.Tests
```

Esperado: PASS. Verifica que `VerifyTwoFactorHandlerTests` y `ResendTwoFactorHandlerTests`
no dependían de que `LoginHandler` omitiera el lockout.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Features/Auth/Commands/Login/LoginHandler.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Auth/LoginHandlerTests.cs
git commit -m "fix(security): invocar el lockout de Identity en el login

MaxFailedAccessAttempts=5 estaba configurado desde el principio pero LoginHandler
nunca llamaba a IsLockedOutAsync, AccessFailedAsync ni ResetAccessFailedCountAsync,
asi que el contador nunca subia y el bloqueo nunca ocurria. El login real no tenia
freno de fuerza bruta.

Afecta el login de AdminWeb y BizCenterWeb, que ambos pasan por aqui."
```

---

## Task 6: Verificación final del plan

- [ ] **Step 1: Compilar todo**

```bash
dotnet build MLMConquerorGlobalEdition.slnx
```

Esperado: 0 errores.

- [ ] **Step 2: Ejecutar todas las baterías de pruebas**

```bash
dotnet test MLMConquerorGlobalEdition.slnx
```

Esperado: PASS en todos los proyectos de prueba.

- [ ] **Step 3: Confirmar que no queda ningún secreto rastreado**

```bash
git grep -nE "PrivateKeyBase64\"\s*:\s*\"MII" -- '*.json'
```

Esperado: sin resultados.

- [ ] **Step 4: Confirmar que los archivos locales no se rastrean**

```bash
git status --short
```

Esperado: limpio. Ni `appsettings.Development.json` ni `priv.der`/`pub.der` deben aparecer.

---

## Qué queda pendiente después de este plan

- **Producción.** Este plan deja lista la configuración de desarrollo. Antes de desplegar
  hay que generar un par distinto para producción y colocarlo en
  `appsettings.Production.json` de SignupAPI y AdminAPI, y la pública también en BizCenter,
  RankEngine y TicketManagementSystem. En ventana de mantenimiento: todas las sesiones se
  invalidan.
- **Historial de git.** La llave vieja sigue ahí. Purgarla requiere reescribir historia
  (`git filter-repo`) y forzar push, coordinando con todos los clones. `JwtKeyGuard` mitiga
  el riesgo de reutilización, pero no la borra del historial.
- **`Billing` y `CommissionEngine`** siguen con autenticación rota (spec §10.6). Ticket
  aparte.
- **Plan B** — librería `Authn` + `Notifications` + migración.
