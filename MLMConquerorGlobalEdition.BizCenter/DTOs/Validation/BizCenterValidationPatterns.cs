namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Validation;

/// <summary>
/// Centralised regex / length limits for BizCenter request DTO validators.
/// Defense-in-depth applied at the model-binding boundary BEFORE handlers run.
/// </summary>
public static class BizCenterValidationPatterns
{
    public const int EmailMaxLength = 254;
    public const string EmailPattern = @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$";

    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,128}$";

    public const string SafeTextPattern = @"^[^<>""'\\;\x00-\x1F\x7F]*$";

    public const int StateMaxLength    = 100;
    public const int CityMaxLength     = 100;
    public const int AddressMaxLength  = 250;
    public const int ZipCodeMaxLength  = 20;

    public const string PhonePattern = @"^\+?[0-9 \-\(\)]{7,20}$";

    public const string CountryIsoPattern = @"^[A-Z]{2}$";

    // Language tag (BCP 47, simple two-letter / two-letter-region)
    public const string LanguageTagPattern = @"^[a-z]{2}(?:-[A-Z]{2})?$";

    public const string PayoutFrequencyPattern = @"^(?:Daily|Weekly)$";

    public const string MemberIdPattern = @"^(?:(?:AMB|MBR)-?\d{3,9}|ROOT\d{3,9})$";

    public const string ReplicateSlugPattern =
        @"^[a-z0-9](?:[a-z0-9\-]{0,48}[a-z0-9])?$";
    public const int ReplicateSlugMaxLength = 50;

    public const string PlacementSidePattern = @"^(?:Left|Right)$";

    // Cardholder name — Unicode letters + space, hyphen, apostrophe, period
    public const string CardholderNamePattern = @"^[\p{L}][\p{L} '\-\.]{0,99}$";
    public const int CardholderNameMaxLength = 100;
    public const string CreditCardPanPattern = @"^\d{13,19}$";
    public const string CreditCardCvvPattern = @"^\d{3,4}$";

    public const string ImageContentTypePattern = @"^image\/(?:png|jpe?g|webp|gif)$";
    public const string Base64Pattern =
        @"^(?:data:image\/[a-zA-Z]+;base64,)?[A-Za-z0-9+/]+=*$";
    public const int Base64MaxLength = 5_700_000;

    // Free-form account identifier for wallet (PayPal handle / crypto address / etc)
    public const string AccountIdentifierPattern = @"^[A-Za-z0-9@._\-:+]{1,200}$";

    public const int SubjectMaxLength    = 200;
    public const int LongTextMaxLength   = 4000;
    public const int ReasonMaxLength     = 500;
}
