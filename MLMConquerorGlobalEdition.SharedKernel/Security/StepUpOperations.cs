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
