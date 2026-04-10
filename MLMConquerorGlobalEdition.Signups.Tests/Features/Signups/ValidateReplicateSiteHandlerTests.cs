using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.ValidateReplicateSite;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Signups;

public class ValidateReplicateSiteHandlerTests
{
    [Fact]
    public async Task Handle_WhenSlugAlreadyTaken_ReturnsFailureWithSlugTakenCode()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-000001",
            ReplicateSiteSlug = "MWRLife",
            FirstName = "Root",
            LastName = "Ambassador",
            MemberType = MemberType.Ambassador,
            EnrollDate = DateTime.UtcNow,
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ValidateReplicateSiteHandler(db);
        var result = await handler.Handle(new ValidateReplicateSiteQuery("MWRLife"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SLUG_TAKEN");
        result.Error.Should().Contain("MWRLife");
    }

    [Fact]
    public async Task Handle_WhenSlugIsAvailable_ReturnsSuccess()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new ValidateReplicateSiteHandler(db);
        var result = await handler.Handle(new ValidateReplicateSiteQuery("brand-new-slug"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenDatabaseIsEmpty_ReturnsSuccess()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new ValidateReplicateSiteHandler(db);
        var result = await handler.Handle(new ValidateReplicateSiteQuery("any-slug"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenSlugMatchesDifferentCasing_DoesNotMatchAndReturnsSuccess()
    {
        // Slug comparison is exact — "mwrlife" is NOT the same as "MWRLife"
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-000001",
            ReplicateSiteSlug = "MWRLife",
            FirstName = "Root",
            LastName = "Ambassador",
            MemberType = MemberType.Ambassador,
            EnrollDate = DateTime.UtcNow,
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ValidateReplicateSiteHandler(db);
        var result = await handler.Handle(new ValidateReplicateSiteQuery("mwrlife"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
