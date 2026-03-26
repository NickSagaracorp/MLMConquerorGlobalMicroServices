using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

public static class RolesSeeder
{
    public static readonly string[] AllRoles =
    [
        "SuperAdmin", "Admin", "CommissionManager", "BillingManager",
        "SupportManager", "SupportLevel1", "SupportLevel2", "SupportLevel3",
        "IT", "Ambassador", "Member"
    ];

    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var roleName in AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                    logger.LogInformation("Role '{Role}' created.", roleName);
                else
                    logger.LogWarning("Failed to create role '{Role}': {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
