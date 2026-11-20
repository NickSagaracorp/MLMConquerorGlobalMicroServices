namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

/// <summary>Lee el texto de un secreto por su nombre completo (mlmconqueror/{entorno}/{secreto}).</summary>
public interface ILectorDeSecretos
{
    /// <exception cref="SecretoNoDisponibleException">Siempre que no se pueda leer.</exception>
    string Leer(string nombreCompleto);
}
