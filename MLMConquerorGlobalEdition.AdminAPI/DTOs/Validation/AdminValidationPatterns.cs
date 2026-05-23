namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

/// <summary>
/// Centralised regex / length limits for AdminAPI request DTO validators.
/// Defense-in-depth applied at the model-binding boundary BEFORE handlers run.
/// </summary>
public static class AdminValidationPatterns
{
    public const int EmailMaxLength = 254;
    public const string EmailPattern = @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$";

    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,128}$";

    // Free-text safe pattern — disallow <, >, ", ', \, ;, control chars
    public const string SafeTextPattern = @"^[^<>""'\\;\x00-\x1F\x7F]*$";

    public const int NameMaxLength         = 100;
    public const int ShortNameMaxLength    = 50;
    public const int DescriptionMaxLength  = 1000;
    public const int NotesMaxLength        = 1000;
    public const int LongTextMaxLength     = 4000;
    public const int UrlMaxLength          = 2048;
    public const int LocationMaxLength     = 200;
    public const int SubjectMaxLength      = 200;

    public const string MemberIdPattern   = @"^(?:(?:AMB|MBR)-?\d{3,9}|ROOT\d{3,9})$";
    public const string ProductIdPattern  = @"^[0-9A-Za-z]{8}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{12}$";
    public const string GuidPattern       = @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";
    public const string UserIdPattern     = @"^[A-Za-z0-9_\-]{1,128}$";

    // URL (http/https) — generous but bounded
    public const string UrlPattern = @"^(?:https?:\/\/)[A-Za-z0-9.\-_%~+/?#\[\]@!\$&'()*+,;=:]+$";

    // Theme class / CSS-safe identifier
    public const string CssClassPattern = @"^[a-zA-Z][a-zA-Z0-9\-_ ]{0,49}$";

    public const decimal AmountMax = 1_000_000_000m;

    public const string PlacementSidePattern = @"^(?:Left|Right)$";

    public const string RankStatusPattern = @"^(?:Active|Inactive|Archived)$";
}
