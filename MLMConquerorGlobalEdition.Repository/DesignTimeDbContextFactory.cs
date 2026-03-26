using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository;

/// <summary>
/// Used exclusively by EF Core tooling (dotnet ef migrations add / update).
/// Reads the connection string from the Signups project's appsettings.json so that
/// credentials are never hardcoded here. Update appsettings.json with the real password
/// before running `dotnet ef database update`.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Walk up from Repository/bin to find the Signups appsettings.json
        var basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "MLMConquerorGlobalEdition.Signups");

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection not found in MLMConquerorGlobalEdition.Signups/appsettings.json");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
