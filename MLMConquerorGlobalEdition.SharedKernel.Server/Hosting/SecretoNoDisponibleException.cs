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

    // FormatoInvalido nunca agrega el detalle de la causa: un error de JSON puede citar parte del valor.
    private static string Mensaje(string secreto, MotivoSecretoNoDisponible motivo, Exception? causa) => motivo switch
    {
        MotivoSecretoNoDisponible.SinPermiso =>
            $"[SecretosAws] La identidad de este servicio no tiene permiso sobre '{secreto}' (o sobre la llave KMS del entorno).",
        MotivoSecretoNoDisponible.NoExiste =>
            $"[SecretosAws] El secreto '{secreto}' no existe en ese entorno.",
        MotivoSecretoNoDisponible.SinCredencial =>
            "[SecretosAws] Este servicio no tiene identidad de AWS: en EKS falta su cuenta de servicio con IRSA; " +
            "en Docker falta el certificado de Roles Anywhere o AWS_CONFIG_FILE; en una máquina de desarrollo, el perfil de AWS." +
            (causa is null ? string.Empty : $" Detalle: {causa.Message}"),
        MotivoSecretoNoDisponible.FormatoInvalido =>
            $"[SecretosAws] '{secreto}' no es un JSON de pares clave-texto.",
        _ =>
            $"[SecretosAws] No se pudo leer '{secreto}'." + (causa is null ? string.Empty : $" Detalle: {causa.Message}")
    };
}
