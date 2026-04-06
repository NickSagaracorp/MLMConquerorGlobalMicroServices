using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.SharedKernel.Logging;

/// <summary>
/// Static utility that redacts PII patterns from log message strings.
/// Handles emails, JWT tokens, and Bearer authorization headers.
/// </summary>
public static class PiiMasker
{
    // user@domain.com → u***@domain.com
    private static readonly Regex EmailRegex = new(
        @"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b",
        RegexOptions.Compiled);

    // eyJxxx.yyy.zzz JWT format
    private static readonly Regex JwtRegex = new(
        @"eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]*",
        RegexOptions.Compiled);

    // "Bearer <token>" in Authorization headers
    private static readonly Regex BearerRegex = new(
        @"Bearer\s+\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Mask(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var result = BearerRegex.Replace(input, "Bearer [REDACTED]");
        result = JwtRegex.Replace(result, "[REDACTED_JWT]");
        result = EmailRegex.Replace(result, MaskEmail);
        return result;
    }

    private static string MaskEmail(Match m)
    {
        var at     = m.Value.IndexOf('@');
        var local  = m.Value[..at];
        var domain = m.Value[at..];
        var masked = local.Length <= 1
            ? "*"
            : local[0] + new string('*', local.Length - 1);
        return masked + domain;
    }
}
