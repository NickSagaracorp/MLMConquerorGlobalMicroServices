namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

/// <summary>
/// Centralised regex patterns and length limits used by FluentValidation
/// rules across SignupAPI request DTOs. Defence-in-depth against injection,
/// malformed input and oversize payloads — applied at the model-binding
/// boundary BEFORE handlers ever see the value.
/// </summary>
public static class ValidationPatterns
{
    // Email — RFC-pragmatic, length-capped (254 = SMTP path limit)
    public const int EmailMaxLength = 254;
    public const string EmailPattern = @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$";

    // Password — 8-128, must contain digit, upper, lower, special
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,128}$";

    // Person name — letters (any script), space, hyphen, apostrophe, period.
    // Must start with a letter. 1-50 chars.
    public const int NameMaxLength = 50;
    public const string NamePattern = @"^[\p{L}][\p{L} '\-\.]{0,49}$";

    // Phone / WhatsApp — digits, spaces, dashes, parens, optional + prefix
    public const string PhonePattern = @"^\+?[0-9 \-\(\)]{7,20}$";

    // ISO 3166-1 alpha-2 country code
    public const string CountryIsoPattern = @"^[A-Z]{2}$";

    // Free-text safe pattern — disallow <, >, ", ', \, ;, and control chars
    // (HTML / SQL safety net for state, city, address, zip, business name, etc.)
    public const string SafeTextPattern = @"^[^<>""'\\;\x00-\x1F\x7F]*$";

    public const int StateMaxLength    = 100;
    public const int CityMaxLength     = 100;
    public const int AddressMaxLength  = 250;
    public const int ZipCodeMaxLength  = 20;
    public const int BusinessNameMaxLength = 150;

    // Member identifier — AMB-DDDDDD / MBR-DDDDDD / ROOTDDD
    public const string MemberIdPattern = @"^(?:(?:AMB|MBR)-?\d{3,9}|ROOT\d{3,9})$";

    // Replicate site slug — 2-50 chars, lowercase alphanumeric + hyphens, no
    // leading/trailing hyphen
    public const string ReplicateSlugPattern =
        @"^[a-z0-9](?:[a-z0-9\-]{0,48}[a-z0-9])?$";
    public const int ReplicateSlugMaxLength = 50;

    // Product ID — accepts strict GUID OR the seed-style
    // "00000003-prod-0000-0000-000000000003" pattern (mixed case, letters in
    // segments). 36 chars, four dashes, charset [0-9A-Za-z-].
    public const string ProductIdPattern = @"^[0-9A-Za-z]{8}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{12}$";

    // Strict GUID
    public const string GuidPattern = @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

    // US Social Security Number
    public const string SsnPattern = @"^\d{3}-\d{2}-\d{4}$";
    // US Employer Identification Number
    public const string EinPattern = @"^\d{2}-\d{7}$";

    // Token / discount code — uppercase alphanumeric + hyphen, 4-32
    public const string CodePattern = @"^[A-Z0-9\-]{4,32}$";

    // Tree side
    public const string PlacementSidePattern = @"^(?:Left|Right)$";

    // FingerprintJS visitorId — base62-ish, 8-64
    public const int VisitorIdMaxLength = 64;
    public const string VisitorIdPattern = @"^[A-Za-z0-9]{8,64}$";

    // Screenshot upload (chargeback evidence)
    public const string ImageContentTypePattern = @"^image\/(?:png|jpe?g|webp)$";
    /// <summary>
    /// Base64 charset including data-URI prefix. ~5.6M chars ≈ 4 MB binary.
    /// </summary>
    public const string Base64Pattern = @"^(?:data:image\/[a-zA-Z]+;base64,)?[A-Za-z0-9+/]+=*$";
    public const int Base64MaxLength = 5_700_000;

    // Credit card
    public const string CreditCardPanPattern  = @"^\d{13,19}$";
    public const string CreditCardCvvPattern  = @"^\d{3,4}$";

    // Money — sensible upper bound; precision enforced separately
    public const decimal AmountMax = 1_000_000_000m;

    // Cryptocurrency identifier (short code or chain name)
    public const int CryptoCurrencyMaxLength = 20;
    public const string CryptoCurrencyPattern = @"^[A-Za-z0-9 \-]{1,20}$";
    // Crypto transaction id — hex or base58 charset, generous length
    public const int CryptoTxIdMaxLength = 128;
    public const string CryptoTxIdPattern = @"^[A-Za-z0-9]{6,128}$";

    // Generic short reason / note
    public const int ReasonMaxLength = 500;
}
