using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace MLMConquerorGlobalEdition.SharedKernel.Logging;

public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Replaces all default log providers with a PII-masking console formatter.
    /// Emails, JWT tokens, and Bearer tokens are redacted before being written to stdout.
    /// </summary>
    public static ILoggingBuilder AddPiiMaskingConsole(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddConsole(opts => opts.FormatterName = PiiMaskingConsoleFormatter.FormatterName);
        builder.AddConsoleFormatter<PiiMaskingConsoleFormatter, ConsoleFormatterOptions>();
        return builder;
    }
}
