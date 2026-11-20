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
