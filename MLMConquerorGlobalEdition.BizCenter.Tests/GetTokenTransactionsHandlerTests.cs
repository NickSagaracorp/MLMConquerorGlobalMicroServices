using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenTransactions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class GetTokenTransactionsHandlerTests : IDisposable
{
    private const string MemberId = "member-txn-001";

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;

    public GetTokenTransactionsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);
    }

    public void Dispose() => _db.Dispose();

    private TokenType SeedTokenType(int id, string name, bool isGuestPass = false)
    {
        var tt = new TokenType { Id = id, Name = name, IsGuestPass = isGuestPass };
        _db.TokenTypes.Add(tt);
        return tt;
    }

    private TokenTransaction SeedTransaction(
        string memberId, int tokenTypeId,
        TokenTransactionType type = TokenTransactionType.EarnedSignup,
        int quantity = 1,
        string? referenceId = null,
        string? usedBy = null,
        string? notes = null)
    {
        var tx = new TokenTransaction
        {
            MemberId        = memberId,
            TokenTypeId     = tokenTypeId,
            TransactionType = type,
            Quantity        = quantity,
            ReferenceId     = referenceId,
            UsedByMemberId  = usedBy,
            Notes           = notes,
            CreationDate    = DateTime.UtcNow,
            CreatedBy       = "seed"
        };
        _db.TokenTransactions.Add(tx);
        return tx;
    }

    private MemberProfile SeedMemberProfile(string memberId, string firstName, string lastName)
    {
        var mp = new MemberProfile
        {
            MemberId       = memberId,
            FirstName      = firstName,
            LastName       = lastName,
            MemberType     = MemberType.Ambassador,
            Status         = MemberAccountStatus.Active,
            EnrollDate     = DateTime.UtcNow.AddDays(-30),
            CreationDate   = DateTime.UtcNow,
            LastUpdateDate = DateTime.UtcNow,
            CreatedBy      = "seed"
        };
        _db.MemberProfiles.Add(mp);
        return mp;
    }


    [Fact]
    public async Task Handle_WhenMemberHasTransactions_ReturnsMappedDtos()
    {
        var tt = SeedTokenType(1, "Standard Token");
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.Purchased, quantity: 5, notes: "Bought 5");
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        var dto = result.Value.Items.Single();
        dto.TokenTypeName.Should().Be("Standard Token");
        dto.TransactionType.Should().Be("Purchased");
        dto.Amount.Should().Be(5);
        dto.Notes.Should().Be("Bought 5");
        dto.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoTransactions_ReturnsEmptyPage()
    {
        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenOtherMemberHasTransactions_DoesNotReturnThem()
    {
        var tt = SeedTokenType(1, "Standard Token");
        SeedTransaction("other-member", tt.Id, TokenTransactionType.EarnedSignup, quantity: 3);
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTransactionIsUsed_PopulatesUsedByFields()
    {
        var tt = SeedTokenType(2, "Guest Pass", isGuestPass: true);
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.Distributed,
            referenceId: "PASS-001", usedBy: "consumer-001");
        SeedMemberProfile("consumer-001", "Carlos", "Reyes");
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        var dto = result.Value!.Items.Single();
        dto.IsUsed.Should().BeTrue();
        dto.TokenCode.Should().Be("PASS-001");
        dto.UsedByMemberId.Should().Be("consumer-001");
        dto.UsedByMemberName.Should().Be("Carlos Reyes");
    }

    [Fact]
    public async Task Handle_WhenTransactionIsNotUsed_UsedByFieldsAreNull()
    {
        var tt = SeedTokenType(2, "Guest Pass", isGuestPass: true);
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.Distributed,
            referenceId: "PASS-002", usedBy: null);
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        var dto = result.Value!.Items.Single();
        dto.IsUsed.Should().BeFalse();
        dto.TokenCode.Should().Be("PASS-002");
        dto.UsedByMemberId.Should().BeNull();
        dto.UsedByMemberName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUsedByMemberHasNoProfile_UsedByMemberNameIsNull()
    {
        // Token was used by a member whose profile was deleted or not seeded
        var tt = SeedTokenType(2, "Guest Pass", isGuestPass: true);
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.Distributed,
            referenceId: "PASS-003", usedBy: "ghost-member");
        // intentionally no MemberProfile for "ghost-member"
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        var dto = result.Value!.Items.Single();
        dto.IsUsed.Should().BeTrue();
        dto.UsedByMemberId.Should().Be("ghost-member");
        dto.UsedByMemberName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenItemsExceedPageSize_PaginatesCorrectly()
    {
        var tt = SeedTokenType(1, "Standard Token");
        for (int i = 0; i < 5; i++)
            SeedTransaction(MemberId, tt.Id, TokenTransactionType.EarnedSignup, quantity: i + 1);
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 3), default);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_Page2_ReturnsCorrectSlice()
    {
        var tt = SeedTokenType(1, "Standard Token");
        for (int i = 0; i < 5; i++)
            SeedTransaction(MemberId, tt.Id, TokenTransactionType.EarnedSignup, quantity: i + 1);
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(2, 3), default);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_AllTransactionTypes_AreReturned()
    {
        var tt = SeedTokenType(1, "Standard Token");
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.EarnedSignup);
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.Purchased);
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.AdminGranted);
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(3);
        result.Value.Items.Select(t => t.TransactionType)
              .Should().Contain("EarnedSignup")
              .And.Contain("Purchased")
              .And.Contain("AdminGranted");
    }

    [Fact]
    public async Task Handle_MixedUsedAndUnused_ReturnsCorrectIsUsedPerRow()
    {
        var tt = SeedTokenType(2, "Guest Pass", isGuestPass: true);
        SeedMemberProfile("consumer-002", "Ana", "Lopez");
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.Distributed,
            referenceId: "PASS-A", usedBy: "consumer-002");
        SeedTransaction(MemberId, tt.Id, TokenTransactionType.Distributed,
            referenceId: "PASS-B", usedBy: null);
        await _db.SaveChangesAsync();

        var handler = new GetTokenTransactionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetTokenTransactionsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().Contain(d => d.IsUsed && d.UsedByMemberName == "Ana Lopez");
        result.Value.Items.Should().Contain(d => !d.IsUsed && d.UsedByMemberName == null);
    }
}
