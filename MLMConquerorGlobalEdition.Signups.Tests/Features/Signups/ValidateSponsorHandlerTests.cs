using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.ValidateSponsor;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Signups;

public class ValidateSponsorHandlerTests
{
    [Fact]
    public async Task Handle_WhenSponsorNotFound_ReturnsFailureWithSponsorNotFoundCode()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new ValidateSponsorHandler(db);
        var result = await handler.Handle(new ValidateSponsorQuery("NON-EXISTENT"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPONSOR_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenSponsorHasBusinessNameEnabled_ReturnsBusinessNameAsDisplayName()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-000001",
            FirstName = "John",
            LastName = "Doe",
            BusinessName = "Doe Enterprises",
            ShowBusinessName = true,
            ReplicateSiteSlug = "johndoe",
            MemberType = MemberType.Ambassador,
            EnrollDate = DateTime.UtcNow,
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ValidateSponsorHandler(db);
        var result = await handler.Handle(new ValidateSponsorQuery("AMB-000001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("Doe Enterprises");
        result.Value.FullName.Should().Be("John Doe");
        result.Value.MemberId.Should().Be("AMB-000001");
        result.Value.ReplicateSiteSlug.Should().Be("johndoe");
    }

    [Fact]
    public async Task Handle_WhenSponsorHasBusinessNameDisabled_ReturnsFullNameAsDisplayName()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-000002",
            FirstName = "Jane",
            LastName = "Smith",
            BusinessName = "Smith Co.",
            ShowBusinessName = false,
            MemberType = MemberType.Ambassador,
            EnrollDate = DateTime.UtcNow,
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ValidateSponsorHandler(db);
        var result = await handler.Handle(new ValidateSponsorQuery("AMB-000002"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task Handle_WhenSponsorHasEmptyBusinessName_ReturnsFullNameAsDisplayName()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId = "AMB-000003",
            FirstName = "Alice",
            LastName = "Brown",
            BusinessName = string.Empty,
            ShowBusinessName = true, // enabled but empty — falls back to full name
            MemberType = MemberType.Ambassador,
            EnrollDate = DateTime.UtcNow,
            Country = "US",
            CreatedBy = "seed",
            LastUpdateDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ValidateSponsorHandler(db);
        var result = await handler.Handle(new ValidateSponsorQuery("AMB-000003"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("Alice Brown");
    }
}
