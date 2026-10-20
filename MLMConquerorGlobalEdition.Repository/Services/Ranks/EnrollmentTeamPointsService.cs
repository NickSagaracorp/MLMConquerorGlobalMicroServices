using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <inheritdoc />
public sealed class EnrollmentTeamPointsService : IEnrollmentTeamPointsService
{
    private readonly AppDbContext _db;

    public EnrollmentTeamPointsService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EnrollmentBranchPoints>> GetEnrollmentBranchPointsAsync(
        string memberId, CancellationToken ct = default)
    {
        var childIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (childIds.Count == 0)
            return Array.Empty<EnrollmentBranchPoints>();

        var stats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => childIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);

        var byMember = stats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);
        return childIds
            .Select(id => new EnrollmentBranchPoints(id, byMember.GetValueOrDefault(id, 0)))
            .ToList();
    }

    public async Task<int> GetRawEnrollmentTeamPointsAsync(string memberId, CancellationToken ct = default)
    {
        var branches = await GetEnrollmentBranchPointsAsync(memberId, ct);
        return branches.Sum(b => b.BranchPoints);
    }

    public async Task<int> GetEligibleEnrollmentTeamPointsAsync(
        string memberId, RankRequirement requirement, CancellationToken ct = default)
    {
        if (requirement.EnrollmentTeam <= 0)
            return 0;

        var branches = await GetEnrollmentBranchPointsAsync(memberId, ct);

        var perBranchCap = requirement.MaxEnrollmentTeamPointsPerBranch > 0
            ? (int)Math.Round(requirement.MaxEnrollmentTeamPointsPerBranch * requirement.EnrollmentTeam)
            : 0;

        var summed = perBranchCap > 0
            ? branches.Sum(b => Math.Min(b.BranchPoints, perBranchCap))
            : branches.Sum(b => b.BranchPoints);

        return Math.Min(summed, requirement.EnrollmentTeam);
    }

    public async Task<int?> RecomputeEnrollmentPointsAsync(string memberId, CancellationToken ct = default)
    {
        // Plan M (revisión C1): sin nodo vigente con Node no hay dato. Quien persiste el resultado
        // (MemberStatisticSnapshotJob) lo omite y lo registra; nunca escribe un 0 inventado.
        var root = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);
        if (root is null)
            return null;

        // Plan Q (DQ16; revision 2 del bloque 2, C2): EnrollmentPoints = lo que aportan las suscripciones ACTIVAS del
        // propio y de todo su equipo, sobre los nodos VIGENTES del subarbol (Plan M, I5: un miembro con dos nodos cuenta
        // una vez). Antes sumaba los pedidos completados de todos, activos o no. Es la misma definicion que mantienen las
        // colas y que recalcula 05-realinear-puntos-por-membresia.sql; nada la llama a diario (la instantanea nocturna ya
        // no reescribe EnrollmentPoints): sirve para comprobar un miembro.
        return await (
            from g in _db.GenealogyTree.AsNoTracking().CurrentUnder(root)
            join s in _db.MembershipSubscriptions.AsNoTracking() on g.MemberId equals s.MemberId
            where s.SubscriptionStatus == MembershipStatus.Active
            select s.EnrollmentContribution
        ).SumAsync(ct);
    }
}
