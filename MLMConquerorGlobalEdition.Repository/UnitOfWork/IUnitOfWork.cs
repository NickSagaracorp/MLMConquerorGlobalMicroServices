using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Repositories;

namespace MLMConquerorGlobalEdition.Repository.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IRepository<MemberProfile> Members { get; }
    IRepository<MembershipSubscription> Subscriptions { get; }
    IRepository<DualTeamEntity> DualTree { get; }
    IRepository<GenealogyEntity> EnrollmentTree { get; }
    IRepository<CommissionEarning> CommissionEarnings { get; }
    IRepository<TokenBalance> TokenBalances { get; }
    IRepository<TokenTransaction> TokenTransactions { get; }
    IRepository<MemberRankHistory> MemberRanks { get; }
    IRepository<Ticket> Tickets { get; }
    IRepository<Orders> Orders { get; }
    IRepository<PaymentHistory> Payments { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
