using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Plan M (revisión M-d): cómo llegan al cliente las dos formas en que el árbol de inscripción frena un
/// alta. Las excepciones las lanzan GenealogyChildrenGate y GenealogyNodes; el alta las traduce aquí a un
/// Result con código, y el controlador a 409 (reintentar) o 503 (no se pudo completar), en vez del
/// INTERNAL_ERROR genérico de ErrorHandlingBehavior.
/// </summary>
public static class SignupGenealogyErrors
{
    /// <summary>Otra alta bajo el mismo patrocinador tiene el candado más de lo esperado: 409, reintentar.</summary>
    public const string SponsorBusy = "SIGNUP_SPONSOR_BUSY";

    /// <summary>El patrocinador aún no tiene Node (relleno pendiente): 503, el alta no se pudo completar.</summary>
    public const string SponsorNotReady = "SIGNUP_SPONSOR_NOT_READY";

    /// <summary>El código y el mensaje para el cliente, o null si la excepción no es del árbol.</summary>
    public static (string Code, string Message)? From(Exception ex) => ex switch
    {
        GenealogyChildrenBusyException  => (SponsorBusy, "Another signup under this sponsor is in progress. Please try again in a moment."),
        GenealogyNodeMissingException   => (SponsorNotReady, "The signup could not be completed right now. Please try again later or contact support."),
        _                               => null
    };
}
