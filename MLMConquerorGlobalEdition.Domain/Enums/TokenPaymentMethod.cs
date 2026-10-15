namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// Con qué se paga una compra de tokens. Los nombres coinciden con
/// <c>SharedKernel.Billing.TokenPaymentMethods</c>, que es lo que viaja por HTTP. Sin SafetyPay.
/// </summary>
public enum TokenPaymentMethod
{
    CommissionBalance = 1,
    EWallet           = 2,
    CreditCard        = 3,
    Crypto            = 4
}
