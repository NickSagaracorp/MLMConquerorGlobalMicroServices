using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Notifications.Sms;
using MLMConquerorGlobalEdition.SharedKernel.Services;

namespace MLMConquerorGlobalEdition.Notifications.Tests;

/// <summary>
/// Los transportes nulos se eligen solos cuando falta el proveedor, también fuera de desarrollo:
/// lo que escriben en el log no puede llevar códigos ni enlaces con los que entrar en una cuenta.
/// </summary>
public class NullTransportLoggingTests
{
    private const string Code      = "482913";
    private const string ResetUrl  = "https://app.example.com/reset?token=SECRETO-DE-RESTABLECER";
    private const string ConfirmLk = "https://app.example.com/confirm?t=SECRETO-DE-CONFIRMAR";
    private const string Email     = "victima@example.com";
    private const string Phone     = "+14155552671";

    private static Dictionary<string, string> Variables() => new()
    {
        ["Code"]             = Code,
        ["ExpiresInMinutes"] = "5",
        ["ResetUrl"]         = ResetUrl,
        ["confirmationLINK"] = ConfirmLk,
        ["AccessToken"]      = "tok-123",
        ["TemporaryPassword"] = "P@ss-temporal",
        ["FullName"]         = "Nombre Visible"
    };

    [Fact]
    public async Task NullEmailService_no_escribe_valores_ni_el_correo_completo()
    {
        var logger = new CapturingLogger<NullEmailService>();
        var sut    = new NullEmailService(logger);

        await sut.SendAsync(Email, Email, "es", "PASSWORD_RESET", Variables());

        var line = logger.Messages.Should().ContainSingle().Subject;
        AssertNoSecrets(line);
        line.Should().NotContain(Email, "el destino va enmascarado: es PII");
        line.Should().Contain("PASSWORD_RESET").And.Contain("v***@example.com");
        line.Should().Contain("Code=[redacted]")
            .And.Contain("ResetUrl=[redacted]")
            .And.Contain("confirmationLINK=[redacted]")
            .And.Contain("AccessToken=[redacted]")
            .And.Contain("TemporaryPassword=[redacted]");
        // Las no sensibles salen por su nombre, sin valor: lo que hace falta para depurar plantillas.
        line.Should().Contain("ExpiresInMinutes").And.Contain("FullName");
        line.Should().NotContain("Nombre Visible");
    }

    [Fact]
    public async Task NullSmsService_no_escribe_el_codigo_ni_el_telefono_completo()
    {
        var logger = new CapturingLogger<NullSmsService>();
        var sut    = new NullSmsService(logger);

        await sut.SendAsync(Phone, "en", "TWO_FACTOR_CODE", Variables());

        var line = logger.Messages.Should().ContainSingle().Subject;
        AssertNoSecrets(line);
        line.Should().NotContain(Phone);
        line.Should().Contain("2671").And.Contain("TWO_FACTOR_CODE").And.Contain("Code=[redacted]");
    }

    [Theory]
    [InlineData("Code", true)]
    [InlineData("VerificationCode", true)]
    [InlineData("RESETURL", true)]
    [InlineData("ConfirmationLink", true)]
    [InlineData("refresh_token", true)]
    [InlineData("NewPassword", true)]
    [InlineData("ExpiresInMinutes", false)]
    [InlineData("Change", false)]
    public void IsSensitive_reconoce_los_fragmentos_sin_distinguir_mayusculas(string name, bool expected) =>
        NotificationLogRedaction.IsSensitive(name).Should().Be(expected);

    private static void AssertNoSecrets(string line)
    {
        line.Should().NotContain(Code);
        line.Should().NotContain("SECRETO");
        line.Should().NotContain("tok-123");
        line.Should().NotContain("P@ss-temporal");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            // Se guarda también el estado estructurado: un sink que guarde las propiedades por
            // separado no debe poder recuperar de ellas lo que el texto ya no muestra.
            Messages.Add(formatter(state, exception) + " | " +
                (state is IEnumerable<KeyValuePair<string, object?>> kvs
                    ? string.Join(";", kvs.Select(kv => $"{kv.Key}={kv.Value}"))
                    : string.Empty));
    }
}
