namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// CardAuthorization = new/updated cards + admin card updates/charges.
/// Payment           = join page, token orders, orders, internal orders, recurring.
/// </summary>
public enum BillingOperationType
{
    CardAuthorization = 1,
    Payment           = 2
}
