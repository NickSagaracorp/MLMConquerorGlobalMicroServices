using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Services;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout;

/// <summary>
/// Configura el cifrado de las credenciales de gateway. Lo llaman AdminAPI (que las cifra)
/// y Billing (que las descifra), y ambos DEBEN quedar configurados igual.
///
/// Esquema en dos capas:
///
///   1. Las llaves del key ring viven en la tabla DataProtectionKeys de la MISMA base que
///      ambos servicios ya comparten. Sin volúmenes compartidos, sin EFS, y entran en el
///      backup de RDS que ya existe.
///
///   2. Esas llaves están ENVUELTAS con un certificado X.509. La base guarda las llaves
///      cifradas; sin la clave privada del certificado son inservibles. Así un backup de la
///      base por sí solo no alcanza para descifrar credenciales — los dos factores están
///      deliberadamente separados.
///
/// Documentación para IT: artículo de KB "gateway-credential-encryption".
/// </summary>
public static class GatewayCredentialProtectionExtensions
{
    public const string CertificatePathKey       = "DataProtection:Certificate:Path";
    public const string CertificatePasswordKey   = "DataProtection:Certificate:Password";
    public const string CertificateThumbprintKey = "DataProtection:Certificate:Thumbprint";

    public static IServiceCollection AddGatewayCredentialProtection(this IServiceCollection services)
    {
        services.AddDataProtection()
            // Fija la derivación de llaves. Sin esto cada host usa su content root como
            // discriminador y AdminAPI/Billing terminan con llaves distintas.
            .SetApplicationName(GatewayCredentialProtector.ApplicationName)
            .PersistKeysToDbContext<AppDbContext>()
            .ProtectKeysWithCertificateFromConfiguration(services);

        services.TryAddScoped<IEncryptionService, GatewayCredentialProtector>();
        return services;
    }

    private static IDataProtectionBuilder ProtectKeysWithCertificateFromConfiguration(
        this IDataProtectionBuilder builder, IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        var config   = provider.GetRequiredService<IConfiguration>();
        var env      = provider.GetService<IHostEnvironment>();

        var certificate = ResolveCertificate(config, env);

        // ProtectKeysWithCertificate cifra las llaves NUEVAS.
        // UnprotectKeysWithAnyCertificate permite seguir leyendo las viejas durante una
        // rotación: se agrega el certificado nuevo, se deja el anterior en la lista de
        // desenvoltura, y recién cuando todas las llaves rotaron se puede retirar el viejo.
        return builder
            .ProtectKeysWithCertificate(certificate)
            .UnprotectKeysWithAnyCertificate(ResolveRetiredCertificates(config).Append(certificate).ToArray());
    }

    private static X509Certificate2 ResolveCertificate(IConfiguration config, IHostEnvironment? env)
    {
        // Opción A: certificado del almacén de Windows, por thumbprint. Es la preferida en
        // producción porque la clave privada nunca toca el disco de la aplicación.
        var thumbprint = config[CertificateThumbprintKey];
        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            var found = FindByThumbprint(thumbprint!);
            if (found is null)
                throw new InvalidOperationException(
                    $"No certificate with thumbprint '{thumbprint}' was found in the LocalMachine or " +
                    "CurrentUser store, or it has no private key. The key ring cannot be unwrapped " +
                    "without it, so the service refuses to start rather than fail on the first payout.");
            return found;
        }

        // Opción B: archivo PFX.
        var path = config[CertificatePathKey];
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                $"Gateway credential encryption needs a certificate. Set '{CertificateThumbprintKey}' " +
                $"(Windows store) or '{CertificatePathKey}' (PFX file). Every host that touches gateway " +
                "credentials — AdminAPI, Billing and BizCenter — must use the SAME certificate. " +
                "See the internal KB article 'gateway-credential-encryption'.");

        var resolved = Environment.ExpandEnvironmentVariables(path!);
        var password = config[CertificatePasswordKey];

        if (!File.Exists(resolved))
        {
            // En desarrollo se genera uno solo para no frenar a nadie. En cualquier otro
            // entorno la ausencia del certificado es un error: generar uno al vuelo en
            // producción crearía un certificado nuevo por host y por despliegue, y cada uno
            // dejaría ilegible lo que cifró el anterior.
            if (env is not null && env.IsDevelopment())
                CreateDevelopmentCertificate(resolved, password);
            else
                throw new InvalidOperationException(
                    $"The key-ring certificate was not found at '{resolved}'. It is never generated " +
                    "outside Development: a fresh certificate would be unable to unwrap the existing " +
                    "key ring, making every stored credential unreadable. Restore it from your secret " +
                    "store and restart.");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            resolved,
            password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    /// <summary>
    /// Certificados retirados que aún deben poder DESENVOLVER llaves viejas durante una
    /// rotación. Se listan como DataProtection:Certificate:Retired:0:Path (+ :Password).
    /// </summary>
    private static IEnumerable<X509Certificate2> ResolveRetiredCertificates(IConfiguration config)
    {
        foreach (var section in config.GetSection("DataProtection:Certificate:Retired").GetChildren())
        {
            var thumbprint = section["Thumbprint"];
            if (!string.IsNullOrWhiteSpace(thumbprint))
            {
                var fromStore = FindByThumbprint(thumbprint!);
                if (fromStore is not null) yield return fromStore;
                continue;
            }

            var path = section["Path"];
            if (string.IsNullOrWhiteSpace(path)) continue;

            var resolved = Environment.ExpandEnvironmentVariables(path!);
            if (!File.Exists(resolved)) continue;

            yield return X509CertificateLoader.LoadPkcs12FromFile(
                resolved, section["Password"],
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }
    }

    private static X509Certificate2? FindByThumbprint(string thumbprint)
    {
        var clean = thumbprint.Replace(" ", string.Empty).Replace("‎", string.Empty).Trim();

        foreach (var location in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);

            var match = store.Certificates
                .Find(X509FindType.FindByThumbprint, clean, validOnly: false)
                .FirstOrDefault(c => c.HasPrivateKey);

            if (match is not null) return match;
        }

        return null;
    }

    /// <summary>
    /// Certificado autofirmado SÓLO para desarrollo, para que un dev recién clonado pueda
    /// levantar sin trámites. Nunca se ejecuta fuera de Development.
    /// </summary>
    private static void CreateDevelopmentCertificate(string path, string? password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=MLMConqueror Gateway Credentials (development only)",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, critical: true));

        // 10 años: si vence, el key ring deja de poder desenvolverse. Renovarlo en dev es
        // trivial (se borra el archivo y se regenera), en producción no.
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, password));
    }
}
