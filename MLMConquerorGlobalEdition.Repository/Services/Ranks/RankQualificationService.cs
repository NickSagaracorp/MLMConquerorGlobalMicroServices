using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <inheritdoc />
public sealed class RankQualificationService : IRankQualificationService
{
    private readonly AppDbContext _db;
    private readonly IEnrollmentTeamPointsService _enrollment;
    private readonly IPersonalCustomerPointsService _personal;

    public RankQualificationService(
        AppDbContext db,
        IEnrollmentTeamPointsService enrollment,
        IPersonalCustomerPointsService personal)
    {
        _db = db;
        _enrollment = enrollment;
        _personal = personal;
    }

    public async Task<bool> MeetsUniversalGateAsync(string memberId, CancellationToken ct = default)
        => (await EvaluateGateAsync(memberId, ct)).MeetsGate;

    public async Task<RankQualificationResult> QualifiesForRankAsync(
        string memberId, RankRequirement requirement, CancellationToken ct = default)
    {
        var gate = await EvaluateGateAsync(memberId, ct);

        var dual = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        var leftLeg  = (int)(dual?.LeftLegPoints  ?? 0);
        var rightLeg = (int)(dual?.RightLegPoints ?? 0);
        var eligibleDt = EligibleDualTeam(requirement, leftLeg, rightLeg);
        var eligibleEt = await _enrollment.GetEligibleEnrollmentTeamPointsAsync(memberId, requirement, ct);

        var stat = await _db.MemberStatistics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == memberId, ct);
        var personalPoints = stat?.PersonalPoints ?? 0;

        var sponsoredCount = gate.SponsoredMembersCount;
        var pcp = gate.PersonalCustomerPoints;
        var externalCount = await _db.MemberProfiles.AsNoTracking()
            .CountAsync(m => m.SponsorMemberId == memberId && m.MemberType == MemberType.ExternalMember, ct);
        var salesVolume = await _db.Orders.AsNoTracking()
            .Where(o => o.MemberId == memberId && o.Status == OrderStatus.Completed)
            .SumAsync(o => (decimal?)o.TotalAmount ?? 0, ct);

        // Threshold <= 0 opts that axis OUT.
        // SponsoredMembers is NOT a per-rank axis — it is governed solely by the universal gate.
        var meetsDt       = requirement.TeamPoints <= 0       || eligibleDt >= requirement.TeamPoints;
        var meetsEt       = requirement.EnrollmentTeam <= 0   || eligibleEt >= requirement.EnrollmentTeam;
        var meetsExternal = requirement.ExternalMembers <= 0  || externalCount >= requirement.ExternalMembers;
        var meetsPersonal = requirement.PersonalPoints <= 0   || personalPoints >= requirement.PersonalPoints;
        var meetsSales    = requirement.SalesVolume <= 0      || salesVolume >= requirement.SalesVolume;

        var qualifies = gate.MeetsGate && meetsDt && meetsEt
                        && meetsExternal && meetsPersonal && meetsSales;

        return new RankQualificationResult
        {
            Qualifies = qualifies,
            MeetsGate = gate.MeetsGate,
            MeetsDualTeam = meetsDt,
            MeetsEnrollmentTeam = meetsEt,
            MeetsExternalMembers = meetsExternal,
            MeetsPersonalPoints = meetsPersonal,
            MeetsSalesVolume = meetsSales,
            EligibleDualTeamPoints = eligibleDt,
            EligibleEnrollmentTeamPoints = eligibleEt,
            PersonalCustomerPoints = pcp,
            SponsoredMembersCount = sponsoredCount,
            SalesVolume = salesVolume
        };
    }

    private sealed record GateInputs(bool MeetsGate, int PersonalCustomerPoints, int SponsoredMembersCount);

    private async Task<GateInputs> EvaluateGateAsync(string memberId, CancellationToken ct)
    {
        var (minSponsored, minPpWith, minPpWithout) = await ReadGateConfigAsync(ct);
        var pcp = await _personal.GetPersonalCustomerPointsAsync(memberId, ct);
        var sponsored = await _db.MemberProfiles.AsNoTracking()
            .CountAsync(m => m.SponsorMemberId == memberId, ct);
        var meetsGate = (sponsored >= minSponsored && pcp >= minPpWith) || pcp >= minPpWithout;
        return new GateInputs(meetsGate, pcp, sponsored);
    }

    /// <summary>Per-leg cap (MaxTeamPointsPerBranch × TeamPoints) then cap at the threshold.</summary>
    private static int EligibleDualTeam(RankRequirement req, int leftLeg, int rightLeg)
    {
        if (req.TeamPoints <= 0)
            return 0;

        var perLegCap = req.MaxTeamPointsPerBranch > 0
            ? (int)Math.Round(req.MaxTeamPointsPerBranch * req.TeamPoints)
            : 0;

        var summed = perLegCap > 0
            ? Math.Min(leftLeg, perLegCap) + Math.Min(rightLeg, perLegCap)
            : leftLeg + rightLeg;

        return Math.Min(summed, req.TeamPoints);
    }

    private async Task<(int MinSponsored, int MinPpWith, int MinPpWithout)> ReadGateConfigAsync(CancellationToken ct)
    {
        var rows = await _db.GlobalParameters.AsNoTracking()
            .Where(p => p.Key == RankGateParameters.MinSponsoredMembersKey
                     || p.Key == RankGateParameters.MinPersonalPointsWithSponsorsKey
                     || p.Key == RankGateParameters.MinPersonalPointsWithoutSponsorsKey)
            .ToDictionaryAsync(p => p.Key, p => p.Value, ct);

        return (
            ReadInt(rows, RankGateParameters.MinSponsoredMembersKey, RankGateParameters.DefaultMinSponsoredMembers),
            ReadInt(rows, RankGateParameters.MinPersonalPointsWithSponsorsKey, RankGateParameters.DefaultMinPersonalPointsWithSponsors),
            ReadInt(rows, RankGateParameters.MinPersonalPointsWithoutSponsorsKey, RankGateParameters.DefaultMinPersonalPointsWithoutSponsors));
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> rows, string key, int fallback)
        => rows.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : fallback;
}
