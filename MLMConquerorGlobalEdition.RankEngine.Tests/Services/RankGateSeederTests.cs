using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class RankGateSeederTests
{
    [Fact]
    public async Task SeedAsync_SeedsTheThreeGateParameters()
    {
        await using var db = InMemoryDbHelper.Create();

        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);

        var keys = db.GlobalParameters.Select(p => p.Key).ToList();
        keys.Should().Contain(RankGateParameters.MinSponsoredMembersKey);
        keys.Should().Contain(RankGateParameters.MinPersonalPointsWithSponsorsKey);
        keys.Should().Contain(RankGateParameters.MinPersonalPointsWithoutSponsorsKey);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await using var db = InMemoryDbHelper.Create();

        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);

        (await db.GlobalParameters.CountAsync()).Should().Be(3);
    }
}
