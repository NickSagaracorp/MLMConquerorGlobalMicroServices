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
