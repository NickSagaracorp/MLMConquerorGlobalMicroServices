using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

/// <summary>
/// Per-locale overrides for a <see cref="CorporateContest"/>. The BizCenter
/// widget picks the row that matches the viewer's UI culture (case-insensitive
/// on <see cref="LanguageCode"/>) and falls back to the parent contest's
/// English defaults when no translation row exists. One row per
/// (ContestId, LanguageCode) — enforced by a unique index.
/// </summary>
public class CorporateContestTranslation : AuditChangesIntKey
{
    public string ContestId    { get; set; } = string.Empty;

    /// <summary>ISO 639-1 lowercase code (en, es, pt, fr, de, it, ko, zh, ka).</summary>
    public string LanguageCode { get; set; } = string.Empty;

    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? BannerUrl   { get; set; }

    public CorporateContest? Contest { get; set; }
}
