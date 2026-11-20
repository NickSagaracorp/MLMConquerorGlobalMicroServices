using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

/// <summary>
/// Lo que necesita un servicio para correr detrás del balanceador (ALB) y con varias réplicas.
/// Lo usan los once hosts.
/// </summary>
public static class ProxyHosting
{
    /// <summary>Red desde la que llegan los saltos del balanceador (el CIDR de la VPC).</summary>
    public const string KnownNetworkKey = "ForwardedHeaders:KnownNetwork";

    /// <summary>Carpeta compartida (EFS) donde se guardan las llaves de Data Protection.</summary>
    public const string KeysPathKey = "DataProtection:KeysPath";

    /// <summary>
    /// Toma la IP y el esquema del cliente de <c>X-Forwarded-For</c> y <c>X-Forwarded-Proto</c>,
    /// pero solo cuando la conexión viene de la red configurada en <see cref="KnownNetworkKey"/>.
    /// Sin esa clave solo se confía en el propio equipo (loopback), como en desarrollo: una
    /// cabecera inventada por un cliente no cambia nada. Se usa un único salto (el del ALB),
    /// así que la IP que queda es la que el balanceador añadió, no la que mandó el cliente.
    /// </summary>
    public static IServiceCollection AddProxyForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        var cidr = configuration[KnownNetworkKey];

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();

            if (!string.IsNullOrWhiteSpace(cidr))
                o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr.Trim()));
            else
                o.KnownProxies.Add(IPAddress.Loopback);
        });

        return services;
    }

    /// <summary>
    /// Guarda el anillo de llaves de Data Protection en <see cref="KeysPathKey"/> con un nombre de
    /// aplicación fijo, para que las cookies, los tokens antifalsificación y lo cifrado sigan
    /// valiendo tras un reinicio y entre réplicas. Sin la clave (desarrollo) no cambia nada.
    /// No usar la tabla DataProtectionKeys: es del anillo de credenciales de las pasarelas.
    /// <paramref name="applicationName"/> no se cambia nunca: cambiarlo invalida lo protegido.
    /// </summary>
    public static IServiceCollection AddPersistentDataProtectionKeys(
        this IServiceCollection services, IConfiguration configuration, string applicationName)
    {
        var keysPath = configuration[KeysPathKey];
        if (string.IsNullOrWhiteSpace(keysPath)) return services;

        services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        return services;
    }
}
