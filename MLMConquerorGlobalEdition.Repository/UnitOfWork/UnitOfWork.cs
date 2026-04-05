using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Repositories;

namespace MLMConquerorGlobalEdition.Repository.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public IRepository<MemberProfile> Members { get; }
    public IRepository<MembershipSubscription> Subscriptions { get; }
    public IRepository<DualTeamEntity> DualTree { get; }
    public IRepository<GenealogyEntity> EnrollmentTree { get; }
    public IRepository<CommissionEarning> CommissionEarnings { get; }
    public IRepository<TokenBalance> TokenBalances { get; }
    public IRepository<TokenTransaction> TokenTransactions { get; }
    public IRepository<MemberRankHistory> MemberRanks { get; }
    public IRepository<Ticket> Tickets { get; }
    public IRepository<TicketComment> TicketComments { get; }
    public IRepository<TicketHistory> TicketHistory { get; }
    public IRepository<SlaPolicy> SlaPolicies { get; }
    public IRepository<SlaBreach> SlaBreaches { get; }
    public IRepository<CannedResponse> CannedResponses { get; }
    public IRepository<KnowledgeBaseArticle> KbArticles { get; }
    public IRepository<KbArticleVersion> KbArticleVersions { get; }
    public IRepository<SupportTeam> SupportTeams { get; }
    public IRepository<SupportAgent> SupportAgents { get; }
    public IRepository<TicketCategory> TicketCategories { get; }
    public IRepository<TicketMetricsDaily> TicketMetrics { get; }
    public IRepository<Orders> Orders { get; }
    public IRepository<PaymentHistory> Payments { get; }

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Members = new GenericRepository<MemberProfile>(context);
        Subscriptions = new GenericRepository<MembershipSubscription>(context);
        DualTree = new GenericRepository<DualTeamEntity>(context);
        EnrollmentTree = new GenericRepository<GenealogyEntity>(context);
        CommissionEarnings = new GenericRepository<CommissionEarning>(context);
        TokenBalances = new GenericRepository<TokenBalance>(context);
        TokenTransactions = new GenericRepository<TokenTransaction>(context);
        MemberRanks = new GenericRepository<MemberRankHistory>(context);
        Tickets = new GenericRepository<Ticket>(context);
        TicketComments = new GenericRepository<TicketComment>(context);
        TicketHistory = new GenericRepository<TicketHistory>(context);
        SlaPolicies = new GenericRepository<SlaPolicy>(context);
        SlaBreaches = new GenericRepository<SlaBreach>(context);
        CannedResponses = new GenericRepository<CannedResponse>(context);
        KbArticles = new GenericRepository<KnowledgeBaseArticle>(context);
        KbArticleVersions = new GenericRepository<KbArticleVersion>(context);
        SupportTeams = new GenericRepository<SupportTeam>(context);
        SupportAgents = new GenericRepository<SupportAgent>(context);
        TicketCategories = new GenericRepository<TicketCategory>(context);
        TicketMetrics = new GenericRepository<TicketMetricsDaily>(context);
        Orders = new GenericRepository<Orders>(context);
        Payments = new GenericRepository<PaymentHistory>(context);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _context.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            await _transaction.CommitAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync(ct);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
