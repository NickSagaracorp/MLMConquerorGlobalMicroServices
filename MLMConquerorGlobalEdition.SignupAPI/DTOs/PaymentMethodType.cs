namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public enum PaymentMethodType
{
    CreditCard = 1,
    Crypto = 2,
    Token = 3,
    DiscountCode = 4,

    /// <summary>Rublos por SBP con Entrax (QR). Solo si Entrax:Enabled y el país está en Entrax:Countries.</summary>
    Entrax = 5
}
