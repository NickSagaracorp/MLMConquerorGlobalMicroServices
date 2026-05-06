using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Tests for TransferTokenInstance — moves a single token (by code) from the current owner to a
/// recipient who must be in the owner's enrollment subtree. The same code stays attached to the
/// instance row, preserving chain-of-custody for fraud audits.
/// </summary>
public class TransferTokenInstanceHandlerTests : IDisposable
{
    private const string SenderId         = "AMB-SENDER";
    private const string DownlineMemberId = "AMB-DOWN-1";
    private const string OutsiderMemberId = "AMB-OTHER";
    private const int    TokenTypeId      = 13;
    private const string TokenCode        = "X4P2A9N";

    private static readonly DateTime FixedNow = new(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<ICacheService> _cache;
    private readonly Mock<IPushNotificationService> _push;
    private readonly Mock<IDateTimeProvider> _dateTime;

    public TransferTokenInstanceHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(SenderId);
        _currentUser.Setup(x => x.UserId).Returns(SenderId);

        _cache = new Mock<ICacheService>();
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        _push = new Mock<IPushNotificationService>();
        _push.Setup(x => x.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        _dateTime = new Mock<IDateTimeProvider>();
        _dateTime.Setup(x => x.UtcNow).Returns(FixedNow);
    }

    public void Dispose() => _db.Dispose();

    private TransferTokenInstanceHandler MakeHandler() => new(
        _db, _currentUser.Object, _dateTime.Object,
        _cache.Object, _push.Object,
        NullLogger<TransferTokenInstanceHandler>.Instance);

    private async Task SeedAsync(
        string ownerId         = SenderId,
        TokenInstanceStatus status = TokenInstanceStatus.Issued,
        bool addDownlineNode   = true,
        int senderBalance      = 1)
    {
        // Members
        await _db.MemberProfiles.AddRangeAsync(
            new MemberProfile { MemberId = SenderId,         FirstName = "S", LastName = "S", MemberType = MemberType.Ambassador, EnrollDate = FixedNow.AddYears(-1), Country = "US", CreatedBy = "seed", LastUpdateDate = FixedNow },
            new MemberProfile { MemberId = DownlineMemberId, FirstName = "D", LastName = "D", MemberType = MemberType.Ambassador, EnrollDate = FixedNow.AddMonths(-3), Country = "US", CreatedBy = "seed", LastUpdateDate = FixedNow },
            new MemberProfile { MemberId = OutsiderMemberId, FirstName = "O", LastName = "O", MemberType = MemberType.Ambassador, EnrollDate = FixedNow.AddMonths(-3), Country = "US", CreatedBy = "seed", LastUpdateDate = FixedNow });

        // Genealogy: sender at root, downline as descendant; outsider in a separate branch.
        var senderPath = $"/{SenderId}/";
        await _db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId          = SenderId,
            ParentMemberId    ="ROOT",
            HierarchyPath     = senderPath,
            Level             = 1,
            CreatedBy         = "seed",
            CreationDate      = FixedNow.AddYears(-1)
        });

        if (addDownlineNode)
        {
            await _db.GenealogyTree.AddAsync(new GenealogyEntity
            {
                MemberId          = DownlineMemberId,
                ParentMemberId    =SenderId,
                HierarchyPath     = $"{senderPath}{DownlineMemberId}/",
                Level             = 2,
                CreatedBy         = "seed",
                CreationDate      = FixedNow.AddMonths(-3)
            });
        }

        await _db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId          = OutsiderMemberId,
            ParentMemberId    ="ROOT",
            HierarchyPath     = $"/{OutsiderMemberId}/",
            Level             = 1,
            CreatedBy         = "seed",
            CreationDate      = FixedNow.AddMonths(-3)
        });

        // TokenType + instance
        await _db.TokenTypes.AddAsync(new TokenType
        {
            Id          = TokenTypeId,
            Name        = "Enrollment: VIP",
            Category    = TokenCategory.Enrollment,
            IsActive    = true,
            CreatedBy   = "seed",
            CreationDate= FixedNow.AddDays(-10)
        });

        await _db.TokenTransactions.AddAsync(new TokenTransaction
        {
            Id                    = 1,
            MemberId              = ownerId,
            TokenTypeId           = TokenTypeId,
            TransactionType       = TokenTransactionType.AdminGranted,
            Quantity              = 1,
            ReferenceId           = TokenCode,
            Status                = status,
            OriginalOwnerMemberId = ownerId,
            CreatedBy             = "seed",
            CreationDate          = FixedNow.AddDays(-1)
        });

        // Sender balance cache
        if (senderBalance > 0)
        {
            await _db.TokenBalances.AddAsync(new TokenBalance
            {
                Id           = Guid.NewGuid().ToString(),
                MemberId     = ownerId,
                TokenTypeId  = TokenTypeId,
                Balance      = senderBalance,
                CreatedBy    = "seed",
                CreationDate = FixedNow.AddDays(-1)
            });
        }

        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Transfer_WhenSenderOwnsTokenAndRecipientInDownline_MovesOwnershipAndWritesLedger()
    {
        await SeedAsync();
        var handler = MakeHandler();

        var result = await handler.Handle(
            new TransferTokenInstanceCommand(TokenCode, DownlineMemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecipientMemberId.Should().Be(DownlineMemberId);

        // Instance row mutated.
        var instance = await _db.TokenTransactions.AsNoTracking()
            .FirstAsync(t => t.ReferenceId == TokenCode);
        instance.MemberId.Should().Be(DownlineMemberId);
        instance.PreviousOwnerMemberId.Should().Be(SenderId);
        instance.OriginalOwnerMemberId.Should().Be(SenderId);
        instance.Status.Should().Be(TokenInstanceStatus.Distributed);

        // Ledger event row inserted.
        var ledger = await _db.TokenTransactions.AsNoTracking()
            .Where(t => t.ReferenceId == null && t.TransactionType == TokenTransactionType.Distributed)
            .ToListAsync();
        ledger.Should().ContainSingle();
        ledger[0].MemberId.Should().Be(SenderId);
        ledger[0].DistributedToMemberId.Should().Be(DownlineMemberId);

        // Sender balance decremented to zero.
        var senderBal = await _db.TokenBalances.AsNoTracking()
            .FirstAsync(tb => tb.MemberId == SenderId && tb.TokenTypeId == TokenTypeId);
        senderBal.Balance.Should().Be(0);

        // Recipient balance now exists with 1.
        var recipBal = await _db.TokenBalances.AsNoTracking()
            .FirstAsync(tb => tb.MemberId == DownlineMemberId && tb.TokenTypeId == TokenTypeId);
        recipBal.Balance.Should().Be(1);

        // Notification fired.
        _push.Verify(p => p.SendAsync(
            DownlineMemberId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Transfer_WhenRecipientNotInSubtree_ReturnsRecipientNotInDownline()
    {
        await SeedAsync();
        var handler = MakeHandler();

        var result = await handler.Handle(
            new TransferTokenInstanceCommand(TokenCode, OutsiderMemberId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RECIPIENT_NOT_IN_DOWNLINE");
    }

    [Fact]
    public async Task Transfer_WhenSenderIsNotCurrentOwner_ReturnsTokenNotOwned()
    {
        // Token belongs to OutsiderMemberId. Sender (current user) is not the owner.
        await SeedAsync(ownerId: OutsiderMemberId, senderBalance: 0);
        var handler = MakeHandler();

        var result = await handler.Handle(
            new TransferTokenInstanceCommand(TokenCode, DownlineMemberId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_NOT_OWNED");
    }

    [Fact]
    public async Task Transfer_WhenTokenAlreadyUsed_ReturnsTokenNotTransferable()
    {
        await SeedAsync(status: TokenInstanceStatus.Used);
        var handler = MakeHandler();

        var result = await handler.Handle(
            new TransferTokenInstanceCommand(TokenCode, DownlineMemberId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_NOT_TRANSFERABLE");
    }

    [Fact]
    public async Task Transfer_WhenRecipientIsSelf_ReturnsInvalidRecipient()
    {
        await SeedAsync();
        var handler = MakeHandler();

        var result = await handler.Handle(
            new TransferTokenInstanceCommand(TokenCode, SenderId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_RECIPIENT");
    }

    [Fact]
    public async Task Transfer_WhenCodeNotFound_ReturnsTokenNotFound()
    {
        await SeedAsync();
        var handler = MakeHandler();

        var result = await handler.Handle(
            new TransferTokenInstanceCommand("NOPE000", DownlineMemberId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_NOT_FOUND");
    }

    [Fact]
    public async Task Transfer_WhenRecipientNotRegistered_ReturnsRecipientNotFound()
    {
        await SeedAsync();
        var handler = MakeHandler();

        var result = await handler.Handle(
            new TransferTokenInstanceCommand(TokenCode, "AMB-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RECIPIENT_NOT_FOUND");
    }
}
