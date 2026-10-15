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
