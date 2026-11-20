# Secretos desde AWS Secrets Manager — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cada uno de los 11 servicios lee al arrancar, de AWS Secrets Manager, solo los secretos de su lista, en el entorno `local`, `staging` o `prod`, con una identidad de AWS que solo alcanza esos secretos (IRSA en EKS, IAM Roles Anywhere en Docker fuera de AWS).

**Architecture:** Una fuente de configuración en `SharedKernel.Server` (`AddSecretosDeAws()`) lee `SecretosAws` de la configuración, pide a Secrets Manager `mlmconqueror/{entorno}/{secreto}` para cada nombre de la lista y mezcla las claves (`Seccion__Clave` → `Seccion:Clave`) justo antes de las variables de entorno. Si algo falla, el servicio no arranca. Un archivo `deploy/aws/secretos-por-servicio.json` es la única tabla de quién lee qué: de él salen los permisos de IAM y contra él se prueba la lista de cada servicio. Los scripts de `deploy/aws/` crean llaves KMS, secretos, roles y (en Docker) Roles Anywhere y certificados.

**Tech Stack:** .NET 10, `AWSSDK.SecretsManager` 3.7.504.43 (la línea 3.7 de los `AWSSDK.S3` 3.7.414.3 que ya usa el repositorio), xUnit + FluentAssertions + Moq, bash + AWS CLI v2 + jq + openssl, `aws_signing_helper` de IAM Roles Anywhere, Docker Compose, eksctl.

**Diseño:** `docs/superpowers/specs/2026-11-19-secretos-aws-design.md`.

**Reglas del repositorio que aplican a todo el plan:**
- Commits en español sin acentos, sin firma ni coautoría ni menciones de herramientas.
- No se agrega a ningún commit el `.gitignore` modificado de Nick ni la carpeta `Proceso ImportarData OldSystem/`.
- Rama `secretos-aws` (ya creada desde master, con el diseño en `3a67322`).
- Al cambiar un `.csproj`, se regeneran los lockfiles con `dotnet restore <proyecto> --force-evaluate` y se suben solo los que cambian en contenido (los que solo cambian finales de línea se descartan con `git checkout --`).

---

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `MLMConquerorGlobalEdition.SharedKernel.Server/MLMConquerorGlobalEdition.SharedKernel.Server.csproj` | Modificar | Agregar `AWSSDK.SecretsManager` |
| `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretosAwsOpciones.cs` | Crear | Leer y validar la sección `SecretosAws`; armar nombres |
| `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretoNoDisponibleException.cs` | Crear | Excepción con el motivo (sin permiso, no existe, sin credencial, formato, otro) |
| `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/ILectorDeSecretos.cs` | Crear | Contrato: dado un nombre completo, devuelve el texto del secreto |
| `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/LectorDeSecretosAws.cs` | Crear | Implementación con el SDK; traduce errores de AWS a `SecretoNoDisponibleException` |
| `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretosAwsConfiguracion.cs` | Crear | `AddSecretosDeAws()`: lee, convierte, inserta, escribe el log |
| `MLMConquerorGlobalEdition.Repository/MLMConquerorGlobalEdition.Repository.csproj` | Modificar | `InternalsVisibleTo` para `Billing.Tests` |
| `MLMConquerorGlobalEdition.Repository/Services/Payout/GatewayCredentialProtectionExtensions.cs` | Modificar | Certificado del anillo desde `Base64` (actual y retirados) |
| `MLMConquerorGlobalEdition.Repository/Services/FirebaseCredentialLoader.cs` | Crear | Credencial de Firebase desde `CredentialsJsonBase64` o archivo |
| `MLMConquerorGlobalEdition.Repository/Services/FirebasePushNotificationService.cs` | Modificar | Usar `FirebaseCredentialLoader` |
| `MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/*.cs` | Crear | Pruebas de opciones, lector, fuente de configuración y consistencia |
| `MLMConquerorGlobalEdition.Billing.Tests/Services/CertificadoDelAnilloDesdeBase64Tests.cs` | Crear | Pruebas del PFX en base64 |
| `MLMConquerorGlobalEdition.Billing.Tests/Services/FirebaseCredentialLoaderTests.cs` | Crear | Pruebas de Firebase en base64 |
| `deploy/aws/secretos-por-servicio.json` | Crear | Tabla única: secretos y permisos extra (S3, SES) por servicio |
| 11 × `MLMConquerorGlobalEdition.<Servicio>/appsettings.json` | Modificar | Sección `SecretosAws` |
| 11 × `MLMConquerorGlobalEdition.<Servicio>/Program.cs` | Modificar | `builder.Configuration.AddSecretosDeAws();` |
| `deploy/aws/comun.sh` | Crear | Variables y funciones compartidas por los scripts |
| `deploy/aws/crear-kms-y-secretos.sh` | Crear | Llave KMS del entorno y secretos vacíos |
| `deploy/aws/cargar-secreto.sh` | Crear | Poner el valor de un secreto desde un JSON local |
| `deploy/aws/crear-roles.sh` | Crear | Un rol por servicio (modo `eks` o `docker`) con su política |
| `deploy/aws/roles-anywhere.sh` | Crear | Autoridad certificadora, trust anchor y perfil |
| `deploy/aws/emitir-certificados.sh` | Crear | Un certificado por servicio |
| `deploy/aws/politica-desarrollador-local.sh` | Crear | Política para que un desarrollador lea `mlmconqueror/local/*` |
| `Dockerfile` | Modificar | Incluir `aws_signing_helper` |
| `deploy/docker/docker-compose.yml` | Modificar | Compose local: perfil del desarrollador, entorno `local` |
| `deploy/docker/docker-compose.servidor.yml` | Crear | Compose del servidor fuera de AWS con certificados por servicio |
| `deploy/docker/generar-secretos.sh` | Modificar | Queda solo para lo local y no secreto |
| `deploy/docker/.env.servidor.ejemplo` | Crear | Variables no secretas del servidor (ARN, registro, URLs) |
| `deploy/docker/.gitignore`, `deploy/aws/.gitignore` | Crear | Que `.env`, `.env.servidor`, la autoridad certificadora y los certificados nunca se suban |
| `docs/deployment/despliegue-aws-eks.md` (+ `.pdf`) | Modificar | Secretos por entorno, IRSA por servicio, sin External Secrets |
| `docs/deployment/despliegue-docker.md` (+ `.pdf`) | Crear | Guía del servidor fuera de AWS y de la máquina local |
| `docs/superpowers/specs/2026-11-19-secretos-aws-design.md` | Modificar | Signups sin secretos; S3/SES en la tabla |

---

### Task 1: Ajustar el diseño (Signups no usa la base)

**Files:**
- Modify: `docs/superpowers/specs/2026-11-19-secretos-aws-design.md`

Durante la revisión del código se comprobó que `MLMConquerorGlobalEdition.Signups` no tiene `AppDbContext`, no llama `GetConnectionString` ni usa Hangfire: no necesita `datos`.

- [ ] **Step 1: Quitar la marca de Signups en la fila `datos`**

En la tabla de la sección 3, la fila `datos` pasa a:

```
| `datos` | `ConnectionStrings__DefaultConnection`, `ConnectionStrings__HangFire`, `ConnectionStrings__Redis` | ✔ | | ✔ | ✔ | | ✔ | ✔ | ✔ | ✔ | | ✔ |
```

- [ ] **Step 2: Reemplazar la regla de Signups**

Reemplazar el párrafo que empieza con "**Signups** figura con `datos`" por:

```
- **Signups no lee ningún secreto**: no usa la base. Su lista queda vacía y, con la lista vacía, el servicio no llama a AWS. Las cadenas de conexión que trae su `appsettings.json` se borran porque nadie las lee.
- **S3 y SES** se dan, además de los secretos, a signupapi, adminapi, billing, bizcenter y rankengine (los mismos que hoy usan la cuenta `mlm-aws` de la guía de EKS). Se declaran en el mismo `secretos-por-servicio.json`.
```

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-11-19-secretos-aws-design.md
git commit -m "Diseno de secretos: Signups no usa la base y no lee ningun secreto; S3 y SES por servicio en la misma tabla"
```

---

### Task 2: Opciones y excepción de `SecretosAws`

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretosAwsOpciones.cs`
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretoNoDisponibleException.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/SecretosAwsOpcionesTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

public class SecretosAwsOpcionesTests
{
    private static IConfiguration Config(params (string Clave, string? Valor)[] pares) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pares.Select(p => new KeyValuePair<string, string?>(p.Clave, p.Valor)))
            .Build();

    [Fact]
    public void LeeHabilitadoEntornoRegionYLista()
    {
        var o = SecretosAwsOpciones.Leer(Config(
            ("SecretosAws:Habilitado", "true"),
            ("SecretosAws:Entorno", "staging"),
            ("SecretosAws:Region", "us-east-1"),
            ("SecretosAws:Secretos:0", "datos"),
            ("SecretosAws:Secretos:1", "jwt-firma")));

        o.Habilitado.Should().BeTrue();
        o.Entorno.Should().Be("staging");
        o.Region.Should().Be("us-east-1");
        o.Secretos.Should().Equal("datos", "jwt-firma");
    }

    [Fact]
    public void SinSeccion_QuedaDeshabilitado()
    {
        SecretosAwsOpciones.Leer(Config()).Habilitado.Should().BeFalse();
    }

    [Theory]
    [InlineData("local")]
    [InlineData("staging")]
    [InlineData("prod")]
    public void ElNombreCompletoLlevaElEntorno(string entorno)
    {
        var o = new SecretosAwsOpciones(true, entorno, "us-east-1", ["datos"]);
        o.Validar();
        o.NombreCompleto("datos").Should().Be($"mlmconqueror/{entorno}/datos");
    }

    [Theory]
    [InlineData("")]
    [InlineData("production")]
    [InlineData("Prod")]
    public void EntornoNoValido_Falla(string entorno)
    {
        var o = new SecretosAwsOpciones(true, entorno, "us-east-1", ["datos"]);
        o.Invoking(x => x.Validar()).Should().Throw<InvalidOperationException>()
            .WithMessage("*Entorno*local, staging o prod*");
    }

    [Fact]
    public void SinRegion_Falla()
    {
        new SecretosAwsOpciones(true, "local", "", ["datos"])
            .Invoking(x => x.Validar()).Should().Throw<InvalidOperationException>()
            .WithMessage("*Region*");
    }

    [Theory]
    [InlineData("../prod/datos")]
    [InlineData("prod/datos")]
    [InlineData("Datos")]
    [InlineData("datos ")]
    public void NombreDeSecretoConCaracteresRaros_Falla(string secreto)
    {
        // Un nombre con "/" podría salirse del entorno: "../prod/datos" desde local.
        new SecretosAwsOpciones(true, "local", "us-east-1", [secreto])
            .Invoking(x => x.Validar()).Should().Throw<InvalidOperationException>()
            .WithMessage("*nombre de secreto*");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~SecretosAwsOpcionesTests" -v q`
Expected: error de compilación, `SecretosAwsOpciones` no existe.

- [ ] **Step 3: Implementar las opciones**

`MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretosAwsOpciones.cs`:

```csharp
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

/// <summary>
/// La sección <c>SecretosAws</c> del appsettings.json de cada servicio: si se leen secretos de
/// AWS, de qué entorno y cuáles. No es secreta; la lista de cada servicio queda a la vista en el
/// repositorio y se compara en pruebas con deploy/aws/secretos-por-servicio.json.
/// </summary>
public sealed record SecretosAwsOpciones(
    bool Habilitado, string Entorno, string Region, IReadOnlyList<string> Secretos)
{
    public const string Seccion = "SecretosAws";

    /// <summary>Fijo a propósito: un prefijo configurable sería otra forma de leer lo ajeno.</summary>
    public const string Prefijo = "mlmconqueror";

    private static readonly HashSet<string> EntornosValidos = new(StringComparer.Ordinal)
        { "local", "staging", "prod" };

    private static readonly Regex NombreValido = new("^[a-z0-9-]+$", RegexOptions.CultureInvariant);

    public static SecretosAwsOpciones Leer(IConfiguration config)
    {
        var s = config.GetSection(Seccion);
        return new SecretosAwsOpciones(
            s.GetValue<bool>("Habilitado"),
            (s["Entorno"] ?? string.Empty).Trim(),
            (s["Region"] ?? string.Empty).Trim(),
            s.GetSection("Secretos").GetChildren()
                .Select(c => c.Value ?? string.Empty)
                .Where(v => v.Length > 0)
                .ToArray());
    }

    public string NombreCompleto(string secreto) => $"{Prefijo}/{Entorno}/{secreto}";

    public void Validar()
    {
        if (!EntornosValidos.Contains(Entorno))
            throw new InvalidOperationException(
                $"[SecretosAws] Entorno '{Entorno}' no válido: tiene que ser local, staging o prod.");

        if (Region.Length == 0)
            throw new InvalidOperationException("[SecretosAws] Falta SecretosAws:Region.");

        foreach (var secreto in Secretos)
            if (!NombreValido.IsMatch(secreto))
                throw new InvalidOperationException(
                    $"[SecretosAws] '{secreto}' no es un nombre de secreto válido: solo minúsculas, " +
                    "números y guiones, sin '/'.");
    }
}
```

`MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretoNoDisponibleException.cs`:

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

public enum MotivoSecretoNoDisponible { SinPermiso, NoExiste, SinCredencial, FormatoInvalido, Otro }

/// <summary>Un secreto no se pudo leer. El mensaje dice cuál y por qué; nunca trae su valor.</summary>
public sealed class SecretoNoDisponibleException : InvalidOperationException
{
    public string Secreto { get; }
    public MotivoSecretoNoDisponible Motivo { get; }

    public SecretoNoDisponibleException(string secreto, MotivoSecretoNoDisponible motivo, Exception? causa = null)
        : base(Mensaje(secreto, motivo, causa), causa)
    {
        Secreto = secreto;
        Motivo  = motivo;
    }

    private static string Mensaje(string secreto, MotivoSecretoNoDisponible motivo, Exception? causa) => motivo switch
    {
        MotivoSecretoNoDisponible.SinPermiso =>
            $"[SecretosAws] La identidad de este servicio no tiene permiso sobre '{secreto}' (o sobre la llave KMS del entorno). Revisa su fila en deploy/aws/secretos-por-servicio.json y vuelve a correr deploy/aws/crear-roles.sh.",
        MotivoSecretoNoDisponible.NoExiste =>
            $"[SecretosAws] El secreto '{secreto}' no existe. Créalo con deploy/aws/crear-kms-y-secretos.sh y cárgalo con deploy/aws/cargar-secreto.sh.",
        MotivoSecretoNoDisponible.SinCredencial =>
            "[SecretosAws] Este servicio no tiene identidad de AWS: en EKS falta su cuenta de servicio con IRSA; en Docker falta el certificado de Roles Anywhere o AWS_CONFIG_FILE; en tu máquina, `aws sso login`." +
            (causa is null ? string.Empty : $" Detalle: {causa.Message}"),
        MotivoSecretoNoDisponible.FormatoInvalido =>
            $"[SecretosAws] '{secreto}' no es un JSON de pares clave-texto.",
        _ =>
            $"[SecretosAws] No se pudo leer '{secreto}'." + (causa is null ? string.Empty : $" Detalle: {causa.Message}")
    };
}
```

- [ ] **Step 4: Correr y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~SecretosAwsOpcionesTests" -v q`
Expected: `Passed!` con 13 pruebas.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretosAwsOpciones.cs MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretoNoDisponibleException.cs MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/SecretosAwsOpcionesTests.cs
git commit -m "SecretosAws: opciones por servicio (habilitado, entorno local/staging/prod, region y lista) con nombres mlmconqueror/{entorno}/{secreto} y excepcion con el motivo"
```

---

### Task 3: Lector de Secrets Manager

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedKernel.Server/MLMConquerorGlobalEdition.SharedKernel.Server.csproj`
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/ILectorDeSecretos.cs`
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/LectorDeSecretosAws.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/LectorDeSecretosAwsTests.cs`

- [ ] **Step 1: Agregar el paquete**

En el `ItemGroup` de `PackageReference` del csproj de SharedKernel.Server, después de `StackExchange.Redis`:

```xml
    <!-- SecretosAws: cada servicio lee al arrancar solo sus secretos de AWS Secrets Manager.
         Misma línea 3.7 que los AWSSDK.S3 que ya usan AdminAPI, BizCenter, RankEngine y SignupAPI. -->
    <PackageReference Include="AWSSDK.SecretsManager" Version="3.7.504.43" />
```

Run: `dotnet restore MLMConquerorGlobalEdition.SharedKernel.Server --force-evaluate`
Expected: restaura sin errores.

- [ ] **Step 2: Escribir las pruebas**

```csharp
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using Moq;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

public class LectorDeSecretosAwsTests
{
    private const string Nombre = "mlmconqueror/local/datos";

    private static LectorDeSecretosAws ConCliente(Action<Mock<IAmazonSecretsManager>> preparar)
    {
        var cliente = new Mock<IAmazonSecretsManager>();
        preparar(cliente);
        return new LectorDeSecretosAws(cliente.Object);
    }

    private static void Lanza(Mock<IAmazonSecretsManager> c, Exception ex) =>
        c.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(ex);

    [Fact]
    public void DevuelveElTextoDelSecreto()
    {
        var lector = ConCliente(c => c
            .Setup(x => x.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == Nombre), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "{\"a\":\"b\"}" }));

        lector.Leer(Nombre).Should().Be("{\"a\":\"b\"}");
    }

    [Fact]
    public void NoExiste()
    {
        var lector = ConCliente(c => Lanza(c, new ResourceNotFoundException("no")));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.NoExiste && e.Secreto == Nombre);
    }

    [Fact]
    public void SinPermiso()
    {
        var lector = ConCliente(c => Lanza(c, new AmazonSecretsManagerException("denegado")
            { ErrorCode = "AccessDeniedException" }));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.SinPermiso);
    }

    [Fact]
    public void SinPermisoSobreLaLlaveKms()
    {
        var lector = ConCliente(c => Lanza(c, new DecryptionFailureException("kms")));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.SinPermiso);
    }

    [Fact]
    public void SinCredencial()
    {
        var lector = ConCliente(c => Lanza(c, new AmazonClientException("Failed to resolve AWS credentials")));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.SinCredencial);
    }

    [Fact]
    public void SecretoBinario_EsFormatoInvalido()
    {
        var lector = ConCliente(c => c
            .Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = null }));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.FormatoInvalido);
    }

    [Fact]
    public void OtroErrorDeAws()
    {
        var lector = ConCliente(c => Lanza(c, new AmazonSecretsManagerException("throttle")
            { ErrorCode = "ThrottlingException" }));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.Otro);
    }
}
```

- [ ] **Step 3: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~LectorDeSecretosAwsTests" -v q`
Expected: error de compilación, `LectorDeSecretosAws` no existe.

- [ ] **Step 4: Implementar**

`ILectorDeSecretos.cs`:

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

/// <summary>Lee el texto de un secreto por su nombre completo (mlmconqueror/{entorno}/{secreto}).</summary>
public interface ILectorDeSecretos
{
    /// <exception cref="SecretoNoDisponibleException">Siempre que no se pueda leer.</exception>
    string Leer(string nombreCompleto);
}
```

`LectorDeSecretosAws.cs`:

```csharp
using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

/// <summary>
/// Secrets Manager con la cadena de credenciales por defecto del SDK: IRSA en EKS,
/// credential_process de Roles Anywhere en Docker, el perfil del desarrollador en local.
/// Corre una sola vez, al arrancar, antes de que exista el contenedor de dependencias; por eso es
/// síncrono.
/// </summary>
public sealed class LectorDeSecretosAws : ILectorDeSecretos, IDisposable
{
    private readonly IAmazonSecretsManager _cliente;

    public LectorDeSecretosAws(string region)
        : this(new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region))) { }

    public LectorDeSecretosAws(IAmazonSecretsManager cliente) => _cliente = cliente;

    public string Leer(string nombreCompleto)
    {
        GetSecretValueResponse respuesta;
        try
        {
            respuesta = _cliente
                .GetSecretValueAsync(new GetSecretValueRequest { SecretId = nombreCompleto })
                .GetAwaiter().GetResult();
        }
        catch (ResourceNotFoundException ex)
        {
            throw new SecretoNoDisponibleException(nombreCompleto, MotivoSecretoNoDisponible.NoExiste, ex);
        }
        catch (DecryptionFailureException ex)
        {
            throw new SecretoNoDisponibleException(nombreCompleto, MotivoSecretoNoDisponible.SinPermiso, ex);
        }
        catch (AmazonSecretsManagerException ex) when (ex.ErrorCode == "AccessDeniedException")
        {
            throw new SecretoNoDisponibleException(nombreCompleto, MotivoSecretoNoDisponible.SinPermiso, ex);
        }
        catch (AmazonServiceException ex)
        {
            throw new SecretoNoDisponibleException(nombreCompleto, MotivoSecretoNoDisponible.Otro, ex);
        }
        catch (AmazonClientException ex)
        {
            // El SDK no consiguió credenciales (o el credential_process falló) antes de llamar.
            throw new SecretoNoDisponibleException(nombreCompleto, MotivoSecretoNoDisponible.SinCredencial, ex);
        }

        return respuesta.SecretString
            ?? throw new SecretoNoDisponibleException(nombreCompleto, MotivoSecretoNoDisponible.FormatoInvalido);
    }

    public void Dispose() => _cliente.Dispose();
}
```

Nota para quien implemente: en el SDK 3.7, `AmazonServiceException` hereda de `AmazonClientException`. Por eso el `catch` de `AmazonServiceException` va **antes** que el de `AmazonClientException`; si el compilador avisa CS0160 (bloque inalcanzable), es que la herencia está al revés en la versión instalada: invertir los dos bloques.

- [ ] **Step 5: Correr y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~LectorDeSecretosAwsTests" -v q`
Expected: `Passed!` con 7 pruebas.

- [ ] **Step 6: Lockfiles y commit**

```bash
dotnet restore MLMConquerorGlobalEdition.slnx --force-evaluate
git status --short '*packages.lock.json'
```

Para cada lockfile modificado: si `git diff --ignore-cr-at-eol --numstat -- <archivo>` da 0 líneas, `git checkout -- <archivo>`. Los que quedan se suben.

```bash
git add MLMConquerorGlobalEdition.SharedKernel.Server/MLMConquerorGlobalEdition.SharedKernel.Server.csproj MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/ILectorDeSecretos.cs MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/LectorDeSecretosAws.cs MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/LectorDeSecretosAwsTests.cs $(git diff --name-only -- '*packages.lock.json')
git commit -m "SecretosAws: lector de Secrets Manager con la cadena de credenciales del SDK; traduce no existe, sin permiso (tambien KMS), sin credencial y formato a un motivo claro"
```

---

### Task 4: `AddSecretosDeAws()`

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretosAwsConfiguracion.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/AddSecretosDeAwsTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

public class AddSecretosDeAwsTests
{
    /// <summary>Lector en memoria que anota qué se le pidió.</summary>
    private sealed class LectorFalso(Dictionary<string, string> secretos) : ILectorDeSecretos
    {
        public List<string> Pedidos { get; } = [];
        public string Leer(string nombre)
        {
            Pedidos.Add(nombre);
            return secretos.TryGetValue(nombre, out var v)
                ? v
                : throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.NoExiste);
        }
    }

    private static ConfigurationManager Config(string entorno = "local", bool habilitado = true, string[]? secretos = null)
    {
        var c = new ConfigurationManager();
        var datos = new Dictionary<string, string?>
        {
            ["SecretosAws:Habilitado"] = habilitado.ToString(),
            ["SecretosAws:Entorno"]    = entorno,
            ["SecretosAws:Region"]     = "us-east-1",
            ["ConnectionStrings:DefaultConnection"] = "valor-de-appsettings",
        };
        secretos ??= [];
        for (var i = 0; i < secretos.Length; i++) datos[$"SecretosAws:Secretos:{i}"] = secretos[i];
        c.AddInMemoryCollection(datos);
        return c;
    }

    [Fact]
    public void CargaLasClavesYConvierteDobleGuionBajoEnDosPuntos()
    {
        var c = Config(secretos: ["datos"]);
        var lector = new LectorFalso(new() {
            ["mlmconqueror/local/datos"] = "{\"ConnectionStrings__DefaultConnection\":\"de-aws\",\"ConnectionStrings__Redis\":\"r\"}" });

        c.AddSecretosDeAws(lector, TextWriter.Null);

        c["ConnectionStrings:DefaultConnection"].Should().Be("de-aws");
        c["ConnectionStrings:Redis"].Should().Be("r");
    }

    [Fact]
    public void QuedaPorDebajoDeLasVariablesDeEntorno()
    {
        var prefijo = $"MLMPRUEBA_{Guid.NewGuid():N}_";
        Environment.SetEnvironmentVariable(prefijo + "ConnectionStrings__DefaultConnection", "de-entorno");
        try
        {
            var c = Config(secretos: ["datos"]);
            c.Sources.Add(new EnvironmentVariablesConfigurationSource { Prefix = prefijo });
            var lector = new LectorFalso(new() {
                ["mlmconqueror/local/datos"] = "{\"ConnectionStrings__DefaultConnection\":\"de-aws\"}" });

            c.AddSecretosDeAws(lector, TextWriter.Null);

            c["ConnectionStrings:DefaultConnection"].Should().Be("de-entorno");
        }
        finally { Environment.SetEnvironmentVariable(prefijo + "ConnectionStrings__DefaultConnection", null); }
    }

    [Fact]
    public void SoloPideLosDeSuEntorno()
    {
        var c = Config(entorno: "local", secretos: ["datos", "twilio"]);
        var lector = new LectorFalso(new() {
            ["mlmconqueror/local/datos"]  = "{}",
            ["mlmconqueror/local/twilio"] = "{}" });

        c.AddSecretosDeAws(lector, TextWriter.Null);

        lector.Pedidos.Should().Equal("mlmconqueror/local/datos", "mlmconqueror/local/twilio");
    }

    [Fact]
    public void Deshabilitado_NoLlamaAAws()
    {
        var c = Config(habilitado: false, secretos: ["datos"]);
        var lector = new LectorFalso([]);

        c.AddSecretosDeAws(lector, TextWriter.Null);

        lector.Pedidos.Should().BeEmpty();
        c["ConnectionStrings:DefaultConnection"].Should().Be("valor-de-appsettings");
    }

    [Fact]
    public void ListaVacia_NoLlamaAAws()
    {
        var c = Config(secretos: []);
        var lector = new LectorFalso([]);

        c.AddSecretosDeAws(lector, TextWriter.Null);

        lector.Pedidos.Should().BeEmpty();
    }

    [Fact]
    public void SiFaltaUnSecreto_NoArrancaYNoDejaElValorDeAppsettings()
    {
        var c = Config(secretos: ["datos"]);
        var lector = new LectorFalso([]);

        c.Invoking(x => x.AddSecretosDeAws(lector, TextWriter.Null))
            .Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.NoExiste && e.Secreto == "mlmconqueror/local/datos");
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("[1,2]")]
    [InlineData("{\"Clave\": 5}")]
    [InlineData("{\"Clave\": {\"anidado\":\"x\"}}")]
    public void SecretoQueNoEsJsonDeTextos_FormatoInvalido(string valor)
    {
        var c = Config(secretos: ["datos"]);
        var lector = new LectorFalso(new() { ["mlmconqueror/local/datos"] = valor });

        c.Invoking(x => x.AddSecretosDeAws(lector, TextWriter.Null))
            .Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.FormatoInvalido);
    }

    [Fact]
    public void EntornoNoValido_NoArranca()
    {
        var c = Config(entorno: "production", secretos: ["datos"]);
        c.Invoking(x => x.AddSecretosDeAws(new LectorFalso([]), TextWriter.Null))
            .Should().Throw<InvalidOperationException>().WithMessage("*Entorno*");
    }

    [Fact]
    public void ElLogTraeNombresYNuncaValores()
    {
        var c = Config(secretos: ["jwt-firma"]);
        var lector = new LectorFalso(new() {
            ["mlmconqueror/local/jwt-firma"] = "{\"Jwt__PrivateKeyBase64\":\"MIIEvSECRETO\"}" });
        var log = new StringWriter();

        c.AddSecretosDeAws(lector, log);

        log.ToString().Should().Contain("mlmconqueror/local/jwt-firma").And.Contain("1 clave");
        log.ToString().Should().NotContain("MIIEvSECRETO").And.NotContain("Jwt__PrivateKeyBase64");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~AddSecretosDeAwsTests" -v q`
Expected: error de compilación, `AddSecretosDeAws` no existe.

- [ ] **Step 3: Implementar**

`SecretosAwsConfiguracion.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Memory;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

public static class SecretosAwsConfiguracion
{
    /// <summary>
    /// Lee de AWS Secrets Manager los secretos de la lista <c>SecretosAws:Secretos</c> de este
    /// servicio, en su entorno, y los mezcla en la configuración justo antes de las variables de
    /// entorno: ganan sobre appsettings.json y pierden contra una variable de entorno (para forzar
    /// un valor en una emergencia sin tocar AWS). Si uno falla, el servicio no arranca: nunca se
    /// sigue con el valor de appsettings.json.
    ///
    /// Se llama en Program.cs justo después de WebApplication.CreateBuilder, antes de que nada
    /// lea la configuración.
    /// </summary>
    public static ConfigurationManager AddSecretosDeAws(
        this ConfigurationManager config, ILectorDeSecretos? lector = null, TextWriter? log = null)
    {
        log ??= Console.Out;
        var opciones = SecretosAwsOpciones.Leer(config);

        if (!opciones.Habilitado)
        {
            log.WriteLine("[SecretosAws] Deshabilitado: se usan solo appsettings y variables de entorno.");
            return config;
        }

        opciones.Validar();
        if (opciones.Secretos.Count == 0) return config;

        var propio = lector is null;
        lector ??= new LectorDeSecretosAws(opciones.Region);
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var secreto in opciones.Secretos)
            {
                var nombre = opciones.NombreCompleto(secreto);
                var claves = Interpretar(nombre, lector.Leer(nombre));
                foreach (var (clave, valor) in claves) valores[clave] = valor;
                log.WriteLine($"[SecretosAws] {nombre}: {claves.Count} clave(s).");
            }
        }
        finally
        {
            if (propio) (lector as IDisposable)?.Dispose();
        }

        var fuente = new MemoryConfigurationSource { InitialData = valores };
        var indice = UltimaFuenteDeVariablesDeEntorno(config);
        if (indice < 0) config.Sources.Add(fuente);
        else config.Sources.Insert(indice, fuente);

        return config;
    }

    /// <summary>
    /// WebApplication.CreateBuilder agrega varias fuentes de variables de entorno: las de
    /// DOTNET_/ASPNETCORE_ al principio (configuración del anfitrión) y la sin prefijo después de
    /// appsettings. La que importa es la última.
    /// </summary>
    private static int UltimaFuenteDeVariablesDeEntorno(ConfigurationManager config)
    {
        for (var i = config.Sources.Count - 1; i >= 0; i--)
            if (config.Sources[i] is EnvironmentVariablesConfigurationSource) return i;
        return -1;
    }

    private static Dictionary<string, string> Interpretar(string nombre, string texto)
    {
        try
        {
            using var doc = JsonDocument.Parse(texto);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.FormatoInvalido);

            var claves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.String)
                    throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.FormatoInvalido);
                claves[p.Name.Replace("__", ConfigurationPath.KeyDelimiter)] = p.Value.GetString()!;
            }
            return claves;
        }
        catch (JsonException ex)
        {
            throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.FormatoInvalido, ex);
        }
    }
}
```

Importante: `SecretoNoDisponibleException` con `FormatoInvalido` no debe incluir el mensaje de `JsonException` en su texto, porque `JsonException` puede citar parte del valor. El `switch` de `Mensaje` en la Task 2 ya no agrega `causa.Message` para `FormatoInvalido`; no cambiarlo.

- [ ] **Step 4: Correr y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~SecretosAws" -v q`
Expected: `Passed!`, todas las de las Tasks 2, 3 y 4 (32 pruebas).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel.Server/Hosting/SecretosAwsConfiguracion.cs MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/AddSecretosDeAwsTests.cs
git commit -m "SecretosAws: AddSecretosDeAws lee la lista del servicio en su entorno y la mezcla justo antes de las variables de entorno; si un secreto falla el servicio no arranca, y el log trae nombres y cantidad de claves, nunca valores"
```

---

### Task 5: Certificado del anillo de pasarelas desde base64

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/MLMConquerorGlobalEdition.Repository.csproj`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Payout/GatewayCredentialProtectionExtensions.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/CertificadoDelAnilloDesdeBase64Tests.cs`

- [ ] **Step 1: Abrir los internos a las pruebas de Billing**

En el csproj de Repository, en un `ItemGroup` nuevo al final:

```xml
  <ItemGroup>
    <!-- ResolveCertificate y ResolveRetiredCertificates: cómo se carga el certificado del anillo. -->
    <InternalsVisibleTo Include="MLMConquerorGlobalEdition.Billing.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: Escribir las pruebas**

```csharp
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Payout;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// En EKS y en Docker el PFX del anillo llega desde Secrets Manager, en base64, y no como archivo.
/// </summary>
public class CertificadoDelAnilloDesdeBase64Tests
{
    private static (string Base64, string Huella) NuevoPfx(string clave)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=Prueba anillo", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = pedido.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        return (Convert.ToBase64String(cert.Export(X509ContentType.Pfx, clave)), cert.Thumbprint);
    }

    private static IConfiguration Config(params (string, string?)[] pares) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pares.Select(p => new KeyValuePair<string, string?>(p.Item1, p.Item2)))
            .Build();

    [Fact]
    public void CargaElCertificadoDesdeBase64()
    {
        var (b64, huella) = NuevoPfx("clave-1");
        var config = Config(
            (GatewayCredentialProtectionExtensions.CertificateBase64Key, b64),
            (GatewayCredentialProtectionExtensions.CertificatePasswordKey, "clave-1"));

        var cert = GatewayCredentialProtectionExtensions.ResolveCertificate(config, env: null);

        cert.Thumbprint.Should().Be(huella);
        cert.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void Base64GanaSobrePath()
    {
        var (b64, huella) = NuevoPfx("clave-1");
        var config = Config(
            (GatewayCredentialProtectionExtensions.CertificateBase64Key, b64),
            (GatewayCredentialProtectionExtensions.CertificatePasswordKey, "clave-1"),
            (GatewayCredentialProtectionExtensions.CertificatePathKey, "/no/existe.pfx"));

        GatewayCredentialProtectionExtensions.ResolveCertificate(config, env: null)
            .Thumbprint.Should().Be(huella);
    }

    [Fact]
    public void Base64Invalido_DiceQueClaveFalla()
    {
        var config = Config((GatewayCredentialProtectionExtensions.CertificateBase64Key, "%%no-es-base64%%"));

        FluentActions.Invoking(() => GatewayCredentialProtectionExtensions.ResolveCertificate(config, env: null))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"*{GatewayCredentialProtectionExtensions.CertificateBase64Key}*");
    }

    [Fact]
    public void CargaLosRetiradosDesdeBase64()
    {
        var (b64, huella) = NuevoPfx("vieja");
        var config = Config(
            ("DataProtection:Certificate:Retired:0:Base64", b64),
            ("DataProtection:Certificate:Retired:0:Password", "vieja"));

        GatewayCredentialProtectionExtensions.ResolveRetiredCertificates(config)
            .Select(c => c.Thumbprint).Should().Equal(huella);
    }
}
```

- [ ] **Step 3: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~CertificadoDelAnilloDesdeBase64Tests" -v q`
Expected: error de compilación (`CertificateBase64Key` no existe; `ResolveCertificate` es privado).

- [ ] **Step 4: Implementar**

En `GatewayCredentialProtectionExtensions.cs`:

1. Junto a las otras constantes:

```csharp
    public const string CertificateBase64Key     = "DataProtection:Certificate:Base64";
```

2. `ResolveCertificate` y `ResolveRetiredCertificates` pasan de `private static` a `internal static`.

3. En `ResolveCertificate`, entre el bloque de "Opción A" (thumbprint) y el de "Opción B" (archivo PFX):

```csharp
        // Opción B: el PFX en base64. Así llega desde AWS Secrets Manager (secreto dp-gateway)
        // en EKS y en Docker; gana sobre Path.
        var base64 = config[CertificateBase64Key];
        if (!string.IsNullOrWhiteSpace(base64))
            return CargarPfxDesdeBase64(base64!, config[CertificatePasswordKey], CertificateBase64Key);
```

y el comentario `// Opción B: archivo PFX.` pasa a `// Opción C: archivo PFX.`. El mensaje de la excepción "needs a certificate" agrega `'{CertificateBase64Key}' (PFX en base64)` a las opciones que lista.

4. En `ResolveRetiredCertificates`, después del bloque de `Thumbprint` y antes de `var path = section["Path"];`:

```csharp
            var base64 = section["Base64"];
            if (!string.IsNullOrWhiteSpace(base64))
            {
                yield return CargarPfxDesdeBase64(base64!, section["Password"], $"{section.Path}:Base64");
                continue;
            }
```

5. Método nuevo, antes de `FindByThumbprint`:

```csharp
    private static X509Certificate2 CargarPfxDesdeBase64(string base64, string? password, string clave)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"'{clave}' no es base64 válido: tiene que ser el PFX completo en base64.", ex);
        }

        var cert = X509CertificateLoader.LoadPkcs12(
            bytes, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);

        if (!cert.HasPrivateKey)
            throw new InvalidOperationException($"El certificado de '{clave}' no trae la llave privada.");

        return cert;
    }
```

- [ ] **Step 5: Correr las pruebas nuevas y las del protector**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~CertificadoDelAnilloDesdeBase64Tests|FullyQualifiedName~GatewayCredential" -v q`
Expected: `Passed!`.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/MLMConquerorGlobalEdition.Repository.csproj MLMConquerorGlobalEdition.Repository/Services/Payout/GatewayCredentialProtectionExtensions.cs MLMConquerorGlobalEdition.Billing.Tests/Services/CertificadoDelAnilloDesdeBase64Tests.cs
git commit -m "Anillo de credenciales de pasarelas: el PFX (actual y retirados) se puede dar en base64, que es como llega desde Secrets Manager; gana sobre Path y un base64 invalido dice que clave falla"
```

---

### Task 6: Credencial de Firebase desde base64

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/FirebaseCredentialLoader.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/FirebasePushNotificationService.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/FirebaseCredentialLoaderTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class FirebaseCredentialLoaderTests
{
    private const string Correo = "push@proyecto-prueba.iam.gserviceaccount.com";

    private static string CuentaDeServicioJson()
    {
        using var rsa = RSA.Create(2048);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "proyecto-prueba",
            ["private_key_id"] = "k1",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = Correo,
            ["client_id"] = "1",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
        });
    }

    private static IConfiguration Config(string clave, string valor) =>
        new ConfigurationBuilder().AddInMemoryCollection([new(clave, valor)]).Build();

    [Fact]
    public void CargaDesdeBase64()
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(CuentaDeServicioJson()));

        var cred = FirebaseCredentialLoader.Load(Config(FirebaseCredentialLoader.JsonBase64Key, b64));

        cred.UnderlyingCredential.Should().BeOfType<ServiceAccountCredential>()
            .Which.Id.Should().Be(Correo);
    }

    [Fact]
    public void CargaDesdeArchivo()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"firebase-{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, CuentaDeServicioJson());
        try
        {
            var cred = FirebaseCredentialLoader.Load(Config(FirebaseCredentialLoader.FilePathKey, ruta));
            cred.UnderlyingCredential.Should().BeOfType<ServiceAccountCredential>()
                .Which.Id.Should().Be(Correo);
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public void Base64Invalido_DiceQueClaveFalla()
    {
        FluentActions.Invoking(() => FirebaseCredentialLoader.Load(Config(FirebaseCredentialLoader.JsonBase64Key, "%%")))
            .Should().Throw<InvalidOperationException>().WithMessage($"*{FirebaseCredentialLoader.JsonBase64Key}*");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~FirebaseCredentialLoaderTests" -v q`
Expected: error de compilación, `FirebaseCredentialLoader` no existe.

- [ ] **Step 3: Implementar**

`FirebaseCredentialLoader.cs`:

```csharp
using System.Text;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.Repository.Services;

/// <summary>
/// La credencial de la cuenta de servicio de Firebase. En EKS y en Docker llega desde Secrets
/// Manager (secreto firebase) como JSON en base64; en una máquina de desarrollo puede seguir
/// siendo un archivo. Sin ninguna de las dos se usan las credenciales por defecto de Google.
/// </summary>
public static class FirebaseCredentialLoader
{
    public const string JsonBase64Key = "Firebase:CredentialsJsonBase64";
    public const string FilePathKey   = "Firebase:CredentialsFilePath";

    public static GoogleCredential Load(IConfiguration config)
    {
        var base64 = config[JsonBase64Key];
        if (!string.IsNullOrWhiteSpace(base64))
        {
            string json;
            try { json = Encoding.UTF8.GetString(Convert.FromBase64String(base64!)); }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"'{JsonBase64Key}' no es base64 válido.", ex);
            }
            return GoogleCredential.FromJson(json);
        }

        var path = config[FilePathKey];
        return string.IsNullOrWhiteSpace(path)
            ? GoogleCredential.GetApplicationDefault()
            : GoogleCredential.FromFile(path);
    }
}
```

En `FirebasePushNotificationService.cs`, el bloque

```csharp
                var credPath = configuration["Firebase:CredentialsFilePath"];
                var credential = string.IsNullOrWhiteSpace(credPath)
                    ? GoogleCredential.GetApplicationDefault()
                    : GoogleCredential.FromFile(credPath);
```

pasa a

```csharp
                var credential = FirebaseCredentialLoader.Load(configuration);
```

y el comentario de la clase agrega la línea `///   Firebase:CredentialsJsonBase64 — JSON de la cuenta de servicio en base64 (gana sobre el archivo)`.

Si el compilador marca `GoogleCredential.FromJson` como obsoleto (CS0618, según la versión de `Google.Apis.Auth` que trae FirebaseAdmin 3.1.0), usar `CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential()`, que es el reemplazo indicado por Google para cuentas de servicio, y cambiar igual `FromFile` por `CredentialFactory.FromFile<ServiceAccountCredential>(path).ToGoogleCredential()`.

- [ ] **Step 4: Correr y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~FirebaseCredentialLoaderTests" -v q`
Expected: `Passed!` con 3 pruebas.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/FirebaseCredentialLoader.cs MLMConquerorGlobalEdition.Repository/Services/FirebasePushNotificationService.cs MLMConquerorGlobalEdition.Billing.Tests/Services/FirebaseCredentialLoaderTests.cs
git commit -m "Firebase: la credencial de la cuenta de servicio se puede dar en base64 (Firebase:CredentialsJsonBase64), que es como llega desde Secrets Manager; el archivo sigue funcionando"
```

---

### Task 7: Tabla única, lista por servicio y conexión en los 11 `Program.cs`

**Files:**
- Create: `deploy/aws/secretos-por-servicio.json`
- Modify: 11 × `MLMConquerorGlobalEdition.<Servicio>/appsettings.json`
- Modify: 11 × `MLMConquerorGlobalEdition.<Servicio>/Program.cs`
- Modify: `MLMConquerorGlobalEdition.Signups/appsettings.json` (además, quitar `ConnectionStrings`)
- Test: `MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/ListaDeCadaServicioTests.cs`

- [ ] **Step 1: Crear la tabla**

`deploy/aws/secretos-por-servicio.json`:

```json
{
  "_comentario": "Unica tabla de quien lee que. De aqui salen los roles de IAM (crear-roles.sh) y contra ella se prueba la lista SecretosAws:Secretos del appsettings.json de cada servicio (ListaDeCadaServicioTests). El nombre de cada servicio es el del proyecto sin el prefijo MLMConquerorGlobalEdition.",
  "secretos": ["datos", "jwt-firma", "dp-gateway", "signupapi", "billing", "sharedapicenter", "portales", "firebase", "twilio"],
  "servicios": {
    "AdminAPI":               { "secretos": ["datos", "jwt-firma", "dp-gateway", "twilio"], "aws": ["s3", "ses"] },
    "AdminWeb":               { "secretos": ["portales"], "aws": [] },
    "Billing":                { "secretos": ["datos", "dp-gateway", "billing", "firebase", "twilio"], "aws": ["s3", "ses"] },
    "BizCenter":              { "secretos": ["datos", "dp-gateway", "firebase", "twilio"], "aws": ["s3", "ses"] },
    "BizCenterWeb":           { "secretos": ["portales"], "aws": [] },
    "CommissionEngine":       { "secretos": ["datos", "firebase"], "aws": [] },
    "RankEngine":             { "secretos": ["datos", "firebase", "twilio"], "aws": ["s3", "ses"] },
    "SharedAPICenter":        { "secretos": ["datos", "sharedapicenter", "firebase"], "aws": [] },
    "SignupAPI":              { "secretos": ["datos", "jwt-firma", "signupapi", "firebase", "twilio"], "aws": ["s3", "ses"] },
    "Signups":                { "secretos": [], "aws": [] },
    "TicketManagementSystem": { "secretos": ["datos", "firebase"], "aws": [] }
  }
}
```

- [ ] **Step 2: Escribir la prueba de consistencia**

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

/// <summary>
/// La lista SecretosAws:Secretos del appsettings.json de cada servicio y su fila en
/// deploy/aws/secretos-por-servicio.json tienen que ser iguales. Si se agrega un secreto en un lado
/// y no en el otro, o el servicio no arranca (le falta permiso) o tiene un permiso que no usa.
/// </summary>
public class ListaDeCadaServicioTests
{
    private static readonly string Raiz = BuscarRaiz();

    private static string BuscarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static JsonElement Tabla() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Raiz, "deploy", "aws", "secretos-por-servicio.json"))).RootElement;

    /// <summary>appsettings.json admite comentarios // y comas finales.</summary>
    private static JsonElement Appsettings(string servicio)
    {
        var ruta = Path.Combine(Raiz, $"MLMConquerorGlobalEdition.{servicio}", "appsettings.json");
        return JsonDocument.Parse(File.ReadAllText(ruta), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
        }).RootElement;
    }

    public static TheoryData<string> Servicios()
    {
        var datos = new TheoryData<string>();
        foreach (var s in Tabla().GetProperty("servicios").EnumerateObject()) datos.Add(s.Name);
        return datos;
    }

    [Fact]
    public void EstanLosOnceServicios()
    {
        Tabla().GetProperty("servicios").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "AdminAPI", "AdminWeb", "Billing", "BizCenter", "BizCenterWeb", "CommissionEngine",
            "RankEngine", "SharedAPICenter", "SignupAPI", "Signups", "TicketManagementSystem");
    }

    [Theory]
    [MemberData(nameof(Servicios))]
    public void LaListaDelAppsettingsEsSuFilaDeLaTabla(string servicio)
    {
        var fila = Tabla().GetProperty("servicios").GetProperty(servicio).GetProperty("secretos")
            .EnumerateArray().Select(e => e.GetString()).ToArray();

        var seccion = Appsettings(servicio).GetProperty("SecretosAws");
        var lista = seccion.GetProperty("Secretos").EnumerateArray().Select(e => e.GetString()).ToArray();

        lista.Should().Equal(fila);
        seccion.GetProperty("Habilitado").GetBoolean().Should().BeTrue();
        seccion.GetProperty("Entorno").GetString().Should().Be("local",
            "el repositorio apunta a local; staging y prod se eligen con SecretosAws__Entorno");
    }

    [Fact]
    public void CadaSecretoDeUnaFilaEstaDeclarado()
    {
        var declarados = Tabla().GetProperty("secretos").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        foreach (var s in Tabla().GetProperty("servicios").EnumerateObject())
            s.Value.GetProperty("secretos").EnumerateArray().Select(e => e.GetString())
                .Should().OnlyContain(n => declarados.Contains(n), $"{s.Name} usa un secreto no declarado");
    }

    [Theory]
    [MemberData(nameof(Servicios))]
    public void ElProgramLlamaAddSecretosDeAwsJustoDespuesDeCrearElBuilder(string servicio)
    {
        var program = File.ReadAllText(Path.Combine(Raiz, $"MLMConquerorGlobalEdition.{servicio}", "Program.cs"));
        Regex.IsMatch(program,
            @"var builder = WebApplication\.CreateBuilder\(args\);\s*(//[^\n]*\n\s*)*builder\.Configuration\.AddSecretosDeAws\(\);")
            .Should().BeTrue($"{servicio} tiene que leer sus secretos antes de que nada lea la configuración");
    }
}
```

- [ ] **Step 3: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~ListaDeCadaServicioTests" -v q`
Expected: FALLA. `EstanLosOnceServicios` y `CadaSecretoDeUnaFilaEstaDeclarado` pasan; las de `appsettings` fallan con `KeyNotFoundException` (`SecretosAws`) y las de `Program.cs` con el mensaje de la aserción.

- [ ] **Step 4: Agregar la sección a los 11 `appsettings.json`**

En cada uno, como primera propiedad del objeto raíz (antes de `ConnectionStrings` o de la primera que haya), con la lista de su fila en la tabla:

```json
  "SecretosAws": {
    "Habilitado": true,
    "Entorno": "local",
    "Region": "us-east-1",
    "Secretos": [ "datos", "jwt-firma", "dp-gateway", "twilio" ]
  },
```

(el ejemplo es AdminAPI; para cada servicio va su fila). Se edita con el editor, no con un script que reescriba el JSON, porque los archivos tienen comentarios que hay que conservar.

En `MLMConquerorGlobalEdition.Signups/appsettings.json`, además, se borra el objeto `ConnectionStrings` completo: Signups no usa la base.

- [ ] **Step 5: Llamar `AddSecretosDeAws()` en los 11 `Program.cs`**

Inmediatamente después de `var builder = WebApplication.CreateBuilder(args);`:

```csharp
// Secretos de AWS Secrets Manager (lista SecretosAws:Secretos de este servicio). Va antes que
// cualquier cosa que lea la configuración: si falta uno, el servicio no arranca.
builder.Configuration.AddSecretosDeAws();
```

Los 11 ya tienen `using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;` (Fase 0). Comprobar con:

```bash
for s in AdminAPI AdminWeb Billing BizCenter BizCenterWeb CommissionEngine RankEngine SharedAPICenter SignupAPI Signups TicketManagementSystem; do
  grep -q "using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;" MLMConquerorGlobalEdition.$s/Program.cs || echo "falta using en $s"
done
```

Expected: ninguna línea.

- [ ] **Step 6: Desactivar en tu máquina mientras no existan los secretos `local`**

Para que `dotnet run` siga funcionando en la máquina de Nick hasta la Task 9, en el `appsettings.Development.json` de cada uno de los 10 servicios con lista no vacía (no se suben al repositorio: `.gitignore` ignora `appsettings.*.json`):

```json
  "SecretosAws": { "Habilitado": false }
```

Si el archivo no existe, se crea con `{ "SecretosAws": { "Habilitado": false } }`. Esto no va en ningún commit.

- [ ] **Step 7: Correr las pruebas y compilar todo**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "FullyQualifiedName~SecretosAws" -v q`
Expected: `Passed!`.

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q`
Expected: `0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add deploy/aws/secretos-por-servicio.json MLMConquerorGlobalEdition.Billing.Tests/SecretosAws/ListaDeCadaServicioTests.cs $(for s in AdminAPI AdminWeb Billing BizCenter BizCenterWeb CommissionEngine RankEngine SharedAPICenter SignupAPI Signups TicketManagementSystem; do echo MLMConquerorGlobalEdition.$s/appsettings.json MLMConquerorGlobalEdition.$s/Program.cs; done)
git commit -m "Los 11 servicios leen sus secretos de AWS al arrancar: lista SecretosAws en cada appsettings.json (entorno local en el repositorio) y AddSecretosDeAws justo despues de crear el builder. deploy/aws/secretos-por-servicio.json es la tabla unica de quien lee que, con prueba de que cada lista coincide. Signups no lee ningun secreto y pierde sus cadenas de conexion, que no usaba"
```

---

### Task 8: Scripts de AWS (`deploy/aws/`)

**Files:**
- Create: `deploy/aws/comun.sh`, `crear-kms-y-secretos.sh`, `cargar-secreto.sh`, `crear-roles.sh`, `roles-anywhere.sh`, `emitir-certificados.sh`, `politica-desarrollador-local.sh`
- Create: `deploy/aws/.gitignore`

Todos con `set -euo pipefail`, se corren desde la raíz del repositorio con Git Bash o Linux, requieren AWS CLI v2, `jq` y `openssl`, y reciben el entorno como primer argumento. Ninguno escribe valores de secretos en pantalla.

- [ ] **Step 1: `deploy/aws/.gitignore`**

```
# Llaves de la autoridad certificadora y de los servicios: nunca al repositorio.
ca/
certificados/
*.key
*.pem
```

- [ ] **Step 2: `comun.sh`**

```bash
#!/usr/bin/env bash
# Variables y funciones compartidas. Se carga con: source "$(dirname "$0")/comun.sh" "$ENTORNO"
set -euo pipefail
export MSYS_NO_PATHCONV=1

ENTORNO="${1:?uso: $0 <local|staging|prod> ...}"
case "$ENTORNO" in local|staging|prod) ;; *) echo "Entorno no valido: $ENTORNO" >&2; exit 1;; esac

AWS_REGION="${AWS_REGION:-us-east-1}"
CUENTA="$(aws sts get-caller-identity --query Account --output text)"
DIR_AWS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TABLA="$DIR_AWS/secretos-por-servicio.json"
PREFIJO="mlmconqueror/$ENTORNO"
ALIAS_KMS="alias/mlmconqueror-$ENTORNO"

# servicios: nombres de la tabla (AdminAPI, AdminWeb, ...)
servicios() { jq -r '.servicios | keys[]' "$TABLA"; }
# secretos_de <Servicio>
secretos_de() { jq -r --arg s "$1" '.servicios[$s].secretos[]' "$TABLA"; }
# aws_de <Servicio>: s3, ses
aws_de() { jq -r --arg s "$1" '.servicios[$s].aws[]' "$TABLA"; }
# rol_de <Servicio>: MLM-prod-billing
rol_de() { echo "MLM-$ENTORNO-$(echo "$1" | tr '[:upper:]' '[:lower:]')"; }
# arn_secreto <secreto>: ARN con comodin para el sufijo aleatorio de 6 caracteres que agrega AWS
arn_secreto() { echo "arn:aws:secretsmanager:$AWS_REGION:$CUENTA:secret:$PREFIJO/$1-??????"; }
arn_kms() { aws kms describe-key --key-id "$ALIAS_KMS" --query KeyMetadata.Arn --output text; }
```

- [ ] **Step 3: `crear-kms-y-secretos.sh`**

```bash
#!/usr/bin/env bash
# Crea la llave KMS del entorno y los secretos declarados en la tabla, vacios ({}).
# Uso: bash deploy/aws/crear-kms-y-secretos.sh <entorno>
source "$(dirname "$0")/comun.sh" "$1"

if ! aws kms describe-key --key-id "$ALIAS_KMS" >/dev/null 2>&1; then
  id=$(aws kms create-key --description "Secretos de mlmconqueror ($ENTORNO)" \
        --tags TagKey=proyecto,TagValue=mlmconqueror TagKey=entorno,TagValue="$ENTORNO" \
        --query KeyMetadata.KeyId --output text)
  aws kms create-alias --alias-name "$ALIAS_KMS" --target-key-id "$id"
  aws kms enable-key-rotation --key-id "$id"
  echo "Llave KMS $ALIAS_KMS creada"
fi

for s in $(jq -r '.secretos[]' "$TABLA"); do
  if aws secretsmanager describe-secret --secret-id "$PREFIJO/$s" >/dev/null 2>&1; then
    echo "ya existe: $PREFIJO/$s"
  else
    aws secretsmanager create-secret --name "$PREFIJO/$s" --kms-key-id "$ALIAS_KMS" \
      --secret-string '{}' --tags Key=proyecto,Value=mlmconqueror Key=entorno,Value="$ENTORNO" >/dev/null
    echo "creado: $PREFIJO/$s (vacio; cargalo con cargar-secreto.sh)"
  fi
done
```

- [ ] **Step 4: `cargar-secreto.sh`**

```bash
#!/usr/bin/env bash
# Pone el valor de un secreto desde un archivo JSON local de pares "Seccion__Clave": "texto".
# Uso: bash deploy/aws/cargar-secreto.sh <entorno> <secreto> <archivo.json>
# Despues de cargarlo, borra el archivo (shred -u) y reinicia los servicios que lo leen.
source "$(dirname "$0")/comun.sh" "$1"
SECRETO="${2:?falta el secreto}"; ARCHIVO="${3:?falta el archivo JSON}"

jq -r '.secretos[]' "$TABLA" | grep -qx "$SECRETO" || { echo "$SECRETO no esta en la tabla" >&2; exit 1; }
jq -e 'type == "object" and ([.[] | type] | all(. == "string"))' "$ARCHIVO" >/dev/null \
  || { echo "$ARCHIVO tiene que ser un objeto JSON con valores de texto" >&2; exit 1; }

aws secretsmanager put-secret-value --secret-id "$PREFIJO/$SECRETO" \
  --secret-string "file://$ARCHIVO" >/dev/null
echo "cargado: $PREFIJO/$SECRETO ($(jq 'length' "$ARCHIVO") claves). Lo leen:"
for s in $(servicios); do secretos_de "$s" | grep -qx "$SECRETO" && echo "  $s"; done
```

- [ ] **Step 5: `crear-roles.sh`**

```bash
#!/usr/bin/env bash
# Un rol por servicio con permiso solo sobre sus secretos (y S3/SES si los usa).
# Uso: bash deploy/aws/crear-roles.sh <entorno> eks  <cluster> <namespace>
#      bash deploy/aws/crear-roles.sh <entorno> docker
# En modo docker, roles-anywhere.sh tiene que haberse corrido antes (lee el ARN del trust anchor).
source "$(dirname "$0")/comun.sh" "$1"
MODO="${2:?falta el modo: eks o docker}"
KMS="$(arn_kms)"
BUCKET="${BUCKET:-}"; SES_FROM="${SES_FROM:-}"
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

confianza() {  # confianza <Servicio> <nombre-k8s>
  case "$MODO" in
    eks)
      local cluster="${3:?cluster}" ns="${4:?namespace}"
      local oidc; oidc=$(aws eks describe-cluster --name "$cluster" \
        --query 'cluster.identity.oidc.issuer' --output text | sed 's#https://##')
      jq -n --arg arn "arn:aws:iam::$CUENTA:oidc-provider/$oidc" --arg oidc "$oidc" \
            --arg sub "system:serviceaccount:$ns:$2" '{
        Version:"2012-10-17", Statement:[{Effect:"Allow", Principal:{Federated:$arn},
          Action:"sts:AssumeRoleWithWebIdentity",
          Condition:{StringEquals:{($oidc+":sub"):$sub, ($oidc+":aud"):"sts.amazonaws.com"}}}]}' ;;
    docker)
      local ancla; ancla=$(aws rolesanywhere list-trust-anchors \
        --query "trustAnchors[?name=='mlmconqueror-$ENTORNO'].trustAnchorArn | [0]" --output text)
      [ "$ancla" != "None" ] || { echo "Falta el trust anchor: corre roles-anywhere.sh $ENTORNO" >&2; exit 1; }
      jq -n --arg ancla "$ancla" --arg cn "$2" '{
        Version:"2012-10-17", Statement:[{Effect:"Allow", Principal:{Service:"rolesanywhere.amazonaws.com"},
          Action:["sts:AssumeRole","sts:TagSession","sts:SetSourceIdentity"],
          Condition:{StringEquals:{"aws:PrincipalTag/x509Subject/CN":$cn},
                     ArnEquals:{"aws:SourceArn":$ancla}}}]}' ;;
    *) echo "Modo no valido: $MODO" >&2; exit 1 ;;
  esac
}

for s in $(servicios); do
  rol=$(rol_de "$s"); nombre=$(echo "$s" | tr '[:upper:]' '[:lower:]')
  mapfile -t secretos < <(secretos_de "$s"); mapfile -t extra < <(aws_de "$s")

  confianza "$s" "$nombre" "${3:-}" "${4:-}" > "$tmp/confianza.json"
  if aws iam get-role --role-name "$rol" >/dev/null 2>&1; then
    aws iam update-assume-role-policy --role-name "$rol" --policy-document "file://$tmp/confianza.json"
  else
    aws iam create-role --role-name "$rol" --assume-role-policy-document "file://$tmp/confianza.json" \
      --tags Key=proyecto,Value=mlmconqueror Key=entorno,Value="$ENTORNO" >/dev/null
  fi

  arns=$(for x in "${secretos[@]}"; do arn_secreto "$x"; done | jq -R . | jq -s .)
  jq -n --argjson arns "$arns" --arg kms "$KMS" --arg bucket "$BUCKET" --arg from "$SES_FROM" \
        --argjson s3 "$(printf '%s\n' "${extra[@]}" | grep -qx s3 && echo true || echo false)" \
        --argjson ses "$(printf '%s\n' "${extra[@]}" | grep -qx ses && echo true || echo false)" '{
    Version:"2012-10-17", Statement:(
      (if ($arns|length) > 0 then [
        {Effect:"Allow", Action:"secretsmanager:GetSecretValue", Resource:$arns},
        {Effect:"Allow", Action:"kms:Decrypt", Resource:$kms,
         Condition:{StringEquals:{"kms:ViaService":"secretsmanager.'"$AWS_REGION"'.amazonaws.com"}}}] else [] end)
      + (if $s3 and $bucket != "" then [
        {Effect:"Allow", Action:["s3:GetObject","s3:PutObject","s3:DeleteObject"], Resource:("arn:aws:s3:::"+$bucket+"/*")},
        {Effect:"Allow", Action:"s3:ListBucket", Resource:("arn:aws:s3:::"+$bucket)}] else [] end)
      + (if $ses and $from != "" then [
        {Effect:"Allow", Action:["ses:SendEmail","ses:SendRawEmail"], Resource:"*",
         Condition:{StringEquals:{"ses:FromAddress":$from}}}] else [] end))}' > "$tmp/permisos.json"

  if [ "$(jq '.Statement | length' "$tmp/permisos.json")" -gt 0 ]; then
    aws iam put-role-policy --role-name "$rol" --policy-name secretos-y-aws \
      --policy-document "file://$tmp/permisos.json"
  else
    aws iam delete-role-policy --role-name "$rol" --policy-name secretos-y-aws 2>/dev/null || true
  fi
  echo "$rol: ${#secretos[@]} secreto(s); aws: ${extra[*]:-ninguno}"
done
```

Nota para quien implemente: el `kms:ViaService` restringe el descifrado a peticiones que pasan por Secrets Manager; si el bucket de S3 usa la misma llave KMS (como en la guía actual, sección 11.1), se agrega un segundo `Statement` de `kms:Decrypt`/`kms:GenerateDataKey` sin esa condición para los servicios con `s3`, con la llave del bucket como `Resource` (variable `KMS_S3`).

- [ ] **Step 6: `roles-anywhere.sh`**

```bash
#!/usr/bin/env bash
# Autoridad certificadora propia del entorno, trust anchor y perfil de IAM Roles Anywhere.
# La llave privada de la autoridad queda en deploy/aws/ca/<entorno>/ (ignorado por git) y NO
# se copia al servidor Docker: guardala fuera de esa maquina (por ejemplo, en un USB cifrado).
# Uso: bash deploy/aws/roles-anywhere.sh <entorno>
source "$(dirname "$0")/comun.sh" "$1"
CA="$DIR_AWS/ca/$ENTORNO"; mkdir -p "$CA"; chmod 700 "$CA"

if [ ! -f "$CA/ca.key" ]; then
  openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
    -keyout "$CA/ca.key" -out "$CA/ca.crt" -subj "/CN=mlmconqueror $ENTORNO CA" \
    -addext "basicConstraints=critical,CA:TRUE" -addext "keyUsage=critical,keyCertSign,cRLSign"
  chmod 600 "$CA/ca.key"
fi

ancla=$(aws rolesanywhere list-trust-anchors \
  --query "trustAnchors[?name=='mlmconqueror-$ENTORNO'].trustAnchorArn | [0]" --output text)
if [ "$ancla" = "None" ]; then
  ancla=$(aws rolesanywhere create-trust-anchor --name "mlmconqueror-$ENTORNO" --enabled \
    --source "sourceType=CERTIFICATE_BUNDLE,sourceData={x509CertificateData=$(cat "$CA/ca.crt")}" \
    --query trustAnchor.trustAnchorArn --output text)
fi

roles=$(for s in $(servicios); do echo "arn:aws:iam::$CUENTA:role/$(rol_de "$s")"; done | paste -sd' ')
perfil=$(aws rolesanywhere list-profiles \
  --query "profiles[?name=='mlmconqueror-$ENTORNO'].profileArn | [0]" --output text)
if [ "$perfil" = "None" ]; then
  perfil=$(aws rolesanywhere create-profile --name "mlmconqueror-$ENTORNO" --enabled \
    --duration-seconds 3600 --role-arns $roles --query profile.profileArn --output text)
else
  aws rolesanywhere update-profile --profile-id "${perfil##*/}" --role-arns $roles >/dev/null
fi

echo "TRUST_ANCHOR_ARN=$ancla"
echo "PROFILE_ARN=$perfil"
```

Orden en modo docker: `roles-anywhere.sh` crea el trust anchor; `crear-roles.sh <entorno> docker` crea los roles (necesita el trust anchor); se vuelve a correr `roles-anywhere.sh` para que el perfil incluya los roles recién creados. La guía de Docker lo dice en ese orden.

- [ ] **Step 7: `emitir-certificados.sh`**

```bash
#!/usr/bin/env bash
# Un certificado por servicio (CN = nombre del servicio en minusculas), valido un año, firmado
# por la autoridad del entorno. Deja en deploy/aws/certificados/<entorno>/<servicio>/ cert.pem y
# key.pem para copiar al servidor Docker.
# Uso: bash deploy/aws/emitir-certificados.sh <entorno> [servicio]
source "$(dirname "$0")/comun.sh" "$1"
CA="$DIR_AWS/ca/$ENTORNO"; [ -f "$CA/ca.key" ] || { echo "Falta la autoridad: roles-anywhere.sh" >&2; exit 1; }
SALIDA="$DIR_AWS/certificados/$ENTORNO"

for s in ${2:-$(servicios)}; do
  [ -n "$(secretos_de "$s")$(aws_de "$s")" ] || { echo "$s: no usa AWS, sin certificado"; continue; }
  n=$(echo "$s" | tr '[:upper:]' '[:lower:]'); d="$SALIDA/$n"; mkdir -p "$d"; chmod 700 "$d"
  openssl req -new -newkey rsa:2048 -nodes -keyout "$d/key.pem" -out "$d/csr.pem" -subj "/CN=$n" 2>/dev/null
  openssl x509 -req -in "$d/csr.pem" -CA "$CA/ca.crt" -CAkey "$CA/ca.key" -CAcreateserial \
    -days 365 -sha256 -out "$d/cert.pem" \
    -extfile <(printf "basicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature\n") 2>/dev/null
  rm -f "$d/csr.pem"; chmod 600 "$d/key.pem"
  echo "$n: $(openssl x509 -in "$d/cert.pem" -noout -enddate)"
done
```

- [ ] **Step 8: `politica-desarrollador-local.sh`**

```bash
#!/usr/bin/env bash
# Permiso para que un usuario o grupo de IAM (o un permission set de IAM Identity Center, pegando
# el JSON) lea todo mlmconqueror/local/* y nada de staging ni prod.
# Uso: bash deploy/aws/politica-desarrollador-local.sh local <grupo-iam>
source "$(dirname "$0")/comun.sh" "$1"
[ "$ENTORNO" = local ] || { echo "Solo para el entorno local" >&2; exit 1; }
GRUPO="${2:?falta el grupo de IAM}"
jq -n --arg arn "arn:aws:secretsmanager:$AWS_REGION:$CUENTA:secret:$PREFIJO/*" --arg kms "$(arn_kms)" '{
  Version:"2012-10-17", Statement:[
    {Effect:"Allow", Action:"secretsmanager:GetSecretValue", Resource:$arn},
    {Effect:"Allow", Action:"kms:Decrypt", Resource:$kms}]}' > /tmp/mlm-dev-local.json
aws iam put-group-policy --group-name "$GRUPO" --policy-name mlmconqueror-local-secretos \
  --policy-document file:///tmp/mlm-dev-local.json
rm -f /tmp/mlm-dev-local.json
echo "El grupo $GRUPO puede leer $PREFIJO/*"
```

- [ ] **Step 9: Revisar los scripts sin AWS**

Run: `for f in deploy/aws/*.sh; do bash -n "$f" && echo "ok $f"; done`
Expected: `ok` para los 7.

Run (si está instalado shellcheck): `shellcheck -x deploy/aws/*.sh`
Expected: sin errores (los avisos SC2086 de `$roles` sin comillas son intencionales).

Run: `jq -e . deploy/aws/secretos-por-servicio.json >/dev/null && echo ok`
Expected: `ok`.

- [ ] **Step 10: Commit**

```bash
git add deploy/aws/
git status --short deploy/aws   # comprobar que no hay ca/ ni certificados/
git commit -m "deploy/aws: scripts por entorno para la llave KMS y los secretos, cargar un secreto desde un JSON local, un rol por servicio con permiso solo sobre su fila de la tabla (confianza de EKS por IRSA o de Roles Anywhere por CN del certificado), autoridad certificadora y perfil de Roles Anywhere, emision de un certificado por servicio y permiso de desarrollador sobre local"
```

---

### Task 9: Secretos `local` en la cuenta de Nick y prueba real en la máquina

Esta tarea la corre Nick (o se corre con su autorización explícita), porque crea recursos en su cuenta de AWS.

- [ ] **Step 1: Crear la llave y los secretos `local`**

```bash
aws sso login            # o el perfil que use Nick
bash deploy/aws/crear-kms-y-secretos.sh local
```

Expected: `Llave KMS alias/mlmconqueror-local creada` y `creado: mlmconqueror/local/<x>` para los 9.

- [ ] **Step 2: Cargar los valores de desarrollo**

Para cada secreto se escribe un JSON temporal con los valores de la máquina de desarrollo (los mismos que hoy usa en `appsettings.Development.json`: base local, Redis local, la llave JWT de desarrollo, el PFX de desarrollo en base64...), se carga y se borra:

```bash
umask 077
cat > /tmp/datos.json <<'EOF'
{"ConnectionStrings__DefaultConnection":"Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=True",
 "ConnectionStrings__HangFire":"Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True",
 "ConnectionStrings__Redis":"localhost:6379"}
EOF
bash deploy/aws/cargar-secreto.sh local datos /tmp/datos.json && rm -f /tmp/datos.json
```

Igual para `jwt-firma`, `dp-gateway` (`DataProtection__Certificate__Base64` = `base64 -w0` del PFX de desarrollo), `signupapi`, `billing`, `sharedapicenter`, `portales`, `firebase` y `twilio`. Los que no tengan valor de desarrollo se cargan con las claves que hoy están vacías en `appsettings.json` y valor `""`.

Nunca se apunta un `local` a `MLMConquerorGlobalEditionDb_Escala` ni a la RDS del sistema anterior.

- [ ] **Step 3: Volver a encender `SecretosAws` en la máquina**

Quitar `"SecretosAws": { "Habilitado": false }` de los `appsettings.Development.json` (Task 7, Step 6).

- [ ] **Step 4: Arrancar un servicio y ver el log**

Run: `dotnet run --project MLMConquerorGlobalEdition.SignupAPI`
Expected: en la consola, antes del resto del arranque,
```
[SecretosAws] mlmconqueror/local/datos: 3 clave(s).
[SecretosAws] mlmconqueror/local/jwt-firma: 1 clave(s).
...
```
y el servicio sano en `/health`.

- [ ] **Step 5: Comprobar el fallo cerrado**

Run: `AWS_PROFILE=no-existe dotnet run --project MLMConquerorGlobalEdition.SignupAPI`
Expected: no arranca; mensaje `[SecretosAws] Este servicio no tiene identidad de AWS ...`.

---

### Task 10: Docker — imagen, compose local y compose del servidor

**Files:**
- Modify: `Dockerfile`
- Create: `deploy/docker/.env.servidor.ejemplo`
- Create: `deploy/docker/.gitignore`
- Modify: `deploy/docker/docker-compose.yml`
- Create: `deploy/docker/docker-compose.servidor.yml`
- Modify: `deploy/docker/generar-secretos.sh`

- [ ] **Step 1: `aws_signing_helper` en la imagen**

Versión 1.8.6, la publicada el 19-nov-2026 en `https://docs.aws.amazon.com/rolesanywhere/latest/userguide/credential-helper.html`, con su SHA-256 de esa misma página. En la etapa `final` del `Dockerfile`, después del bloque de `update-ca-certificates` y antes de `WORKDIR /app`:

```dockerfile
# IAM Roles Anywhere: en un servidor Docker fuera de AWS, cada servicio obtiene credenciales
# temporales de su propio rol presentando su certificado (credential_process en AWS_CONFIG_FILE).
# En EKS y en la maquina del desarrollador no se usa. Version y SHA-256 de la pagina oficial:
# https://docs.aws.amazon.com/rolesanywhere/latest/userguide/credential-helper.html
ARG ROLES_ANYWHERE_VERSION=1.8.6
ARG ROLES_ANYWHERE_SHA256=3553fdc515e5682541a9ebdd37031ec7f461169aa13ccbb6fbd5b7259644113d
ADD https://rolesanywhere.amazonaws.com/releases/${ROLES_ANYWHERE_VERSION}/X86_64/Linux/Amzn2023/aws_signing_helper /usr/local/bin/aws_signing_helper
RUN echo "${ROLES_ANYWHERE_SHA256}  /usr/local/bin/aws_signing_helper" | sha256sum -c - \
 && chmod 755 /usr/local/bin/aws_signing_helper \
 && /usr/local/bin/aws_signing_helper version
```

El binario está compilado para Amazon Linux 2023; la última línea comprueba que corre en la imagen base de .NET. Si falla con un error de bibliotecas (`GLIBC_...`), la compilación se detiene ahí y hay que avisar a Nick antes de seguir: no se cambia de binario sin decidirlo.

- [ ] **Step 2: Sin plantilla aparte**

El `AWS_CONFIG_FILE` de cada servicio se escribe dentro del compose del servidor con `configs:` y `content:` (Compose 2.23+), que sí sustituye variables, así que no hay archivo de plantilla. Este paso no crea nada.

- [ ] **Step 3: Compose local (`docker-compose.yml`)**

Cambios:
1. `x-comun` agrega `SecretosAws__Entorno: local` y `AWS_PROFILE: ${AWS_PROFILE:-default}`.
2. Un ancla nueva para montar el perfil del desarrollador:

```yaml
# El perfil de AWS del desarrollador, de solo lectura (aws sso login en la maquina). Los servicios
# leen mlmconqueror/local/* con el. En Windows, ~/.aws es %USERPROFILE%\.aws.
x-perfil-aws: &perfil-aws
  type: bind
  source: ${HOME:-${USERPROFILE}}/.aws
  target: /home/app/.aws
  read_only: true
```

y cada servicio con lista no vacía agrega `volumes: [*perfil-aws]` (los que ya tienen `volumes` agregan el elemento).
3. `x-datos` se queda: las cadenas a `sqlserver,1433` siguen como variables de entorno y **ganan** sobre `mlmconqueror/local/datos` (que apunta a `Server=.`), porque el SQL Server y el Redis del compose son los del propio compose.
4. `x-pasarelas` se borra: el PFX llega desde `mlmconqueror/local/dp-gateway`.
5. Se borran `Jwt__PrivateKeyBase64: ${JWT_PRIVATE}`, `RootAmbassador__Password`, `ExternalApi__ApiKey` y `Syncfusion__LicenseKey` de los servicios: llegan desde los secretos `local`.
6. La llave pública: `Jwt__PublicKeyBase64` sigue en `x-comun`, pero su valor ahora es la pública que corresponde a la privada de `mlmconqueror/local/jwt-firma` (variable `JWT_PUBLIC` del `.env`).
7. El comentario de cabecera explica el nuevo requisito: `aws sso login` antes de `up`.

- [ ] **Step 4: Compose del servidor (`docker-compose.servidor.yml`)**

Se arma a partir del `docker-compose.yml` local ya modificado en el Step 3, con estos cambios exactos:

1. Se borran los servicios `sqlserver` y `redis`, sus volúmenes y todos los `depends_on` sobre ellos. El servidor usa su propio SQL Server y Redis, que llegan en `mlmconqueror/{entorno}/datos`.
2. Se borra el ancla `x-datos` y su uso: las cadenas de conexión ya no van por variable de entorno.
3. Se borra el ancla `x-perfil-aws` y su uso en cada servicio.
4. `build:` desaparece; cada servicio usa `image: ${REGISTRO}/<servicio>:${VERSION}`.
5. `x-comun` pasa a:

```yaml
x-comun: &comun
  ASPNETCORE_ENVIRONMENT: Production
  TZ: America/New_York
  AllowedHosts: "*"
  SecretosAws__Entorno: ${ENTORNO:?falta ENTORNO en .env.servidor}
  SecretosAws__Region: ${AWS_REGION:-us-east-1}
  AWS_CONFIG_FILE: /run/aws/config
  ForwardedHeaders__KnownNetwork: ${RED_DEL_PROXY:?falta RED_DEL_PROXY}
  IpRateLimiting__RealIpHeader: ""
  Jwt__Issuer: MLMConquerorGlobalEdition
  Jwt__Audience: MLMConquerorGlobalEdition.Clients
  Jwt__PublicKeyBase64: ${JWT_PUBLIC:?falta JWT_PUBLIC}
```

6. Cada servicio que usa AWS (todos menos `signups`) agrega su configuración de AWS y su certificado. Ejemplo completo de `billing`:

```yaml
  billing:
    image: ${REGISTRO}/billing:${VERSION}
    environment:
      <<: *comun
      Billing__Recurring__CardEnabled: "false"
      Billing__TokenPurchase__CardEnabled: "false"
      ReceiptStorage__LocalPath: /data/payout-receipts
      ReceiptStorage__PublicBaseUrl: ${URL_RECIBOS}
    configs: [{ source: aws-billing, target: /run/aws/config }]
    volumes:
      - recibos:/data/payout-receipts
      - ./certificados/billing/cert.pem:/run/aws/cert.pem:ro
      - ./certificados/billing/key.pem:/run/aws/key.pem:ro
    ports: ["8107:8080"]
    healthcheck: *salud
    restart: unless-stopped
```

y su bloque en `configs:` al final del archivo:

```yaml
configs:
  aws-billing:
    content: |
      [default]
      region = ${AWS_REGION:-us-east-1}
      credential_process = /usr/local/bin/aws_signing_helper credential-process --certificate /run/aws/cert.pem --private-key /run/aws/key.pem --trust-anchor-arn ${TRUST_ANCHOR_ARN} --profile-arn ${PROFILE_ARN} --role-arn arn:aws:iam::${CUENTA}:role/MLM-${ENTORNO}-billing
```

7. Lo que lleva cada servicio (las variables no secretas son las mismas que tiene hoy en `docker-compose.yml`, con el cambio del punto 8):

| Servicio | Puerto | Volúmenes de datos | `configs` y certificado |
|---|---|---|---|
| adminapi | 8102 | `uploads:/app/wwwroot/uploads`, `recibos:/data/payout-receipts` | `aws-adminapi`, `certificados/adminapi` |
| adminweb | 8101 | `llaves-adminweb:/keys` | `aws-adminweb`, `certificados/adminweb` |
| billing | 8107 | `recibos:/data/payout-receipts` | `aws-billing`, `certificados/billing` |
| bizcenter | 8103 | `uploads:/app/wwwroot/uploads` | `aws-bizcenter`, `certificados/bizcenter` |
| bizcenterweb | 8104 | `llaves-bizcenterweb:/keys` | `aws-bizcenterweb`, `certificados/bizcenterweb` |
| commissionengine | 8108 | — | `aws-commissionengine`, `certificados/commissionengine` |
| rankengine | 8109 | `certificados:/app/wwwroot/certificates` | `aws-rankengine`, `certificados/rankengine` |
| sharedapicenter | 8111 | — | `aws-sharedapicenter`, `certificados/sharedapicenter` |
| signupapi | 8105 | `llaves-signupapi:/keys` | `aws-signupapi`, `certificados/signupapi` |
| signups | 8147 | `llaves-signups:/keys` | ninguno: no usa AWS |
| ticketmanagementsystem | 8110 | — | `aws-ticketmanagementsystem`, `certificados/ticketmanagementsystem` |

8. En las variables no secretas, las direcciones públicas `http://localhost:81xx` se cambian por las del servidor, tomadas de `.env.servidor` (`URL_ADMIN`, `URL_APP`, `URL_SIGNUP`, `URL_AUTH_API`, `URL_CERTIFICADOS`, `URL_RECIBOS`), y los `Cors__AllowedOrigins__N` repiten el origen público en todas las posiciones (regla de la guía de EKS, Fase 10). Las direcciones entre servicios (`http://signupapi:8080`, etc.) no cambian. `Syncfusion__LicenseKey`, `RootAmbassador__Password` y `ExternalApi__ApiKey` no aparecen: llegan de los secretos.
9. El encabezado del archivo:

```yaml
# Servidor Docker FUERA de AWS (staging o prod). Cada servicio lee sus secretos de
# mlmconqueror/${ENTORNO}/* con su propio rol, por IAM Roles Anywhere. Guia:
# docs/deployment/despliegue-docker.md
#
#   docker compose -f deploy/docker/docker-compose.servidor.yml --env-file deploy/docker/.env.servidor up -d
```

Archivo de ejemplo `deploy/docker/.env.servidor.ejemplo` (sí se sube; el real `.env.servidor` no):

```
ENTORNO=staging
AWS_REGION=us-east-1
CUENTA=123456789012
TRUST_ANCHOR_ARN=arn:aws:rolesanywhere:us-east-1:123456789012:trust-anchor/...
PROFILE_ARN=arn:aws:rolesanywhere:us-east-1:123456789012:profile/...
REGISTRO=123456789012.dkr.ecr.us-east-1.amazonaws.com/mlmconqueror
VERSION=1.0.0
RED_DEL_PROXY=172.16.0.0/12
# Llave publica JWT del entorno (base64 DER). No es secreta.
JWT_PUBLIC=
URL_ADMIN=https://admin.mlmconqueror.com
URL_APP=https://app.mlmconqueror.com
URL_SIGNUP=https://signup.mlmconqueror.com
URL_AUTH_API=https://auth-api.mlmconqueror.com
URL_CERTIFICADOS=https://certificados.mlmconqueror.com
URL_RECIBOS=https://recibos.mlmconqueror.com
```

y `deploy/docker/.gitignore` (nuevo) con `.env`, `.env.servidor` y `certificados/`.

- [ ] **Step 5: `generar-secretos.sh` queda para lo local y no secreto**

El script deja de generar `JWT_PRIVATE`, `PFX_PASSWORD`, `ROOT_PASSWORD`, `EXTERNAL_API_KEY`, `SYNCFUSION_LICENSE_KEY` y el PFX. Genera solo `SA_PASSWORD` (el SQL Server desechable del compose) y escribe `JWT_PUBLIC` leyendo la pública de `mlmconqueror/local/jwt-firma`:

```bash
priv=$(aws secretsmanager get-secret-value --secret-id mlmconqueror/local/jwt-firma \
        --query SecretString --output text | jq -r '.Jwt__PrivateKeyBase64')
pub=$(printf '%s' "$priv" | base64 -d | openssl pkey -inform DER -pubout -outform DER | base64 -w0)
unset priv
```

El encabezado del script explica que necesita `aws sso login` y los secretos `local` (Task 9).

- [ ] **Step 6: Probar el compose local**

```bash
docker compose -f deploy/docker/docker-compose.yml config --quiet && echo ok
docker compose -f deploy/docker/docker-compose.servidor.yml --env-file deploy/docker/.env.servidor.ejemplo config --quiet && echo ok
bash deploy/docker/generar-secretos.sh   # tras borrar el .env anterior
docker compose -f deploy/docker/docker-compose.yml build signupapi
docker compose -f deploy/docker/docker-compose.yml up -d
docker compose -f deploy/docker/docker-compose.yml ps
docker compose -f deploy/docker/docker-compose.yml logs signupapi | grep SecretosAws
```

Expected: los dos `ok`; los 11 servicios `healthy`; en el log de signupapi las líneas `[SecretosAws] mlmconqueror/local/...`; login de la cuenta raíz con la contraseña de `mlmconqueror/local/signupapi` y token aceptado por BizCenter (401 sin token, 200 con token), como en la prueba del 19-nov.

- [ ] **Step 7: Probar Roles Anywhere en la máquina (sin servidor)**

Con un entorno `staging` de prueba creado por Nick (Task 8, scripts en orden: `crear-kms-y-secretos.sh staging`, `roles-anywhere.sh staging`, `crear-roles.sh staging docker`, `roles-anywhere.sh staging`, `emitir-certificados.sh staging billing`):

```bash
docker run --rm -v "$PWD/deploy/aws/certificados/staging/billing:/run/aws:ro" \
  --entrypoint /usr/local/bin/aws_signing_helper mlmconqueror/billing:local \
  credential-process --certificate /run/aws/cert.pem --private-key /run/aws/key.pem \
  --trust-anchor-arn "$TRUST_ANCHOR_ARN" --profile-arn "$PROFILE_ARN" \
  --role-arn "arn:aws:iam::$CUENTA:role/MLM-staging-billing" | jq -r .Expiration
```

Expected: una fecha una hora en el futuro. Y con el rol de otro servicio (`MLM-staging-signupapi`) el mismo certificado de billing **falla** (`AccessDeniedException`): la confianza exige el CN.

- [ ] **Step 8: Commit**

```bash
git add Dockerfile deploy/docker/docker-compose.yml deploy/docker/docker-compose.servidor.yml deploy/docker/.env.servidor.ejemplo deploy/docker/.gitignore deploy/docker/generar-secretos.sh
git commit -m "Docker: la imagen trae aws_signing_helper de Roles Anywhere con su sha256; compose local lee mlmconqueror/local con el perfil del desarrollador montado de solo lectura (SQL y Redis del compose siguen por variable de entorno y ganan); compose nuevo para el servidor fuera de AWS con certificado y credential_process por servicio; generar-secretos.sh solo genera la contrasena del SQL local y toma la publica JWT de local"
```

---

### Task 11: Guía de EKS actualizada

**Files:**
- Modify: `docs/deployment/despliegue-aws-eks.md`, `docs/deployment/despliegue-aws-eks.pdf`

- [ ] **Step 1: Cambios en el texto**

1. **Sección 1 (decisiones):** "Secretos: cada servicio los lee de Secrets Manager al arrancar, solo los de su lista, con un rol propio (IRSA). Sin External Secrets." Entornos `staging` y `prod` con la variable `ENTORNO` al principio de la guía (`export ENTORNO=prod`).
2. **Fase 2 (6.2 External Secrets Operator):** se borra entera; la numeración de las subsecciones siguientes se corrige.
3. **Fase 6 (8.x Secretos):**
   - 8.1 pasa a ser la tabla de la sección 3 del diseño (sin la columna de Signups marcada), con `mlmconqueror/${ENTORNO}/...`, y la referencia a `deploy/aws/secretos-por-servicio.json`.
   - La creación (8.6) pasa a `bash deploy/aws/crear-kms-y-secretos.sh $ENTORNO` y un `cargar-secreto.sh` por secreto, con los JSON armados como hoy (las claves no cambian salvo: `pfxBase64` → `DataProtection__Certificate__Base64`; `firebaseJson` → `Firebase__CredentialsJsonBase64`; Stripe va en el secreto nuevo `billing`).
   - 8.8 (permiso de External Secrets) se reemplaza por `bash deploy/aws/crear-roles.sh $ENTORNO eks $CLUSTER $NS`, con `BUCKET` y `SES_FROM` exportados.
4. **Fase 9:**
   - 11.1 se reemplaza: ya no hay `mlm-aws` ni `mlm-sin-aws`. Una cuenta de servicio por Deployment, anotada con su rol:
     ```
     for s in $(jq -r '.servicios | keys[]' deploy/aws/secretos-por-servicio.json); do
       n=$(echo "$s" | tr '[:upper:]' '[:lower:]')
       kubectl create serviceaccount "$n" -n $NS --dry-run=client -o yaml | kubectl apply -f -
       kubectl annotate serviceaccount "$n" -n $NS --overwrite \
         eks.amazonaws.com/role-arn=arn:aws:iam::$CUENTA:role/MLM-$ENTORNO-$n
     done
     ```
   - 11.3 (Secretos de Kubernetes desde Secrets Manager) se borra entera, con sus `ExternalSecret` y el volumen del PFX.
5. **Fase 10 (ConfigMaps):** el ConfigMap común agrega `SecretosAws__Entorno: ${ENTORNO}` y `SecretosAws__Region: ${AWS_REGION}`; se quitan `DataProtection__Certificate__Path` y cualquier referencia a `/secrets/dp`.
6. **Fase 11 (Deployments, generador `gen()`):** `serviceAccountName` pasa a ser el nombre del servicio; se quitan `envFrom: secretRef` y los volúmenes de secretos.
7. **15.4 Cambiar un secreto:** `cargar-secreto.sh` y `kubectl rollout restart deployment/<servicio>` de los servicios que lista el script.
8. **Checklist final:** quitar External Secrets; agregar "cada pod arranca con las líneas `[SecretosAws]` y sin `AccessDenied`" y "un pod de un servicio no puede leer un secreto que no está en su fila" (prueba con `kubectl exec` y `aws secretsmanager get-secret-value` desde la imagen de `amazon/aws-cli` usando la cuenta de servicio de billing contra `jwt-firma`: tiene que dar `AccessDeniedException`).

- [ ] **Step 2: Comprobar que no quedan restos**

Run: `grep -n "ExternalSecret\|external-secrets\|eso-aws\|mlm-aws\|mlm-sin-aws\|pfxBase64\|firebaseJson\|secretRef" docs/deployment/despliegue-aws-eks.md`
Expected: ninguna línea.

- [ ] **Step 3: PDF y commit**

```bash
python -I docs/build_pdf.py docs/deployment/despliegue-aws-eks.md docs/deployment/despliegue-aws-eks.pdf "Despliegue en AWS EKS" 7.4
git add docs/deployment/despliegue-aws-eks.md docs/deployment/despliegue-aws-eks.pdf
git commit -m "Guia de EKS: secretos por entorno desde la tabla unica, un rol por servicio con IRSA y una cuenta de servicio por Deployment; se quita External Secrets. PDF regenerado"
```

---

### Task 12: Guía nueva de Docker fuera de AWS

**Files:**
- Create: `docs/deployment/despliegue-docker.md`, `docs/deployment/despliegue-docker.pdf`

- [ ] **Step 1: Escribir la guía**

Mismo estilo que la de EKS (fases numeradas, comandos completos, nada se salta). Secciones:

1. **Qué se despliega y qué se asume.** Servidor Linux x86_64 fuera de AWS con Docker Engine 24+ y Compose 2.23+; SQL Server y Redis propios (fuera del compose); un proxy inverso con TLS delante (dirección de su red en `RED_DEL_PROXY`); acceso de salida HTTPS a `secretsmanager.<region>.amazonaws.com`, `rolesanywhere.<region>.amazonaws.com` y `sts.amazonaws.com`; reloj del servidor sincronizado por NTP (Roles Anywhere rechaza firmas con más de 5 minutos de desfase).
2. **Fase 1 — Herramientas en la máquina de administración** (no el servidor): AWS CLI v2, jq, openssl, acceso a la cuenta.
3. **Fase 2 — Llave KMS y secretos del entorno:** `crear-kms-y-secretos.sh` y `cargar-secreto.sh` para cada uno de los 9, con el contenido de cada JSON.
4. **Fase 3 — Autoridad certificadora y Roles Anywhere:** los tres comandos en orden (`roles-anywhere.sh`, `crear-roles.sh <entorno> docker`, `roles-anywhere.sh`). Dónde guardar `ca.key` (fuera del servidor) y por qué.
5. **Fase 4 — Certificados de los servicios:** `emitir-certificados.sh`, copia al servidor con `scp` a `/opt/mlmconqueror/deploy/docker/certificados/<servicio>/`, permisos `600` y dueño uid 1654 (el usuario de la imagen).
6. **Fase 5 — Imágenes:** construir y subir a un registro (ECR u otro), o `docker save`/`docker load`.
7. **Fase 6 — Base de datos:** migraciones con el mismo `DeploymentScripts/migrate-rds.sql` de la guía de EKS, contra el SQL Server del servidor.
8. **Fase 7 — `.env.servidor` y arranque:** copiar `.env.servidor.ejemplo`, llenar los valores, `docker compose ... up -d`, comprobar `ps` y las líneas `[SecretosAws]`.
9. **Fase 8 — Comprobaciones:** salud de los 11, login, token aceptado, y la prueba de aislamiento (el certificado de billing no puede asumir el rol de signupapi).
10. **Operación:** cambiar un secreto (`cargar-secreto.sh` + `docker compose restart <servicios que lista el script>`); renovar certificados antes de que venzan (`emitir-certificados.sh`, copiar, reiniciar; recordatorio a los 11 meses); revocar un certificado robado (generar CRL con `openssl ca -gencrl`, `aws rolesanywhere import-crl`, y emitir uno nuevo para ese servicio); qué hacer si AWS no responde (los servicios que ya están corriendo siguen; uno que reinicie no arranca hasta que vuelva).
11. **Tu máquina (`local`):** permiso de desarrollador (`politica-desarrollador-local.sh`), `aws sso login`, `dotnet run`, el compose local, y cómo trabajar sin conexión (`SecretosAws:Habilitado=false` en `appsettings.Development.json`).
12. **Checklist final.**

- [ ] **Step 2: PDF y commit**

```bash
python -I docs/build_pdf.py docs/deployment/despliegue-docker.md docs/deployment/despliegue-docker.pdf "Despliegue en Docker fuera de AWS" 7.4
git add -f docs/deployment/despliegue-docker.pdf
git add docs/deployment/despliegue-docker.md
git commit -m "Guia de despliegue en Docker fuera de AWS: secretos por entorno, autoridad certificadora y Roles Anywhere, un certificado y un rol por servicio, compose del servidor, operacion (cambio de secretos, renovacion y revocacion de certificados) y la maquina del desarrollador con el entorno local. PDF"
```

---

### Task 13: Verificación final

- [ ] **Step 1: Las 11 suites**

Run:
```bash
for t in AdminAPI Authn Billing BizCenter CommissionEngine Domain Notifications RankEngine Signups TicketManagementSystem; do
  printf "%s: " $t; dotnet test MLMConquerorGlobalEdition.$t.Tests -nologo -v q 2>&1 | grep -E "Passed!|Failed!" ; done
```
Expected: todas `Passed!`.

- [ ] **Step 2: Nada secreto en el repositorio**

Run: `git grep -nE "BEGIN (RSA |EC )?PRIVATE KEY|sk_live_|AKIA[0-9A-Z]{16}" -- . ':!docs'`
Expected: ninguna línea.

Run: `git ls-files deploy | grep -E "\.env$|\.env\.servidor$|/ca/|/certificados/|\.pem$|\.key$"`
Expected: ninguna línea.

- [ ] **Step 3: Memoria y aviso**

Actualizar `despliegue-compilaciones.md` en la memoria con el estado (rama `secretos-aws`, qué falta correr en AWS) y preguntar a Nick antes de pasar a master o subir.
