using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Extensions;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.DI;

/// <summary>
/// Regression guard: verifies that the Billing host's DI registrations satisfy all
/// transitive dependencies of IPayoutOrchestrator so a missing registration can
/// never silently reach production.  The service provider is built from a minimal
/// ServiceCollection that mirrors Billing/Program.cs — no web host required.
/// </summary>
public class PayoutOrchestratorDiTests
{
    /// <summary>
    /// Build a ServiceCollection that mirrors the registrations in Billing/Program.cs
    /// that are relevant to the payout pipeline, then resolve IPayoutOrchestrator.
    /// A missing dep would throw InvalidOperationException here rather than at
    /// runtime inside a Hangfire job.
    /// </summary>
    [Fact]
    public void PayoutOrchestrator_Resolves_WithoutMissingDependencies()
    {
        var services = new ServiceCollection();

        // ── Logging (required by NullEmailService) ────────────────────────────
        services.AddLogging(b => b.AddConsole());

        // ── Configuration (required by LocalReceiptStorage via AddPayoutServices)
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReceiptStorage:LocalPath"]    = Path.Combine(Path.GetTempPath(), "di-test-receipts"),
                ["ReceiptStorage:PublicBaseUrl"] = "https://localhost:7001",
                // El protector de credenciales exige un certificado para envolver el key
                // ring y falla el arranque si no lo encuentra, igual que Billing/AdminAPI.
                // En Development el PFX se genera solo si no existe.
                ["DataProtection:Certificate:Path"] =
                    Path.Combine(Path.GetTempPath(), "di-test-keyring", "cert.pfx")
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // ── AppDbContext (in-memory — no real SQL required) ───────────────────
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase("di-validation-" + Guid.NewGuid().ToString("N")));

        // ── Billing-local singletons (as registered in Billing/Program.cs ~40)
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor(); // CurrentUserService requires IHttpContextAccessor

        // El certificado del key ring sólo se autogenera en Development.
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
                ApplicationName = "DiTests",
                ContentRootPath = Path.GetTempPath()
            });

        // ── SharedKernel.IEmailService — the fix under test ───────────────────
        // This is the registration that was missing and caused the runtime gap.
        // If this line is removed, the test below will fail with InvalidOperationException,
        // proving the guard works.
        services.AddTransient<
            MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService,
            MLMConquerorGlobalEdition.SharedKernel.Services.NullEmailService>();

        // ── Payout pipeline (AddPayoutServices mirrors Billing/Program.cs ~90)
        services.AddPayoutServices();

        // ── Build and resolve ─────────────────────────────────────────────────
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<IPayoutOrchestrator>();

        act.Should().NotThrow("all transitive dependencies of IPayoutOrchestrator must be registered in the Billing host");
    }

    /// <summary>
    /// Companion negative test: proves the guard fires when IEmailService is absent.
    /// This prevents the guard itself from becoming a false-positive.
    /// </summary>
    [Fact]
    public void PayoutOrchestrator_Throws_WhenEmailServiceIsMissing()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReceiptStorage:LocalPath"]    = Path.Combine(Path.GetTempPath(), "di-test-receipts-neg"),
                ["ReceiptStorage:PublicBaseUrl"] = "https://localhost:7001",
                ["DataProtection:Certificate:Path"] =
                    Path.Combine(Path.GetTempPath(), "di-test-keyring", "cert.pfx")
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase("di-validation-neg-" + Guid.NewGuid().ToString("N")));

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();

        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
                ApplicationName = "DiTests",
                ContentRootPath = Path.GetTempPath()
            });

        // Intentionally omit IEmailService registration.
        // services.AddTransient<SharedKernel.Interfaces.IEmailService, ...>();

        services.AddPayoutServices();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<IPayoutOrchestrator>();

        act.Should().Throw<InvalidOperationException>(
            "IPayoutOrchestrator resolution must fail when IEmailService is not registered, " +
            "confirming the positive test is not a false-positive");
    }
}
