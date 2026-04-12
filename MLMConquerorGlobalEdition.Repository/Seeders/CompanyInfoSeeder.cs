using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

public static class CompanyInfoSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        if (await db.CompanyInfo.AnyAsync()) return;

        db.CompanyInfo.Add(new CompanyInfo
        {
            CompanyName    = "MLM Conqueror",
            CompanyLegalId = "REPLACE_WITH_LEGAL_ID",
            Address        = "REPLACE_WITH_ADDRESS",
            Phone          = "REPLACE_WITH_PHONE",
            SupportEmail   = "support@mlmconqueror.com",
            PresidentName  = "REPLACE_WITH_PRESIDENT_NAME",
            WebsiteUrl     = "https://www.mlmconqueror.com",
            LogoUrl        = null,
            CreatedBy      = "seed",
            CreationDate   = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        logger.LogInformation("CompanyInfoSeeder: placeholder company info row created.");
    }
}
