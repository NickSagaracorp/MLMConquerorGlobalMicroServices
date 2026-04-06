using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace MLMConquerorGlobalEdition.SharedKernel.Logging;

/// <summary>
/// Custom console formatter that redacts PII (emails, JWT tokens, Bearer headers)
/// from all log messages before writing to stdout.
/// </summary>
public sealed class PiiMaskingConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "piimask";

    public PiiMaskingConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
        : base(FormatterName) { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var rawMessage    = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var maskedMessage = PiiMasker.Mask(rawMessage);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = logEntry.LogLevel switch
        {
            LogLevel.Trace       => "TRC",
            LogLevel.Debug       => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning     => "WRN",
            LogLevel.Error       => "ERR",
            LogLevel.Critical    => "CRT",
            _                    => "UNK"
        };

        textWriter.WriteLine($"[{timestamp} {level}] {logEntry.Category}: {maskedMessage}");

        if (logEntry.Exception is not null)
            textWriter.WriteLine(PiiMasker.Mask(logEntry.Exception.ToString()));
    }
}
