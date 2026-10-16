namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>
/// Cómo leer el resultado de un cobro con tarjeta para decidir si se puede soltar la reserva.
/// </summary>
/// <remarks>
/// GatewayChargeOrchestrator devuelve un fallo con el código del gateway y no distingue un rechazo del
/// emisor de un resultado desconocido (timeout, 5xx, respuesta ilegible). Liberar el cupo ante un
/// fallo cualquiera podía dejar un cobro hecho sin tokens y sin nadie que lo mire. Por eso la regla es
/// al revés: un fallo es DUDOSO (a conciliar) salvo que su código esté en esta lista.
/// </remarks>
public static class CardChargeOutcomes
{
    /// <summary>
    /// Códigos que prueban que NO se movió dinero. Hoy son solo comprobaciones previas: el gateway no
    /// llegó a llamarse.
    ///   • GATEWAY_NOT_REGISTERED — GatewayChargeOrchestrator: no hay servicio para ese procesador.
    ///   • SPREEDLY_* — SpreedlyCardGatewayService: falta el token del miembro, la credencial de Spreedly
    ///     o el token del gateway de destino; corta antes de cualquier llamada.
    ///   • CREDENTIAL_* — StubGatewayBase: misma comprobación previa en los gateways de prueba.
    /// No hay todavía rechazos reales del emisor porque el cobro con Spreedly es un stub. Cuando sea
    /// real, sus rechazos explícitos (respuesta del procesador con succeeded = false) se añaden aquí;
    /// un timeout o un 5xx nunca.
    /// </summary>
    public static readonly IReadOnlySet<string> ExplicitNoChargeCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "GATEWAY_NOT_REGISTERED",
        "SPREEDLY_MEMBER_TOKEN_MISSING",
        "SPREEDLY_CREDENTIAL_MISSING",
        "SPREEDLY_CREDENTIAL_INCOMPLETE",
        "SPREEDLY_DOWNSTREAM_TOKEN_MISSING",
        "SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
        "CREDENTIAL_NOT_FOUND",
        "CREDENTIAL_INACTIVE",
        "CREDENTIAL_INCOMPLETE"
    };

    public static bool IsExplicitNoCharge(string? errorCode) =>
        errorCode is not null && ExplicitNoChargeCodes.Contains(errorCode);

    /// <summary>
    /// ¿El "éxito" viene de un stub? SpreedlyCardGatewayService devuelve ids "simulated-…" y
    /// StubGatewayBase "STUB-…": no hubo cobro y no se puede emitir aunque la tarjeta esté encendida por
    /// error.
    /// </summary>
    public static bool IsSimulated(string? gatewayTransactionId) =>
        gatewayTransactionId is not null
        && (gatewayTransactionId.StartsWith("simulated", StringComparison.OrdinalIgnoreCase)
            || gatewayTransactionId.StartsWith("STUB-", StringComparison.OrdinalIgnoreCase));
}
