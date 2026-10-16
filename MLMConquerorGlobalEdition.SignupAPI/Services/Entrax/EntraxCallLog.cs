using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

/// <summary>Filas de EntraxApiCalls. Solo campos estructurados: nunca cuerpos ni datos del formulario.</summary>
public static class EntraxCallLog
{
    public const string CreateQr   = "CreateQr";
    public const string GetPayment = "GetPayment";
    public const string Callback   = "Callback";

    public static EntraxApiCall From(
        string operation, int httpStatus, string? errorCode, long elapsedMs,
        string? externalOrderId, string? externalPaymentId, string? providerStatus, DateTime now, string actor)
        => new()
        {
            Operation         = operation,
            HttpStatusCode    = httpStatus,
            Success           = errorCode is null,
            ErrorCode         = Cut(errorCode, 60),
            ExternalOrderId   = Cut(externalOrderId, 100),
            ExternalPaymentId = Cut(externalPaymentId, 64),
            ProviderStatus    = Cut(providerStatus, 30),
            DurationMs        = (int)Math.Clamp(elapsedMs, 0, int.MaxValue),
            CreationDate      = now,
            CreatedBy         = Cut(actor, 100) ?? EntraxGateway.Name
        };

    private static string? Cut(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}
