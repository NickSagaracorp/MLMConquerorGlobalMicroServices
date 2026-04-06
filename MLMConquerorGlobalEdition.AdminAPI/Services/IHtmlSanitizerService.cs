namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// Sanitizes untrusted HTML before it is persisted to the database.
/// Strips scripts, event handlers, and any tags/attributes not on the allowlist.
/// </summary>
public interface IHtmlSanitizerService
{
    /// <summary>
    /// Returns a sanitized copy of <paramref name="html"/>.
    /// Returns an empty string when <paramref name="html"/> is null or whitespace.
    /// </summary>
    string Sanitize(string? html);
}
