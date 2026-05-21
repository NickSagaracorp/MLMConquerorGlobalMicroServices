using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.Repository.Seeders;

/// <summary>Seeds the universal rank-gate GlobalParameter rows. Idempotent.</summary>
public static class RankGateSeeder
{
    private const string Actor = "seed";

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var rows = new (string Key, string Value, string Description)[]
        {
            (RankGateParameters.MinSponsoredMembersKey,
                RankGateParameters.DefaultMinSponsoredMembers.ToString(),
                "Universal rank gate: minimum directly-sponsored members for the lower personal-points path."),
            (RankGateParameters.MinPersonalPointsWithSponsorsKey,
                RankGateParameters.DefaultMinPersonalPointsWithSponsors.ToString(),
                "Universal rank gate: minimum Personal Customer Points when sponsored members >= threshold."),
            (RankGateParameters.MinPersonalPointsWithoutSponsorsKey,
                RankGateParameters.DefaultMinPersonalPointsWithoutSponsors.ToString(),
                "Universal rank gate: minimum Personal Customer Points when below the sponsored-member threshold."),
        };

        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var (key, value, description) in rows)
        {
            if (await db.GlobalParameters.AnyAsync(p => p.Key == key, ct))
                continue;

            db.GlobalParameters.Add(new GlobalParameter
            {
                Key = key, Value = value, Description = description,
                CreatedBy = Actor, CreationDate = now
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("RankGateSeeder: {Added} gate parameter(s) seeded.", added);
        }
        else
        {
            logger.LogInformation("RankGateSeeder: gate parameters already exist — skipped.");
        }
    }
}
